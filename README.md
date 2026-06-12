# DDNetNW

**DDNetNW** is a Windows desktop companion app for DDNet. It watches public DDNet servers, tracks selected nicknames and maps, and sends local Windows notifications when important events happen.

Current version: `v1.35`

> DDNetNW reads public DDNet server browser data. It does not modify the DDNet client, does not connect as a player, and does not require a private backend for local notifications.

## Highlights in v1.35

- Quick nickname search is now the main way to add watched players.
- Search runs automatically after the user stops typing for 1 second.
- Search result is shown as a compact strip, not a large profile card.
- Online results are highlighted and show nickname, clan and current map.
- Search logic is isolated in `Services/NicknameSearchService.cs`.
- Added Spanish UI option (`ESP`).
- Added a centered region selector for map alert filters.

## Features

### Player watcher

- Track selected DDNet nicknames from public servers.
- Detect player join, leave, server change and map change events.
- Show player status cards with current server and map details.
- Send local Windows notifications for watched-player events.

### Quick Search

- Search an exact nickname from the public DDNet server list.
- Show a compact result strip below the search box.
- Display nickname, clan and current map when the player is online.
- Add searched nicknames directly to the watched list.
- Avoid duplicate watched nicknames.

### Map watcher

- Track selected maps as separate watched folders.
- Open a watched map and view active servers running that map.
- Show server count, total online players and the best active server.
- Notify when a watched map reaches a selected online threshold.
- Filter map alerts by selected server regions such as GER, RUS, POL, FRA, USA and BRA.

### Interface

- Dark and light theme support.
- English, Russian and Spanish UI options.
- Custom modern controls instead of default Windows-style dropdowns.
- Local settings storage under the user's AppData folder.

## How it works

DDNetNW periodically requests the public DDNet master server list, parses the server data, scans the current `clients` list for tracked nicknames, and compares the new scan with the previous local state. The app then creates local events such as `joined`, `left`, `changed server` or `changed map`.

Quick Search uses the same public DDNet data source, but it is intentionally isolated in a small service file:

```text
Services/NicknameSearchService.cs
```

This keeps the search logic separate from the WPF interface code, making the feature easier to review and improve.

## Limitations

- DDNet nicknames are not verified accounts. The app tracks nickname text only.
- If another player uses the same nickname, they may be detected as a match.
- If a player changes their nickname, they will not be found under the old watched name.
- Very fast reconnects may be missed depending on the selected check interval.
- Map alert region filters are based on server name text, not GeoIP.
- Discord and Telegram notifications are planned, but not implemented yet.

## Build

Requirements:

- Windows 10 or Windows 11
- .NET 8 SDK
- Visual Studio 2022 with **.NET desktop development** workload

Build in Visual Studio:

1. Open `DDNetNW.csproj`.
2. Select `Release`.
3. Build or publish the project.

Publish from terminal:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false
```

The publish output will be created in:

```text
bin/Release/net8.0-windows/win-x64/publish/
```

For GitHub Releases, zip the contents of the `publish` folder and attach the zip as a release asset.

## Project structure

```text
DDNetNW/
├─ Assets/                      # Application icon assets
├─ Models/                      # Data models and UI card models
├─ Services/                    # DDNet client, settings, localization, notifications, quick search
├─ docs/                        # Release notes and technical documentation
├─ .github/                     # GitHub issue templates and PR template
├─ App.xaml                     # Application resources and control styles
├─ MainWindow.xaml              # Main UI layout
├─ MainWindow.xaml.cs           # Main window behavior
├─ AddNicknameWindow.xaml       # Reused dialog for editing nicknames and watched maps
├─ SelectRegionsWindow.xaml     # Region filter selector for map alerts
└─ DDNetNW.csproj               # Project file
```

## Privacy

DDNetNW stores settings locally under the user's AppData folder. It does not upload watched nicknames, watched maps or settings to a private server.

## Roadmap

- `v1.40` — online nickname suggestions/autocomplete.
- `v1.50` — larger search and profile foundation update.
- `v1.65` — first player statistics skeleton powered by public DDNet/DDStats data.
- Later — Discord/Telegram notifications, deeper analytics and player progress tools.

## License

This project is released under the MIT License. See [LICENSE](LICENSE).
