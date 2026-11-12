using System.Text.Json;
using MacroDeck.Plugin.Testing;
using MacroDeck.Ui.Testing;
using NUnit.Framework;
using VikunjaPlugin.Api;
using VikunjaPlugin.Widgets;
using MacroDeck.Plugin.Testing.Fakes;
using MacroDeck.Localization;
using MacroDeck.Sdk.Ui;
using MacroDeck.Ui.Model.Events;
using MacroDeck.Ui.Model.Surfaces;

namespace VikunjaPlugin.Tests;

[TestFixture]
public sealed class PluginIntegrationTests
{
	[Test]
	public async Task The_plugin_builds_and_initializes_without_configuration()
	{
		await using var harness = PluginTestHarness.Create(builder =>
		{
			builder.UseLocalization(Strings.LocalizationCatalog).RegisterIntegration<PluginIntegration>();
			builder.Services.AddVikunjaTestServices();
		});

		Assert.DoesNotThrowAsync(harness.InitializeIntegrationsAsync);
	}

	[TestCase("""{"fontSize":150,"cardSize":75}""", 1.5, 0.75)]
	[TestCase("""{"fontSize":10,"cardSize":900}""", 0.5, 2.0)]
	[TestCase("""{}""", 1.0, 1.0)]
	[TestCase("""{"fontSize":"big"}""", 1.0, 1.0)]
	public void Font_and_card_size_are_percentages_clamped_to_a_sane_range(string json, double font, double card)
	{
		using var document = JsonDocument.Parse(json);

		var settings = WidgetSettings.Read(document.RootElement);

		Assert.That(settings.FontScale, Is.EqualTo(font).Within(0.0001));
		Assert.That(settings.CardScale, Is.EqualTo(card).Within(0.0001));
	}

	[TestCase("""{"squareDisplay":"counter"}""", true)]
	[TestCase("""{"squareDisplay":"list"}""", false)]
	[TestCase("""{"squareDisplay":7}""", false)]
	[TestCase("""{}""", false)]
	public void Square_tiles_show_the_list_unless_the_counter_is_chosen(string json, bool counter)
	{
		using var document = JsonDocument.Parse(json);

		Assert.That(WidgetSettings.Read(document.RootElement).SquareCounter, Is.EqualTo(counter));
	}

	[Test]
	public void Missing_widget_data_means_the_default_look_and_no_cap_beyond_what_the_tile_fits()
	{
		Assert.That(WidgetSettings.Read(default), Is.EqualTo(WidgetSettings.Default));
	}

	private static async Task<(PluginIntegration Plugin, string ReadOnlyType, string ChecklistType)> RegisteredPluginAsync()
	{
		var fake = new FakeVikunja();
		var store = fake.CreateStore(out var credentials);
		var plugin = new PluginIntegration(new VikunjaClient(new HttpClient(fake), credentials), credentials, store, Serilog.Core.Logger.None);
		var context = new FakeWidgetTypeProviderContext();
		await plugin.InitializeAsync(context);
		return (plugin, context.WidgetTypes.Keys.First(key => key.EndsWith(PluginIntegration.ReadOnlyWidgetId, StringComparison.Ordinal)),
			context.WidgetTypes.Keys.First(key => key.EndsWith(PluginIntegration.ChecklistWidgetId, StringComparison.Ordinal)));
	}

	private static UiSessionRequest Request(string kind, string widgetType, bool sample = false)
	{
		var attributes = new Dictionary<string, JsonElement>
		{
			[UiWidgetSurfaceAttributes.WidgetType] = JsonSerializer.SerializeToElement(widgetType),
			[UiWidgetSurfaceAttributes.Data] = JsonSerializer.SerializeToElement(new { }),
		};
		if (sample)
		{
			attributes[UiWidgetSurfaceAttributes.Sample] = JsonSerializer.SerializeToElement(true);
		}

		return new UiSessionRequest { UiModelVersion = 1, Surface = new UiSurface { Kind = kind, SessionMode = UiSessionModes.Shared, Attributes = attributes } };
	}

