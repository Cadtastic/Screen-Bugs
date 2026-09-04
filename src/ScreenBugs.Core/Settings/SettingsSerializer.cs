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
            slots.Add(slot.Species is { } species ? species.ToString() : RandomName);
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

    private static IReadOnlyList<BugTypeSlot> ReadSlots(JsonNode? node)
    {
        if (node is not JsonArray array)
        {
            return BugOptions.Default.TypeSlots;
        }

        var slots = new List<BugTypeSlot>();
        foreach (var element in array)
        {
            if (TryReadSlot(element, out var slot))
            {
                slots.Add(slot);
            }
        }

        return slots.Count == 0 ? BugOptions.Default.TypeSlots : BugTypeSlots.Sanitize(slots);
    }

    private static bool TryReadSlot(JsonNode? node, out BugTypeSlot slot)
    {
        slot = BugTypeSlot.Random;
        if (node is not JsonValue value || !value.TryGetValue(out string? name) || name is null)
        {
            return false;
        }

        if (string.Equals(name, RandomName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // TryParse alone accepts numeric strings such as "99", so IsDefined must confirm it.
        if (Enum.TryParse(name, ignoreCase: true, out SpeciesId species) && Enum.IsDefined(species))
        {
            slot = new BugTypeSlot(species);
            return true;
        }

        return false;
    }

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
