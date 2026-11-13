namespace VikunjaPlugin.Api;

/// <summary>
/// What the config flow produced, held as a singleton because the plugin is configured after it starts,
/// so the typed client cannot bake the address and token in at registration time.
/// </summary>
public sealed class VikunjaCredentials
{
	private volatile Snapshot _current = new(null, null);

	public Uri? BaseAddress => _current.BaseAddress;

	public string? Token => _current.Token;

	public void Apply(Uri? baseAddress, string? token) => _current = new Snapshot(baseAddress, token);

	public void Clear() => _current = new Snapshot(null, null);

	private sealed record Snapshot(Uri? BaseAddress, string? Token);
}
