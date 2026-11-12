using System.Globalization;
using MacroDeck.Ui.Dsl;
using VikunjaPlugin.Api;

namespace VikunjaPlugin.Widgets;

/// <summary>How a task's priority and due date are shown: the color of its dot and the words of its due label.</summary>
internal static class TaskLabels
{
	internal const string LateColor = "#ff5a5f";

	// Vikunja priorities: 0 unset, 1 low, 2 medium, 3 high, 4 urgent, 5 do now.
	internal static string PriorityColor(int priority) => priority switch
	{
		<= 0 => "#6b7078",
		1 => "#51cf66",
		2 => "#ffa94d",
		_ => LateColor,
	};

	internal static bool IsOverdue(VikunjaTask task, DateOnly today) => task.HasDueDate && LocalDay(task.DueDate!.Value) < today;

	private static DateOnly LocalDay(DateTimeOffset moment) => DateOnly.FromDateTime(moment.ToLocalTime().DateTime);

	internal static UiText DueLabel(DateTimeOffset due, DateOnly today)
	{
		var day = LocalDay(due);
		var days = day.DayNumber - today.DayNumber;
		return days switch
		{
			< 0 => Strings.Widgets.Due.Overdue(),
			0 => Strings.Widgets.Due.Today(),
			1 => Strings.Widgets.Due.Tomorrow(),
			< 7 => UiText.Of(day.ToString("ddd", CultureInfo.CurrentCulture)),
			_ => UiText.Of(day.ToString("MMM d", CultureInfo.CurrentCulture)),
		};
	}
}
