using System.Text.Json;
using MacroDeck.Plugin.Testing;
using NUnit.Framework;

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

	[TestCase("""{"maxTasks":3}""", 3)]
	[TestCase("""{"maxTasks":0}""", 1)]
	[TestCase("""{"maxTasks":9999}""", 50)]
	[TestCase("""{}""", 50)]
	[TestCase("""{"maxTasks":"many"}""", 50)]
	public void Max_tasks_is_read_from_widget_data_and_clamped(string json, int expected)
	{
		using var document = JsonDocument.Parse(json);

		Assert.That(PluginIntegration.MaxTasks(document.RootElement), Is.EqualTo(expected));
	}

	[Test]
	public void Missing_widget_data_means_no_cap_beyond_what_the_tile_fits()
	{
		Assert.That(PluginIntegration.MaxTasks(default), Is.EqualTo(50));
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
