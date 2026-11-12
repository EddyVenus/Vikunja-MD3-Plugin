using System.Net;
using System.Text.Json;
using NUnit.Framework;
using VikunjaPlugin.Api;
using VikunjaPlugin.Widgets;

namespace VikunjaPlugin.Tests;

[TestFixture]
public sealed class TaskStoreTests
{
	private static readonly string[] ExpectedTitles = ["Buy milk", "Call Ana"];
	private static readonly long[] RemainingIds = [2];
	private static readonly long[] ExpectedOrder = [3, 2, 1];
	private const string TwoTasks = """[{"id":1,"title":"Buy milk","done":false},{"id":2,"title":"Call Ana","done":false}]""";

	[Test]
	public async Task Refresh_loads_only_open_tasks_from_the_api()
	{
		var fake = new FakeVikunja { Respond = (_, _) => FakeVikunja.Json(TwoTasks) };
		var store = fake.CreateStore(out _);

		await store.RefreshAsync(CancellationToken.None);

		Assert.That(store.Tasks.Peek().Select(t => t.Title), Is.EqualTo(ExpectedTitles));
		Assert.That(fake.Requests[0].Path, Does.Contain("done%20%3D%20false"));
	}

	[Test]
	public async Task Refresh_without_configuration_clears_the_tasks_and_calls_nothing()
	{
		var fake = new FakeVikunja();
		var store = fake.CreateStore(out _, configured: false);

		await store.RefreshAsync(CancellationToken.None);

		Assert.That(store.Tasks.Peek(), Is.Empty);
		Assert.That(fake.Requests, Is.Empty);
	}

	[Test]
	public async Task A_failed_refresh_keeps_the_tasks_already_shown()
	{
		var fake = new FakeVikunja { Respond = (_, _) => FakeVikunja.Json(TwoTasks) };
		var store = fake.CreateStore(out _);
		await store.RefreshAsync(CancellationToken.None);

		fake.Respond = (_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError);
		await store.RefreshAsync(CancellationToken.None);

		Assert.That(store.Tasks.Peek(), Has.Count.EqualTo(2));
	}

	[Test]
	public async Task Completing_sends_the_whole_task_back_with_done_set_and_removes_it()
	{
		var fake = new FakeVikunja
		{
			Respond = (method, path) => method == HttpMethod.Get && path.Contains("tasks/1")
				? FakeVikunja.Json("""{"id":1,"title":"Buy milk","done":false,"description":"2 liters"}""")
				: method == HttpMethod.Get
					? FakeVikunja.Json(TwoTasks)
					: FakeVikunja.Json("""{"id":1,"done":true}"""),
		};
		var store = fake.CreateStore(out _);
		await store.RefreshAsync(CancellationToken.None);

		await store.CompleteAsync(1, CancellationToken.None);

		var post = fake.Requests.Single(r => r.Method == HttpMethod.Post);
		using var body = JsonDocument.Parse(post.Body);
		Assert.That(post.Path, Does.EndWith("/tasks/1"));
		Assert.That(body.RootElement.GetProperty("done").GetBoolean(), Is.True);
		Assert.That(body.RootElement.GetProperty("description").GetString(), Is.EqualTo("2 liters"));
		Assert.That(store.Tasks.Peek().Select(t => t.Id), Is.EqualTo(RemainingIds));
	}

	[Test]
	public async Task Completing_keeps_the_task_and_reports_when_the_token_is_rejected()
	{
		var fake = new FakeVikunja
		{
			Respond = (method, path) => method == HttpMethod.Get && !path.Contains("tasks/")
				? FakeVikunja.Json(TwoTasks)
				: new HttpResponseMessage(HttpStatusCode.Unauthorized),
		};
		var store = fake.CreateStore(out _);
		await store.RefreshAsync(CancellationToken.None);

		var failure = Assert.ThrowsAsync<VikunjaException>(() => store.CompleteAsync(1, CancellationToken.None));

		Assert.That(failure!.Reason, Is.EqualTo(VikunjaFailure.Unauthorized));
		Assert.That(store.Tasks.Peek(), Has.Count.EqualTo(2));
	}

	[Test]
	public async Task A_repeating_task_stays_listed_because_vikunja_keeps_it_open()
	{
		var fake = new FakeVikunja
		{
			Respond = (method, path) => method == HttpMethod.Post
				? FakeVikunja.Json("""{"id":1,"done":false}""")
				: path.Contains("/tasks/1")
					? FakeVikunja.Json("""{"id":1,"title":"Buy milk","done":false}""")
					: FakeVikunja.Json(TwoTasks),
		};
		var store = fake.CreateStore(out _);
		await store.RefreshAsync(CancellationToken.None);

		await store.CompleteAsync(1, CancellationToken.None);

		Assert.That(store.Tasks.Peek().Select(t => t.Id), Does.Contain(1L));
	}

	[Test]
	public async Task A_non_json_answer_is_a_rejection_not_a_crash()
	{
		var fake = new FakeVikunja { Respond = (_, _) => FakeVikunja.Json("<html>login</html>") };
		var store = fake.CreateStore(out _);

		Assert.DoesNotThrowAsync(() => store.RefreshAsync(CancellationToken.None));
		var failure = Assert.ThrowsAsync<VikunjaException>(() => store.CompleteAsync(1, CancellationToken.None));
		Assert.That(failure!.Reason, Is.EqualTo(VikunjaFailure.Rejected));
	}

