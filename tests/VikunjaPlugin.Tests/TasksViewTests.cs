using MacroDeck.Ui.Components;
using MacroDeck.Ui.Testing;
using NUnit.Framework;
using VikunjaPlugin.Api;
using VikunjaPlugin.Widgets;

namespace VikunjaPlugin.Tests;

[TestFixture]
public sealed class TasksViewTests
{
	private static TaskStore StoreWith(params string[] titles)
	{
		var store = new FakeVikunja().CreateStore(out _);
		store.Tasks.Value = titles.Select((title, index) => new VikunjaTask(index + 1, title, false)).ToList();
		return store;
	}

	[Test]
	public void The_read_only_view_lists_titles_and_has_nothing_to_press()
	{
		var host = UiTestHost.Render(TasksView.Build(StoreWith("Buy milk", "Call Ana"), WidgetSettings.Default, onCheck: null));

		Assert.That(host.ByText("Buy milk"), Is.Not.Empty);
		Assert.That(host.ByText("Call Ana"), Is.Not.Empty);
		Assert.That(host.ByType("ui.button"), Is.Empty);
	}

	[Test]
	public void The_checklist_view_reports_the_pressed_task()
	{
		VikunjaTask? checkedTask = null;
		var host = UiTestHost.Render(TasksView.Build(StoreWith("Buy milk", "Call Ana"), WidgetSettings.Default, (task, _) =>
		{
			checkedTask = task;
			return Task.CompletedTask;
		}));

		host.ByType("ui.button")[1].Raise(UiComponentEvents.Press);

		Assert.That(checkedTask?.Title, Is.EqualTo("Call Ana"));
	}

	[Test]
	public void The_message_is_hidden_while_tasks_are_listed()
	{
		var store = StoreWith("Buy milk");
		store.Status.Value = StoreStatus.Unavailable;

		var host = UiTestHost.Render(TasksView.Build(store, WidgetSettings.Default, onCheck: null));

		Assert.That(host.Describe(), Does.Not.Contain("Widgets.Unavailable"));
	}

	[Test]
	public void A_checked_task_disappears_from_the_view_when_the_store_drops_it()
	{
		var store = StoreWith("Buy milk", "Call Ana");
		var host = UiTestHost.Render(TasksView.Build(store, WidgetSettings.Default, onCheck: null));

		store.Tasks.Value = store.Tasks.Peek().Where(t => t.Id != 1).ToList();

		Assert.That(host.ByText("Buy milk"), Is.Empty);
	}

	[Test]
	public void A_larger_font_fits_fewer_rows_in_the_same_tile()
	{
		var normal = new TasksView.RowLayout(WidgetSettings.Default, pressable: false).RowsThatFit(1.0);
		var large = new TasksView.RowLayout(WidgetSettings.Default with { FontScale = 2 }, pressable: false).RowsThatFit(1.0);

		Assert.That(normal, Is.EqualTo(5));
		Assert.That(large, Is.LessThan(normal));
	}

	[Test]
	public void A_smaller_card_fits_more_rows_and_a_taller_tile_fits_more_than_a_square_one()
	{
		var normal = new TasksView.RowLayout(WidgetSettings.Default, pressable: false);
		var slim = new TasksView.RowLayout(WidgetSettings.Default with { CardScale = 0.5 }, pressable: false);

		Assert.That(slim.RowsThatFit(1.0), Is.GreaterThan(normal.RowsThatFit(1.0)));
		Assert.That(normal.RowsThatFit(2.5), Is.GreaterThan(normal.RowsThatFit(1.0)));
	}

	[Test]
	public void At_least_one_row_is_always_drawn()
	{
		var huge = new TasksView.RowLayout(new WidgetSettings(2, 2), pressable: true);

		Assert.That(huge.RowsThatFit(0.5), Is.EqualTo(1));
	}

	[Test]
	public void A_header_costs_one_row_in_tall_tiles()
	{
		var layout = new TasksView.RowLayout(WidgetSettings.Default, pressable: false);

		Assert.That(layout.RowsThatFit(2.5, header: true), Is.LessThanOrEqualTo(layout.RowsThatFit(2.5)));
	}

	[TestCase(0, "#6b7078")]
	[TestCase(1, "#51cf66")]
	[TestCase(2, "#ffa94d")]
	[TestCase(3, "#ff5a5f")]
	[TestCase(5, "#ff5a5f")]
	public void Priority_picks_a_color(int priority, string color)
	{
		Assert.That(TasksView.PriorityColor(priority), Is.EqualTo(color));
	}

