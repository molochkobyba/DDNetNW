# Data sources

## DDNet master server list

DDNetNW reads public DDNet server browser data from DDNet master servers. This data contains public servers, maps and current clients.

Used for:

- watched nickname monitoring;
- Quick Search online/offline detection;
- current server and map display;
- watched map activity;
- map alert thresholds.

## DDStats / player-data sources

DDStats-style APIs are planned for later player profile/statistics features, not for `v1.35`.

Possible future use:

- total points;
- player country;
- skin information;
- last finishes;
- point progress timeline.

## Why two sources are useful

```text
Online status     → DDNet master server list
Player statistics → DDStats / profile data API
```

The DDNet master list is good for current online state. DDStats-style data is better for slower profile/statistics features.
