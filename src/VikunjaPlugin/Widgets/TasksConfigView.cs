using System.Text.Json;
using MacroDeck.Ui.Config;
using MacroDeck.Ui.Config.Options;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Runtime;

namespace VikunjaPlugin.Widgets;

internal static class TasksConfigView
{
	public static UiElement Build(JsonElement data)
	{
		var fontSize = new UiState<double>(WidgetSettings.ReadPercent(data, "fontSize"));
		var cardSize = new UiState<double>(WidgetSettings.ReadPercent(data, "cardSize"));
		var squareDisplay = new UiState<string>(WidgetSettings.ReadSquareDisplay(data));

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
					new UiChoiceInput
					{
						Key = "squareDisplay",
						Label = Strings.Config.SquareDisplay.Label(),
						Description = Strings.Config.SquareDisplay.Description(),
						Segmented = UiValue.Of(true),
						Options = UiValue.Of<IReadOnlyList<UiOption>>(
						[
							new UiOption { Value = WidgetSettings.ListValue, Label = Strings.Config.SquareDisplay.List() },
							new UiOption { Value = WidgetSettings.CounterValue, Label = Strings.Config.SquareDisplay.Counter() },
						]),
						Binding = Bind.To(squareDisplay),
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
