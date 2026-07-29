using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Keepawake.Data;
using Keepawake.Native;

namespace Keepawake.Ui;

/// <summary>
/// The whole UI: builds/rebuilds the tray icon's context menu and swaps the icon glyph between
/// "on"/"off" so state is visible at a glance without opening anything — there is no window in this
/// app at all. Left-clicking the icon directly toggles "keep screen on" (see constructor); right-click
/// gives the full menu (keep screen on, start-with-Windows, exit).
/// </summary>
public sealed class TrayController
{
    private readonly AppSettings _settings;
    private readonly SettingsStore _settingsStore;
    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly WindowIcon _onIcon;
    private readonly WindowIcon _offIcon;
    private readonly TrayIcon _trayIcon;

    public TrayController(AppSettings settings, SettingsStore settingsStore, IClassicDesktopStyleApplicationLifetime desktop)
    {
        _settings = settings;
        _settingsStore = settingsStore;
        _desktop = desktop;

        _onIcon = LoadIcon("avares://keepawake/Assets/app-on.ico");
        _offIcon = LoadIcon("avares://keepawake/Assets/app-off.ico");

        _trayIcon = new TrayIcon { Icon = _offIcon, IsVisible = true };
        _trayIcon.Clicked += (_, _) => ToggleEnabled();

        TrayIcon.SetIcons(Application.Current!, new TrayIcons { _trayIcon });

        Rebuild();
    }

    private static WindowIcon LoadIcon(string uri)
    {
        using var stream = AssetLoader.Open(new Uri(uri));
        return new WindowIcon(stream);
    }

    private void Rebuild()
    {
        _trayIcon.Icon = _settings.Enabled ? _onIcon : _offIcon;
        _trayIcon.ToolTipText = _settings.Enabled ? "keepawake — Screen kept on" : "keepawake — Off";

        var statusText = _settings.Enabled ? "Screen kept on" : "Off";

        _trayIcon.Menu = new NativeMenu
        {
            new NativeMenuItem(statusText) { IsEnabled = false },
            new NativeMenuItemSeparator(),
            BuildCheckItem("Keep screen on", _settings.Enabled, ToggleEnabled),
            new NativeMenuItemSeparator(),
            BuildCheckItem("Start with Windows", StartupRegistration.IsEnabled(), ToggleStartWithWindows),
            new NativeMenuItemSeparator(),
            new NativeMenuItem("Exit") { Command = new RelayCommand(Exit) },
        };
    }

    private static NativeMenuItem BuildCheckItem(string header, bool isChecked, Action onClick)
    {
        var item = new NativeMenuItem(header)
        {
            ToggleType = NativeMenuItemToggleType.CheckBox,
            IsChecked = isChecked,
        };
        item.Click += (_, _) => onClick();
        return item;
    }

    private void ToggleEnabled()
    {
        _settings.Enabled = !_settings.Enabled;
        _settingsStore.Save(_settings);
        PowerManager.Apply(_settings.Enabled);
        Rebuild();
    }

    private void ToggleStartWithWindows()
    {
        var enabled = !StartupRegistration.IsEnabled();
        StartupRegistration.SetEnabled(enabled);
        _settings.StartWithWindows = enabled;
        _settingsStore.Save(_settings);
        Rebuild();
    }

    /// <summary>Exit stops the effect immediately — SetThreadExecutionState(ES_CONTINUOUS) — so the
    /// machine is back to normal Windows sleep/screen behavior, but deliberately does not change
    /// AppSettings.Enabled: that's the user's last explicit on/off choice, restored on the next launch
    /// (App.axaml.cs), exactly mirroring what was set when they quit.</summary>
    private void Exit()
    {
        PowerManager.Apply(enabled: false);
        _trayIcon.IsVisible = false;
        _desktop.Shutdown();
    }
}

/// <summary>Minimal ICommand wrapper — NativeMenuItem.Command needs one and this app has no MVVM
/// framework command type of its own.</summary>
internal sealed class RelayCommand(Action execute) : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}
