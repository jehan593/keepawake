# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

keepawake: a from-scratch Windows tray app whose entire feature set is keeping the screen on. .NET 8,
plain Win32 (`Native/Win32.cs`) — **no UI framework, no Avalonia** — driving `Shell_NotifyIcon` and an
owner-drawn native popup menu directly, painted in the same Nord color palette dnsw uses for its tray
icon (Nord0 background, Nord8/Nord3 glyph swap for on/off — see "Theme") plus the same Martian Mono
font, applied via GDI rather than a shared XAML style. Package/namespace `Keepawake`, solution at
`keepawake.sln`, single project at `keepawake\keepawake.csproj`.

Unlike `../dnsw`, there is **no service/IPC split and no window at all** — the one thing this app does
(`SetThreadExecutionState`) needs no elevation, so a single unprivileged process is the entire app,
and the tray icon's context menu is the entire UI surface. That menu is *not* OS-drawn chrome — Windows
never supplies its own themed rendering for a tray context menu, on this build or the Avalonia one that
preceded it (see "Theme" for why an earlier version of this file's claim that it was "drawn by Windows
shell chrome" was wrong) — so getting the Nord/Martian-Mono look here means owning every pixel of the
menu via `WM_MEASUREITEM`/`WM_DRAWITEM`. One toggle, one setting to remember — resist the urge to grow
this into a general power-management tool (per-process rules, scheduling, hotkeys, separate "keep
system awake but let the screen sleep" mode, etc.).

**Why not Avalonia**: an early version of this app used Avalonia (same stack as `../dnsw`/
`../linker-windows`) purely for its `TrayIcon`/`NativeMenu` types, since there's no window here for
Avalonia to do anything else with. That pulled in Skia (~9MB) and HarfBuzz (~1.5MB) as native
dependencies of the AOT-compiled exe — real rendering engines staged in full for an app that never
draws through them, since the actual menu pixels were already coming from Avalonia's own Skia backend,
not from Windows. Replacing that with hand-rolled `Shell_NotifyIcon` + owner-draw GDI (this version)
produces the same pixels without carrying a UI framework to do it — see "Architecture".

## Core flow

```
Launch (manual or "Start with Windows")
  -> SettingsStore.Load() reads %AppData%\keepawake\settings.json
  -> PowerManager.Apply(Enabled) re-asserts whatever state was on at last exit
  -> TrayIcon builds the tray icon + menu reflecting that state

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

- **`Native/PowerManager.cs`** — the entire keep-awake mechanism: one P/Invoke, `SetThreadExecutionState`,
  called from the app's single long-lived thread (the Win32 message-loop thread started in
  `Program.cs`). `ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED` while enabled (system *and*
  display both stay awake — this app never offers a "system awake, screen can sleep" middle state);
  plain `ES_CONTINUOUS` otherwise. The flag lives with the calling thread — Windows clears it
  automatically the moment that thread (and so the process) exits, which is exactly the "exit restores
  normal behavior" guarantee; `TrayIcon.Exit` calls `Apply(false)` explicitly anyway so the revert is
  immediate rather than depending on process-teardown timing.
- **`Native/StartupRegistration.cs`** — "Start with Windows" via the per-user `Run` registry key,
  pointed at the plain exe with no arguments. No autostart-detection argument like dnsw's
  `--autostart`: that existed there only to suppress opening a window on a login-triggered launch, and
  this app never opens a window on *any* launch, so there's nothing to suppress. Uses
  `Microsoft.Win32.Registry` from the BCL, not a package — ships with the `net8.0-windows` targeting
  pack regardless of what else the project references.
- **`Native/Win32.cs`** — every P/Invoke declaration and struct this app needs (window class/message
  loop, `Shell_NotifyIcon`, owner-draw menu APIs, GDI brush/pen/font/text calls), kept in one file since
  every declaration exists purely to support the single call site in `Ui/TrayIcon.cs`. Nothing here is
  a general-purpose Win32 wrapper library.
- **`Data/SettingsStore.cs`** — flat JSON at `%AppData%\keepawake\settings.json`, same "no database for
  one small object" shape as dnsw's `SettingsStore`. Owned by this one process; no cross-process
  locking needed since `Program.cs` enforces single-instance via a named mutex.
- **`Ui/TrayIcon.cs`** — the whole UI. A hidden top-level window (never shown — no `WS_VISIBLE` — but
  a real window, not `HWND_MESSAGE`, because `SetForegroundWindow` doesn't work on message-only
  windows and is needed to make the popup menu dismiss correctly on an outside click) owns the
  `Shell_NotifyIcon` tray icon and rebuilds an owner-drawn `HMENU` on every state change, the same
  "rebuild the whole menu" shape the old Avalonia `NativeMenu` version used. `WndProc` is a static
  method exposed to native code via `[UnmanagedCallersOnly]` and a raw function pointer (the
  AOT-safe way to hand a callback to Win32 — no runtime delegate marshaling) routed to a single
  live instance (this app only ever has one). Left-click and the "Keep screen on" menu item both
  call the same `ToggleEnabled`, so the icon itself is a fully-functional one-click toggle and the
  menu is only needed for "Start with Windows" and Exit. Re-adds the icon on the broadcast
  `"TaskbarCreated"` message so an Explorer crash/restart doesn't silently drop it.
- **`Ui/MenuTheme.cs`** — the Nord "Dark" palette (same hex values the old `Theme/Colors.axaml`
  `Dark` dictionary used) as GDI `COLORREF`s, plus loading `Fonts/martian_mono_regular.ttf` as an
  `FR_PRIVATE` font resource (process-scoped, no system-wide font install, auto-cleaned up on exit) —
  only the regular weight, since that's the only one the menu was ever actually styled with.
- **Native AOT**, same reasoning as dnsw/linker-windows — smaller published exe than a framework-
  dependent `PublishSingleFile`. With no Avalonia/Skia/HarfBuzz in the dependency graph at all, there's
  nothing left to trim by hand the way `av_libglesv2.dll` used to need stripping. No
  `JsonSerializerContext` complexity beyond the one already in `Data/SettingsJsonContext.cs`
  (settings.json is the only thing ever serialized here — no IPC wire format to keep separate, unlike
  dnsw).

## Theme

`Assets/generate-icon.ps1` draws a monitor glyph (GDI+, hand-rolled multi-size ICO container, same
technique as dnsw's padlock) — bezel + inset screen (punched out in the background color, so it reads
as a dark screen framed by a bezel rather than a solid blob) + a small neck/base — in the same two
Nord colors dnsw uses for its padlock: Nord8 (`app-on.ico`, screen kept on) and Nord3 dim gray
(`app-off.ico`, off), both on the same Nord0 rounded-square background. Differs from dnsw only in
glyph shape (monitor vs. padlock), not in palette or background treatment.

The tray context menu itself (status line, "Keep screen on", "Start with Windows", "Exit") is drawn
entirely by this app, not by Windows — `Ui/TrayIcon.cs` builds the popup with every item flagged
`MF_OWNERDRAW` and handles `WM_MEASUREITEM`/`WM_DRAWITEM` directly: Nord0 background (via
`SetMenuInfo`'s `MIM_BACKGROUND`, which is what covers the popup's own border/padding — item rects
alone don't reach it), Nord2 row-hover highlight, Nord6 text in Martian Mono, a Nord8 checkmark drawn
as a two-segment polyline (owner-draw items get no default check glyph — the owner has to paint one),
and a Nord3 separator line. See `Ui/MenuTheme.cs` for the exact colors/metrics. This reproduces what
the Avalonia-based version's `Theme/Styles.axaml` did through Skia (that file's now-deleted comment
correctly noted that Avalonia's Windows tray menu is a real Skia-rendered popup, not OS chrome — this
version keeps that same "we draw it ourselves" reality, just through GDI instead of a UI framework).

`app.manifest` requests `asInvoker` — no privileged component exists in this app at all, so there is
no elevation story to document beyond "it never needs one." It also requests legacy `dpiAware` (not
per-monitor-v2) — the owner-draw menu's fixed pixel metrics in `Ui/MenuTheme.cs` assume the same
single system-DPI scaling factor this flag has always implied for this app.

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
immediately. Changes touching `Ui/TrayIcon.cs`/`Ui/MenuTheme.cs`'s owner-draw code specifically need a
visual check too (right-click the icon) — there's no automated way to catch a wrong `COLORREF`, a
`DrawText` rect that clips text, or a `WM_MEASUREITEM` size that's off by a few pixels.

- **`installer/keepawake.iss`** — Inno Setup script, same shape as dnsw's but much shorter: no Windows
  Service to stop/reinstall and `PrivilegesRequired=lowest` throughout (install, uninstall, and the
  app itself never elevate). The only `[Code]` step is a `taskkill` before the file copy so an
  upgrade isn't blocked by a running `keepawake.exe` holding its own file open. Its `[Files]` section
  is a recursive glob over `..\keepawake\publish\*`, so it needs no changes to pick up
  `Assets\*.ico`/`Fonts\martian_mono_regular.ttf` — those are `CopyToOutputDirectory` items in
  `keepawake.csproj` (loaded from disk at runtime via `LoadImageW`/`AddFontResourceExW`) rather than
  resources embedded in the exe, unlike the old `AvaloniaResource` versions of the same files.
  Built via `.github/workflows/release.yml`'s manually-triggered Release workflow
  (`gh workflow run release.yml`), which publishes (Native AOT, same as the local Release config),
  compiles `keepawake.iss` with Inno Setup, and attaches the resulting `keepawakeSetup.exe` to a
  GitHub Release — mirroring dnsw's release flow, minus the service-specific steps.
