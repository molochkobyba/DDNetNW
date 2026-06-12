# Changelog

## v1.35 — Quick Search Foundation

### Added

- Added Quick Search as the main flow for adding watched nicknames.
- Added 1-second delayed search after the user stops typing.
- Added a compact search result strip with online/offline state.
- Added online result details: nickname, clan and current map.
- Added quick add from search to the watched nicknames list.
- Added duplicate protection for nicknames added through search.
- Added Spanish UI option (`ESP`).
- Added a centered tracked-region selector for map alerts.
- Added region options: GER, RUS, POL, FRA, USA and BRA.
- Added `Models/QuickNicknameSearchResult.cs`.
- Added `Services/NicknameSearchService.cs` as an isolated programmatic search block.

### Changed

- Replaced the old Players-mode `+ Add nickname` button with Quick Search.
- Updated the empty watched-nicknames state to point users to Quick Search.
- Kept existing watched nickname cards unchanged: details, edit and delete controls remain the same.
- Updated app metadata to `v1.35`.
- Improved GitHub-ready documentation and repository structure.

### Notes

- Quick Search checks an exact nickname against the current public DDNet server list.
- Autocomplete/suggestions are planned for `v1.40`.
- Player statistics UI is intentionally not part of this release.

## v1.20

### Added

- Map tracking mode in the main menu.
- Watched map folders with server count and total online.
- Map details screen with active servers for the selected map.
- Map alert threshold for online count.
- Server name filters for map alerts: Any, GER, RUS and GER + RUS.
- English/Russian UI switch using custom segmented controls.
- Dark/light theme switch using custom segmented controls.
- Expanded About page with data source, storage, privacy and limitations.
- Application icon assets.

### Changed

- Reworked interface structure for Players and Maps.
- Replaced default dropdowns with custom UI controls.
- Improved local settings storage under AppData.
- Updated project metadata and release documentation.

### Fixed

- Reduced repeated interface text.
- Improved add/edit nickname flow.
- Improved local notification handling fallback.
