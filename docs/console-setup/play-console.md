# Google Play Console

App: **Circuit Cube** (`com.zyerkun.circuitcube`). Owner signs in; nothing here needs a password typed by anyone else.

## Play Games Services (already set up)

- Play Console > Grow users > Play Games Services > Setup and management > Configuration.
- Project id `6185798786`; Android credential for the app's signing key (upload and Play App Signing SHA-1).
- Saved games: **on** (cloud save, `Game/Services/CloudSave.cs`).
- Leaderboards and achievements here are **not used**: the boards run on Unity Gaming Services.

## Game server credential (for Unity Gaming Services sign-in)

1. Configuration > Credentials > **Add credential** > type **Game server**.
2. It asks for an OAuth client: create one in the linked Google Cloud project, type **Web application**
   (no redirect URIs needed), then pick it and save the credential.
3. Copy the **client id** into:
   - Unity Editor: Window > Google Play Games > Setup > Android setup > **Web client ID**, then Setup
     (writes `mWebClientId` in `PlayGamesSettings.asset`; commit it, it is not secret).
   - Unity Cloud Dashboard > Player Authentication > Identity providers > Google Play Games: client id.
4. The **client secret** goes only into the Unity Cloud Dashboard field, typed by the owner. Never in git or chat.
5. **Publish** the Play Games configuration changes (Play Games Services > Publishing > Publish), or only
   testers can sign in. The first publish needs Properties filled: description, category, icon 512x512 and
   feature graphic 1024x500 (both in `StoreListing/`; uploads are done by the owner).

## Data safety form (whenever data use changes)

App content > Data safety. With the leaderboards the app also shares:
- **App activity > Other actions** (game results: taps, moves, time), collected, shared with Unity, for app
  functionality, not optional when signed in.
- **App info > Other user-generated content**: the player name.
- **Device or other IDs**: the UGS player id.
Keep it in step with `PRIVACY.md`.

## Release

The version code goes up in Player Settings for every upload (Play refuses a code it has seen, even from a draft).
Next build: 1.4.1 (code 8).

### State

| Date | Track | Release | Notes |
|---|---|---|---|
| 2026-11-17 | Closed testing - Alpha | 5 (1.2.0) | |
| 2026-11-23 | Closed testing - Alpha | **7 (1.4.0)**, rolled out about 10:40 (UTC+7), replaces 5 (1.2.0) | Notes from `StoreListing/release_notes.txt` (en-US, vi-VN). Code 6 (1.3.0) was never uploaded. |

Checks Play showed for 7 (1.4.0):
- No deobfuscation file: the game does not use R8/ProGuard. Ignore.
- Native code without debug symbols, and `android.hardware.screen.portrait` declared as required (keeps the game off
  Chromebooks, Play Games on PC and XR). Ignored for this test with "I already have plans to resolve this issue";
  both are fixed for the next build by `Assets/CircuitCube/Editor/AndroidReleaseFixes.cs` (commit bd00367: the
  feature becomes required=false, symbols go into the bundle).
- The bundle (103 MB) was uploaded by the owner by hand: browser tools cannot attach files over 10 MB. Next builds go
  through `Tools/PlayUpload/upload_play.py` once its service account is set up (see Release upload below).

Before Production: update the **Data safety** form (section above) for the leaderboards: User IDs (UGS player id),
App activity / other user-generated content (scores, player name), purpose App functionality, encrypted in transit.

## Release upload

- Build the signed AAB on the PC (version code above every earlier upload), then upload with
  `py -3.12 Tools/PlayUpload/upload_play.py Builds/<name>.aab` (default track: Closed testing - Alpha, rolled out;
  `--draft` to check it in Play Console first). Release notes come from `StoreListing/release_notes.txt`.
- One-time setup (Google Play Android Developer API, service account, JSON key in `D:\Projects\KeyPublish`, Play
  Console invite with "Release apps to testing tracks"): [Tools/PlayUpload/README.md](../../Tools/PlayUpload/README.md).
- The browser route only works for files under 10 MB with Claude's browser tools; a 100 MB bundle must be dragged in by
  the owner or sent through the script.