	[Test]
	public async Task Dated_tasks_come_first_soonest_due_first_and_undated_ones_last()
	{
		var fake = new FakeVikunja
		{
			Respond = (_, _) => FakeVikunja.Json("""
				[{"id":1,"title":"Undated","due_date":"0001-01-01T00:00:00Z"},
				 {"id":2,"title":"Later","due_date":"2026-12-01T10:00:00Z"},
				 {"id":3,"title":"Sooner","due_date":"2026-11-15T10:00:00Z"}]
				"""),
		};
		var store = fake.CreateStore(out _);

		await store.RefreshAsync(CancellationToken.None);

		Assert.That(store.Tasks.Peek().Select(t => t.Id), Is.EqualTo(ExpectedOrder));
	}

	private static readonly int[] ExpectedPriorities = [4, 0];

	[Test]
	public async Task Priority_is_read_and_missing_priority_means_none()
	{
		var fake = new FakeVikunja { Respond = (_, _) => FakeVikunja.Json("""[{"id":1,"title":"Urgent","priority":4},{"id":2,"title":"Plain"}]""") };
		var store = fake.CreateStore(out _);

		await store.RefreshAsync(CancellationToken.None);

		Assert.That(store.Tasks.Peek().Select(t => t.Priority), Is.EqualTo(ExpectedPriorities));
	}

	[Test]
	public async Task Status_tells_apart_ready_unavailable_and_not_configured()
	{
		var fake = new FakeVikunja { Respond = (_, _) => FakeVikunja.Json(TwoTasks) };
		var store = fake.CreateStore(out var credentials);
		Assert.That(store.Status.Peek(), Is.EqualTo(StoreStatus.Loading));

		await store.RefreshAsync(CancellationToken.None);
		Assert.That(store.Status.Peek(), Is.EqualTo(StoreStatus.Ready));

		fake.Respond = (_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized);
		await store.RefreshAsync(CancellationToken.None);
		Assert.That(store.Status.Peek(), Is.EqualTo(StoreStatus.Unavailable));

		credentials.Clear();
		await store.RefreshAsync(CancellationToken.None);
		Assert.That(store.Status.Peek(), Is.EqualTo(StoreStatus.NotConfigured));
	}

	[Test]
	public async Task An_unchanged_list_is_not_written_back()
	{
		var fake = new FakeVikunja { Respond = (_, _) => FakeVikunja.Json(TwoTasks) };
		var store = fake.CreateStore(out _);
		await store.RefreshAsync(CancellationToken.None);
		var before = store.Tasks.Peek();

		await store.RefreshAsync(CancellationToken.None);

		Assert.That(store.Tasks.Peek(), Is.SameAs(before));
	}

	[Test]
	public async Task Reset_forgets_the_previous_accounts_tasks()
	{
		var fake = new FakeVikunja { Respond = (_, _) => FakeVikunja.Json(TwoTasks) };
		var store = fake.CreateStore(out _);
		await store.RefreshAsync(CancellationToken.None);

		store.Reset();

		Assert.That(store.Tasks.Peek(), Is.Empty);
		Assert.That(store.Status.Peek(), Is.EqualTo(StoreStatus.Loading));
	}

	[Test]
	public void A_network_error_maps_to_unreachable()
	{
		var client = ClientThrowing(new HttpRequestException());

		var failure = Assert.ThrowsAsync<VikunjaException>(() => client.GetOpenTasksAsync(5, CancellationToken.None));

		Assert.That(failure!.Reason, Is.EqualTo(VikunjaFailure.Unreachable));
	}

	[Test]
	public void A_timeout_maps_to_timeout()
	{
		var client = ClientThrowing(new TaskCanceledException());

		var failure = Assert.ThrowsAsync<VikunjaException>(() => client.GetOpenTasksAsync(5, CancellationToken.None));

		Assert.That(failure!.Reason, Is.EqualTo(VikunjaFailure.Timeout));
	}

	[Test]
	public void The_caller_cancelling_is_not_reported_as_a_timeout()
	{
		var client = ClientThrowing(new TaskCanceledException());
		using var cancelled = new CancellationTokenSource();
		cancelled.Cancel();

		Assert.ThrowsAsync<TaskCanceledException>(() => client.GetOpenTasksAsync(5, cancelled.Token));
	}

	private static VikunjaClient ClientThrowing(Exception exception)
	{
		var credentials = new VikunjaCredentials();
		credentials.Apply(new Uri("https://vikunja.test/api/v1/"), "t");
		return new VikunjaClient(new HttpClient(new ThrowingHandler(exception)), credentials);
	}

	private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			=> throw exception;
	}

	[Test]
	public async Task The_token_is_sent_as_a_bearer_header()
	{
		string? header = null;
		var fake = new FakeVikunja();
		fake.Respond = (_, _) => FakeVikunja.Json("[]");
		var client = new HttpClient(new HeaderSpy(fake, value => header = value));
		var credentials = new VikunjaCredentials();
		credentials.Apply(new Uri("https://vikunja.test/api/v1/"), "secret-token");

		await new VikunjaClient(client, credentials).GetOpenTasksAsync(5, CancellationToken.None);

		Assert.That(header, Is.EqualTo("Bearer secret-token"));
	}

	private sealed class HeaderSpy(HttpMessageHandler inner, Action<string?> capture) : DelegatingHandler(inner)
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			capture(request.Headers.Authorization?.ToString());
			return base.SendAsync(request, cancellationToken);
		}
	}
}
