using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.ConfigFlow;
using VikunjaPlugin.Api;

namespace VikunjaPlugin.ConfigFlow;

internal sealed class VikunjaConfigFlow(VikunjaClient client) : IConfigFlow
{
	internal const string ServerUrlKey = "serverUrl";
	internal const string TokenKey = "token";

	private const string StepId = "connection";

	public Task<ConfigFlowResult> StartAsync(IConfigFlowContext context, CancellationToken cancellationToken)
		=> Task.FromResult(ConfigFlowResult.Step(ConnectionStep()));

	public async Task<ConfigFlowResult> SubmitAsync(
		string stepId,
		IReadOnlyDictionary<string, object?> input,
		IConfigFlowContext context,
		CancellationToken cancellationToken)
	{
		if (stepId != StepId)
		{
			return ConfigFlowResult.Error(ConnectionStep(), Strings.ConfigFlow.UnknownStep());
		}

		if (input.GetValueOrDefault(ServerUrlKey) is not string url || ToApiAddress(url) is not { } baseAddress)
		{
			return FieldError(ServerUrlKey,
				MacroDeckStrings.Validation.InvalidUrl(Strings.ConfigFlow.ServerUrl.Label()));
		}

		if (input.GetValueOrDefault(TokenKey) is not string rawToken || rawToken.Trim() is not { Length: > 0 } token)
		{
			return FieldError(TokenKey,
				MacroDeckStrings.Validation.Required(Strings.ConfigFlow.Token.Label()));
		}

		try
		{
			await client.VerifyAsync(baseAddress, token, cancellationToken);
		}
		catch (VikunjaException exception)
		{
			return ConfigFlowResult.Error(ConnectionStep(), exception.UserMessage);
		}

		return ConfigFlowResult.Complete($"Vikunja ({baseAddress.Host})", new Dictionary<string, ConfigFlowValue>
		{
			[ServerUrlKey] = ConfigFlowValue.Plain(baseAddress.ToString()),
			[TokenKey] = ConfigFlowValue.Secret(token),
		});
	}

	/// <summary>Accepts the instance address with or without the API path and returns the API root.</summary>
	internal static Uri? ToApiAddress(string input)
	{
		var trimmed = input.Trim().TrimEnd('/');
		if (trimmed.EndsWith("/api/v1", StringComparison.OrdinalIgnoreCase))
		{
			trimmed = trimmed[..^"/api/v1".Length];
		}

		return Uri.TryCreate($"{trimmed}/api/v1/", UriKind.Absolute, out var address)
			&& address.Scheme is "http" or "https"
			? address
			: null;
	}

	private static ConfigFlowResult FieldError(string field, LocalizedText message)
		=> ConfigFlowResult.Error(ConnectionStep(), message, new Dictionary<string, LocalizedText> { [field] = message });

	private static ConfigFlowStep ConnectionStep() => new()
	{
		StepId = StepId,
		Title = Strings.ConfigFlow.Title(),
		Description = Strings.ConfigFlow.Description(),
		Instructions = [new ConfigFlowInstruction { Text = Strings.ConfigFlow.TokenInstruction() }],
		Fields =
		[
			ActionParameter.Url(ServerUrlKey,
				label: Strings.ConfigFlow.ServerUrl.Label(),
				placeholder: "https://vikunja.example.com",
				required: true,
				autoPrefixHttps: true),
			ActionParameter.Secret(TokenKey, label: Strings.ConfigFlow.Token.Label(), required: true),
		],
	};
}
