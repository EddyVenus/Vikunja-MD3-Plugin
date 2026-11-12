using System.Globalization;
using MacroDeck.Localization;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;
using VikunjaPlugin.Api;

namespace VikunjaPlugin.Widgets;

internal static class TasksView
{
	private const double TextSize = 0.085;
	private const double MinTextSize = 0.06;
	private const string RowBackground = "#ffffff1f";

	/// <summary>
	/// Draws the open tasks as rows; a null <paramref name="onCheck"/> makes them read-only. Every length is a
	/// fraction of the tile's smaller side, so the row count is what has to follow the tile's shape.
	/// </summary>
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
				Message("empty", store, StoreStatus.Ready, Strings.Widgets.Empty()),
				Message("loading", store, StoreStatus.Loading, Strings.Widgets.Loading()),
				Message("not-configured", store, StoreStatus.NotConfigured, Strings.Widgets.NotConfigured()),
				Message("unavailable", store, StoreStatus.Unavailable, Strings.Widgets.Unavailable()),
				new UiResponsive
				{
					Key = "list",
					Fill = true,
					Default = Rows("compact", 5, store, maxTasks, onCheck),
					// Tried in order: the taller the tile is relative to its width, the more rows fit under the text size.
					Variants =
					[
						new UiResponsiveVariant { MaxAspect = 0.4, Content = Rows("tallest", 15, store, maxTasks, onCheck) },
						new UiResponsiveVariant { MaxAspect = 0.55, Content = Rows("taller", 11, store, maxTasks, onCheck) },
						new UiResponsiveVariant { MaxAspect = 0.8, Content = Rows("tall", 7, store, maxTasks, onCheck) },
					],
				},
			],
		};

	private static UiStack Rows(
		string key,
		int rowsThatFit,
		TaskStore store,
		int maxTasks,
		Func<VikunjaTask, CancellationToken, Task>? onCheck)
	{
		var count = Math.Min(rowsThatFit, maxTasks);
		return new UiStack
		{
			Key = key,
			Gap = 0.02,
			Children =
			[
				new UiRepeat<VikunjaTask>
				{
					Key = "rows",
					Items = UiValue.From<IReadOnlyList<VikunjaTask>>(() => store.Tasks.Value.Take(count).ToList()),
					KeySelector = task => task.Id.ToString(CultureInfo.InvariantCulture),
					Template = (task, rowKey) => Row(task, rowKey, onCheck),
				},
			],
		};
	}

	/// <summary>Shown only while there is nothing to list, so the tile says why instead of looking empty.</summary>
	private static UiWhen Message(string key, TaskStore store, StoreStatus status, LocalizedText text) => new()
	{
		Key = key,
		Condition = () => store.Tasks.Value.Count == 0 && store.Status.Value == status,
		Content = () => new UiTextRun
		{
			Key = "message",
			Text = text,
			Size = TextSize,
			MinSize = MinTextSize,
			Wrap = UiValue.Of(true),
			Role = UiComponentTextRoles.Secondary,
		},
	};

	private static UiElement Row(VikunjaTask task, string key, Func<VikunjaTask, CancellationToken, Task>? onCheck)
	{
		if (onCheck is null)
		{
			return Card(key, task.Title);
		}

		return new UiButton
		{
			Key = key,
			Events = [UiEventHandler.OnAsync(UiComponentEvents.Press, cancellationToken => onCheck(task, cancellationToken))],
			Children = [Card("card", $"[ ] {task.Title}")],
		};
	}

	/// <summary>A rounded one-line row at a fixed text size, ellipsized when too long, so every row has the same height.</summary>
	private static UiModifier Card(string key, string title) => new()
	{
		Key = key,
		Background = RowBackground,
		Radius = 0.05,
		Padding = 0.025,
		Child = new UiTextRun { Key = "label", Text = title, Size = TextSize },
	};
}
