using System.Text.Json;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.ConfigFlow;
using MacroDeck.Sdk.Ui;
using MacroDeck.Sdk.Widgets;
using MacroDeck.Ui.Model.Surfaces;
using Serilog;
using VikunjaPlugin.Api;
using VikunjaPlugin.ConfigFlow;
using VikunjaPlugin.Widgets;

namespace VikunjaPlugin;

public sealed class PluginIntegration : IPluginIntegration, IConfigFlowProvider, IWidgetTypeProvider, IUiProvider
{
	internal const string ReadOnlyWidgetId = "tasks-readonly";
	internal const string ChecklistWidgetId = "tasks-checklist";

	private const string WidgetSchema = """{"type":"object","properties":{"maxTasks":{"type":"integer","minimum":1}}}""";
	private const string WidgetDefaultData = """{"maxTasks":5}""";
	private const int DefaultMaxTasks = 5;

	private readonly VikunjaClient _client;
	private readonly VikunjaCredentials _credentials;
	private readonly TaskStore _store;
	private readonly ILogger _logger;

	private string? _readOnlyType;
	private string? _checklistType;

	public PluginIntegration(VikunjaClient client, VikunjaCredentials credentials, TaskStore store, ILogger logger)
	{
		_client = client;
		_credentials = credentials;
		_store = store;
		_logger = logger.ForContext<PluginIntegration>();
	}

	public IReadOnlyList<IActionDefinition> Actions { get; } = [];

	public bool AllowsMultipleConfigurations => false;

	public IReadOnlyList<UiSurfaceDeclaration> Surfaces { get; } =
	[
		new() { Kind = UiSurfaceKinds.Widget, SessionMode = UiSessionModes.Shared },
		new() { Kind = UiSurfaceKinds.Preview, SessionMode = UiSessionModes.Shared },
	];

	public string ProviderName => "Vikunja";

	public IConfigFlow CreateConfigFlow() => new VikunjaConfigFlow(_client);

	/// <summary>Runs again on every configuration change, so the credentials are re-read each time.</summary>
	public async Task InitializeAsync(IIntegrationContext context)
	{
		var entries = await context.Config.GetEntriesAsync();
		if (entries.Count == 0)
		{
			_credentials.Clear();
			_logger.Information("Waiting for configuration.");
		}
		else
		{
			var entryId = entries[0].Id;
			var serverUrl = await context.Config.GetStringAsync(entryId, VikunjaConfigFlow.ServerUrlKey);
			var token = await context.Config.GetSecretAsync(entryId, VikunjaConfigFlow.TokenKey);
			_credentials.Apply(Uri.TryCreate(serverUrl, UriKind.Absolute, out var address) ? address : null, token);
		}

		// The store's loop does the network call, so initialization never waits on Vikunja.
		_store.Reset();
	}

	public Task ShutdownAsync()
	{
		_credentials.Clear();
		return Task.CompletedTask;
	}

	public async Task InitializeAsync(IWidgetTypeProviderContext context, CancellationToken cancellationToken = default)
	{
		var readOnly = await context.RegisterWidgetTypeAsync(
			new WidgetTypeDescriptor(
				ReadOnlyWidgetId,
				Strings.Widgets.ReadOnly.Name(),
				Strings.Widgets.ReadOnly.Description(),
				DefaultData: WidgetDefaultData,
				DataSchema: WidgetSchema),
			cancellationToken);
		var checklist = await context.RegisterWidgetTypeAsync(
			new WidgetTypeDescriptor(
				ChecklistWidgetId,
				Strings.Widgets.Checklist.Name(),
				Strings.Widgets.Checklist.Description(),
				DefaultData: WidgetDefaultData,
				DataSchema: WidgetSchema),
			cancellationToken);

		_readOnlyType = readOnly.WidgetTypeId;
		_checklistType = checklist.WidgetTypeId;
	}

	public Task<IUiSession?> CreateSessionAsync(UiSessionRequest request, CancellationToken cancellationToken)
	{
		var surface = request.Surface;
		var attributes = surface.Attributes;

		var isTileSurface = surface.Kind is UiSurfaceKinds.Widget or UiSurfaceKinds.Preview;
		var isSample = attributes.TryGetValue(UiWidgetSurfaceAttributes.Sample, out var sample)
			&& sample.ValueKind == JsonValueKind.True;
		if (!isTileSurface || isSample
			|| !attributes.TryGetValue(UiWidgetSurfaceAttributes.WidgetType, out var typeElement))
		{
			return Task.FromResult<IUiSession?>(null);
		}

		var widgetType = typeElement.GetString();
		if (widgetType != _readOnlyType && widgetType != _checklistType)
		{
			return Task.FromResult<IUiSession?>(null);
		}

		attributes.TryGetValue(UiWidgetSurfaceAttributes.Data, out var data);

		// The editor preview never writes to Vikunja, whatever the widget type.
		var checkable = widgetType == _checklistType && surface.Kind == UiSurfaceKinds.Widget;
		return Task.FromResult<IUiSession?>(new TasksSession(surface, _store, MaxTasks(data), checkable));
	}

	internal static int MaxTasks(JsonElement data)
		=> data.ValueKind == JsonValueKind.Object
			&& data.TryGetProperty("maxTasks", out var value)
			&& value.ValueKind == JsonValueKind.Number
			&& value.TryGetInt32(out var count)
				? Math.Clamp(count, 1, TaskStore.MaxTasks)
				: DefaultMaxTasks;
}
