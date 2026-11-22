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
5. **Publish** the Play Games configuration changes (Review and publish), or sign-in fails for testers.

## Data safety form (whenever data use changes)

App content > Data safety. With the leaderboards the app also shares:
- **App activity > Other actions** (game results: taps, moves, time), collected, shared with Unity, for app
  functionality, not optional when signed in.
- **App info > Other user-generated content**: the player name.
- **Device or other IDs**: the UGS player id.
Keep it in step with `PRIVACY.md`.

## Release

Production / closed testing tracks as before; the version code goes up in Player Settings for each upload.
