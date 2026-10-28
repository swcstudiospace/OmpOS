---
name: migrate-to-skills
description: Convert custom slash commands (.cursor/commands and similar) and eligible rules into Agent Skills, keeping the originals until you confirm.
disable-model-invocation: false
user-invocable: true
---

# Migrate commands and rules to skills

Custom commands are single markdown files that run when you type their name. Skills are folders with a `SKILL.md`, can carry scripts and references, and can be picked by the agent when relevant. This skill converts commands (and, when asked, intelligent rules) into skills. It never deletes anything on its own.

## 1. Inventory

1. List the command files: `commands/*.md` in `.ompos`, `.omp`, `.cursor`, `.claude`, `.codex` and `.agents`, in the workspace root and (only if the user asks) in the home directory.
2. Read each one. Record its name (the file name without `.md`), its `description` and `argument-hint` frontmatter if present, and its body.
3. If the user asked to include rules, list `.mdc` rules that have a `description` and neither `alwaysApply: true` nor `globs`: these are the "agent decides" rules, and they map naturally to skills. Leave always-applied and file-scoped rules alone and say why.
4. Show the user a table: source file, proposed skill name, what will change. If there is nothing to convert, say so and stop.

## 2. Confirm

Ask which items to convert (default: all listed) and the destination folder (default `.ompos/skills`, or the folder the repository already uses for skills). Do not write anything before the user answers.

## 3. Convert each item

For each confirmed command `commands/<name>.md`, create `<destination>/<name>/SKILL.md`:

- Frontmatter: `name` equal to the folder, `description` taken from the command's description or, when it has none, a one-sentence summary you write (what it does and when to use it, at most about 160 characters), `disable-model-invocation: true` when the command was meant to be typed explicitly or does anything with side effects, otherwise `false`, and `user-invocable: true`.
- Body: the command's body, kept as is. Replace the argument placeholders (`$ARGUMENTS`, `$1`, and so on) with a sentence such as "The request is the text the user typed after the command", and keep the argument hint as a line near the top.
- If the destination already holds a skill of that name, skip the item and report it. Never overwrite.

For a confirmed rule, create the same kind of folder from its description and body, and add a note that the rule's `globs` and `alwaysApply` keys are not carried over.

## 4. Check

1. Re-read every new file: frontmatter closed with `---`, name equals the folder, body present.
2. Confirm the skill shows up where the slash menu looks (the destination must be one of the skills folders OmpOS reads).
3. Report what was created, what was skipped and why.

## 5. The originals

Leave the original command and rule files in place. A command and a skill with the same name both exist until one is removed, and the command is found first, so the new skill is not used until the command is gone. Offer to delete the originals **only after** the user has tried the skills, and delete a file only when the user names it explicitly. Suggest `git mv` or a git commit so the change is reviewable and reversible.

## Never

- Never delete or move an original file without an explicit yes for that file.
- Never convert files from the home directory unless the user asked for it.
- Never merge two commands into one skill.
