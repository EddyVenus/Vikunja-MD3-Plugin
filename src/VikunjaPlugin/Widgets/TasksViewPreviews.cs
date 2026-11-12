using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Previews;
using VikunjaPlugin.Api;

namespace VikunjaPlugin.Widgets;

internal static class TasksViewPreviews
{
	private static readonly string[] _titles =
	[
		"Buy milk",
		"Call the dentist to reschedule the appointment for next week",
		"Pay electricity bill",
		"Review pull request",
		"Water plants",
		"Book train tickets",
		"Backup photos",
		"Renew passport",
		"Clean the garage",
		"Send invoice",
	];

	[UiPreview("Read only", Profile = UiPreviewProfiles.Widget)]
	public static UiElement ReadOnly() => TasksView.Build(StoreWith(_titles), WidgetSettings.Default, onCheck: null);

	[UiPreview("Checklist", Profile = UiPreviewProfiles.Widget)]
	public static UiElement Checklist() => TasksView.Build(StoreWith(_titles), WidgetSettings.Default, (_, _) => Task.CompletedTask);

	[UiPreview("Large text", Profile = UiPreviewProfiles.Widget)]
	public static UiElement LargeText() => TasksView.Build(StoreWith(_titles), WidgetSettings.Default with { FontScale = 1.5 }, onCheck: null);

	[UiPreview("Slim cards", Profile = UiPreviewProfiles.Widget)]
	public static UiElement SlimCards() => TasksView.Build(StoreWith(_titles), WidgetSettings.Default with { FontScale = 0.8, CardScale = 0.5 }, onCheck: null);

	[UiPreview("Counter on squares", Profile = UiPreviewProfiles.Widget)]
	public static UiElement Counter() => TasksView.Build(StoreWith(_titles), WidgetSettings.Default with { SquareCounter = true }, onCheck: null);

	[UiPreview("Few tasks", Profile = UiPreviewProfiles.Widget)]
	public static UiElement FewTasks() => TasksView.Build(StoreWith(_titles[..2]), WidgetSettings.Default, onCheck: null);

	[UiPreview("Empty", Profile = UiPreviewProfiles.Widget)]
	public static UiElement Empty() => TasksView.Build(StoreWith([], StoreStatus.Ready), WidgetSettings.Default, onCheck: null);

	[UiPreview("Unavailable", Profile = UiPreviewProfiles.Widget)]
	public static UiElement Unavailable() => TasksView.Build(StoreWith([], StoreStatus.Unavailable), WidgetSettings.Default, onCheck: null);

	private static TaskStore StoreWith(string[] titles, StoreStatus status = StoreStatus.Ready)
	{
		var store = new TaskStore(new VikunjaClient(new HttpClient(), new VikunjaCredentials()), Serilog.Core.Logger.None);
		store.Tasks.Value = titles.Select((title, index) => new VikunjaTask(
			index + 1,
			title,
			false,
			index % 3 == 2 ? null : DateTimeOffset.Now.AddDays(index - 1),
			index % 4)).ToList();
		store.Status.Value = status;
		return store;
	}
}
