# DDNetNW

DDNetNW is a desktop nickname watcher for public DDNet servers. It reads the public DDNet server browser list, checks selected nicknames, and shows their current status in a local Windows application.

Originally created by molochko.

Version: `v1.00`

## Features

- Track one or more DDNet nicknames.
- Display each nickname as a status card.
- Show online/offline state with clear colored borders.
- Display current server, map, address, score/time, team and AFK state when available.
- Detect basic status changes:
  - joined
  - left
  - changed server
  - changed map
- Manual scan button.
- Adjustable scan interval from 5 to 180 seconds.
- Local event log inside the application.
- Persistent settings saved in the user profile.
- Discord and Telegram sections are present in the interface as planned future channels.

## How it works

DDNetNW does not connect to a DDNet server as a player and does not read game memory.

The application downloads the public DDNet server list from the official master endpoints:

```text
https://master1.ddnet.org/ddnet/15/servers.json
https://master2.ddnet.org/ddnet/15/servers.json
https://master3.ddnet.org/ddnet/15/servers.json
https://master4.ddnet.org/ddnet/15/servers.json
```

For every scan, it reads the `servers` array, checks each server's `info.clients` list, and compares each `client.name` with the nicknames saved by the user.

A nickname match is text-based. DDNet nicknames are not accounts, so this application cannot guarantee a person's identity. If another player uses the same nickname, the application may show that nickname as online.

## Settings

Settings are stored in:

```text
%APPDATA%\DDNetNW\settings.json
```

Saved values:

- tracked nicknames
- scan interval
- local Windows notification setting

Delete `settings.json` to reset the app configuration.

## Requirements

For development:

- Windows 10 or Windows 11
- .NET 8 SDK
- Visual Studio 2022 or newer with .NET desktop development workload

For published builds:

- Framework-dependent build: requires .NET 8 Desktop Runtime on the target PC
- Self-contained build: does not require a separate runtime installation

## Run from source

```bash
dotnet run
```

## Build

Framework-dependent build:

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

Self-contained single-file build:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The published executable will be named:

```text
DDNetNW.exe
```

## Project structure

```text
DDNetNW.csproj
App.xaml
MainWindow.xaml
AddNicknameWindow.xaml
Models/
  AppMetadata.cs
  AppSettings.cs
  PlayerCard.cs
  PlayerScanResult.cs
  PlayerState.cs
Services/
  AppSettingsService.cs
  DdnetMasterClient.cs
```

## Notes

DDNetNW is intended as a friend online notifier for public DDNet servers. It should not be presented as an identity tracking or surveillance tool.
