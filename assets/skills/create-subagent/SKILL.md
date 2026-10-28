---
name: create-subagent
description: Create a custom subagent definition (.ompos/agents/<name>.md) with a focused role and delegation instructions.
disable-model-invocation: false
user-invocable: true
---

# Create a subagent

A subagent is a specialist the main agent hands a task to: it has its own instructions, may use another model, and returns a short result. This skill writes the definition file. The text the user typed after the command says what the subagent is for.

## 1. Clarify

Ask in one message only for what you cannot infer:

- **The job**, in one sentence, and what a finished result looks like (a list of findings, a patch, a yes or no).
- **Read-only or not.** A reviewer, researcher or verifier should be read-only (`readonly: true`). Only a subagent that must change files gets write access.
- **Background or foreground.** Set `is_background: true` only for a long job the user does not wait for.
- **Model.** Default `inherit` (use the conversation's model). Set a specific model only when the user names one.
- **Where.** Project (`.ompos/agents/<name>.md`, shared with the team) or user (`~/.ompos/agents/<name>.md`). Default to the project. If the repository already keeps agents in `.cursor/agents`, `.omp/agents`, `.claude/agents`, `.codex/agents` or `.agents/agents`, use that folder.

## 2. Name it

The name is typed after a slash (`/verifier <task>`), so use lowercase letters, digits and hyphens, starting with a letter or digit. Pick a role noun (`verifier`, `test-writer`, `docs-checker`). Check that no subagent of that name exists in any agents folder; if one does, ask before replacing it.

## 3. Write the definition

```markdown
---
name: verifier
description: Checks that a finished change really works. Use after an implementation, before reporting done.
model: inherit
readonly: true
is_background: false
---

You are a verifier. ...
```

Frontmatter:

| Key | Meaning |
| --- | --- |
| `name` | The slash name. Defaults to the file name. |
| `description` | When the main agent should delegate to this subagent. This is what the main agent reads to decide, so state the trigger and the job. |
| `model` | `inherit` or a model id. |
| `readonly` | `true` forbids file changes. |
| `is_background` | `true` runs it without waiting. |

Body (the subagent's own instructions, written to the subagent):

- Start with its role in one sentence.
- Give a numbered procedure: what to read, what to check, what to run.
- Define the output format exactly (for example "a list of findings, each with file and line, ordered by severity") so the main agent can use the result without reading a long transcript.
- Say what it must not do (change files when read-only, call the network, guess when evidence is missing) and tell it to report what it could not verify.
- Keep it focused on one job. A subagent that does several unrelated jobs should be split.

## 4. Report

Show the path, the slash command and an example call (`/<name> check the login change`). Subagent definitions are read when a session starts and when the slash menu opens.

## Never

- Never give a subagent write access when its job is to review or research.
- Never put secrets or tokens in a definition file.
- Never overwrite an existing definition without showing the change and getting a yes.
