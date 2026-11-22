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
		var fake = new FakeVikunja { Respond = (_, _) => FakeVikunja.Tasks(TwoTasks) };
		var store = fake.CreateStore(out _);

		await store.RefreshAsync(CancellationToken.None);

		Assert.That(store.Tasks.Peek().Select(t => t.Title), Is.EqualTo(ExpectedTitles));
		Assert.That(fake.Requests[0].Path, Does.Contain("done%20%3D%20false"));
	}

	[Test]
	public async Task More_than_fifty_tasks_are_kept_so_the_pages_can_reach_them()
	{
		var fake = new FakeVikunja { Respond = Paged(120) };
		var store = fake.CreateStore(out _);

		await store.RefreshAsync(CancellationToken.None);

		Assert.That(store.Tasks.Peek(), Has.Count.EqualTo(120));
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
		var fake = new FakeVikunja { Respond = (_, _) => FakeVikunja.Tasks(TwoTasks) };
		var store = fake.CreateStore(out _);
		await store.RefreshAsync(CancellationToken.None);

		fake.Respond = (_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError);
		await store.RefreshAsync(CancellationToken.None);

		Assert.That(store.Tasks.Peek(), Has.Count.EqualTo(2));
	}

	[Test]
	public async Task Completing_patches_only_done_and_removes_the_task()
	{
		var fake = new FakeVikunja
		{
			Respond = (method, _) => method == HttpMethod.Patch
				? FakeVikunja.Json("""{"id":1,"title":"Buy milk","done":true}""")
				: FakeVikunja.Tasks(TwoTasks),
		};
		var store = fake.CreateStore(out _);
		await store.RefreshAsync(CancellationToken.None);

		await store.CompleteAsync(1, CancellationToken.None);

		var patch = fake.Requests.Single(r => r.Method != HttpMethod.Get);
		using var body = JsonDocument.Parse(patch.Body);
		Assert.That(patch.Method, Is.EqualTo(HttpMethod.Patch));
		Assert.That(patch.Path, Is.EqualTo("/api/v2/tasks/1"));
		Assert.That(body.RootElement.EnumerateObject().Select(p => p.Name), Is.EqualTo(OnlyDone));
		Assert.That(body.RootElement.GetProperty("done").GetBoolean(), Is.True);
		Assert.That(store.Tasks.Peek().Select(t => t.Id), Is.EqualTo(RemainingIds));
	}

	[Test]
	public async Task Completing_keeps_the_task_and_reports_when_the_token_is_rejected()
	{
		var fake = new FakeVikunja
		{
			Respond = (method, path) => method == HttpMethod.Get && !path.Contains("tasks/")
				? FakeVikunja.Tasks(TwoTasks)
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
			Respond = (method, _) => method == HttpMethod.Patch
				? FakeVikunja.Json("""{"id":1,"title":"Buy milk","done":false}""")
				: FakeVikunja.Tasks(TwoTasks),
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
		Assert.That(failure!.Reason, Is.EqualTo(VikunjaFailure.NotVikunja));
	}

	// Serves `total` tasks as pages of `perPage`, the way a server capping per_page would, and records the pages asked for.
	private static Func<HttpMethod, string, HttpResponseMessage> Paged(int total, List<int>? pagesAsked = null, int perPage = 50) => (_, path) =>
	{
		var page = int.Parse(path.Split("&page=")[1].Split('&')[0], System.Globalization.CultureInfo.InvariantCulture);
		pagesAsked?.Add(page);
		var first = (page - 1) * perPage + 1;
		var ids = Enumerable.Range(first, Math.Max(0, Math.Min(perPage, total - first + 1)));
		var items = "[" + string.Join(",", ids.Select(id => $$"""{"id":{{id}},"title":"Task {{id}}","done":false}""")) + "]";
		return FakeVikunja.Tasks(items, totalPages: (total + perPage - 1) / perPage);
	};

	[Test]
	public async Task A_server_that_caps_the_page_size_is_still_read_to_the_end()
	{
		var fake = new FakeVikunja { Respond = Paged(70, perPage: 20) };
		var store = fake.CreateStore(out _);

		await store.RefreshAsync(CancellationToken.None);

		Assert.That(store.Tasks.Peek(), Has.Count.EqualTo(70));
	}

	[Test]
	public async Task A_vikunja_without_api_v2_is_reported_as_too_old()
	{
		var fake = new FakeVikunja { Respond = (_, _) => new HttpResponseMessage(HttpStatusCode.NotFound) };
		var store = fake.CreateStore(out _);

		await store.RefreshAsync(CancellationToken.None);

		Assert.That(store.Status.Peek(), Is.EqualTo(StoreStatus.Outdated));
	}

	[Test]
	public async Task At_most_two_hundred_tasks_are_read_and_kept()
	{
		var pages = new List<int>();
		var fake = new FakeVikunja { Respond = Paged(500, pages) };
		var store = fake.CreateStore(out _);

		await store.RefreshAsync(CancellationToken.None);

		Assert.That(store.Tasks.Peek(), Has.Count.EqualTo(200));
		Assert.That(pages, Is.EqualTo(FourPages));
	}

	[TestCase("")]
	[TestCase("{}")]
	[TestCase("<html>login</html>")]
	public async Task An_empty_or_unexpected_answer_keeps_the_old_tasks_and_marks_the_tile_unavailable(string body)
	{
		var fake = new FakeVikunja { Respond = (_, _) => FakeVikunja.Tasks(TwoTasks) };
		var store = fake.CreateStore(out _);
		await store.RefreshAsync(CancellationToken.None);

		fake.Respond = (_, _) => FakeVikunja.Json(body);
		Assert.DoesNotThrowAsync(() => store.RefreshAsync(CancellationToken.None));

		Assert.That(store.Tasks.Peek(), Has.Count.EqualTo(2));
		Assert.That(store.Status.Peek(), Is.EqualTo(StoreStatus.Unavailable));
	}

	[Test]
	public async Task An_empty_list_and_a_null_body_both_mean_no_tasks()
	{
		foreach (var body in EmptyBodies)
		{
			var fake = new FakeVikunja { Respond = (_, _) => FakeVikunja.Json(body) };
			var store = fake.CreateStore(out _);

			await store.RefreshAsync(CancellationToken.None);

			Assert.That(store.Tasks.Peek(), Is.Empty);
			Assert.That(store.Status.Peek(), Is.EqualTo(StoreStatus.Ready));
		}
	}

	[Test]
	public async Task A_request_that_never_answers_times_out_and_frees_the_store_for_the_next_call()
	{
		var credentials = new VikunjaCredentials();
		credentials.Apply(new Uri("https://vikunja.test/api/v2/"), "t");
		var hang = new HangingHandler();
		var store = new TaskStore(new VikunjaClient(new HttpClient(hang) { Timeout = TimeSpan.FromMilliseconds(200) }, credentials), Serilog.Core.Logger.None);

		var failure = Assert.ThrowsAsync<VikunjaException>(() => store.CompleteAsync(1, CancellationToken.None));
		Assert.That(failure!.Reason, Is.EqualTo(VikunjaFailure.Timeout));

		// The gate was released: a second call fails the same way instead of waiting behind the first.
		var second = Assert.ThrowsAsync<VikunjaException>(() => store.CompleteAsync(1, CancellationToken.None));
		Assert.That(second!.Reason, Is.EqualTo(VikunjaFailure.Timeout));
		Assert.DoesNotThrowAsync(() => store.RefreshAsync(CancellationToken.None));
		Assert.That(store.Status.Peek(), Is.EqualTo(StoreStatus.Unavailable));
	}

	private sealed class HangingHandler : HttpMessageHandler
	{
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			await Task.Delay(Timeout.Infinite, cancellationToken);
			return new HttpResponseMessage();
		}
	}

	[Test]
	public void The_http_client_gives_up_after_ten_seconds()
	{
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		services.AddVikunjaApi();
		using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
		var factory = (IHttpClientFactory)provider.GetService(typeof(IHttpClientFactory))!;

		Assert.That(factory.CreateClient(nameof(VikunjaClient)).Timeout, Is.EqualTo(TimeSpan.FromSeconds(10)));
	}

	private sealed class ListSink : Serilog.Core.ILogEventSink
	{
		public List<Serilog.Events.LogEvent> Events { get; } = [];

		public void Emit(Serilog.Events.LogEvent logEvent) => Events.Add(logEvent);

		public string Everything() => string.Join(Environment.NewLine, Events.Select(e => e.RenderMessage(System.Globalization.CultureInfo.InvariantCulture) + e.Exception));
	}

	private static (TaskStore Store, ListSink Sink, FakeVikunja Fake) LoggedStore(string token)
	{
		var fake = new FakeVikunja { Respond = (_, _) => FakeVikunja.Tasks(TwoTasks) };
		var credentials = new VikunjaCredentials();
		credentials.Apply(new Uri("https://vikunja.test/api/v2/"), token);
		var sink = new ListSink();
		var logger = new Serilog.LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
		return (new TaskStore(new VikunjaClient(new HttpClient(fake), credentials), logger), sink, fake);
	}

	[Test]
	public async Task A_run_of_failures_is_logged_once_not_every_minute()
	{
		var (store, sink, fake) = LoggedStore("tk_secret");
		fake.Respond = (_, _) => throw new HttpRequestException("connection refused");

		for (var attempt = 0; attempt < 5; attempt++)
		{
			await store.RefreshAsync(CancellationToken.None);
		}

		Assert.That(sink.Events.Count(e => e.Level == Serilog.Events.LogEventLevel.Warning), Is.EqualTo(1));
	}

	[Test]
	public async Task The_token_never_appears_in_logs_or_error_text_whatever_goes_wrong()
	{
		const string token = "tk_SECRET_do_not_log";
		var (store, sink, fake) = LoggedStore(token);
		var failures = new List<string>();

		foreach (var respond in new Func<HttpMethod, string, HttpResponseMessage>[]
		{
			(_, _) => FakeVikunja.Tasks(TwoTasks),
			(_, _) => throw new HttpRequestException("connection refused"),
			(_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized),
			(_, _) => FakeVikunja.Json("<html>login</html>"),
			(_, _) => FakeVikunja.Json(""),
		})
		{
			fake.Respond = respond;
			await store.RefreshAsync(CancellationToken.None);
			try
			{
				await store.CompleteAsync(1, CancellationToken.None);
			}
			catch (VikunjaException exception)
			{
				failures.Add(exception.ToString());
			}
		}

		Assert.That(sink.Everything(), Does.Not.Contain(token));
		Assert.That(string.Join(Environment.NewLine, failures), Does.Not.Contain(token));
	}

	[Test]
	public async Task Dated_tasks_come_first_soonest_due_first_and_undated_ones_last()
	{
		var fake = new FakeVikunja
		{
			Respond = (_, _) => FakeVikunja.Tasks("""
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
	private static readonly int[] FourPages = [1, 2, 3, 4];
	private static readonly string[] EmptyBodies = ["""{"items":[],"total_pages":0}""", "null"];
	private static readonly string[] OnlyDone = ["done"];

	[Test]
	public async Task Priority_is_read_and_missing_priority_means_none()
	{
		var fake = new FakeVikunja { Respond = (_, _) => FakeVikunja.Tasks("""[{"id":1,"title":"Urgent","priority":4},{"id":2,"title":"Plain"}]""") };
		var store = fake.CreateStore(out _);

		await store.RefreshAsync(CancellationToken.None);

		Assert.That(store.Tasks.Peek().Select(t => t.Priority), Is.EqualTo(ExpectedPriorities));
	}

	[Test]
	public async Task Status_tells_apart_ready_unavailable_unauthorized_and_not_configured()
	{
		var fake = new FakeVikunja { Respond = (_, _) => FakeVikunja.Tasks(TwoTasks) };
		var store = fake.CreateStore(out var credentials);
		Assert.That(store.Status.Peek(), Is.EqualTo(StoreStatus.Loading));

		await store.RefreshAsync(CancellationToken.None);
		Assert.That(store.Status.Peek(), Is.EqualTo(StoreStatus.Ready));

		fake.Respond = (_, _) => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
		await store.RefreshAsync(CancellationToken.None);
		Assert.That(store.Status.Peek(), Is.EqualTo(StoreStatus.Unavailable));

		fake.Respond = (_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized);
		await store.RefreshAsync(CancellationToken.None);
		Assert.That(store.Status.Peek(), Is.EqualTo(StoreStatus.Unauthorized));

		credentials.Clear();
		await store.RefreshAsync(CancellationToken.None);
		Assert.That(store.Status.Peek(), Is.EqualTo(StoreStatus.NotConfigured));
	}

	[Test]
	public async Task An_unchanged_list_is_not_written_back()
	{
		var fake = new FakeVikunja { Respond = (_, _) => FakeVikunja.Tasks(TwoTasks) };
		var store = fake.CreateStore(out _);
		await store.RefreshAsync(CancellationToken.None);
		var before = store.Tasks.Peek();

		await store.RefreshAsync(CancellationToken.None);

		Assert.That(store.Tasks.Peek(), Is.SameAs(before));
	}

	[Test]
	public async Task Reset_forgets_the_previous_accounts_tasks()
	{
		var fake = new FakeVikunja { Respond = (_, _) => FakeVikunja.Tasks(TwoTasks) };
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
		credentials.Apply(new Uri("https://vikunja.test/api/v2/"), "t");
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
		fake.Respond = (_, _) => FakeVikunja.Tasks("[]");
		var client = new HttpClient(new HeaderSpy(fake, value => header = value));
		var credentials = new VikunjaCredentials();
		credentials.Apply(new Uri("https://vikunja.test/api/v2/"), "secret-token");

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
