using NUnit.Framework;
using System.Net;
using MacroDeck.Sdk.ConfigFlow;
using VikunjaPlugin.Api;
using VikunjaPlugin.ConfigFlow;

namespace VikunjaPlugin.Tests;

[TestFixture]
public sealed class ConfigFlowTests
{
	[TestCase("https://vikunja.example.com", "https://vikunja.example.com/api/v1/")]
	[TestCase("https://vikunja.example.com/", "https://vikunja.example.com/api/v1/")]
	[TestCase("https://vikunja.example.com/api/v1", "https://vikunja.example.com/api/v1/")]
	[TestCase("  http://192.168.1.5:3456/api/v1/  ", "http://192.168.1.5:3456/api/v1/")]
	public void The_instance_address_becomes_the_api_root(string input, string expected)
	{
		Assert.That(VikunjaConfigFlow.ToApiAddress(input)?.ToString(), Is.EqualTo(expected));
	}

	[TestCase("")]
	[TestCase("not a url")]
	[TestCase("ftp://vikunja.example.com")]
	public void An_address_that_is_not_http_is_refused(string input)
	{
		Assert.That(VikunjaConfigFlow.ToApiAddress(input), Is.Null);
	}

	private static async Task<ConfigFlowResult> SubmitAsync(FakeVikunja fake, string url, string? token)
	{
		var flow = new VikunjaConfigFlow(new VikunjaClient(new HttpClient(fake), new VikunjaCredentials()));
		var input = new Dictionary<string, object?> { ["serverUrl"] = url, ["token"] = token };
		return await flow.SubmitAsync("connection", input, null!, CancellationToken.None);
	}

	[Test]
	public async Task A_valid_connection_completes_and_stores_the_trimmed_token_as_a_secret()
	{
		var fake = new FakeVikunja { Respond = (_, _) => FakeVikunja.Json("[]") };

		var result = await SubmitAsync(fake, "https://vikunja.test", "  tk_123\n");

		Assert.That(result.Kind, Is.EqualTo(ConfigFlowResultKind.Complete));
		Assert.That(fake.Requests.Single().Path, Does.StartWith("/api/v1/tasks"));
		Assert.That(fake.Requests.Single().Token, Is.EqualTo("tk_123"));
	}

	[Test]
	public async Task A_rejected_token_returns_to_the_step_without_storing_anything()
	{
		var fake = new FakeVikunja { Respond = (_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized) };

		var result = await SubmitAsync(fake, "https://vikunja.test", "bad");

		Assert.That(result.Kind, Is.EqualTo(ConfigFlowResultKind.Error));
		Assert.That(result.Values, Is.Null);
	}

	[TestCase("not a url", "tk")]
	[TestCase("https://vikunja.test", "   ")]
	[TestCase("https://vikunja.test", null)]
	public async Task Invalid_input_is_refused_before_calling_vikunja(string url, string? token)
	{
		var fake = new FakeVikunja();

		var result = await SubmitAsync(fake, url, token);

		Assert.That(result.Kind, Is.EqualTo(ConfigFlowResultKind.Error));
		Assert.That(fake.Requests, Is.Empty);
	}
}
