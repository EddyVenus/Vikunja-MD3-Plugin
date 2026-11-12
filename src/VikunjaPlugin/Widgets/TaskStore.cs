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

	public UiState<IReadOnlyList<VikunjaTask>> Tasks { get; } = new([]);

	public async Task RefreshAsync(CancellationToken cancellationToken)
	{
		try
		{
			Tasks.Value = await client.GetOpenTasksAsync(MaxTasks, cancellationToken);
			if (_failures.RecordSuccess() is { } ended)
			{
				_logger.Information("Vikunja is reachable again after {Failures} failed refreshes.", ended.Failures);
			}
		}
		catch (VikunjaException exception) when (exception.Reason == VikunjaFailure.NotConfigured)
		{
			Tasks.Value = [];
		}
		catch (VikunjaException exception)
		{
			// Stale tasks stay on screen so a failed refresh never looks like an empty list.
			var signal = _failures.RecordFailure(exception.Reason.ToString());
			if (signal.Kind != FailureEpisodeSignalKind.Quiet)
			{
				_logger.Warning(exception, "Refreshing Vikunja tasks failed ({Count} in a row).", signal.ConsecutiveFailures);
			}
		}
	}

	public async Task CompleteAsync(long taskId, CancellationToken cancellationToken)
	{
		await client.CompleteTaskAsync(taskId, cancellationToken);
		Tasks.Value = Tasks.Peek().Where(task => task.Id != taskId).ToList();
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(_pollInterval);
		do
		{
			await RefreshAsync(stoppingToken);
		}
		while (await timer.WaitForNextTickAsync(stoppingToken));
	}
}
