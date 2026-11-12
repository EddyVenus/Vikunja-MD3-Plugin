using MacroDeck.Localization;

namespace VikunjaPlugin.Api;

public enum VikunjaFailure
{
	NotConfigured,
	Unauthorized,
	Unreachable,
	Timeout,
	Rejected,
}

public sealed class VikunjaException(VikunjaFailure reason, Exception? inner = null)
	: Exception($"Vikunja request failed: {reason}.", inner)
{
	public VikunjaFailure Reason { get; } = reason;

	public LocalizedText UserMessage => Reason switch
	{
		VikunjaFailure.NotConfigured => Strings.Errors.NotConfigured(),
		VikunjaFailure.Unauthorized => Strings.Errors.Unauthorized(),
		VikunjaFailure.Unreachable => Strings.Errors.Unreachable(),
		VikunjaFailure.Timeout => Strings.Errors.Timeout(),
		_ => Strings.Errors.Rejected(),
	};
}
