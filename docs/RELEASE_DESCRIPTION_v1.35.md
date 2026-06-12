## DDNetNW v1.35 — Quick Search Foundation

This update replaces the old add-nickname flow with a faster Quick Search system and prepares the app for future autocomplete and player-profile features.

### Added

- Quick nickname search on the Players page.
- Automatic search after the user stops typing for 1 second.
- Compact search result strip below the search box.
- Online result highlight.
- Online result details: nickname, clan and current map.
- `+ Watch` action from the search result.
- Duplicate protection when adding searched nicknames.
- Spanish UI option (`ESP`).
- Centered tracked-region selector for map alerts.
- Region options: GER, RUS, POL, FRA, USA and BRA.
- Isolated quick-search logic in `Services/NicknameSearchService.cs`.
- Quick-search result model in `Models/QuickNicknameSearchResult.cs`.

### Changed

- The old Players-mode `+ Add nickname` header button was replaced by Quick Search.
- Existing watched nickname cards were kept unchanged. Delete, edit and details controls still work as before.
- Empty player-list text now points users to Quick Search.
- Repository documentation has been cleaned up for GitHub publishing.

### Current limitations

- Quick Search currently uses exact nickname matching.
- Autocomplete/suggestions are not included yet and are planned for `v1.40`.
- Offline player profiles/statistics are not included in this release.
- Discord and Telegram notifications are still planned for future versions.

### Notes

DDNetNW reads public DDNet server browser data only. It does not modify the DDNet client, does not connect to servers as a player, and does not require a private backend for local Windows notifications.
