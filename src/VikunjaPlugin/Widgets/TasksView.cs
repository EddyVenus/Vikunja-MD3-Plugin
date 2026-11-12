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
	private const double LineThickness = 0.004;
	private const string LineColor = "#ffffff1f";
	private const string LateColor = "#ff5a5f";
	private const string Transparent = "#00000000";

	// A press target is drawn in a button, whose text comes out about a tenth taller than a plain row.
	private const double ButtonRowFactor = 1.1;

	// Tile shapes, as width over height. Squares (1x1, 2x2...) cannot be told apart by the plugin, only by shape.
	private const double SquareMinAspect = 0.8;
	private const double SquareMaxAspect = 1.25;
	private const double WideMinAspect = 1.5;

	/// <summary>
	/// Draws the open tasks as a minimal list; a null <paramref name="onCheck"/> makes it read-only. Every length is a
	/// fraction of the tile's smaller side, so the row count is what has to follow the tile's shape and the settings.
	/// </summary>
	public static UiElement Build(
		TaskStore store,
		WidgetSettings settings,
		Func<VikunjaTask, CancellationToken, Task>? onCheck)
	{
		var layout = new RowLayout(settings, onCheck is not null);
		var variants = new List<UiResponsiveVariant>
		{
			// Tried in order: the taller the tile is relative to its width, the more rows fit, and a header fits too.
			new() { MaxAspect = 0.4, Content = Rows("tallest", 2.5, store, layout, onCheck, header: true, due: false) },
			new() { MaxAspect = 0.55, Content = Rows("taller", 1.82, store, layout, onCheck, header: true, due: false) },
			new() { MaxAspect = SquareMinAspect, Content = Rows("tall", 1.25, store, layout, onCheck, header: true, due: false) },
		};

		if (settings.SquareCounter)
		{
			variants.Add(new() { MinAspect = SquareMinAspect, MaxAspect = SquareMaxAspect, Content = new UiModifier
			{
				Key = "counter-tile",
				Frame = UiValue.Of(new UiFrame { Width = new UiLength { Basis = 0, ParentFraction = 1 }, Height = new UiLength { Basis = 0, ParentFraction = 1 } }),
				Child = new UiStack { Key = "centered", Justify = UiValue.Of(UiComponentJustify.Center), Align = UiValue.Of(UiComponentAlignments.Center), Children = [Counter(store, layout)] },
			} });
		}

		// Wide tiles have the room for the due date; it is dropped on squares so the title keeps its width.
		variants.Add(new() { MinAspect = WideMinAspect, Content = Rows("wide", 1.0, store, layout, onCheck, header: false, due: true) });

		return new UiStack
		{
			Key = "tasks",
			Align = UiValue.Of(UiComponentAlignments.Stretch),
			Padding = TilePadding,
			Gap = layout.Gap,
			Children =
			[
				Message("empty", store, StoreStatus.Ready, Strings.Widgets.Empty(), layout),
				Message("loading", store, StoreStatus.Loading, Strings.Widgets.Loading(), layout),
				Message("not-configured", store, StoreStatus.NotConfigured, Strings.Widgets.NotConfigured(), layout),
				Message("unavailable", store, StoreStatus.Unavailable, Strings.Widgets.Unavailable(), layout),
				new UiResponsive
				{
					Key = "list",
					Fill = true,
					Default = Rows("compact", 1.0, store, layout, onCheck, header: false, due: false),
					Variants = variants,
				},
			],
		};
	}

	/// <param name="minHeight">The least height, in tile bases, a tile has when this variant is chosen.</param>
	private static UiStack Rows(
		string key,
		double minHeight,
		TaskStore store,
		RowLayout layout,
		Func<VikunjaTask, CancellationToken, Task>? onCheck,
		bool header,
		bool due)
	{
		var count = layout.RowsThatFit(minHeight, header);
		var children = new List<UiElement>();
		if (header)
		{
			children.Add(Header(store, layout));
		}

		children.Add(new UiRepeat<VikunjaTask>
		{
			Key = "rows",
			Items = UiValue.From<IReadOnlyList<VikunjaTask>>(() => store.Tasks.Value.Take(count).ToList()),
			KeySelector = task => task.Id.ToString(CultureInfo.InvariantCulture),
			Template = (task, rowKey) => Row(task, rowKey, layout, onCheck, due),
		});

		return new UiStack
		{
			Key = key,
			Align = UiValue.Of(UiComponentAlignments.Stretch),
			Gap = layout.Gap,
			Children = children,
		};
	}

	/// <summary>Shown only while there is nothing to list, so the tile says why instead of looking empty.</summary>
	private static UiWhen Message(string key, TaskStore store, StoreStatus status, LocalizedText text, RowLayout layout) => new()
	{
		Key = key,
		Condition = () => store.Tasks.Value.Count == 0 && store.Status.Value == status,
		Content = () => new UiTextRun
		{
			Key = "message",
			Text = text,
			Size = layout.TextSize,
			MinSize = layout.TextSize * 0.6,
			Wrap = UiValue.Of(true),
			Role = UiComponentTextRoles.Secondary,
		},
	};

	/// <summary>The square-tile alternative to the list: how many tasks are open and nothing else.</summary>
	private static UiWhen Counter(TaskStore store, RowLayout layout) => new()
	{
		Key = "counter",
		Condition = () => store.Tasks.Value.Count > 0,
		Content = () => new UiStack
		{
			Key = "count",
			Justify = UiValue.Of(UiComponentJustify.Center),
			Align = UiValue.Of(UiComponentAlignments.Center),
			Gap = layout.Gap,
			Children =
			[
				new UiTextRun
				{
					Key = "number",
					// The store keeps at most TaskStore.MaxTasks tasks, so the count stops there.
					Text = UiText.From(() => store.Tasks.Value.Count.ToString(CultureInfo.InvariantCulture)),
					Size = layout.TextSize * 5,
					MinSize = layout.TextSize * 2,
					Weight = UiValue.Of(UiComponentTextWeights.Bold),
					Align = UiValue.Of(UiComponentAlignments.Center),
				},
				new UiTextRun
				{
					Key = "label",
					Text = Strings.Widgets.OpenTasks(),
					Size = layout.TextSize * 1.05,
					MinSize = layout.TextSize * 0.6,
					Align = UiValue.Of(UiComponentAlignments.Center),
					Role = UiComponentTextRoles.Secondary,
				},
			],
		},
	};

	private static UiStack Header(TaskStore store, RowLayout layout) => new()
	{
		Key = "header",
		Direction = UiValue.Of(UiComponentDirections.Horizontal),
		Justify = UiValue.Of(UiComponentJustify.SpaceBetween),
		Align = UiValue.Of(UiComponentAlignments.Baseline),
		Children =
		[
			new UiTextRun { Key = "title", Text = Strings.Widgets.Header(), Size = layout.HeaderSize, Role = UiComponentTextRoles.Secondary },
			new UiTextRun
			{
				Key = "count",
				Text = UiText.From(() => store.Tasks.Value.Count.ToString(CultureInfo.InvariantCulture)),
				Size = layout.HeaderSize,
				Role = UiComponentTextRoles.Secondary,
			},
		],
	};

	/// <summary>One list row with a thin divider under it; a pressable row puts its content in a button.</summary>
	private static UiStack Row(
		VikunjaTask task,
		string key,
		RowLayout layout,
		Func<VikunjaTask, CancellationToken, Task>? onCheck,
		bool due)
	{
		var content = RowContent(task, layout, onCheck is not null, due);
		UiElement row = onCheck is null
			? content
			: new UiButton
			{
				Key = "press",
				Background = Transparent,
				Padding = 0,
				Events = [UiEventHandler.OnAsync(UiComponentEvents.Press, cancellationToken => onCheck(task, cancellationToken))],
				Children = [content],
			};

		return new UiStack
		{
			Key = key,
			Align = UiValue.Of(UiComponentAlignments.Stretch),
			Children =
			[
				row,
				new UiModifier
				{
					Key = "line",
					Frame = UiValue.Of(new UiFrame { Height = LineThickness }),
					Child = new UiShape { Key = "rule", Shape = UiValue.Of(UiComponentShapes.Rectangle), Color = UiValue.Of(LineColor) },
				},
			],
		};
	}

	private static UiStack RowContent(VikunjaTask task, RowLayout layout, bool pressable, bool due)
	{
		var children = new List<UiElement>
		{
			pressable ? Ring(layout) : PriorityDot(task, layout),
			new UiTextRun { Key = "label", Text = task.Title, Size = layout.TextSize, Fill = UiValue.Of(true) },
		};

		if (due && task.HasDueDate)
		{
			var overdue = IsOverdue(task);
			children.Add(new UiTextRun
			{
				Key = "due",
				Text = DueLabel(task.DueDate!.Value),
				Size = layout.TextSize * 0.75,
				MainSize = layout.TextSize * 4.5,
				Align = UiValue.Of(UiComponentAlignments.End),
				Role = overdue ? default : (UiValue<string>)UiComponentTextRoles.Secondary,
				Color = overdue ? UiValue.Of(LateColor) : default,
			});
		}

		return new UiStack
		{
			Key = "row",
			Direction = UiValue.Of(UiComponentDirections.Horizontal),
			Align = UiValue.Of(UiComponentAlignments.Center),
			Gap = layout.Gap * 1.5,
			Padding = layout.Padding,
			Children = children,
		};
	}

	/// <summary>An empty circle: the checklist's promise that pressing the row completes it.</summary>
	private static UiModifier Ring(RowLayout layout) => Round("mark", layout.TextSize * 0.85, new UiShape
	{
		Key = "ring",
		Shape = UiValue.Of(UiComponentShapes.Circle),
		Color = UiValue.Of(Transparent),
		StrokeColor = UiValue.Of("#9aa0a6"),
		StrokeWidth = layout.TextSize * 0.1,
	});

	private static UiModifier PriorityDot(VikunjaTask task, RowLayout layout) => Round("mark", layout.TextSize * 0.4, new UiShape
	{
		Key = "dot",
		Shape = UiValue.Of(UiComponentShapes.Circle),
		Color = UiValue.Of(PriorityColor(task.Priority)),
	});

	private static UiModifier Round(string key, double size, UiShape shape) => new()
	{
		Key = key,
		Frame = UiValue.Of(new UiFrame { Width = size, Height = size, MinWidth = size }),
		Child = shape,
	};

	// Vikunja priorities: 0 unset, 1 low, 2 medium, 3 high, 4 urgent, 5 do now.
	internal static string PriorityColor(int priority) => priority switch
	{
		<= 0 => "#6b7078",
		1 => "#51cf66",
		2 => "#ffa94d",
		_ => LateColor,
	};

	internal static bool IsOverdue(VikunjaTask task) => task.HasDueDate && task.DueDate!.Value.ToLocalTime().Date < DateTime.Today;

	internal static UiText DueLabel(DateTimeOffset due)
	{
		var day = due.ToLocalTime().Date;
		var days = (day - DateTime.Today).Days;
		return days switch
		{
			< 0 => Strings.Widgets.Due.Overdue(),
			0 => Strings.Widgets.Due.Today(),
			1 => Strings.Widgets.Due.Tomorrow(),
			< 7 => UiText.Of(day.ToString("ddd", CultureInfo.CurrentCulture)),
			_ => UiText.Of(day.ToString("MMM d", CultureInfo.CurrentCulture)),
		};
	}

	/// <summary>The lengths of one row for the chosen settings, and how many rows a tile of a given height holds.</summary>
	internal sealed class RowLayout(WidgetSettings settings, bool pressable)
	{
		public double TextSize { get; } = BaseTextSize * settings.FontScale;

		public double HeaderSize { get; } = BaseTextSize * 0.75 * settings.FontScale;

		public double Padding { get; } = BasePadding * settings.CardScale;

		public double Gap { get; } = BaseGap * settings.CardScale;

		public int RowsThatFit(double minHeight, bool header = false)
		{
			var rowHeight = (TextSize + 2 * Padding) * (pressable ? ButtonRowFactor : 1) + LineThickness;
			var usable = minHeight - 2 * TilePadding + Gap - (header ? HeaderSize + Gap : 0);
			var fit = (int)Math.Floor(usable / (rowHeight + Gap));
			return Math.Clamp(fit, 1, settings.MaxTasks);
		}
	}
}
