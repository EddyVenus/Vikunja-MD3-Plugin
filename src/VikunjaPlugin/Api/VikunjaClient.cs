using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VikunjaPlugin.Api;

public sealed class VikunjaClient(HttpClient httpClient, VikunjaCredentials credentials)
{
	/// <summary>The first release whose API v2 and token permission names the plugin is written against.</summary>
	public static readonly Version MinimumVersion = new(2, 7, 1);

	private const int PageSize = 50;

	/// <summary>Open tasks with dated ones first, soonest due first. Sorted here because the server's order for undated tasks is database specific.</summary>
	public async Task<IReadOnlyList<VikunjaTask>> GetOpenTasksAsync(int limit, CancellationToken cancellationToken)
	{
		var filter = Uri.EscapeDataString("done = false");
		var all = new List<VikunjaTask>();
		// The server may cap per_page below what is asked, so paging follows total_pages rather than the page size.
		for (var page = 1; all.Count < limit; page++)
		{
			using var message = CreateMessage(HttpMethod.Get, $"tasks?filter={filter}&sort_by=id&order_by=asc&per_page={PageSize}&page={page}");
			var result = await SendAsync<TaskPage>(message, cancellationToken, notFound: VikunjaFailure.Outdated);
			if (result is null)
			{
				break;
			}

			var tasks = result.Items ?? throw new VikunjaException(VikunjaFailure.NotVikunja);
			all.AddRange(tasks);
			if (tasks.Count == 0 || page >= result.TotalPages)
			{
				break;
			}
		}

		return all
			.OrderBy(task => task.HasDueDate ? 0 : 1)
			.ThenBy(task => task.DueDate)
			.ThenBy(task => task.Id)
			.Take(limit)
			.ToList();
	}

	/// <summary>Returns whether the task is now done; a repeating task stays open and only moves its dates.</summary>
	public async Task<bool> CompleteTaskAsync(long taskId, CancellationToken cancellationToken)
	{
		// A merge patch changes only done, so edits made elsewhere since the last refresh are kept.
		using var patch = CreateMessage(HttpMethod.Patch, $"tasks/{taskId.ToString(CultureInfo.InvariantCulture)}");
		patch.Content = new StringContent("""{"done":true}""", Encoding.UTF8, "application/merge-patch+json");
		var updated = await SendAsync<VikunjaTask>(patch, cancellationToken)
			?? throw new VikunjaException(VikunjaFailure.Rejected);
		return updated.Done;
	}

	/// <summary>Takes the values explicitly so the config flow can check what the user typed before applying it.</summary>
	public async Task VerifyAsync(Uri baseAddress, string token, CancellationToken cancellationToken)
	{
		using var info = new HttpRequestMessage(HttpMethod.Get, new Uri(baseAddress, "info"));
		// A wrong address also answers 404 or a body without a version, so only a readable old version means outdated.
		var instance = await SendAsync<InstanceInfo>(info, cancellationToken, notFound: VikunjaFailure.NotVikunja);
		if (instance?.Version is null)
		{
			throw new VikunjaException(VikunjaFailure.NotVikunja);
		}

		if (!IsSupported(instance.Version))
		{
			throw new VikunjaException(VikunjaFailure.Outdated);
		}

		// Reads tasks because that is the permission the plugin needs, so a narrowly scoped token passes.
		using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(baseAddress, "tasks?per_page=1"));
		message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		// A success status is not enough: another web app or a login page can answer 200 to any path, so the body must be a task page.
		if (await SendAsync<TaskPage>(message, cancellationToken) is not { Items: not null })
		{
			throw new VikunjaException(VikunjaFailure.NotVikunja);
		}
	}

	/// <summary>Development builds report versions such as "v2.8.0-42-gabc1234" or "dev"; one that cannot be read is let through and the API itself decides.</summary>
	internal static bool IsSupported(string? version)
	{
		if (version is null)
		{
			return false;
		}

		var core = version.TrimStart('v', 'V').Split('-', '+')[0];
		return !Version.TryParse(core, out var parsed) || parsed >= MinimumVersion;
	}

	private HttpRequestMessage CreateMessage(HttpMethod method, string path)
	{
		if (credentials is not { BaseAddress: { } baseAddress, Token: { } token })
		{
			throw new VikunjaException(VikunjaFailure.NotConfigured);
		}

		var message = new HttpRequestMessage(method, new Uri(baseAddress, path));
		message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return message;
	}

	private async Task<T?> SendAsync<T>(HttpRequestMessage message, CancellationToken cancellationToken, VikunjaFailure notFound = VikunjaFailure.Rejected)
	{
		using var response = await SendCoreAsync(message, notFound, cancellationToken);
		try
		{
			return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
		}
		catch (Exception exception) when (exception is JsonException or NotSupportedException)
		{
			// A proxy or login page answering 200 with HTML must not escape as an unhandled exception.
			throw new VikunjaException(VikunjaFailure.NotVikunja, exception);
		}
	}

	private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage message, VikunjaFailure notFound, CancellationToken cancellationToken)
	{
		HttpResponseMessage response;
		try
		{
			response = await httpClient.SendAsync(message, cancellationToken);
		}
		catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
		{
			throw new VikunjaException(VikunjaFailure.Timeout, exception);
		}
		catch (HttpRequestException exception)
		{
			throw new VikunjaException(VikunjaFailure.Unreachable, exception);
		}

		if (response.IsSuccessStatusCode)
		{
			return response;
		}

		var status = response.StatusCode;
		response.Dispose();
		throw new VikunjaException(status switch
		{
			HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => VikunjaFailure.Unauthorized,
			HttpStatusCode.NotFound => notFound,
			_ => VikunjaFailure.Rejected,
		});
	}

	private sealed record TaskPage(
		List<VikunjaTask>? Items,
		[property: JsonPropertyName("total_pages")] long TotalPages);

	private sealed record InstanceInfo(string? Version);
}
