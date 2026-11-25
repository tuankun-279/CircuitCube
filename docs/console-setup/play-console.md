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

App content > Data safety. Required for closed, open and production tracks; only apps that are on internal testing
alone are exempt. Keep it in step with `PRIVACY.md`.

### Data safety answers (1.4.1)

Drafted from the packages and code of 1.4.1; the owner reviews and enters them. Play's terms: *collected* = leaves the
device, SDKs included; *shared* = goes to a third party that is not a service provider acting for the developer.
Unity Gaming Services, Firebase and Play Games are treated as service providers here (the owner should confirm that
reading); AdMob says itself that it collects **and shares**. Overall: data is collected; encrypted in transit: yes
(AdMob TLS, Firebase HTTPS, the rest over HTTPS); a way to request deletion: yes (`PRIVACY.md`, GitHub issue).

| Data type | Source | Collected | Shared | Required or optional | Purposes |
|---|---|---|---|---|---|
| Personal info > User IDs | UGS player id, Play Games account | yes | no | required (anonymous id without Play Games) | App functionality, Analytics (daily player count) |
| Location > Approximate location | IP address: AdMob, Firebase Analytics | yes | yes (AdMob) | required | Advertising, Analytics, Fraud prevention |
| App activity > App interactions | AdMob, Firebase Analytics, daily check-in | yes | yes (AdMob) | required | Advertising, Analytics, Fraud prevention |
| App activity > Other user-generated content | player name on the boards | yes | no | optional (a name is generated) | App functionality |
| App activity > Other actions | taps, moves, time, scores; saved progress | yes | no | required | App functionality |
| App info and performance > Crash logs | Crashlytics | yes | no | required | Analytics |
| App info and performance > Diagnostics | Crashlytics, AdMob | yes | yes (AdMob) | required | Analytics, Advertising, Fraud prevention |
| Device or other IDs | AdMob (advertising id, app set id), Firebase instance and installation ids | yes | yes (AdMob) | required | Advertising, Analytics, Fraud prevention |

- Ads are off in the content manifest (`adsOn` 0), but the AdMob SDK still starts and loads ads when consent allows, and
  the manifest has the AD_ID permission, so its data is declared.
- Names, ranks and scores are visible to other players on the boards. Play's page does not say how public display
  counts; it is declared as collected, not shared.
- Daily check-in: no arguments are sent; the server stores one entry per player per day on `daily_players` and keeps past days.
- Not in the build's data: in-app purchases (none), Unity Analytics, Unity Ads and Cloud Diagnostics (all off).
- Not checked: Google's own data page for Analytics (support.google.com/analytics/answer/11582702) and Unity's for UGS.

## Release

The version code goes up in Player Settings for every upload (Play refuses a code it has seen, even from a draft).
1.4.1 (code 8) is prepared; the next one is 1.4.2 (code 9).

### State

| Date | Track | Release | Notes |
|---|---|---|---|
| 2026-11-17 | Closed testing - Alpha | 5 (1.2.0) | |
| 2026-11-23 | Closed testing - Alpha | **7 (1.4.0)**, rolled out about 10:40 (UTC+7), replaces 5 (1.2.0) | Notes from `StoreListing/release_notes.txt` (en-US, vi-VN). Built with an **empty Play Games web client id** (see below). |

Known fault in 7 (1.4.0): the bundle has an empty Play Games web client id (checked inside the AAB and APK). From the
plugin code, a player signed in to Play Games then never signs in to Unity Gaming Services: no leaderboards, no scores,
no daily count; not tried on a phone. For 1.4.1 the id is also in `ProjectSettings/GooglePlayGameSettings.txt`, which the
plugin refills the asset from, and `PlayGamesBuildCheck` stops a build without it. What blanked the asset is not known.
Test 1.4.1 on a phone signed in to Play Games before rolling it out.

Checks Play showed for 7 (1.4.0):
- No deobfuscation file: the game does not use R8/ProGuard. Ignore.
- Native code without debug symbols, and `android.hardware.screen.portrait` declared as required (keeps the game off
  Chromebooks, Play Games on PC and XR). Ignored for this test with "I already have plans to resolve this issue";
  both should be fixed by `Assets/CircuitCube/Editor/AndroidReleaseFixes.cs` (commit bd00367: the feature becomes
  required=false, symbols go into the bundle), not yet confirmed in a real build: check them on 1.4.1.
- The bundle (103 MB) was uploaded by the owner by hand: browser tools cannot attach files over 10 MB. Next builds go
  through `Tools/PlayUpload/upload_play.py` once its service account is set up (see Release upload below).

Update the **Data safety** form (answers above) with 1.4.1, not only before Production: closed testing needs it too.

## Release upload

- Build the signed AAB on the PC (version code above every earlier upload), then upload with
  `py -3.12 Tools/PlayUpload/upload_play.py Builds/<name>.aab` (default track: Closed testing - Alpha, rolled out;
  `--draft` to check it in Play Console first). Release notes come from `StoreListing/release_notes.txt`.
- One-time setup (Google Play Android Developer API, service account, JSON key in `D:\Projects\KeyPublish`, Play
  Console invite with "Release apps to testing tracks"): [Tools/PlayUpload/README.md](../../Tools/PlayUpload/README.md).
- The browser route only works for files under 10 MB with Claude's browser tools; a 100 MB bundle must be dragged in by
  the owner or sent through the script.
