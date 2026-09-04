using System.Text.Json;
using System.Text.Json.Nodes;
using ScreenBugs.Core.Simulation;

namespace ScreenBugs.Core.Settings;

/// <summary>
/// Reads and writes <see cref="BugOptions"/> as JSON. Reading is total: any input at all yields a
/// valid record, and each field falls back independently so one bad value cannot lose the rest.
/// </summary>
public static class SettingsSerializer
{
    private const string RandomName = "Random";
    private const int MinBugCount = 1;
    private const int MaxBugCount = 50;
    private static readonly int[] AllowedFrameRates = [30, 60, 120];

    public static string Serialize(BugOptions options)
    {
        var slots = new JsonArray();
        foreach (var slot in options.TypeSlots)
        {
            slots.Add(new JsonObject
            {
                ["Type"] = slot.Type.Species is { } species ? species.ToString() : RandomName,
                ["Speed"] = slot.SpeedMultiplier,
            });
        }

        var root = new JsonObject
        {
            ["TypeSlots"] = slots,
            ["BugCount"] = options.BugCount,
            ["FrameRate"] = options.FrameRate,
            ["OnTypeChange"] = options.OnTypeChange.ToString(),
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static BugOptions Deserialize(string json)
    {
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return BugOptions.Default;
        }

        if (root is null)
        {
            return BugOptions.Default;
        }

        return new BugOptions(
            ReadSlots(root["TypeSlots"]),
            ReadBugCount(root["BugCount"]),
            ReadFrameRate(root["FrameRate"]),
            ReadTypeChange(root["OnTypeChange"]));
    }

    private static IReadOnlyList<SlotSetting> ReadSlots(JsonNode? node)
    {
        if (node is not JsonArray array)
        {
            return BugOptions.Default.TypeSlots;
        }

        var slots = new List<SlotSetting>();
        foreach (var element in array)
        {
            if (TryReadSlot(element, out var slot))
            {
                slots.Add(slot);
            }
        }

        return slots.Count == 0 ? BugOptions.Default.TypeSlots : BugTypeSlots.Sanitize(slots);
    }

    /// <summary>
    /// Reads a row as either {"Type": "...", "Speed": n} or, from files written before speeds
    /// existed, a bare type name at the default speed.
    /// </summary>
    private static bool TryReadSlot(JsonNode? node, out SlotSetting slot)
    {
        slot = SlotSetting.Random;

        JsonNode? typeNode = node is JsonObject slotObject ? slotObject["Type"] : node;
        if (typeNode is not JsonValue typeValue || !typeValue.TryGetValue(out string? name) || name is null)
        {
            return false;
        }

        float speed = node is JsonObject withSpeed ? ReadSpeed(withSpeed["Speed"]) : SlotSetting.DefaultSpeed;

        if (string.Equals(name, RandomName, StringComparison.OrdinalIgnoreCase))
        {
            slot = new SlotSetting(BugTypeSlot.Random, speed);
            return true;
        }

        // TryParse alone accepts numeric strings such as "99", so IsDefined must confirm it.
        if (Enum.TryParse(name, ignoreCase: true, out SpeciesId species) && Enum.IsDefined(species))
        {
            slot = new SlotSetting(new BugTypeSlot(species), speed);
            return true;
        }

        return false;
    }

    private static float ReadSpeed(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out double speed)
            ? SlotSetting.ClampSpeed((float)speed)
            : SlotSetting.DefaultSpeed;

    private static int ReadBugCount(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out int count)
            ? Math.Clamp(count, MinBugCount, MaxBugCount)
            : BugOptions.Default.BugCount;

    private static int ReadFrameRate(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out int rate) && AllowedFrameRates.Contains(rate)
            ? rate
            : BugOptions.Default.FrameRate;

    private static TypeChangeBehavior ReadTypeChange(JsonNode? node) =>
        node is JsonValue value
        && value.TryGetValue(out string? name)
        && name is not null
        && Enum.TryParse(name, ignoreCase: true, out TypeChangeBehavior behavior)
        && Enum.IsDefined(behavior)
            ? behavior
            : BugOptions.Default.OnTypeChange;
}
