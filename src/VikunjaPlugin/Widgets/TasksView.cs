using System.Globalization;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;
using VikunjaPlugin.Api;

namespace VikunjaPlugin.Widgets;

internal static class TasksView
{
	/// <summary>Draws the open tasks; a null <paramref name="onCheck"/> makes the rows plain read-only text.</summary>
	public static UiElement Build(
		TaskStore store,
		int maxTasks,
		Func<VikunjaTask, CancellationToken, Task>? onCheck)
		=> new UiStack
		{
			Key = "tasks",
			Padding = 0.05,
			Gap = 0.02,
			Children =
			[
				new UiRepeat<VikunjaTask>
				{
					Key = "rows",
					Items = UiValue.From<IReadOnlyList<VikunjaTask>>(() => store.Tasks.Value.Take(maxTasks).ToList()),
					KeySelector = task => task.Id.ToString(CultureInfo.InvariantCulture),
					Template = (task, key) => Row(task, key, onCheck),
				},
				new UiWhen
				{
					Key = "empty",
					Condition = () => store.Tasks.Value.Count == 0,
					Content = () => new UiTextRun
					{
						Key = "message",
						Text = Strings.Widgets.Empty(),
						Size = 0.09,
						Role = UiComponentTextRoles.Secondary,
					},
				},
			],
		};

	private static UiElement Row(VikunjaTask task, string key, Func<VikunjaTask, CancellationToken, Task>? onCheck)
	{
		if (onCheck is null)
		{
			return new UiTextRun { Key = key, Text = $"- {task.Title}", Size = 0.09 };
		}

		return new UiButton
		{
			Key = key,
			Events = [UiEventHandler.OnAsync(UiComponentEvents.Press, cancellationToken => onCheck(task, cancellationToken))],
			Children = [new UiTextRun { Key = "label", Text = $"[ ] {task.Title}", Size = 0.09 }],
		};
	}
}
