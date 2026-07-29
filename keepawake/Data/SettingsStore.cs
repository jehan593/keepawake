using System.Text.Json;

namespace Keepawake.Data;

/// <summary>
/// Flat JSON file at %AppData%\keepawake\settings.json. No cross-process mutex here — the app
/// enforces single-instance at startup (see Program.cs), so there's never a second process that could
/// race a write against this one.
/// </summary>
public sealed class SettingsStore
{
    private static readonly string DirectoryPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "keepawake");

    private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");

    public AppSettings Load()
    {
        try
        {
            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);
            if (settings is not null) return settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Missing file (first run) or corrupt JSON — fall through to defaults rather than crash a
            // tray-only app the user expects to just work.
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        var json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);
        File.WriteAllText(FilePath, json);
    }
}
