---
name: create-rule
description: Create a project or user rule (a .mdc rule file or an AGENTS.md section) with the right scope and short, checkable instructions.
disable-model-invocation: false
user-invocable: true
---

# Create a rule

A rule is standing guidance the agent reads before it works: conventions, commands to run, things to avoid. This skill writes it in the right place with the right scope. The text the user typed after the command says what the rule should say.

## 1. Decide what kind of rule it is

If the request is vague, ask one short question; otherwise decide and say what you chose.

| The guidance is... | Use |
| --- | --- |
| Short, applies to the whole repository, plain prose | A section in the root `AGENTS.md` (create the file when there is none) |
| Applies to one folder or one area of code | A nested `AGENTS.md` in that folder |
| Applies only to some files (by glob) | A `.mdc` rule with `globs` |
| Applies always, but is long or structured | A `.mdc` rule with `alwaysApply: true` |
| Should be attached only when the agent decides it is relevant | A `.mdc` rule with a clear `description` and no globs |
| Personal and the same in every project | A user rule: `~/.ompos/rules/<name>.mdc` |

Default location for a project rule is `.ompos/rules/<name>.mdc` in the workspace root. OmpOS also reads `.cursor/rules`, `.omp/rules`, `.claude/rules`, `.codex/rules` and `.agents/rules`: if the repository already keeps its rules in one of those folders, put the new rule there instead so the team has one place to look. Rule files may sit in sub-folders up to three levels deep.

## 2. Check what exists

1. List the existing rules and instruction files (`AGENTS.md`, `CLAUDE.md`, `.cursorrules`, the `rules` folders). Read the ones that touch the same topic.
2. If a rule already says the same thing, tell the user and offer to edit that one instead of adding a second.
3. If a new rule would contradict an existing one, show both and ask which wins.

## 3. Write the rule

A `.mdc` file starts with frontmatter, then the body:

```markdown
---
description: One sentence saying when this rule applies.
globs: ["src/**/*.cs"]
alwaysApply: false
---

- Instruction one, written as something that can be checked.
- Instruction two.
```

- `description` is required for every rule that is not `alwaysApply`; the agent uses it to decide relevance.
- `globs` is a list of patterns. Leave it out for rules that are not file scoped.
- `alwaysApply: true` costs context on every turn. Use it only for guidance every task needs.
- Name the file in kebab-case after the topic (`csharp-naming.mdc`, not `rule1.mdc`).

Writing guidance for the body:

- Keep it under about 50 lines. If it needs more, split it by topic.
- Write instructions as specific, verifiable statements ("Run `dotnet format` before committing") instead of general advice ("write clean code").
- Give the reason when it is not obvious, in a few words.
- Show a short example only when the convention cannot be said in a sentence.
- Reference files by path instead of copying their content.
- Do not put secrets, tokens, internal URLs or personal data in a rule file: rule files are committed.

## 4. Report

Show the user the file path, the scope you chose (always, intelligent, file scoped, manual) and the final text. Rules are read when a session starts, so say that an already running conversation may need to be restarted to pick the rule up.

## Never

- Never overwrite an existing rule file without showing the difference and getting a yes.
- Never edit a rule file outside the workspace or the user's `~/.ompos/rules` folder.
