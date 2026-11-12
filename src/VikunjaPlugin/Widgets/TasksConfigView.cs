using System.Text.Json;
using MacroDeck.Ui.Config;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Runtime;

namespace VikunjaPlugin.Widgets;

internal static class TasksConfigView
{
	public static UiElement Build(JsonElement data)
	{
		var fontSize = new UiState<double>(WidgetSettings.ReadPercent(data, "fontSize"));
		var cardSize = new UiState<double>(WidgetSettings.ReadPercent(data, "cardSize"));
		var maxTasks = new UiState<double>(WidgetSettings.Read(data).MaxTasks);

		return new UiWidgetConfiguration
		{
			Key = "root",
			Properties = new UiWidgetProperties
			{
				Key = "properties",
				Children =
				[
					Percent("fontSize", Strings.Config.FontSize.Label(), Strings.Config.FontSize.Description(), fontSize),
					Percent("cardSize", Strings.Config.CardSize.Label(), Strings.Config.CardSize.Description(), cardSize),
					new UiNumberInput
					{
						Key = "maxTasks",
						Label = Strings.Config.MaxTasks.Label(),
						Description = Strings.Config.MaxTasks.Description(),
						Min = 1,
						Max = TaskStore.MaxTasks,
						Step = 1,
						Binding = Bind.To(maxTasks),
					},
				],
			},
		};
	}

	private static UiNumberInput Percent(string key, UiText label, UiText description, UiState<double> state) => new()
	{
		Key = key,
		Label = label,
		Description = description,
		Min = WidgetSettings.MinPercent,
		Max = WidgetSettings.MaxPercent,
		Step = 5,
		ShowSlider = true,
		Binding = Bind.To(state),
	};
}
