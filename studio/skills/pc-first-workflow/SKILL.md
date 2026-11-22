---
name: "pc-first-workflow"
description: "Use for any game project task for this user: every game is created, run and changed on the Windows PC, even when the user drives it from the Mac, a phone or claude.ai. How to reach the PC, and the permission traps."
---

# PC-first workflow

The user's rule (2026-11-22): every game project is **created, run and changed on the Windows PC**. The Mac, the phone
and claude.ai are only remote controls and screens for showing and testing. The Mac may also archive Xcode builds for
the App Store from a pushed branch. Nothing else about a game is changed anywhere but the PC.

## The PC

| Item | Value (Circuit Cube) |
|---|---|
| Machine | DESKTOP-JV1R213 (Windows) |
| Projects root | `D:\Projects\Own_Project\` (Circuit Cube: `D:\Projects\Own_Project\Puzzle_Robot`) |
| Unity | `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe` |
| Browser | Opera |
| Claude app | Claude desktop, Settings > Code > Remote Control: "Connect new sessions" and "Let your phone and claude.ai start sessions here" ON; Settings > Desktop app: "Enable computer use" ON |

For a new game, add its folder in Settings > Code > Folders on the PC so it can be picked from other devices.

## Which route to use

Decide before the first step. Check what is connected (`list_devices`, `get_device_info`).

| Work | Route | Notes |
|---|---|---|
| Edit code, scenes, prefabs, assets; git commit and push from the device | **Remote Control session on the PC** (`start_remote_control`, folder = the game folder) | Preferred for anything that runs: Unity, tests, builds, scripts, git |
| Read or copy a few files | Folder access on the PC | Only when the thread is linked to the PC, not the Mac |
| Generate art (ChatGPT images) | **Codex CLI** from the Remote Control session on the PC (`codex exec ... -i ref.png`) | No computer use, no Allow prompts; see chatgpt-art-pipeline |
| Drive a PC app (ChatGPT app as a fallback, Unity window screenshots) | Computer use **from the Remote Control session on the PC** | See "Permission traps" |
| Plans, docs, mockups, research, code drafts | Cloud clone of the repo | Never called done until compiled and tested on the PC |
| Web consoles (Play Console, Unity Dashboard, Firebase) | Any browser that is connected | Not a game change |
| Xcode archive for the App Store | Mac, from the pushed branch | No game edits on the Mac; fixes go back to the PC |

If the thread's device link points at the Mac (or nothing), do not edit the game from there. Start a Remote Control
session on the PC instead; if the PC is offline ("Not connected" in the device list), ask the user to open the Claude
desktop app on the PC and check the Remote Control switches above. Do the parts that need no game change meanwhile.

## Creating a new game

1. On the PC: create the Unity project under `D:\Projects\Own_Project\<Game>` (Unity Hub or unity:unity-cli), init git,
   create the GitHub repo, push.
2. Copy from Circuit Cube: `studio/`, `docs/console-setup/`, `.gitignore` rules (raw art, Library, Temp),
   `studio/templates/claude-settings.local.json` to `<Game>\.claude\settings.local.json`.
3. Add the folder in the PC Claude app (Settings > Code > Folders) and to the Claude project's repositories.
4. Follow `studio/templates/new-game-checklist.md`.

## Permission traps (learned 2026-11-22)

- A Remote Control session on the PC runs in the **default** permission mode. Tool prompts it raises (for example
  `computer: request_access` before the ChatGPT app dialog) show **only** in that session: the Code tab of the Claude
  app on the PC, or the session's `claude.ai/code/session_...` page. They never show in the project chat.
- An unanswered prompt aborts after about 5 minutes and the tool returns `user_denied`. It is not a real refusal.
  Read `%LOCALAPPDATA%\Claude\logs\main.log` ("Emitted tool permission request ... aborted") to confirm.
- Before a step that needs computer use, tell the user: "Open the running session (PC Code tab, or the session link
  on the phone) and press Allow twice: first for computer: request_access, then for the app." Give the session link.
- Pre-allow the computer-use request tool in the game's `.claude\settings.local.json` (template in
  `studio/templates/claude-settings.local.json`) so only the app dialog remains. It is local, not committed.
- Report to the thread **before** calling `request_access` (the call blocks until answered or aborted), so the user
  knows to open the session within 5 minutes.
- If it still returns `user_denied`, do not retry blindly: check main.log for "aborted", report, and wait until the
  user says the session is open. A Claude app restart mid-request interrupts the call; just request again.
- Permission settings (`settings.local.json`, permission mode) change only when the user asks, for example the
  new-game checklist step; never as a workaround mid-task. The app dialog always needs the user's click.
- The cloud thread's own computer-use tools act on whichever device `connect_device` linked (often the Mac). Do not
  drive PC apps through them unless `get_device_info` shows the PC.

## Before calling a task done

- Compiled and edit-mode tests passed in Unity on the PC.
- Screenshots of the changed screens taken on the PC (both themes when UI changed).
- Committed and pushed from the PC (or pulled there after a cloud push, with Unity reimported).
