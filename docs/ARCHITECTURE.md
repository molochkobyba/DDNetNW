# Architecture

DDNetNW is intentionally split into small parts so UI code does not own the monitoring logic.

## Main parts

```text
MainWindow.xaml / MainWindow.xaml.cs
  Shows the WPF interface and binds user actions to services.

Services/DdnetMasterClient.cs
  Reads public DDNet master server data and creates snapshots.

Services/NicknameSearchService.cs
  Small isolated quick-search block. It scans DDNet data for one exact nickname.

Services/AppSettingsService.cs
  Loads and saves local settings from AppData.

Services/LocalizationService.cs
  Keeps UI text translations for supported languages.

Services/LocalNotificationService.cs
  Sends local Windows notifications.

Models/*
  Contains app settings, scan results, player cards, map cards and quick-search models.
```

## Data flow

```text
DDNet master servers
        ↓
DdnetMasterClient.ReadSnapshotAsync()
        ↓
server/map/player snapshot
        ↓
monitoring loop or NicknameSearchService
        ↓
UI cards + event log + Windows notifications
```

## Quick Search flow

```text
User types nickname
        ↓
1-second debounce in UI
        ↓
NicknameSearchService.SearchExactAsync(nickname)
        ↓
DdnetMasterClient reads latest public server list
        ↓
Search result: online/offline + player data
        ↓
MainWindow shows compact result strip
```

The important point: `NicknameSearchService` is a standalone programmatic block. It can be reviewed or rewritten without touching most of the UI.

## Local-only design

DDNetNW does not require a private backend for the current feature set. All watched nicknames, watched maps and settings are stored locally.
