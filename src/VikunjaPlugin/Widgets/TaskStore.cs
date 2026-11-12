using MacroDeck.Sdk.Logging;
using MacroDeck.Ui.Runtime;
using Microsoft.Extensions.Hosting;
using Serilog;
using VikunjaPlugin.Api;

namespace VikunjaPlugin.Widgets;

/// <summary>The open tasks every widget draws from, refreshed on a timer so tiles never call the API themselves.</summary>
public sealed class TaskStore(VikunjaClient client, ILogger logger) : BackgroundService
{
	internal const int MaxTasks = 50;

	private static readonly TimeSpan _pollInterval = TimeSpan.FromMinutes(1);

	private readonly ILogger _logger = logger.ForContext<TaskStore>();
	private readonly FailureEpisodeTracker _failures = new();
	private readonly SemaphoreSlim _gate = new(1, 1);
	private readonly SemaphoreSlim _wake = new(0);

	public UiState<IReadOnlyList<VikunjaTask>> Tasks { get; } = new([]);

	public UiState<StoreStatus> Status { get; } = new(StoreStatus.Loading);

	/// <summary>The local date, moved on by each refresh, so rows that say "Today" or "Overdue" are redrawn when the day changes.</summary>
	public UiState<DateOnly> Today { get; } = new(DateOnly.FromDateTime(DateTime.Now));

	/// <summary>Drops what belongs to the previous credentials and asks the background loop for an immediate refresh.</summary>
	public void Reset()
	{
		Tasks.Value = [];
		Status.Value = StoreStatus.Loading;
		_wake.Release();
	}

	public async Task RefreshAsync(CancellationToken cancellationToken)
	{
		// Serialized with CompleteAsync so a refresh that started before a press cannot bring the task back.
		await _gate.WaitAsync(cancellationToken);
		try
		{
			await RefreshCoreAsync(cancellationToken);
		}
		finally
		{
			_gate.Release();
		}
	}

	public async Task CompleteAsync(long taskId, CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken);
		try
		{
			if (await client.CompleteTaskAsync(taskId, cancellationToken))
			{
				Tasks.Value = Tasks.Peek().Where(task => task.Id != taskId).ToList();
				return;
			}

			await RefreshCoreAsync(cancellationToken);
		}
		finally
		{
			_gate.Release();
		}
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			await RefreshAsync(stoppingToken);
			await _wake.WaitAsync(_pollInterval, stoppingToken);
		}
	}

	private async Task RefreshCoreAsync(CancellationToken cancellationToken)
	{
		Today.Value = DateOnly.FromDateTime(DateTime.Now);
		try
		{
			var tasks = await client.GetOpenTasksAsync(MaxTasks, cancellationToken);
			// An unchanged list is not written back, so tiles are not patched every minute for nothing.
			if (!tasks.SequenceEqual(Tasks.Peek()))
			{
				Tasks.Value = tasks;
			}

			Status.Value = StoreStatus.Ready;
			if (_failures.RecordSuccess() is { } ended)
			{
				_logger.Information("Vikunja is reachable again after {Failures} failed refreshes.", ended.Failures);
			}
		}
		catch (VikunjaException exception) when (exception.Reason == VikunjaFailure.NotConfigured)
		{
			Tasks.Value = [];
			Status.Value = StoreStatus.NotConfigured;
		}
		catch (VikunjaException exception)
		{
			// Stale tasks stay on screen so a failed refresh never looks like an empty list.
			Status.Value = StoreStatus.Unavailable;
			var signal = _failures.RecordFailure(exception.Reason.ToString());
			if (signal.Kind != FailureEpisodeSignalKind.Quiet)
			{
				_logger.Warning(exception, "Refreshing Vikunja tasks failed ({Count} in a row).", signal.ConsecutiveFailures);
			}
		}
	}
}
