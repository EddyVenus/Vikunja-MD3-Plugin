using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using VikunjaPlugin.Api;
using VikunjaPlugin.Widgets;

namespace VikunjaPlugin.Tests;

/// <summary>A scripted Vikunja: answers from a queue-free route table and records every request it saw.</summary>
internal sealed class FakeVikunja : HttpMessageHandler
{
	public List<(HttpMethod Method, string Path, string Body, string? Token)> Requests { get; } = [];

	public Func<HttpMethod, string, HttpResponseMessage> Respond { get; set; } = (_, _) => new(HttpStatusCode.NotFound);

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
		var path = request.RequestUri!.PathAndQuery;
		Requests.Add((request.Method, path, body, request.Headers.Authorization?.Parameter));
		return Respond(request.Method, path);
	}

	public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK)
		=> new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

	/// <summary>An API v2 task list page around the given JSON array.</summary>
	public static HttpResponseMessage Tasks(string items, long totalPages = 1)
		=> Json($$"""{"items":{{items}},"total":0,"page":1,"per_page":50,"total_pages":{{totalPages}}}""");

	public static HttpResponseMessage Info(string version = "v2.8.0") => Json($$"""{"version":"{{version}}"}""");

	public TaskStore CreateStore(out VikunjaCredentials credentials, bool configured = true)
	{
		credentials = new VikunjaCredentials();
		if (configured)
		{
			credentials.Apply(new Uri("https://vikunja.test/api/v2/"), "secret-token");
		}

		return new TaskStore(new VikunjaClient(new HttpClient(this), credentials), Serilog.Core.Logger.None);
	}
}

internal static class TestServices
{
	public static IServiceCollection AddVikunjaTestServices(this IServiceCollection services)
	{
		services.AddVikunjaApi();
		services.AddSingleton<TaskStore>();
		return services;
	}
}
