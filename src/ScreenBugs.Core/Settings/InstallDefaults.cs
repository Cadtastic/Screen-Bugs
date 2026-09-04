using System.Text.Json;
using System.Text.Json.Nodes;

namespace ScreenBugs.Core.Settings;

/// <summary>
/// The installer's seed, read from install-defaults.json beside the executable: the options to
/// start a new user with, and what to do about running at sign-in.
/// </summary>
/// <param name="StartAtLogin">Null when the seed does not say, which means leave startup as it is.</param>
public sealed record InstallDefaults(BugOptions Options, bool? StartAtLogin)
{
    public const string FileName = "install-defaults.json";

    public static InstallDefaults Default { get; } = new(BugOptions.Default, StartAtLogin: null);

    /// <summary>Total: any input at all yields a valid record.</summary>
    public static InstallDefaults Parse(string json) =>
        new(SettingsSerializer.Deserialize(json), ReadStartAtLogin(json));

    /// <summary>
    /// Three-state on purpose. A missing or non-boolean field reads as null, so a damaged seed
    /// neither registers startup behind the user's back nor unregisters what they chose. The JSON
    /// is parsed a second time here to keep this field out of <see cref="BugOptions"/>, which
    /// describes what the Options dialog controls and nothing else.
    /// </summary>
    private static bool? ReadStartAtLogin(string json)
    {
        try
        {
            return JsonNode.Parse(json) is JsonObject root
                && root["StartAtLogin"] is JsonValue value
                && value.TryGetValue(out bool startAtLogin)
                    ? startAtLogin
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
