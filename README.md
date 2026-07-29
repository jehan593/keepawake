# keepawake

A minimal Windows tray app that keeps the screen from turning off — one click to toggle, right-click
for the rest.

## Features

- **Left-click the tray icon** to toggle "keep screen on" — no menu needed for the everyday case
- **Right-click** for the full menu: Keep screen on, Start with Windows, Exit
- **Exit** immediately restores normal Windows sleep/screen behavior — nothing keeps running in the
  background afterward
- **Remembers state** across restarts: whatever was on/off when you last exited (or when the app
  last ran, if "Start with Windows" launches it) is what it restores to

## Install

Download the latest installer from [Releases](../../releases/latest) and run `keepawakeSetup.exe`.
No admin rights needed — this app never elevates, install or otherwise.

## Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```sh
dotnet build                       # from the repo root
dotnet run --project keepawake      # runs the tray app (shows a console window — dev-only; the
                                     # built keepawake.exe itself never does, see CLAUDE.md)
```

## How it works

A single unprivileged process calls `SetThreadExecutionState` (no admin rights needed, no background
service) to tell Windows the system and display shouldn't sleep. See [CLAUDE.md](CLAUDE.md) for the
full writeup.

## License

No license file yet — all rights reserved.
