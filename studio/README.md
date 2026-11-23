# Studio kit

Circuit Cube is the model project for the user's games. This folder holds what new games reuse.

| Path | What |
|---|---|
| `skills/` | The user's Claude skills, one folder each with its `SKILL.md`. The account copy is the source; these copies are refreshed when a skill changes. |
| `templates/new-game-checklist.md` | First-day and release checklist for a new game. |
| `templates/claude-settings.local.json` | Local Claude permissions for a game folder on the PC (pre-allows the computer-use request). |
| `../docs/console-setup/` | Play Console, Unity Dashboard (UGS) and Firebase setup with templates. |
| `../docs/leaderboards/` | Leaderboard rules (weekly challenge, shape boards), reusable for other puzzle games. |

Skills here:
- `game-studio-workflow`: machines (all game changes only on the PC; Mac and phone for showing, testing and Xcode
  builds), art rule, which skills to load.
- `pc-first-workflow`: every game is created, run and changed on the Windows PC; how to reach it from the Mac,
  phone or claude.ai (Remote Control, computer use) and the permission traps.
- `unity-game-dev-rules`: Unity UI, themes, performance, tests, git from the PC.
- `chatgpt-art-pipeline`: generate art in ChatGPT on the PC, cut and import it.
- `english-only-code`: all code and commits in English.
- `circuit-cube-project`: this game's map; copy and rename it for a new game.

Not copied: Anthropic's built-in skills (docs, pdf, computer-use, browser skills, ...) and Unity's `unity:*` plugin
skills. They come with the account or the Unity plugin.

To install a skill from here on another account: claude.ai > Settings > Capabilities > Skills > upload the folder
(zip it first).
