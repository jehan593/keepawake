using System.Runtime.InteropServices;

namespace Keepawake.Native;

/// <summary>
/// The entire "keep awake" mechanism: SetThreadExecutionState, called from the app's one long-lived
/// UI/dispatcher thread. Needs no elevation and no background service — the flag lives with the
/// calling thread for as long as that thread runs, and Windows itself clears it the moment the thread
/// (and so the process) exits, which is exactly the "exit restores normal Windows behavior" behavior
/// this app promises. <see cref="Apply"/> is called again explicitly on exit anyway (see
/// Ui/TrayController.cs), purely so the revert is immediate and doesn't depend on that implicit
/// cleanup racing against anything else during shutdown.
/// </summary>
public static class PowerManager
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;
    private const uint EsDisplayRequired = 0x00000002;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);

    /// <summary>
    /// enabled=true keeps both the system and the display awake (ES_SYSTEM_REQUIRED |
    /// ES_DISPLAY_REQUIRED) — the one thing this app does. enabled=false clears back to plain
    /// ES_CONTINUOUS, restoring normal Windows sleep/screen-off behavior.
    /// </summary>
    public static void Apply(bool enabled)
    {
        var flags = EsContinuous;
        if (enabled)
        {
            flags |= EsSystemRequired | EsDisplayRequired;
        }

        SetThreadExecutionState(flags);
    }
}
