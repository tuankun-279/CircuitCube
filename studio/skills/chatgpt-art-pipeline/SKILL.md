---
name: "chatgpt-art-pipeline"
description: "Use when game art must be generated with the ChatGPT desktop app on the user's PC and cut into Unity sprites: demo first, prompt, download, cut, import."
---

# ChatGPT art pipeline

All game art comes from ChatGPT image generation, driven through computer use on the user's Windows PC. Code only crops, cleans, places and splits the art; it does not paint new art, except clean geometry fixes the user asked for (for example redrawing wires from a measured profile).

## 1. Agree on the look first

- Make a cheap demo: a procedural image (numpy/PIL), or the current game screenshot composited over candidate backgrounds.
  - To composite game UI over a new background, capture the game twice, with the camera on black and on white. alpha = 1 - mean(white - black), then out = black + (1 - alpha) * bg.
- Show 2-4 options side by side and ask which one to generate (AskUserQuestion). Do not generate before the user picks.

## 2. Generate in ChatGPT (computer use)

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