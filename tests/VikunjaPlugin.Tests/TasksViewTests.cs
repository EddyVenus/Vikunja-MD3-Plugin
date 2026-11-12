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
		var host = UiTestHost.Render(TasksView.Build(StoreWith("Buy milk", "Call Ana"), 5, onCheck: null));

		Assert.That(host.ByText("- Buy milk"), Is.Not.Empty);
		Assert.That(host.ByText("- Call Ana"), Is.Not.Empty);
		Assert.That(host.ByType("ui.button"), Is.Empty);
	}

	[Test]
	public void The_checklist_view_reports_the_pressed_task()
	{
		VikunjaTask? checkedTask = null;
		var host = UiTestHost.Render(TasksView.Build(StoreWith("Buy milk", "Call Ana"), 5, (task, _) =>
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
		var host = UiTestHost.Render(TasksView.Build(StoreWith("A", "B", "C"), 2, onCheck: null));

		Assert.That(host.ByText("- C"), Is.Empty);
		Assert.That(host.ByText("- B"), Is.Not.Empty);
	}

	[Test]
	public void An_empty_list_shows_the_empty_message()
	{
		var host = UiTestHost.Render(TasksView.Build(StoreWith(), 5, onCheck: null));

		Assert.That(host.ById("tasks.message"), Is.Not.Null);
	}

	[Test]
	public void A_checked_task_disappears_from_the_view_when_the_store_drops_it()
	{
		var store = StoreWith("Buy milk", "Call Ana");
		var host = UiTestHost.Render(TasksView.Build(store, 5, onCheck: null));

		store.Tasks.Value = store.Tasks.Peek().Where(t => t.Id != 1).ToList();

		Assert.That(host.ByText("- Buy milk"), Is.Empty);
	}
}
