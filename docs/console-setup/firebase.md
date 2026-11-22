# Firebase

Project `circuit-cube`, Android app `com.zyerkun.circuitcube`. Config file `Assets/google-services.json` (it holds
only public identifiers; replace it from Project settings > Your apps when the app or SHA keys change).

## Analytics

- Events from `Game/Services/GameAnalytics.cs`: `level_start`, `level_complete`, `level_restart`, `undo_used`,
  `shape_unlocked`, `cloud_signin`, `cloud_pull`, `ranks_signin`, and others named in the code.
- To see parameters in reports, register them as custom dimensions: Analytics > Custom definitions >
  Create custom dimension (event scope), e.g. `shape`, `level`, `moves`, `seconds`, `expert`.
- Live check: Analytics > DebugView, with `adb shell setprop debug.firebase.analytics.app com.zyerkun.circuitcube`.

## Crashlytics

- Turned on with the package (`CC_CRASHLYTICS`); the level being played is set as a custom key.
- After a release build, check Crashlytics > the new version shows up with crash-free users.

## Nothing to set up for the leaderboards

The boards run on Unity Gaming Services. Firebase stays for analytics and crashes only.
