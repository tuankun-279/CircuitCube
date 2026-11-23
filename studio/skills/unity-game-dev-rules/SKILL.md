---
name: "unity-game-dev-rules"
description: "Use when building or changing a Unity mobile game for this user (any project): UI in prefabs, themes, TMP, atlases, perf budgets, tests, approval flow, git push and the remote device bridge."
---

# Unity game development rules

The user's standing rules for Unity mobile games, learned on Circuit Cube. Follow them in every Unity project unless the user says otherwise. Talk to the user in Vietnamese; write all code, comments, logs and commit messages in English (see the english-only-code skill).

## Working with the user

1. **Plans before code.** For a new feature or a system change, write the plan (a doc or a reply) and wait for approval. A direct order ("làm phần X") is approval for that part only.
2. **Demo before generated art.** Show a cheap mockup or procedural demo, get a pick, then generate the real art (ChatGPT, see chatgpt-art-pipeline).
3. **No APK build until the art for that stage is final**, unless the user asks.
4. **Verify, then report.** Play-test in the editor, take screenshots, and send them with the result. Say plainly what was not done.
5. **Commit and push** when a task is finished (the repo hook asks for it). Commit messages in English, with the session attribution lines.

## UI

- **Never build UI layout at runtime.** Screens, cards, buttons and lists are prefabs made in the editor. Editor scripts that build or restyle prefabs are fine; runtime code only fills text, toggles objects and listens to buttons.
- Instantiating a prefab item for a list is fine, but **reuse** items (Slot / HideRest pattern) instead of destroying and recreating them.
- **Text is TextMeshPro** (TMP_Text / TextMeshProUGUI, TMP_InputField) with a project SDF font asset made from the project font (dynamic, ASCII pre-filled). No legacy UnityEngine.UI.Text.
- **Colours come from the theme**, never hard-coded: a ThemeRole component on each Graphic (role such as Primary, Card, Text, TextMuted) and a GameTheme ScriptableObject per mode (dark, light). White sprites are tinted by the role.
- Buttons use **Color Tint** transitions (pressed ~0.82 grey, disabled alpha 0.45), not sprite swaps.
- Canvas reference 1080 x 1920, portrait, SafeArea root. Check the layout at 16:9, 20:9 and 4:3.
- HUD text: rebuild a string only when its number changes; cache number strings.

## Art and assets

- Folder layout under Assets/<Game>/Art, kept in one editor class (ArtPaths):
  - `Board/` or `World/{Dark,Light,Shared}` for game-board images
  - `UI/{Common,Icons,Shapes,Brand,Mascot,Backgrounds}`
  - `Fonts` (font plus TMP assets)
  - `Atlases` (generated)
  - `Materials`
- Unused old art goes out of Assets into `ArtRef/legacy_<n>` (it stays in git). Generated sheets and references stay in `ArtRef/`.
- **Move or rename assets only with AssetDatabase.MoveAsset** so GUIDs and references survive. Afterwards, scan prefabs for broken references.
- One AssetPostprocessor holds all import rules: UI sprites uncompressed (the atlas compresses them), 9-slice borders from the corner radius, board textures mipmapped and clamped, backgrounds 2048.
- **Atlases:** a Sprite Atlas V2 for UI sprites (not backgrounds). For instanced 3D boards, pack a per-theme texture atlas with extruded edges, and store the rects by image name on the theme so builds carry only the atlas. Provide a menu `Tools/<Game>/Build Atlases` and rerun it after any art change.

## Performance budgets (low-end Android, 2-3 GB RAM)

- **No GameObject per board cell.** Draw repeated cells with `Graphics.RenderMeshInstanced` and an instanced shader (per-instance colour and UV rect), one call per layer. Pick taps with ray-plane math, not colliders.
- Animations of repeated cells are data: AnimationCurves extracted from editor-authored clips into a ScriptableObject ("Build ... Look").
- **Targets:**
  - 60 fps
  - board at most 12 draw calls; a whole frame around 11-25 draw calls
  - **0 GC allocations per frame**
  - at most one small string per tap
- **Prove the targets:**
  - a PerfProbe (ProfilerRecorder: draw calls, batches, SetPass, GC) for in-play numbers
  - edit-mode tests with `Is.Not.AllocatingGCMemory()` and a draw call budget
  - in the editor, measure GC per call with `Recorder.Get("GC.Alloc")` filtered to the current thread; `GC.GetAllocatedBytesForCurrentThread` returns 0 on Mono.

## Unity gotchas

- A ScriptableObject or MonoBehaviour class must be in a file with the same name, or assets lose their script.
- Changing a serialized field's type (Text to TMP_Text, Material to string) drops existing references. Record them (owner path, component, property path, target path) before converting, and restore them after the recompile.
- `ThemeRole` has `[RequireComponent(Graphic)]`: remove it before destroying a Graphic, then add it back.
- URP materials: `mainTexture` maps to `_BaseMap`. Instanced custom shaders need `#pragma multi_compile_instancing` and properties in `UNITY_INSTANCING_BUFFER`.
- Linear colour space: convert colours with `.linear` before `SetVectorArray`.

## Remote device bridge (Unity on the user's Windows PC)

- Edit files in the container. Copy them to `/mnt/user-data/outputs/<dir>` and send them with device_commit_files (force: true).
- **Writes can land late or stale.** Check each file with `File.ReadAllText(...).Contains(marker)` in unity eval, and recommit if needed.
- Then run `AssetDatabase.ImportAsset(path, ForceUpdate)` (or Refresh with ForceUpdate). Wait for `EditorApplication.isCompiling == false`, check recompile_status for errors, and confirm the new type or member exists by reflection before running editor tools.
- Long editor jobs may time out the bridge (60 s). Do not blindly rerun; read the result state instead.
- Screenshots: game_view with save_path under Assets/Screenshots, then stage the files and view them. Move Assets/Screenshots to the trash when done.
- Run editor tests through reflection in eval when no test runner tool is available (handle Test, TestCase, Values and TestCaseSource).

## Git without credentials on the PC

1. On the device, commit with git through `System.Diagnostics.Process` in eval (message from a temp file).
2. Run `git bundle create x.bundle <last-pushed>..main` and stage the bundle into the container.
3. In the container clone: `git checkout -- . && git clean -fd`, `git fetch <bundle> main:refs/remotes/dev/main`, `git merge --ff-only`, then `git push`.
4. On the device, delete the bundle and run `git update-ref refs/remotes/origin/main <sha>`.