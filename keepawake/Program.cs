using System.Threading;
using Avalonia;

namespace Keepawake;

/// <summary>
/// Single unprivileged process, no separate service/roles (unlike dnsw) — SetThreadExecutionState
/// needs no elevation, so the tray process itself is the entire app.
/// </summary>
internal static class Program
{
    private const string SingleInstanceMutexName = "Local\\keepawake-tray-single-instance";

    [STAThread]
    public static int Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            return 0; // another keepawake tray instance already owns the tray icon
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseWin32()
            // No window ever renders (see App.axaml.cs) — software rendering avoids pulling in the
            // ANGLE/Direct3D GPU path for a surface that's never drawn.
            .With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Software } })
            .UseSkia()
            .LogToTrace();
}
