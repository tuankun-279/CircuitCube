# New game checklist (from the Circuit Cube model project)

Copy this file into the new repo and tick it off. Machine rules: `studio/skills/game-studio-workflow/SKILL.md` and `studio/skills/pc-first-workflow/SKILL.md`.

## Day 1 (on the PC)

- [ ] Create the Unity project on the PC under `D:\Projects\Own_Project\<Game>` (same Unity version as the model unless there is a reason).
- [ ] Claude app on the PC: add the folder in Settings > Code > Folders; Remote Control switches and computer use ON.
- [ ] Copy `studio/templates/claude-settings.local.json` to `<Game>\.claude\settings.local.json` (not committed).
- [ ] Create the GitHub repo; copy `studio/` and `docs/console-setup/` from CircuitCube.
- [ ] Copy `.gitignore` (keeps `ArtRef/raw*.png`, Library, builds out of git).
- [ ] Add `CLAUDE.md` pointing at the skills: game-studio-workflow, pc-first-workflow, unity-game-dev-rules,
      english-only-code, chatgpt-art-pipeline, and a new `<game>-project` skill (start from `studio/skills/circuit-cube-project`).
- [ ] Folder layout from unity-game-dev-rules (Art/, Prefabs/UI, Scripts/Core and Game, Tests/Editor).
- [ ] UI canvas 1080x1920, SafeArea root, GameTheme dark/light, TMP font asset.

## Look

- [ ] Mockups on a Design canvas first; Tuan approves.
- [ ] Art from ChatGPT on the PC (chatgpt-art-pipeline), cut to match the mockup, imported with the postprocessor.

## Services (fill the value table in `docs/console-setup/README.md`)

- [ ] Play Console app + Play Games Services project (credentials: Android, plus Game server if UGS sign-in).
- [ ] Firebase project (Analytics, Crashlytics), `google-services.json`.
- [ ] Unity Gaming Services project if the game needs a backend (Authentication, Leaderboards, Cloud Code,
      Access Control deny-write on Leaderboards). Templates in `docs/console-setup/templates/`.
- [ ] Privacy policy (`PRIVACY.md`, English + Vietnamese) and the Data safety form.

## Release

- [ ] Android: build on the PC, upload to Play Console internal testing.
- [ ] iOS: on the Mac, pull the branch, open the Xcode project exported from Unity, archive, upload to App Store
      Connect. No game edits on the Mac.
- [ ] Test on the phone; screenshots for the store from the phone or the PC.
