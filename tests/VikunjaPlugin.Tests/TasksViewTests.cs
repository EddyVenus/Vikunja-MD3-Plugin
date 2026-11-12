using MacroDeck.Ui.Components;
using MacroDeck.Ui.Testing;
using NUnit.Framework;
using VikunjaPlugin.Api;
using VikunjaPlugin.Widgets;

namespace VikunjaPlugin.Tests;

[TestFixture]
public sealed class TasksViewTests
{
	private static WidgetSettings Settings(int maxTasks) => WidgetSettings.Default with { MaxTasks = maxTasks };

	private static TaskStore StoreWith(params string[] titles)
	{
		var store = new FakeVikunja().CreateStore(out _);
		store.Tasks.Value = titles.Select((title, index) => new VikunjaTask(index + 1, title, false)).ToList();
		return store;
	}

	[Test]
	public void The_read_only_view_lists_titles_and_has_nothing_to_press()
	{
		var host = UiTestHost.Render(TasksView.Build(StoreWith("Buy milk", "Call Ana"), Settings(5), onCheck: null));

		Assert.That(host.ByText("Buy milk"), Is.Not.Empty);
		Assert.That(host.ByText("Call Ana"), Is.Not.Empty);
		Assert.That(host.ByType("ui.button"), Is.Empty);
	}

	[Test]
	public void The_checklist_view_reports_the_pressed_task()
	{
		VikunjaTask? checkedTask = null;
		var host = UiTestHost.Render(TasksView.Build(StoreWith("Buy milk", "Call Ana"), Settings(5), (task, _) =>
		{
			checkedTask = task;
			return Task.CompletedTask;
		}));

		host.ByType("ui.button")[1].Raise(UiComponentEvents.Press);

		Assert.That(checkedTask?.Title, Is.EqualTo("Call Ana"));
	}

	[Test]
	public void Only_the_configured_number_of_tasks_is_drawn()
	{
		var host = UiTestHost.Render(TasksView.Build(StoreWith("A", "B", "C"), Settings(2), onCheck: null));

		Assert.That(host.ByText("C"), Is.Empty);
		Assert.That(host.ByText("B"), Is.Not.Empty);
	}

	[TestCase(StoreStatus.Ready, "Widgets.Empty")]
	[TestCase(StoreStatus.Loading, "Widgets.Loading")]
	[TestCase(StoreStatus.NotConfigured, "Widgets.NotConfigured")]
	[TestCase(StoreStatus.Unavailable, "Widgets.Unavailable")]
	public void An_empty_tile_says_why_it_is_empty(StoreStatus status, string expectedKey)
	{
		var store = StoreWith();
		store.Status.Value = status;

		var host = UiTestHost.Render(TasksView.Build(store, Settings(5), onCheck: null));

		Assert.That(host.Describe(), Does.Contain(expectedKey));
	}

	[Test]
	public void The_message_is_hidden_while_tasks_are_listed()
	{
		var store = StoreWith("Buy milk");
		store.Status.Value = StoreStatus.Unavailable;

		var host = UiTestHost.Render(TasksView.Build(store, Settings(5), onCheck: null));

		Assert.That(host.Describe(), Does.Not.Contain("Widgets.Unavailable"));
	}

	[Test]
	public void A_checked_task_disappears_from_the_view_when_the_store_drops_it()
	{
		var store = StoreWith("Buy milk", "Call Ana");
		var host = UiTestHost.Render(TasksView.Build(store, Settings(5), onCheck: null));

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
	public void The_row_cap_from_the_settings_wins_over_the_space_available()
	{
		var capped = new TasksView.RowLayout(WidgetSettings.Default with { MaxTasks = 2 }, pressable: false);

		Assert.That(capped.RowsThatFit(2.5), Is.EqualTo(2));
	}

	[Test]
	public void At_least_one_row_is_always_drawn()
	{
		var huge = new TasksView.RowLayout(new WidgetSettings(2, 2, 50), pressable: true);

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
		Assert.That(TasksView.IsOverdue(new VikunjaTask(1, "a", false, DateTimeOffset.Now.AddDays(-2))), Is.True);
		Assert.That(TasksView.IsOverdue(new VikunjaTask(1, "a", false, DateTimeOffset.Now.AddDays(2))), Is.False);
		Assert.That(TasksView.IsOverdue(new VikunjaTask(1, "a", false, DateTimeOffset.MinValue)), Is.False);
		Assert.That(TasksView.IsOverdue(new VikunjaTask(1, "a", false)), Is.False);
	}

	[Test]
	public void The_counter_option_adds_a_square_layout_showing_the_open_count()
	{
		var counter = UiTestHost.Render(TasksView.Build(StoreWith("A", "B", "C"), WidgetSettings.Default with { SquareCounter = true }, onCheck: null));
		var list = UiTestHost.Render(TasksView.Build(StoreWith("A", "B", "C"), WidgetSettings.Default, onCheck: null));

		Assert.That(counter.Describe(), Does.Contain("Widgets.OpenTasks"));
		Assert.That(list.Describe(), Does.Not.Contain("Widgets.OpenTasks"));
	}
}
