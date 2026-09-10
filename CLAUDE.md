# CLAUDE.md

## What this is

keepawake: a Windows tray app that keeps the screen on. .NET Framework 4.8, plain Win32 (`Native/Win32.cs`) driving `Shell_NotifyIcon` and an owner-drawn popup menu via GDI. No UI framework. Package/namespace `Keepawake`, solution at `keepawake.sln`, single project at `keepawake\keepawake.csproj`.

One toggle, one setting — resist the urge to grow this.

## Core flow

```
Launch -> SettingsStore.Load() -> PowerManager.Apply(Enabled) -> TrayIcon builds icon + menu
Left-click or "Keep screen on" -> toggle, save, re-apply, rebuild
Exit -> PowerManager.Apply(false) immediately, setting left as-is for next launch
```

## Architecture

- **`Native/PowerManager.cs`** — `SetThreadExecutionState` keeps system+display awake while enabled
- **`Native/StartupRegistration.cs`** — "Start with Windows" via per-user Run registry key
- **`Native/Win32.cs`** — all P/Invoke declarations and structs
- **`Data/SettingsStore.cs`** — JSON at `%AppData%\keepawake\settings.json`
- **`Ui/TrayIcon.cs`** — hidden window owning the tray icon and owner-drawn menu
- **`Ui/MenuTheme.cs`** — Nord color palette and Martian Mono font for the menu

## Theme

Icons: Nord8 (on) / Nord3 (off) monitor glyph on Nord0 background. Menu: Nord0 background, Nord2 hover, Nord6 text, Nord8 checkmark, Nord3 separator. Font: Martian Mono regular, loaded as process-private.

## Commands

```sh
dotnet build
dotnet run --project keepawake
```

No test suite. Manual verification: toggle keeps screen on (`powercfg /requests`), exit clears it.
