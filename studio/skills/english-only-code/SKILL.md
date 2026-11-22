---
name: "english-only-code"
description: "Use whenever writing, editing or reviewing code in any project: all code, identifiers, comments, logs, commit messages and in-code text must be in English, and existing non-English code gets translated."
---

# English-only code

Every project follows one rule: **everything inside the codebase is written in English**, whatever language the conversation with the user is in. Talk to the user in their language; write the code in English.

## What must be English

- Identifiers: variables, functions, classes, files, folders, assets, scene/prefab names, database keys, JSON fields.
- Comments, doc comments, TODO/FIXME notes, region labels.
- Log, debug, warning, error and exception messages.
- UI strings hard-coded in source (labels, buttons, tooltips, placeholders, alerts) in tools, editors, debug menus and prototypes.
- Editor menu paths, inspector headers and tooltips.
- READMEs, inline docs, config comments, commit messages and PR descriptions.

## Exceptions (leave as is)

- Player-facing localized text that lives in a proper localization system (string tables, .po/.json locale files). Add an English key and source string; translations go in the locale files.
- Data that is genuinely content: user-generated text, test fixtures that check Unicode handling, names of real people or places.
- Third-party / vendored code and generated files.

## When writing new code

1. Write identifiers, comments and messages in English from the start; never write them in another language with the plan to translate later.
2. Keep comments short and about *why*, in plain English.
3. If the user dictates a label or message in another language for a tool UI, write the English version in code and mention it in the reply (offer localization if the UI really must be in that language).

## When touching an existing project

Run this pass before or alongside the requested change, and always when the user asks to convert a project.

1. **Scan** for non-English text. A quick catch-all is any non-ASCII letter in source files, excluding vendored and generated folders:
   ```bash
   grep -rnP '[^\x00-\x7F]' --include='*.{cs,js,ts,tsx,py,html,css,json,md,shader,uss,uxml}' . \
     | grep -vE '/(node_modules|Library|Packages|vendor|dist|build)/'
   ```
   Then filter out harmless symbols (·, –, —, ✓, ★, arrows, emoji used as icons). For languages without diacritics, also read identifiers and comments for words that are not English.
2. **Translate** each hit in place:
   - Comments and messages: natural, concise English (not word-for-word).
   - Identifiers: rename with the language's refactor tools (or a careful project-wide replace) so every reference, serialized field and asset reference is updated. In Unity, keep serialized data with `[FormerlySerializedAs("oldName")]` when renaming fields.
   - Preserve placeholders, format specifiers and markup exactly: `{0}`, `${x}`, `%s`, `<b>`, `\n`, HTML entities.
   - Keep one glossary for domain terms so the same word is always translated the same way.
3. **Verify**:
   - Re-run the scan; the only remaining hits should be allowed exceptions.
   - Compile / type-check / lint (`node --check`, `tsc --noEmit`, `python -m py_compile`, Unity recompile, etc.).
   - Run the tests and open the UI once to check that renamed strings still fit their layout.
4. **Report** to the user, in their language: how many files and strings were translated, any renamed public APIs or serialized fields, and anything left untranslated on purpose.

## Quick checklist before finishing any coding task

- [ ] No non-English identifiers, comments, logs or hard-coded strings in files I created or changed.
- [ ] Placeholders and markup unchanged in translated strings.
- [ ] Project still builds and tests pass.