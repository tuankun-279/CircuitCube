# Player tracking (`com.studio.player-tracking`)

A small package to reuse in any Unity game. It tells real store installs from test builds, so only real players are
counted.

Rule: the Editor and development builds never count as players and never use production.

## Contents

| Path | What it is |
|---|---|
| `Runtime/ChannelRules.cs` | Plain C# rules. No engine types. |
| `Runtime/BuildChannel.cs` | Finds the channel of the running build on the device. |
| `Runtime/Studio.PlayerTracking.asmdef` | Assembly `Studio.PlayerTracking`. |
| `Sql/real-devices-daily.sql`, `Sql/closed-test.sql` | BigQuery queries for the Firebase export. |
| `Tools~/players_sync.py` | Reads player numbers and writes JSON. |
| `Board~/closed-test-board.html` | A page for closed-test progress. |

Folders ending in `~` are not imported by Unity.

## Channels (`Runtime/ChannelRules.cs`)

Channels: `editor`, `dev`, `testlab`, `sideload`, `play`.

- `Pick(editor, developmentBuild, testLab, installer)`: the channel from what the build knows. Editor wins, then test
  lab, then development build, then the installer (`com.android.vending` = `play`, anything else = `sideload`).
- `CountsAsPlayer(channel)`: true only for `play`.
- `Environment(channel)`: `production` only for `play`, else `development`.
- `Normalize(channel)`: makes a value from a client safe to store. Unknown or null reads as `sideload`.
- `DailyBoard(channel, countedBoard, testBoard)`: the counted board for `play`, the test board for the rest. A null
  channel is an old client and is counted.

The file can also be compiled into a Cloud Code C# module, so the server applies the same rules:

```xml
<Compile Include="../../../Packages/com.studio.player-tracking/Runtime/ChannelRules.cs" />
```

(The path above is an example. Use the path from your module's `.csproj`.)

## Detecting the channel (`Runtime/BuildChannel.cs`)

- `BuildChannel.Current`: the channel, read once and kept.
- `BuildChannel.Installer`: the installer package.
  - Android API 30+: `getInstallSourceInfo` from the PackageManager.
  - Older Android: `getInstallerPackageName`.
  - Other platforms: empty.
- Test devices: the system setting `firebase.test.lab` is `true` (Firebase Test Lab, Play pre-launch robots).
- `BuildChannel.CountsAsPlayer`
- `BuildChannel.ServicesEnvironment`: for UGS `InitializationOptions.SetEnvironmentName`. The calling assembly needs a
  reference to `Unity.Services.Core.Environments`.
- `BuildChannel.Override(channel)`: forces a channel in tests.

## Use it in a game

1. Reference the asmdef `Studio.PlayerTracking`.
2. Set the UGS environment before `InitializeAsync`:
   `options.SetEnvironmentName(BuildChannel.ServicesEnvironment)`.
3. Send the channel with the daily check-in (for example `channel` and `version` as arguments to a Cloud Code
   endpoint). On the server, use `ChannelRules.Normalize` and `ChannelRules.DailyBoard`.
4. In analytics, add the channel as an event parameter `channel` and as a user property `build_channel`.

## SQL (`Sql/`)

- `real-devices-daily.sql`: real devices per day.
- `closed-test.sql`: closed-test testers per day.

Placeholders: `<GCP_PROJECT_ID>`, `<BIGQUERY_DATASET>`, `<DAYS>`, `<SINCE>`. `real-devices-daily.sql` uses `<DAYS>`,
`closed-test.sql` uses `<SINCE>` (the first day of the closed test). `players_sync.py` fills them in.

A real device has the event parameter `channel` = `play`. For older builds without the parameter, it has
`app_info.install_source` = `com.android.vending`.

## `Tools~/players_sync.py`

Needs Python 3.10+. Output is JSON on stdout.

Commands:

- `today`: players checked in today (Cloud Code endpoint `PlayersToday`).
- `reports`: name reports kept by the module.
- `reset ID`: resets one player's name.
- `daily`: real devices per day from BigQuery.
- `closed`: closed-test progress from BigQuery.
- `all`: all of the above as one JSON document.

Settings are in a local JSON file (`--config`, default `players_sync.local.json`). Keep it out of git. Keys:
`ugsProject`, `ugsEnvironmentId`, `module`, `gcpProject`, `dataset`, `since`, `goal`. An example is
`Tools/LevelLab/players_sync.example.json` in CircuitCube.

Secrets come only from environment variables:

- `UGS_KEY_ID`, `UGS_SECRET_KEY`: a UGS service account key.
- `GOOGLE_APPLICATION_CREDENTIALS`: a Google service account JSON with BigQuery Data Viewer and Job User.

`daily` and `closed` need `pip install google-cloud-bigquery`.

The `today`, `reports` and `reset` commands call endpoints that this package does not contain. The Cloud Code module
must offer `PlayersToday`, `ListReports` and `ResetName` (CircuitCube has them in `Tools/CloudCode/CircuitCubeRanks`).

## Closed-test board (`Board~/closed-test-board.html`)

Open the file in a browser. Paste or open the output of `players_sync.py closed`. It shows:

- testers today
- days in a row with 12+ testers (Google Play asks for 12 testers opted in for 14 days in a row)
- testers active on 14+ days
- D1 and D7
- testers per day
