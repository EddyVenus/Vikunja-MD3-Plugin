using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace VikunjaPlugin.Api;

public sealed class VikunjaClient(HttpClient httpClient, VikunjaCredentials credentials)
{
	public async Task<IReadOnlyList<VikunjaTask>> GetOpenTasksAsync(int limit, CancellationToken cancellationToken)
	{
		var filter = Uri.EscapeDataString("done = false");
		using var message = CreateMessage(HttpMethod.Get,
			$"tasks?filter={filter}&sort_by=due_date&sort_by=id&order_by=asc&order_by=asc&per_page={limit}");

		return await SendAsync<List<VikunjaTask>>(message, cancellationToken) ?? [];
	}

	public async Task CompleteTaskAsync(long taskId, CancellationToken cancellationToken)
	{
		// Vikunja resets every field missing from an update, so the whole task is sent back with done set.
		using var get = CreateMessage(HttpMethod.Get, $"tasks/{taskId}");
		var task = await SendAsync<JsonObject>(get, cancellationToken)
			?? throw new VikunjaException(VikunjaFailure.Rejected);
		task["done"] = true;

		using var post = CreateMessage(HttpMethod.Post, $"tasks/{taskId}");
		post.Content = JsonContent.Create(task);
		using var response = await SendCoreAsync(post, cancellationToken);
	}

	/// <summary>Takes the values explicitly so the config flow can check what the user typed before applying it.</summary>
	public async Task VerifyAsync(Uri baseAddress, string token, CancellationToken cancellationToken)
	{
		using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(baseAddress, "user"));
		message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		using var response = await SendCoreAsync(message, cancellationToken);
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

	private async Task<T?> SendAsync<T>(HttpRequestMessage message, CancellationToken cancellationToken)
	{
		using var response = await SendCoreAsync(message, cancellationToken);
		return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
	}

	private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage message, CancellationToken cancellationToken)
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
		throw new VikunjaException(status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
			? VikunjaFailure.Unauthorized
			: VikunjaFailure.Rejected);
	}
}
