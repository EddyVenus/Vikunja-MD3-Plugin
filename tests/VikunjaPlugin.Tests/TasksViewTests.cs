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

		Assert.That(host.ByText("Buy milk"), Is.Not.Empty);
		Assert.That(host.ByText("Call Ana"), Is.Not.Empty);
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

		var host = UiTestHost.Render(TasksView.Build(store, 5, onCheck: null));

		Assert.That(host.Describe(), Does.Contain(expectedKey));
	}

	[Test]
	public void The_message_is_hidden_while_tasks_are_listed()
	{
		var store = StoreWith("Buy milk");
		store.Status.Value = StoreStatus.Unavailable;

		var host = UiTestHost.Render(TasksView.Build(store, 5, onCheck: null));

		Assert.That(host.Describe(), Does.Not.Contain("Widgets.Unavailable"));
	}

	[Test]
	public void A_checked_task_disappears_from_the_view_when_the_store_drops_it()
	{
		var store = StoreWith("Buy milk", "Call Ana");
		var host = UiTestHost.Render(TasksView.Build(store, 5, onCheck: null));

		store.Tasks.Value = store.Tasks.Peek().Where(t => t.Id != 1).ToList();

		Assert.That(host.ByText("Buy milk"), Is.Empty);
	}
}
