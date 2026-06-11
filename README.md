# DDNetNW

DDNetNW is a Windows desktop watcher for public DDNet servers. It tracks selected player nicknames, watched maps, active servers and map online thresholds using the public DDNet master server list.

Originally created by molochko.

Current version: `v1.20`

## Features

- Track selected DDNet nicknames from public servers.
- Track selected DDNet maps as separate folders.
- Open a watched map and view active servers running that map.
- Show server count, total players and best active server for watched maps.
- Notify when a watched map reaches the selected online threshold.
- Filter map alerts by server name groups such as Any, GER, RUS and GER + RUS.
- Show local Windows notifications for player and map events.
- Switch between English and Russian UI.
- Switch between dark and light themes using custom controls.
- Store settings locally in the user's AppData folder.

## How it works

DDNetNW reads the public DDNet `servers.json` list from DDNet master servers, parses server information, then updates local player cards and map cards inside the desktop app.

The app does not log into DDNet, does not require a private backend, and does not need a VPS for local Windows notifications.

## Important notes

DDNet nicknames are not accounts. DDNetNW tracks public nickname text from public DDNet servers. Visually similar nicknames may still be different players.

Map server region filters are based on server name text, for example `GER` or `RUS`. This is intentionally simple and avoids unreliable GeoIP dependencies.

## Build

Requirements:

- Windows 10 or Windows 11
- .NET 8 SDK
- Visual Studio 2022 with .NET desktop development workload, or the `dotnet` CLI

Build a release package:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false
```

The output will be created in:

```text
bin/Release/net8.0-windows/win-x64/publish/
```

For GitHub Releases, zip the contents of the `publish` folder and attach the zip file to the release.

## Project structure

```text
DDNetNW/
├─ Assets/                 # Application icon assets
├─ Models/                 # Data models and UI card models
├─ Services/               # DDNet API client, settings, localization, notifications
├─ App.xaml                # Application resources and control styles
├─ MainWindow.xaml         # Main UI layout
├─ MainWindow.xaml.cs      # Main window behavior
├─ AddNicknameWindow.xaml  # Add/edit nickname dialog
└─ DDNetNW.csproj          # Project file
```

## Privacy

DDNetNW stores settings locally under the user's AppData folder. It does not upload watched nicknames, watched maps or settings to any third-party service.

## License

This project is released under the MIT License. See [LICENSE](LICENSE).
