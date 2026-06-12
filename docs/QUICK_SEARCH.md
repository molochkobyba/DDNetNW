# Quick Search

Quick Search was added in `v1.35` as a faster replacement for the old add-nickname flow.

## User behavior

1. The user types a nickname into the search box.
2. The app waits for 1 second after typing stops.
3. The app searches the current public DDNet server list.
4. A compact result strip appears under the search box.
5. If the player is online, the strip is highlighted and shows nickname, clan and map.
6. The user can click `+ Watch` to add the nickname to the watched list.

## Why it is separated

The programmatic search logic is stored here:

```text
Services/NicknameSearchService.cs
```

The UI handles typing, debounce and visual result rendering. The service handles the actual DDNet lookup.

## Current limitations

- Exact nickname search only.
- No autocomplete in `v1.35`.
- Offline players cannot be resolved through DDNet master server data alone.

## Planned

- `v1.40`: online nickname suggestions/autocomplete.
- `v1.50`: use search as an entry point for future profile/statistics screens.
