---
name: "chatgpt-art-pipeline"
description: "Use when game art must be generated with ChatGPT (Codex CLI, or the ChatGPT desktop app) on the user's PC and cut into Unity sprites: demo first, prompt, generate, cut, import."
---

# ChatGPT art pipeline

All game art comes from ChatGPT image generation on the user's Windows PC: Codex CLI by default, the ChatGPT app through computer use as a fallback. Code only crops, cleans, places and splits the art; it does not paint new art, except clean geometry fixes the user asked for (for example redrawing wires from a measured profile).

## 1. Agree on the look first

- Make a cheap demo: a procedural image (numpy/PIL), or the current game screenshot composited over candidate backgrounds.
  - To composite game UI over a new background, capture the game twice, with the camera on black and on white. alpha = 1 - mean(white - black), then out = black + (1 - alpha) * bg.
- Show 2-4 options side by side and ask which one to generate (AskUserQuestion). Do not generate before the user picks.

## 2. Generate with Codex CLI (default)

Codex CLI on the PC is signed in with the user's ChatGPT account (no API key) and has image generation on. It needs
no computer use, so it works from a Remote Control session with no Allow prompts.

1. Check once: `codex --version`, `codex login status` ("Logged in using ChatGPT"), `codex features list`
   (image_generation true).
2. From the game folder in PowerShell, put the prompt **before** `-i` (`-i` takes several files and swallows a prompt
   after it: "No prompt provided via stdin"):

   ```
   codex exec --skip-git-repo-check "Use the imagegen skill with the built-in image_gen tool to generate one image. <prompt>. Do not edit any files; just generate the image and print its saved path." -i "ArtRef\ref_icons_style.png"
   ```
3. Prompt in English, as in 2b step 4: how to use each reference, size, no text, palette, empty areas. For icon
   sets ask for pure white shapes on flat black in a grid with wide gaps, so cutting is a luminance-to-alpha split.
4. Codex prints the saved path (`%USERPROFILE%\.codex\generated_images\<thread>\exec-<id>.png`); copy it to
   `ArtRef\raw<N>_<name>.png`. About 1-2 minutes per image.
5. The cutting scripts need Python with pillow, numpy and scipy on the PC (`pip install --user pillow numpy scipy`).

Worked example: Circuit Cube leaderboard icons (commit 3f36fc0, `ArtRef/tools/cut_leaderboard_icons.py`).

## 2b. Fallback: ChatGPT desktop app (computer use)

Use only when Codex cannot do the job (for example a feature only the app has). From a Remote Control session the user
must open that session and press Allow twice within 5 minutes (see the pc-first-workflow skill).

1. Load the computer-use skill. Resolve and request access for "ChatGPT" (the bundle id looks like `OpenAI.Codex_...!App`), with clipboardWrite.
2. Open the app and start a New chat (top left).
3. Attach references:
   - Open the + menu in the composer and choose "Add photos & files".
   - In the file dialog press Alt+N, type the quoted Windows paths, then Enter.
   - The reference files must exist on the PC: commit them first to `<project>/ArtRef/`.
4. Prompt in English:
   - Say exactly how to use each reference ("use the FIRST image only as a layout/colour guide").
   - Give the aspect (portrait 2:3 gives 1024x1536), and ask for no text, no UI and no logos.
   - Give the palette hex codes and the empty areas the game needs ("keep the centre calm").
5. Wait in batches: each computer_batch may wait at most 50 s in total. Take a screenshot to check progress.
6. Download:
   - Click the share icon on the image, then Download. Never toggle "Share a public link".
   - Alt+N, type the full path `<project>/ArtRef/raw<N>_<name>.png`, Enter, Esc.
7. Check that the file arrived (device_list_dir), then release the computer-use lock.

## 3. Cut and import

- Stage the raw file into the container. Keep cutting scripts in `ArtRef/tools/` (for example cut_v2.py, wires_v2.py). They write straight into the Unity art folders (`Art/Board/<theme>`, `Art/UI/Common|Icons|Shapes/<theme>|Brand|Mascot|Backgrounds`).
- Cutting rules that worked:
  - Remove specks with connected components.
  - Remove flat white backgrounds with a flood fill.
  - Split bright white cores into their own layer (luminance threshold) only when their geometry is clean. Otherwise rebuild the geometry from a measured cross-section profile with a distance field: arms run past the tile edge so seams stay continuous, and the core is centred.
  - White sprites, so the game tints them by theme role.
- Preview a contact sheet and a composed board before sending files to the PC.
- In Unity, rerun the import rules, then `Tools/<Game>/Build Atlases`. Play-test both themes and send screenshots.

## Raw files

- Keep `raw*.png` out of git (.gitignore). References and the final cut sprites are committed.