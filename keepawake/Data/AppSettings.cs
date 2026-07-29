namespace Keepawake.Data;

/// <summary>Everything persisted to %AppData%\keepawake\settings.json.</summary>
public sealed class AppSettings
{
    /// <summary>Whether "keep screen on" was on at last exit. Read on startup so a reboot (with
    /// "Start with Windows" on) restores the same state instead of quietly reverting to normal
    /// screen-off behavior until the user notices — see App.axaml.cs and PowerManager.</summary>
    public bool Enabled { get; set; }

    public bool StartWithWindows { get; set; }
}
