using NUnit.Framework;
using System.Net;
using MacroDeck.Sdk.ConfigFlow;
using VikunjaPlugin.Api;
using VikunjaPlugin.ConfigFlow;

namespace VikunjaPlugin.Tests;

[TestFixture]
public sealed class ConfigFlowTests
{
	[TestCase("https://vikunja.example.com", "https://vikunja.example.com/api/v2/")]
	[TestCase("https://vikunja.example.com/", "https://vikunja.example.com/api/v2/")]
	[TestCase("https://vikunja.example.com/api/v2", "https://vikunja.example.com/api/v2/")]
	[TestCase("https://vikunja.example.com/api/v1", "https://vikunja.example.com/api/v2/")]
	[TestCase("  http://192.168.1.5:3456/api/v1/  ", "http://192.168.1.5:3456/api/v2/")]
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
		var fake = Supported();

		var result = await SubmitAsync(fake, "https://vikunja.test", "  tk_123\n");

		Assert.That(result.Kind, Is.EqualTo(ConfigFlowResultKind.Complete));
		Assert.That(fake.Requests[0].Path, Is.EqualTo("/api/v2/info"));
		Assert.That(fake.Requests[1].Path, Does.StartWith("/api/v2/tasks"));
		Assert.That(fake.Requests[1].Token, Is.EqualTo("tk_123"));
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

	[TestCase("<html>Sign in</html>")]
	[TestCase("")]
	[TestCase("{}")]
	public async Task An_address_that_answers_but_is_not_vikunja_is_refused_and_nothing_is_stored(string body)
	{
		// Another web app, a login page or a proxy can answer 200 to any path; only a task list counts.
		var fake = new FakeVikunja { Respond = (_, _) => FakeVikunja.Json(body) };

		var result = await SubmitAsync(fake, "https://not-vikunja.test", "tk");

		Assert.That(result.Kind, Is.EqualTo(ConfigFlowResultKind.Error));
		Assert.That(result.Values, Is.Null);
	}

	[Test]
	public async Task An_account_with_no_tasks_still_connects()
	{
		var result = await SubmitAsync(Supported(), "https://vikunja.test", "tk");

		Assert.That(result.Kind, Is.EqualTo(ConfigFlowResultKind.Complete));
	}

	[TestCase("v2.7.0")]
	[TestCase("v2.4.0")]
	[TestCase("v0.24.6")]
	public async Task A_vikunja_older_than_the_minimum_is_refused_before_the_token_is_sent(string version)
	{
		var fake = Supported(version);

		var result = await SubmitAsync(fake, "https://vikunja.test", "tk");

		Assert.That(result.Kind, Is.EqualTo(ConfigFlowResultKind.Error));
		Assert.That(result.Values, Is.Null);
		Assert.That(fake.Requests.Select(r => r.Path), Is.EqualTo(OnlyInfo));
		Assert.That(fake.Requests.Single().Token, Is.Null);
	}

	[TestCase("v2.7.0", VikunjaFailure.Outdated)]
	[TestCase(null, VikunjaFailure.NotVikunja)]
	public void Only_a_readable_old_version_is_called_too_old(string? version, VikunjaFailure expected)
	{
		var fake = version is null
			? new FakeVikunja { Respond = (_, _) => FakeVikunja.Json("{}") }
			: Supported(version);
		var client = new VikunjaClient(new HttpClient(fake), new VikunjaCredentials());

		var failure = Assert.ThrowsAsync<VikunjaException>(() => client.VerifyAsync(new Uri("https://vikunja.test/api/v2/"), "tk", CancellationToken.None));

		Assert.That(failure!.Reason, Is.EqualTo(expected));
	}

	[Test]
	public void An_address_without_api_v2_is_reported_as_not_vikunja()
	{
		// Covers both a wrong address and a Vikunja older than 2.4; the NotVikunja text asks to check both.
		var fake = new FakeVikunja { Respond = (_, _) => new HttpResponseMessage(HttpStatusCode.NotFound) };
		var client = new VikunjaClient(new HttpClient(fake), new VikunjaCredentials());

		var failure = Assert.ThrowsAsync<VikunjaException>(() => client.VerifyAsync(new Uri("https://vikunja.test/api/v2/"), "tk", CancellationToken.None));

		Assert.That(failure!.Reason, Is.EqualTo(VikunjaFailure.NotVikunja));
	}

	[TestCase("v2.7.1", true)]
	[TestCase("v2.8.0", true)]
	[TestCase("2.10.0", true)]
	[TestCase("v2.8.0-42-gabc1234", true)]
	[TestCase("dev", true)]
	[TestCase("v2.7.0", false)]
	[TestCase("v2.7.0-99-gabc1234", false)]
	[TestCase(null, false)]
	public void Only_versions_from_the_minimum_on_are_supported(string? version, bool supported)
	{
		Assert.That(VikunjaClient.IsSupported(version), Is.EqualTo(supported));
	}

	private static readonly string[] OnlyInfo = ["/api/v2/info"];

	private static FakeVikunja Supported(string version = "v2.8.0")
		=> new() { Respond = (_, path) => path.EndsWith("/info", StringComparison.Ordinal) ? FakeVikunja.Info(version) : FakeVikunja.Tasks("[]") };

	[Test]
	public async Task A_server_that_cannot_be_reached_returns_to_the_step_instead_of_failing()
	{
		var fake = new FakeVikunja { Respond = (_, _) => throw new HttpRequestException("No such host is known.") };

		var result = await SubmitAsync(fake, "https://typo.vikunja.test", "tk");

		Assert.That(result.Kind, Is.EqualTo(ConfigFlowResultKind.Error));
		Assert.That(result.Values, Is.Null);
	}
}
