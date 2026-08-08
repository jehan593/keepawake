using Keepawake.Data;
using Keepawake.Native;
using Keepawake.Ui;

namespace Keepawake;

/// <summary>
/// Single unprivileged process, no separate service/roles (unlike dnsw) — SetThreadExecutionState
/// needs no elevation, so the tray process itself is the entire app. No UI framework host either
/// (see Ui/TrayIcon.cs) — just a plain Win32 message loop pumping the hidden window that owns the
/// tray icon and its menu.
/// </summary>
internal static class Program
{
    private const string SingleInstanceMutexName = "Local\\keepawake-tray-single-instance";

    [STAThread]
    public static int Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            return 0; // another keepawake tray instance already owns the tray icon
        }

        var settingsStore = new SettingsStore();
        var settings = settingsStore.Load();

        // Restores whatever keep-awake state was in effect at last exit before the tray icon even
        // appears, so a reboot with "Start with Windows" on resumes silently instead of leaving the
        // machine on normal power management until the user notices and re-toggles it by hand.
        PowerManager.Apply(settings.Enabled);

        _ = new TrayIcon(settings, settingsStore);

        while (Win32.GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            Win32.TranslateMessage(ref msg);
            Win32.DispatchMessageW(ref msg);
        }

        return 0;
    }
}
