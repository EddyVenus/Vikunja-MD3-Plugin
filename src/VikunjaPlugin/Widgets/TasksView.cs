using System.Globalization;
using MacroDeck.Localization;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;
using VikunjaPlugin.Api;

namespace VikunjaPlugin.Widgets;

internal static class TasksView
{
	private const double BaseTextSize = 0.085;
	private const double BasePadding = 0.025;
	private const double BaseGap = 0.02;
	private const double TilePadding = 0.05;
	private const string RowBackground = "#ffffff1f";

	// A press target is drawn in a button, whose text comes out about a tenth taller than a plain row.
	private const double ButtonRowFactor = 1.1;

	/// <summary>
	/// Draws the open tasks as rows; a null <paramref name="onCheck"/> makes them read-only. Every length is a
	/// fraction of the tile's smaller side, so the row count is what has to follow the tile's shape and the settings.
	/// </summary>
	public static UiElement Build(
		TaskStore store,
		WidgetSettings settings,
		Func<VikunjaTask, CancellationToken, Task>? onCheck)
	{
		var rows = new RowLayout(settings, onCheck is not null);
		return new UiStack
		{
			Key = "tasks",
			Padding = TilePadding,
			Gap = rows.Gap,
			Children =
			[
				Message("empty", store, StoreStatus.Ready, Strings.Widgets.Empty(), rows),
				Message("loading", store, StoreStatus.Loading, Strings.Widgets.Loading(), rows),
				Message("not-configured", store, StoreStatus.NotConfigured, Strings.Widgets.NotConfigured(), rows),
				Message("unavailable", store, StoreStatus.Unavailable, Strings.Widgets.Unavailable(), rows),
				new UiResponsive
				{
					Key = "list",
					Fill = true,
					Default = Rows("compact", 1.0, store, rows, onCheck),
					// Tried in order: the taller the tile is relative to its width, the more rows fit.
					Variants =
					[
						new UiResponsiveVariant { MaxAspect = 0.4, Content = Rows("tallest", 2.5, store, rows, onCheck) },
						new UiResponsiveVariant { MaxAspect = 0.55, Content = Rows("taller", 1.82, store, rows, onCheck) },
						new UiResponsiveVariant { MaxAspect = 0.8, Content = Rows("tall", 1.25, store, rows, onCheck) },
					],
				},
			],
		};
	}

	/// <param name="minHeight">The least height, in tile bases, a tile has when this variant is chosen.</param>
	private static UiStack Rows(
		string key,
		double minHeight,
		TaskStore store,
		RowLayout rows,
		Func<VikunjaTask, CancellationToken, Task>? onCheck)
	{
		var count = rows.RowsThatFit(minHeight);
		return new UiStack
		{
			Key = key,
			Gap = rows.Gap,
			Children =
			[
				new UiRepeat<VikunjaTask>
				{
					Key = "rows",
					Items = UiValue.From<IReadOnlyList<VikunjaTask>>(() => store.Tasks.Value.Take(count).ToList()),
					KeySelector = task => task.Id.ToString(CultureInfo.InvariantCulture),
					Template = (task, rowKey) => Row(task, rowKey, rows, onCheck),
				},
			],
		};
	}

	/// <summary>Shown only while there is nothing to list, so the tile says why instead of looking empty.</summary>
	private static UiWhen Message(string key, TaskStore store, StoreStatus status, LocalizedText text, RowLayout rows) => new()
	{
		Key = key,
		Condition = () => store.Tasks.Value.Count == 0 && store.Status.Value == status,
		Content = () => new UiTextRun
		{
			Key = "message",
			Text = text,
			Size = rows.TextSize,
			MinSize = rows.TextSize * 0.6,
			Wrap = UiValue.Of(true),
			Role = UiComponentTextRoles.Secondary,
		},
	};

	private static UiElement Row(VikunjaTask task, string key, RowLayout rows, Func<VikunjaTask, CancellationToken, Task>? onCheck)
	{
		if (onCheck is null)
		{
			return Card(key, task.Title, rows);
		}

		return new UiButton
		{
			Key = key,
			Events = [UiEventHandler.OnAsync(UiComponentEvents.Press, cancellationToken => onCheck(task, cancellationToken))],
			Children = [Card("card", $"[ ] {task.Title}", rows)],
		};
	}

	/// <summary>A rounded one-line row at a fixed text size, ellipsized when too long, so every row has the same height.</summary>
	private static UiModifier Card(string key, string title, RowLayout rows) => new()
	{
		Key = key,
		Background = RowBackground,
		Radius = rows.Radius,
		Padding = rows.Padding,
		Child = new UiTextRun { Key = "label", Text = title, Size = rows.TextSize },
	};

	/// <summary>The lengths of one row for the chosen settings, and how many rows a tile of a given height holds.</summary>
	internal sealed class RowLayout(WidgetSettings settings, bool pressable)
	{
		public double TextSize { get; } = BaseTextSize * settings.FontScale;

		public double Padding { get; } = BasePadding * settings.CardScale;

		public double Gap { get; } = BaseGap * settings.CardScale;

		public double Radius { get; } = 0.05 * Math.Min(settings.CardScale, 1.5);

		public int RowsThatFit(double minHeight)
		{
			var rowHeight = (TextSize + 2 * Padding) * (pressable ? ButtonRowFactor : 1);
			var fit = (int)Math.Floor((minHeight - 2 * TilePadding + Gap) / (rowHeight + Gap));
			return Math.Clamp(fit, 1, settings.MaxTasks);
		}
	}
}
