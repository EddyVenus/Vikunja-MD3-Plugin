using System.Text.Json;

namespace VikunjaPlugin.Widgets;

/// <summary>What a widget stores: text and card scale as percentages of the default look, and whether square tiles show only the count.</summary>
internal readonly record struct WidgetSettings(double FontScale, double CardScale, bool SquareCounter = false)
{
	internal const int MinPercent = 50;
	internal const int MaxPercent = 200;
	internal const int DefaultPercent = 100;

	public static WidgetSettings Default { get; } = new(1, 1);

	public static WidgetSettings Read(JsonElement data) => new(
		ReadPercent(data, "fontSize") / 100d,
		ReadPercent(data, "cardSize") / 100d,
		ReadSquareDisplay(data) == CounterValue);

	internal const string CounterValue = "counter";
	internal const string ListValue = "list";

	internal static string ReadSquareDisplay(JsonElement data)
		=> data.ValueKind == JsonValueKind.Object
			&& data.TryGetProperty("squareDisplay", out var value)
			&& value.ValueKind == JsonValueKind.String
			&& value.GetString() == CounterValue
				? CounterValue
				: ListValue;

	internal static int ReadPercent(JsonElement data, string key) => ReadInt(data, key, MinPercent, MaxPercent, DefaultPercent);

	private static int ReadInt(JsonElement data, string key, int min, int max, int fallback)
		=> data.ValueKind == JsonValueKind.Object
			&& data.TryGetProperty(key, out var value)
			&& value.ValueKind == JsonValueKind.Number
			&& value.TryGetInt32(out var number)
				? Math.Clamp(number, min, max)
				: fallback;
}
