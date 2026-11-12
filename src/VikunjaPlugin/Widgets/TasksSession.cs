using MacroDeck.Sdk.Ui;
using MacroDeck.Ui.Model.Events;
using MacroDeck.Ui.Model.Nodes;
using MacroDeck.Ui.Model.Patches;
using MacroDeck.Ui.Model.Surfaces;
using MacroDeck.Ui.Runtime;
using VikunjaPlugin.Api;

namespace VikunjaPlugin.Widgets;

internal sealed class TasksSession : IUiSession
{
	private readonly UiView _view;
	private readonly TaskStore _store;
	private IUiEventOrigin? _origin;

	public TasksSession(UiSurface surface, TaskStore store, WidgetSettings settings, bool checkable)
	{
		_store = store;
		_view = new UiView(surface, TasksView.Build(store, settings, checkable ? CompleteAsync : null));
		_view.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
		_view.HandlerFaulted += (_, fault)
			=> Faulted?.Invoke(this, new UiSessionFaultedEventArgs(fault.Exception.Message, fault.Exception));
	}

	public event EventHandler? Changed;

	public event EventHandler<UiSessionFaultedEventArgs>? Faulted;

	public UiTree BuildTree() => _view.Tree;

	public IReadOnlyList<UiPatch> DrainPatches() => _view.DrainPatches();

	public void Dispatch(UiEvent uiEvent)
	{
		_origin = null;
		_view.Dispatch(uiEvent);
	}

	// Dispatch is never concurrent for one session, so a handler running inside it reads the right origin.
	public void Dispatch(UiEvent uiEvent, IUiEventOrigin origin)
	{
		_origin = origin;
		try
		{
			_view.Dispatch(uiEvent);
		}
		finally
		{
			_origin = null;
		}
	}

	public ValueTask DisposeAsync()
	{
		_view.Dispose();
		return ValueTask.CompletedTask;
	}

	private async Task CompleteAsync(VikunjaTask task, CancellationToken cancellationToken)
	{
		var origin = _origin;
		try
		{
			await _store.CompleteAsync(task.Id, cancellationToken);
		}
		catch (VikunjaException exception) when (origin is not null)
		{
			await origin.ReportFailureAsync(exception.UserMessage, cancellationToken);
		}
	}
}
