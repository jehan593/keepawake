# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

keepawake: a from-scratch Windows tray app whose entire feature set is keeping the screen on. Avalonia
UI (.NET 8, same stack as `../dnsw`/`../linker-windows`), same Nord color palette as dnsw for the tray
icon (Nord0 background, Nord8/Nord3 glyph swap for on/off — see "Theme"). Package/namespace
`Keepawake`, solution at `keepawake.sln`, single project at `keepawake\keepawake.csproj`.

Unlike `../dnsw`, there is **no service/IPC split and no window at all** — the one thing this app does
(`SetThreadExecutionState`) needs no elevation, so a single unprivileged process is the entire app,
and the tray icon's native context menu is the entire UI surface (which also means there's no app
window to apply dnsw's Martian Mono font or Nord `Styles.axaml`/`ThemeManager` to — the tray tooltip
and menu text are drawn by Windows shell chrome, outside the app's own theming). One toggle, one
setting to remember — resist the urge to grow this into a general power-management tool (per-process
rules, scheduling, hotkeys, separate "keep system awake but let the screen sleep" mode, etc.).

## Core flow

```
Launch (manual or "Start with Windows")
  -> SettingsStore.Load() reads %AppData%\keepawake\settings.json
  -> PowerManager.Apply(Enabled) re-asserts whatever state was on at last exit
  -> TrayController builds the tray icon + menu reflecting that state

Left-click the tray icon, or the "Keep screen on" menu item (equivalent, both call ToggleEnabled)
  -> flips AppSettings.Enabled, saves it, re-applies PowerManager state, rebuilds icon/tooltip/menu

Exit (menu item only — there's no window to close)
  -> PowerManager.Apply(enabled: false) — immediate revert to normal Windows sleep/screen behavior
  -> does NOT touch AppSettings.Enabled: that's "the user's last explicit choice," restored verbatim
     on the next launch. Same "preference vs. current effect" split as dnsw's AppSettings.Enabled,
     just without a background service to keep the effect alive while the tray is closed — here,
     closing the tray *is* what turns the effect off, on purpose.
```

## Architecture

- **`Native/PowerManager.cs`** — the entire mechanism: one P/Invoke, `SetThreadExecutionState`, called
  from the app's single long-lived dispatcher thread. `ES_CONTINUOUS | ES_SYSTEM_REQUIRED |
  ES_DISPLAY_REQUIRED` while enabled (system *and* display both stay awake — this app never offers a
  "system awake, screen can sleep" middle state); plain `ES_CONTINUOUS` otherwise. The flag lives with
  the calling thread — Windows clears it automatically the moment that thread (and so the process)
  exits, which is exactly the "exit restores normal behavior" guarantee; `TrayController.Exit` calls
  `Apply(false)` explicitly anyway so the revert is immediate rather than depending on
  process-teardown timing.
- **`Native/StartupRegistration.cs`** — "Start with Windows" via the per-user `Run` registry key,
  pointed at the plain exe with no arguments. No autostart-detection argument like dnsw's
  `--autostart`: that existed there only to suppress opening a window on a login-triggered launch, and
  this app never opens a window on *any* launch, so there's nothing to suppress.
- **`Data/SettingsStore.cs`** — flat JSON at `%AppData%\keepawake\settings.json`, same "no database for
  one small object" shape as dnsw's `SettingsStore`. Owned by this one process; no cross-process
  locking needed since `Program.cs` enforces single-instance via a named mutex.
- **`Ui/TrayController.cs`** — builds/rebuilds the tray icon's `NativeMenu` and swaps the icon glyph
  between `Assets/app-on.ico`/`app-off.ico`. Left-click and the "Keep screen on" menu item both call
  the same `ToggleEnabled`, so the icon itself is a fully-functional one-click toggle and the menu is
  only needed for "Start with Windows" and Exit.
- **`App.axaml.cs`** — no window is ever created, not even on manual launch (unlike dnsw's
  `ProvidersWindow`-on-manual-launch behavior) — `ShutdownMode.OnExplicitShutdown` is still set
  defensively so nothing about Avalonia's own window-count bookkeeping can tear down the tray icon.
- **Native AOT**, same reasoning as dnsw/linker-windows — smaller published exe than a framework-
  dependent `PublishSingleFile`. No `JsonSerializerContext` complexity beyond the one already in
  `Data/SettingsJsonContext.cs` (settings.json is the only thing ever serialized here — no IPC wire
  format to keep separate, unlike dnsw).

## Theme

`Assets/generate-icon.ps1` draws a monitor glyph (GDI+, hand-rolled multi-size ICO container, same
technique as dnsw's padlock) — bezel + inset screen (punched out in the background color, so it reads
as a dark screen framed by a bezel rather than a solid blob) + a small neck/base — in the same two
Nord colors dnsw uses for its padlock: Nord8 (`app-on.ico`, screen kept on) and Nord3 dim gray
(`app-off.ico`, off), both on the same Nord0 rounded-square background. Differs from dnsw only in
glyph shape (monitor vs. padlock), not in palette or background treatment.

`app.manifest` requests `asInvoker` — no privileged component exists in this app at all, so there is
no elevation story to document beyond "it never needs one."

## A note on the console window during development

`dotnet run`/`dotnet build`'s own host (`dotnet.exe`) is a console app, so running the tray app via
`dotnet run --project keepawake` shows an empty console window for the lifetime of the process — this
is the .NET host, not something keepawake itself opens. `keepawake.csproj` sets `OutputType=WinExe`,
so the actual compiled `keepawake.exe` — whether double-clicked, launched from the Start menu, or
started via the "Start with Windows" Run-key entry — never allocates or shows a console at all. Don't
"fix" this by changing `OutputType` or adding console-hiding code; there's nothing to fix, it's purely
a `dotnet run` artifact of the dev inner loop.

## Commands

```sh
dotnet build                        # from repo root (operates on keepawake.sln)
dotnet run --project keepawake       # runs the tray app directly (shows a console — see above)
```

No test suite — the one thing worth verifying by hand after a change is that toggling "Keep screen on"
actually prevents display/system sleep (e.g. via `powercfg /requests`, which lists the calling
process's active execution-state request, from an elevated prompt) and that Exit clears it
immediately.

- **`installer/keepawake.iss`** — Inno Setup script, same shape as dnsw's but much shorter: no Windows
  Service to stop/reinstall and `PrivilegesRequired=lowest` throughout (install, uninstall, and the
  app itself never elevate). The only `[Code]` step is a `taskkill` before the file copy so an
  upgrade isn't blocked by a running `keepawake.exe` holding its own file open.
  Built via `.github/workflows/release.yml`'s manually-triggered Release workflow
  (`gh workflow run release.yml`), which publishes (Native AOT, same as the local Release config),
  compiles `keepawake.iss` with Inno Setup, and attaches the resulting `keepawakeSetup.exe` to a
  GitHub Release — mirroring dnsw's release flow, minus the service-specific steps.
