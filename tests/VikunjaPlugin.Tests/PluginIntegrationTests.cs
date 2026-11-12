using System.Text.Json;
using MacroDeck.Plugin.Testing;
using MacroDeck.Ui.Testing;
using NUnit.Framework;
using VikunjaPlugin.Widgets;

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
