# DDNetNW v1.35 — Quick Search Foundation

`v1.35` is a focused update that replaces the old direct add-nickname flow with a cleaner Quick Search flow and prepares the project for future search/autocomplete and player-data features.

## Highlights

- Quick Search is now the main way to add watched nicknames.
- Search runs automatically 1 second after the user stops typing.
- Search results are displayed as a compact strip below the search box.
- Online results are highlighted and show nickname, clan and current map.
- Existing watched nickname cards stay unchanged with their current controls.
- Search logic is separated from the UI in `Services/NicknameSearchService.cs`.
- Spanish UI option has been added.
- Map alert regions can now be selected from a centered region selector.

## Added

- Quick nickname search with debounce.
- Compact online/offline result strip.
- `+ Watch` action from the search result.
- Duplicate protection when adding watched nicknames.
- Spanish language option (`ESP`).
- Region selector window for map alerts.
- Region options: GER, RUS, POL, FRA, USA and BRA.
- `Models/QuickNicknameSearchResult.cs`.
- `Services/NicknameSearchService.cs`.

## Changed

- The old `+ Add nickname` header button is hidden in Players mode.
- Users are now guided to Quick Search when the watched nickname list is empty.
- Release documentation and repository files have been cleaned up for GitHub publishing.

## Current limitations

- Quick Search is exact-match based. It does not autocomplete yet.
- The search source is the current public DDNet server list, so offline players are shown as not online.
- Player statistics frontend is not included in this release.
- Discord and Telegram notifications are still planned for future versions.

## Planned next

- `v1.40`: online nickname suggestions/autocomplete.
- `v1.50`: larger search/profile foundation update.
- `v1.65`: first player statistics skeleton.
