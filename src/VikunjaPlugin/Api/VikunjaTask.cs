using System.Text.Json.Serialization;

namespace VikunjaPlugin.Api;

public sealed record VikunjaTask(
	long Id,
	string Title,
	bool Done,
	[property: JsonPropertyName("due_date")] DateTimeOffset? DueDate = null,
	[property: JsonPropertyName("priority")] int Priority = 0)
{
	/// <summary>Vikunja sends the zero date for tasks without a due date.</summary>
	public bool HasDueDate => DueDate is { Year: > 1 };
}
