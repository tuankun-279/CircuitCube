---
name: "game-studio-workflow"
description: "Use at the start of any game task for this user (any project): which machine does what (all game changes only with the Windows PC connected), how art is made, and which skills and templates to load."
---

# Game studio workflow

The user's standing rules for every game project, set 2026-11-22 on Circuit Cube. Circuit Cube
(repo tuankun-279/CircuitCube) is the model project: copy its `studio/` folder and `docs/console-setup/` into new games.

## Machines

| Machine | Used for | Never used for |
|---|---|---|
| **Windows PC** (Unity, the project folder, Opera) | every change to the game: code, scenes, prefabs, art import, tests, Android builds, git commits from the device | |
| **Mac** (Chrome) | showing the product, testing, **Xcode builds for the App Store**, web consoles (Play Console, Unity Dashboard, Firebase) when the PC is busy | editing the game |
| **Phone** | showing the product, playtests, store-build checks | editing the game |

Rules:
1. A task that changes the game starts only when the PC is connected (folder access or Remote Control on the PC).
   If only the Mac or a phone is connected, do the parts that need no game change (plans, docs, mockups, console
   setup in a browser, research) and say the rest waits for the PC. How remote devices reach the PC and how to get
   computer-use approvals: **pc-remote-workflow**.
2. Cloud work (the repo clone in the container) is fine for code and docs, but compiling, tests, screenshots and
   builds are confirmed on the PC before a task is called done.
3. iOS: the Mac pulls the pushed branch, opens the Unity-exported Xcode project and archives it. No game edits on
   the Mac; a fix found there goes back to the PC.

## Art

- All game art is generated with ChatGPT on the PC and cut with scripts: follow the **chatgpt-art-pipeline** skill.
- Start from the approved mockup (a Design canvas or a screenshot comp). Generate pieces at the size and layout the
  mockup shows, cut them so each sprite matches its slot in the mockup, then place them in the prefab exactly as
  drawn. Compare a screenshot of the built screen with the mockup before reporting.

## Skills to load

| Skill | When |
|---|---|
| game-studio-workflow (this) | start of every game task |
| pc-remote-workflow | every create/run/edit task, especially when driven from the Mac or the phone: the PC does the work, approvals for computer use |
| unity-game-dev-rules | any Unity change: UI in prefabs, themes, TMP, perf, tests, git from the PC |
| english-only-code | any code, comment, log or commit |
| chatgpt-art-pipeline | any new or changed art |
| <game>-project (e.g. circuit-cube-project) | the game's own map: folders, menus, data |
| unity:* plugin skills | the matching Unity topic, e.g. unity:build-live-game (UGS: sign-in, leaderboards, Cloud Code), unity:ui-ugui (Canvas UI), unity:unity-cli (editor from the command line) |
| computer-use, chrome-browser, built-in-browser | driving the PC apps or a browser for console setup |

## Templates in the model project

- `studio/templates/new-game-checklist.md`: first-day setup of a new game repo.
- `docs/console-setup/`: Play Console, Unity Dashboard (UGS), Firebase steps and per-game value table.
- `studio/skills/`: copies of the user's skills, kept in sync with the account (the account copy is the source).
