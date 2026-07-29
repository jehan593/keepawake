using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Keepawake.Data;
using Keepawake.Native;
using Keepawake.Ui;

namespace Keepawake;

/// <summary>
/// Tray-only app: no window is ever created, on this launch or any other — the tray icon built by
/// TrayController is the entire UI surface. Restores whatever keep-awake state was in effect at last
/// exit (see AppSettings.Enabled) before the tray icon even appears, so a reboot with "Start with
/// Windows" on resumes silently instead of leaving the machine on normal power management until the
/// user notices and re-toggles it by hand.
/// </summary>
public partial class App : Application
{
    private TrayController? _tray;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // No window is ever assigned to desktop.MainWindow, and no window is ever opened at all —
            // without this, Avalonia's default ShutdownMode (OnLastWindowClose) could tear the app down
            // as soon as the window count is evaluated, taking the tray icon with it.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var settingsStore = new SettingsStore();
            var settings = settingsStore.Load();

            PowerManager.Apply(settings.Enabled);

            _tray = new TrayController(settings, settingsStore, desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