	[Test]
	public async Task Only_the_live_checklist_tile_can_complete_tasks()
	{
		var (plugin, readOnly, checklist) = await RegisteredPluginAsync();

		var live = (TasksSession?)await plugin.CreateSessionAsync(Request(UiSurfaceKinds.Widget, checklist), CancellationToken.None);
		var preview = (TasksSession?)await plugin.CreateSessionAsync(Request(UiSurfaceKinds.Preview, checklist), CancellationToken.None);
		var readOnlyTile = (TasksSession?)await plugin.CreateSessionAsync(Request(UiSurfaceKinds.Widget, readOnly), CancellationToken.None);

		Assert.That(live?.Checkable, Is.True);
		Assert.That(preview?.Checkable, Is.False);
		Assert.That(readOnlyTile?.Checkable, Is.False);
	}

	[Test]
	public async Task Samples_and_other_plugins_widgets_get_no_session()
	{
		var (plugin, _, checklist) = await RegisteredPluginAsync();

		Assert.That(await plugin.CreateSessionAsync(Request(UiSurfaceKinds.Widget, checklist, sample: true), CancellationToken.None), Is.Null);
		Assert.That(await plugin.CreateSessionAsync(Request(UiSurfaceKinds.Widget, "other.plugin::widget"), CancellationToken.None), Is.Null);
	}

	private sealed class RecordingOrigin : IUiEventOrigin
	{
		public List<LocalizedText> Failures { get; } = [];

		public string ClientId => "test";

		public Task<bool> ReportFailureAsync(LocalizedText message, CancellationToken cancellationToken)
		{
			Failures.Add(message);
			return Task.FromResult(true);
		}

		public Task<bool> ShowModalAsync(ModalDefinition modal, CancellationToken cancellationToken) => Task.FromResult(false);

		public Task<ModalResult<T>> ShowModalAsync<T>(ModalDefinition modal, CancellationToken cancellationToken) => throw new NotSupportedException();

		public Task<bool> OpenUrlAsync(Uri url, CancellationToken cancellationToken) => Task.FromResult(false);

		public Task<ReadOnlyMemory<byte>> ReadUploadAsync(string uploadId, CancellationToken cancellationToken) => throw new NotSupportedException();
	}

	[Test]
	public async Task A_failed_press_tells_the_user_instead_of_failing_silently()
	{
		var fake = new FakeVikunja { Respond = (_, _) => new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized) };
		var store = fake.CreateStore(out _);
		store.Tasks.Value = [new VikunjaTask(1, "Buy milk", false)];
		var buttonId = UiTestHost.Render(TasksView.Build(store, WidgetSettings.Default, (_, _) => Task.CompletedTask)).ByType("ui.button")[0].Id;
		await using var session = new TasksSession(new UiSurface { Kind = UiSurfaceKinds.Widget, SessionMode = UiSessionModes.Shared }, store, WidgetSettings.Default, checkable: true);
		var origin = new RecordingOrigin();

		session.Dispatch(new UiEvent { NodeId = buttonId, Name = "press" }, origin);
		SpinWait.SpinUntil(() => origin.Failures.Count > 0, TimeSpan.FromSeconds(2));

		Assert.That(origin.Failures, Has.Count.EqualTo(1));
	}

	[Test]
	public void The_configuration_view_offers_the_stored_values()
	{
		using var document = JsonDocument.Parse("""{"fontSize":130,"cardSize":80,"squareDisplay":"counter"}""");

		var host = UiTestHost.Render(TasksConfigView.Build(document.RootElement));

		var description = host.Describe();
		Assert.That(description, Does.Contain("130"));
		Assert.That(description, Does.Contain("80"));
		Assert.That(description, Does.Contain("counter"));
	}
}

[TestFixture]
public sealed class LocalizationTests
{
	[Test]
	public void The_catalog_is_scoped_to_the_plugin_id()
	{
		Assert.That(Strings.LocalizationCatalog.Scope, Is.EqualTo("plugin:com.eddyvenus.vikunja"));
	}

	[Test]
	public void English_is_the_default_culture()
	{
		Assert.That(Strings.LocalizationCatalog.DefaultCulture, Is.EqualTo("en"));
		Assert.That(Strings.LocalizationCatalog.Cultures, Does.Contain("en"));
	}

	[Test]
	public void The_widget_strings_come_from_the_catalog()
	{
		Assert.That(Strings.LocalizationCatalog.KeysOf("en"), Does.Contain("Widgets.Checklist.Name"));
	}

	[Test]
	public void Every_key_the_default_culture_declares_resolves_to_text()
	{
		foreach (var key in Strings.LocalizationCatalog.KeysOf("en"))
		{
			Assert.That(Strings.LocalizationCatalog.TryGetTemplate("en", key, out var text), Is.True);
			Assert.That(text, Is.Not.Empty);
		}
	}
}
