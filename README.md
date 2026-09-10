# keepawake

A tiny Windows tray app that stops your screen from turning off.

> **FYI:** this project is fully vibe-coded

## How to use

- **Left-click** the tray icon to toggle keep-screen-on
- **Right-click** for the menu: toggle, start with Windows, exit
- Exit restores normal sleep behavior immediately

## Install

Download `keepawakeSetup.exe` from [Releases](../../releases/latest). No admin rights needed.

## Build

Requires [.NET Framework 4.8 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net48) or later.

```sh
dotnet build
dotnet run --project keepawake
```

## How it works

One call to `SetThreadExecutionState` tells Windows not to sleep. No background service, no elevation.
