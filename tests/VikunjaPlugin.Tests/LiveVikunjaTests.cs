using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;
using VikunjaPlugin.Api;
using VikunjaPlugin.ConfigFlow;

namespace VikunjaPlugin.Tests;

/// <summary>Runs against a real Vikunja prepared by .github/scripts/seed-vikunja.sh; ignored when that has not run.</summary>
[TestFixture]
[Category("Live")]
public sealed class LiveVikunjaTests
{
	private Uri _apiAddress = null!;
	private string _token = null!;
	private long _taskId;

	[OneTimeSetUp]
	public void ReadEnvironment()
	{
		var url = Environment.GetEnvironmentVariable("VIKUNJA_LIVE_URL");
		var token = Environment.GetEnvironmentVariable("VIKUNJA_LIVE_TOKEN");
		var taskId = Environment.GetEnvironmentVariable("VIKUNJA_LIVE_TASK_ID");
		if (url is null || token is null || taskId is null)
		{
			Assert.Ignore("No live Vikunja configured.");
		}

		_apiAddress = VikunjaConfigFlow.ToApiAddress(url)!;
		_token = token;
		_taskId = long.Parse(taskId, System.Globalization.CultureInfo.InvariantCulture);
	}

	private VikunjaClient Client()
	{
		var credentials = new VikunjaCredentials();
		credentials.Apply(_apiAddress, _token);
		return new VikunjaClient(new HttpClient(), credentials);
	}

	[Test]
	[Order(1)]
	public void The_documented_token_passes_the_connection_check()
	{
		Assert.DoesNotThrowAsync(() => Client().VerifyAsync(_apiAddress, _token, CancellationToken.None));
	}

	[Test]
	[Order(2)]
	public async Task The_seeded_task_is_listed_as_open()
	{
		var tasks = await Client().GetOpenTasksAsync(200, CancellationToken.None);

		Assert.That(tasks.Select(t => t.Id), Does.Contain(_taskId));
	}

	[Test]
	[Order(3)]
	public async Task Checking_marks_the_task_done_and_keeps_its_other_fields()
	{
		var client = Client();

		Assert.That(await client.CompleteTaskAsync(_taskId, CancellationToken.None), Is.True);

		var open = await client.GetOpenTasksAsync(200, CancellationToken.None);
		Assert.That(open.Select(t => t.Id), Does.Not.Contain(_taskId));

		using var http = new HttpClient();
		http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("VIKUNJA_LIVE_JWT"));
		var task = await http.GetFromJsonAsync<JsonObject>(new Uri(_apiAddress, $"tasks/{_taskId}"));
		Assert.That(task!["title"]!.GetValue<string>(), Is.EqualTo("Live task"));
		Assert.That(task["description"]!.GetValue<string>(), Does.Contain("Must survive the check"));
	}
}
