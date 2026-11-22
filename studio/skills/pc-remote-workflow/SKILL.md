---
name: "pc-remote-workflow"
description: "Use for any game project task for this user that creates, runs or edits a game, including when the user drives it from the Mac or a phone: the Windows PC does all the work, the other devices only control it remotely. Covers how sessions reach the PC, approvals for computer use, and what to do when the PC is not connected."
---

# PC remote workflow

Standing rule set by the user on 2026-11-22: every game project, now and later, is **created, run and edited on
the Windows PC**. The Mac and the phone are only remote controls and viewers. A request typed on the Mac or the phone
still means "do it on the PC". Machine roles in detail: **game-studio-workflow**.

## What happens on the PC

| Step | On the PC |
|---|---|
| Create | Unity project (Unity Hub / `unity` CLI), git repo and first push, `studio/` + `docs/console-setup/` copied from CircuitCube, `CLAUDE.md` (see `studio/templates/new-game-checklist.md`). |
| Run | Unity Editor, play mode, tests, Android builds, device install over USB/ADB, screenshots. |
| Edit | Code, scenes, prefabs, art (ChatGPT desktop app via computer use, cut scripts, import), atlases, git commit and push. |

Game folders live on the PC, for example `D:\Projects\Own_Project\<Game>`. Never create or edit a game in the cloud
container, on the Mac or on the phone and "sync it later". The cloud clone may only draft code or docs that are then
pulled and verified on the PC.

Only exception: iOS archives. The Mac pulls the pushed branch and archives the Unity-exported Xcode project, with no
game edits. Any fix goes back to the PC.

## How the Mac or the phone reaches the PC

1. **Project thread (claude.ai, any device):** the coordinator starts a Claude Code session on the PC over Remote
   Control, in the game folder. That session does the work and reports back to the thread.
2. **Remote Control page:** every PC session has a `https://claude.ai/code/session_...` link (written in
   `%LOCALAPPDATA%\Claude\logs\main.log` as "Remote control enabled"). Open it on the Mac or the phone to watch the
   session and answer its prompts.
3. **Sitting at the PC:** Claude app > Code tab > the session in the game folder.

On the first message in a session, check that you are on the PC (Windows, the `D:\Projects\...` folder, Unity
reachable). If you are not (only the Mac or the phone, or a cloud container), do only work that changes no game:
plans, docs, mockups, console setup in a browser. Say plainly that the rest waits for the PC.

## Approvals (lesson from 2026-11-22)

Computer use from a PC session needs **two** approvals, and both appear only in that PC session (Code tab on the PC,
or its Remote Control page). Neither appears in the project thread.
1. Claude Code's tool permission for `computer:request_access`. It appears when the session's permission mode is
   "default".
2. The computer-use dialog for the app (for example ChatGPT, with clipboard write).

An unanswered prompt is aborted after about 5 minutes, and the tool returns `user_denied`. Each request is logged in
main.log as "Emitted tool permission request ... for computer:request_access" and, when it times out, "... aborted".
`user_denied` does **not** mean the user said no.

Rules:
- Before asking for access, report to the thread first. Give the session link and tell the user to approve within
  5 minutes on the PC or on the session page. Then call `request_access`.
- If a request still comes back `user_denied`, do not retry blindly. Check main.log for "aborted", report it, and
  wait for the user to say they have the session open.
- To skip approval 1 on a game repo, the user may add `"mcp__computer-use__request_access"` to `permissions.allow` in
  `<game>/.claude/settings.local.json`. Only the user can make that change: Claude never edits permission settings
  on its own. Approval 2 always needs the user's click.
- Long ChatGPT waits: computer_batch waits at most 50 s each, with a screenshot between batches (see
  **chatgpt-art-pipeline**).

## Finishing a task

- Verify on the PC: Unity console clean, tests or play mode as the task needs, screenshots from the PC.
- Commit from the PC with an English message (english-only-code), `git pull`, then push the agreed branch.
- Report in the thread: what changed, commit hash, paths of previews, and anything that failed or waits for the user.
