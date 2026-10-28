---
name: create-skill
description: Create an Agent Skill folder with a SKILL.md that has a precise description and clear steps.
disable-model-invocation: false
user-invocable: true
---

# Create a skill

A skill is a folder whose `SKILL.md` teaches the agent a repeatable task. The agent sees only the name and description until it decides the skill is relevant (or the user types the skill name after a slash), then it reads the whole file. This skill creates one. The text the user typed after the command says what the skill is for.

## 1. Clarify

Ask in one message only for what you cannot infer:

- The task the skill performs and when it should be used.
- Whether it is for this project (`.autonogrammer/skills/<name>/`) or for the user in every project (`~/.autonogrammer/skills/<name>/`). Default to the project.
- Whether the model may choose it on its own (default) or only when the user types it (set `disable-model-invocation: true`). Anything that changes files outside the workspace, deploys, deletes, or sends messages should be manual only.

Autonogrammer also reads skills from `.omp`, `.cursor`, `.claude`, `.codex` and `.agents` folders. If the repository already keeps its skills in one of them, use that folder so the team has one place.

## 2. Name it

The folder name is the skill name and the slash command. Use lowercase letters, digits and hyphens, start with a letter or digit, and keep it short (`release-notes`, `db-migrate`). Check that no skill of that name exists in any skills folder; if one does, say so and ask what to do. A project skill hides a built-in skill of the same name.

## 3. Write SKILL.md

```markdown
---
name: release-notes
description: Draft release notes from the commits since the last tag. Use when asked to prepare or summarise a release.
disable-model-invocation: false
user-invocable: true
---

# Release notes

## Steps
1. ...
```

Frontmatter keys:

| Key | Meaning |
| --- | --- |
| `name` | Must equal the folder name. |
| `description` | One or two sentences, at most about 160 characters, saying **what it does and when to use it**. This is the only text the agent sees when choosing, so name the trigger words a user would use. Avoid a colon followed by a space in a plain description, or put the text in quotes. |
| `paths` | Optional list of globs that scope the skill to some files. |
| `disable-model-invocation` | `true` hides it from automatic selection; the user can still type it. |
| `user-invocable` | `false` hides it from the slash menu (a skill only the agent uses). |
| `icon`, `color` | Optional presentation. |

Body guidance:

- Number the steps. Each step is one action the agent can do and check.
- Say what to read first, what to produce, and what "done" looks like.
- State the limits: what the skill must never do and when it must stop and ask.
- Keep `SKILL.md` focused (under about 150 lines). Move long reference material into `references/` files and templates into `assets/`, and link to them by relative path so they load only when needed. Put runnable helpers in `scripts/`; make each script self-contained, with clear error messages.
- Write the steps in the imperative, to the agent. Do not copy text from other products.

## 4. Verify and report

1. Re-read the file: the frontmatter closes with `---`, the name matches the folder, and every referenced file exists.
2. Tell the user the path, the exact slash command (`/<name>`), and how to try it. Skills are discovered when a session starts and when the slash menu opens, so a new skill may need the menu reopened.

## Never

- Never put secrets or tokens in a skill; skills are committed and shared.
- Never write a skill that runs a destructive command (delete, force-push, drop a table, overwrite a file) without a step that asks the user to confirm first.
- Never overwrite an existing skill folder without showing what will change and getting a yes.