	[Test]
	public void A_task_is_overdue_only_when_it_has_a_past_due_date()
	{
		var today = DateOnly.FromDateTime(DateTime.Now);
		Assert.That(TasksView.IsOverdue(new VikunjaTask(1, "a", false, DateTimeOffset.Now.AddDays(-2)), today), Is.True);
		Assert.That(TasksView.IsOverdue(new VikunjaTask(1, "a", false, DateTimeOffset.Now.AddDays(2)), today), Is.False);
		Assert.That(TasksView.IsOverdue(new VikunjaTask(1, "a", false, DateTimeOffset.MinValue), today), Is.False);
		Assert.That(TasksView.IsOverdue(new VikunjaTask(1, "a", false), today), Is.False);
	}

	[Test]
	public void The_counter_option_adds_a_square_layout_showing_the_open_count()
	{
		var counter = UiTestHost.Render(TasksView.Build(StoreWith("A", "B", "C"), WidgetSettings.Default with { SquareCounter = true }, onCheck: null));
		var list = UiTestHost.Render(TasksView.Build(StoreWith("A", "B", "C"), WidgetSettings.Default, onCheck: null));

		Assert.That(counter.Describe(), Does.Contain("Widgets.OpenTasks"));
		Assert.That(list.Describe(), Does.Not.Contain("Widgets.OpenTasks"));
	}

	private static string[] Titles(int count) => Enumerable.Range(1, count).Select(i => $"Task{i:00}").ToArray();

	[Test]
	public void Swiping_up_shows_the_next_page_and_down_goes_back_wrapping_around()
	{
		// A 1x1-shaped tile fits 5 rows, so 12 tasks make 3 pages.
		var host = UiTestHost.Render(TasksView.Build(StoreWith(Titles(12)), WidgetSettings.Default, onCheck: null));
		var pager = host.ByType("ui.stack").First(node => node.Id.EndsWith(".compact", StringComparison.Ordinal));

		Assert.That(host.ByText("Task01"), Is.Not.Empty);
		pager.Raise(UiComponentEvents.Swipe, "up");
		Assert.That(host.ByText("Task01"), Is.Empty);
		Assert.That(host.ByText("Task06"), Is.Not.Empty);

		pager.Raise(UiComponentEvents.Swipe, "down");
		pager.Raise(UiComponentEvents.Swipe, "down");
		Assert.That(host.ByText("Task11"), Is.Not.Empty);
	}

	[Test]
	public void A_page_past_the_end_is_pulled_back_when_tasks_are_completed()
	{
		var store = StoreWith(Titles(12));
		var host = UiTestHost.Render(TasksView.Build(store, WidgetSettings.Default, onCheck: null));
		var pager = host.ByType("ui.stack").First(node => node.Id.EndsWith(".compact", StringComparison.Ordinal));
		pager.Raise(UiComponentEvents.Swipe, "up");
		pager.Raise(UiComponentEvents.Swipe, "up");

		store.Tasks.Value = store.Tasks.Peek().Take(3).ToList();

		Assert.That(host.ByText("Task01"), Is.Not.Empty);
	}

	[TestCase(0, 5, 1)]
	[TestCase(5, 5, 1)]
	[TestCase(6, 5, 2)]
	[TestCase(50, 5, 10)]
	public void Pages_are_counted_from_the_tasks_and_rows_per_page(int total, int perPage, int pages)
	{
		Assert.That(TasksView.PageCount(total, perPage), Is.EqualTo(pages));
		Assert.That(TasksView.CurrentPage(99, total, perPage), Is.EqualTo(pages - 1));
	}

	[Test]
	public void A_new_day_draws_the_rows_again_so_date_labels_are_not_stale()
	{
		var store = StoreWith("Pay bill");
		var host = UiTestHost.Render(TasksView.Build(store, WidgetSettings.Default, onCheck: null));
		var before = host.Describe();

		store.Today.Value = store.Today.Peek().AddDays(1);

		Assert.That(host.Describe(), Is.Not.EqualTo(before));
	}

	[TestCase(StoreStatus.Unavailable)]
	[TestCase(StoreStatus.Unauthorized)]
	public void Old_tasks_still_on_screen_are_dimmed_and_marked_when_they_could_not_be_refreshed(StoreStatus status)
	{
		var store = StoreWith("Buy milk");
		var host = UiTestHost.Render(TasksView.Build(store, WidgetSettings.Default, onCheck: null));
		var before = host.Describe();
		Assert.That(host.ByText("!"), Is.Empty);

		store.Status.Value = status;

		Assert.That(host.ByText("!"), Is.Not.Empty);
		Assert.That(host.Describe(), Is.Not.EqualTo(before));

		store.Status.Value = StoreStatus.Ready;
		Assert.That(host.ByText("!"), Is.Empty);
	}

	[Test]
	public void The_counter_tile_is_marked_too()
	{
		var store = StoreWith("A", "B");
		var host = UiTestHost.Render(TasksView.Build(store, WidgetSettings.Default with { SquareCounter = true }, onCheck: null));

		store.Status.Value = StoreStatus.Unavailable;

		Assert.That(host.ByText("!"), Is.Not.Empty);
	}
}
