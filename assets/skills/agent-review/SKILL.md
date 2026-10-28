---
name: agent-review
description: On-demand local review of your changes against the base branch in Quick or Deep depth, with severity-ranked findings and file and line references.
disable-model-invocation: false
user-invocable: true
---

# Agent review (Quick or Deep)

A dedicated review of everything that differs from the base branch: committed work on this branch and uncommitted changes. It reads and reports. **It never edits, stages, commits or pushes files unless the user asks for a fix after seeing the report.**

The text the user typed after the command may say `quick` or `deep`, and may name a base branch or a focus (for example `deep security`). Default depth is Quick; if the diff is large or touches security-sensitive code, say so and recommend Deep, but do not switch without being asked.

| Depth | Use it for | How |
| --- | --- | --- |
| Quick | Small diffs, formatting changes, a fast sanity check | One pass over the whole diff |
| Deep | Complex logic, security-sensitive code, large refactors | One pass per file, then a cross-file pass |

## 1. Set up

1. Find the base: the branch named by the user, else the remote default branch, else `main`. Compute `git merge-base <base> HEAD`.
2. Collect the change: `git diff <merge-base>` (committed and uncommitted work together) plus untracked files that are not ignored. State the base, the number of files and changed lines, and the depth you are running.
3. Read the review rules the repository provides: `BUGBOT.md` files (the root one and ones in the folders that changed), `AGENTS.md`, and rules whose globs match the changed files. Apply them as extra criteria and cite them when a finding rests on one.
4. If there is no diff, say so and stop.

## 2. Quick

Read the whole diff once, then read the surrounding code of any function whose behaviour changed. Report only findings you can tie to a line and explain. Look for wrong logic, unhandled errors and edge cases, broken callers, missing or weakened tests, leftover debug code, and secrets. Keep the report short.

## 3. Deep

1. Order the changed files by risk (logic and security-sensitive first, then the rest, generated files last).
2. For each file, in turn: read the full file and its diff, then read its callers and its tests. Write down that file's findings before you move on, so a later file cannot crowd out an earlier one. Name any file you skip and why.
3. Then do a cross-file pass: contracts changed on one side only, renamed things still used under the old name, migrations without code, configuration without documentation, an interface and its implementations that disagree, behaviour the tests do not cover across the whole change.
4. Check the security checklist (input validation, path and command handling, authorisation, secrets, deserialisation) for every file that handles external input.
5. Re-check each finding against the code before you report it. Drop what you cannot support.

## 4. Report

1. **Summary**: depth, base, files reviewed, and the count of findings by severity, or "no findings".
2. **Findings**, ordered `critical`, `high`, `medium`, `low`, `nit`. Each has:
   - a short title and its severity, with one sentence on why that severity;
   - the location as `path:line` (a range when needed);
   - what is wrong and why it matters, with the evidence (the lines, the failing input, the call that breaks);
   - a **suggested fix**: a specific change, as a short patch in a code block when it fits.
3. **Not reviewed**: files or areas skipped and the reason. **Could not verify**: for example tests that were not run.

Do not pad the list. Each finding must be something the author can act on. If the change is sound, say that and list what you checked.

## 5. After the report

Offer to apply fixes. Apply only the ones the user names, one at a time, show the diff of each, and never touch files the review did not mention. Do not commit unless asked.

## Never

- Never change files, the index, branches or remotes during the review itself.
- Never run installs, migrations, deploys or other commands with side effects. Running the project's own tests or build is fine when it is clearly safe.
- Never print secrets found in the diff; give the location and the kind of secret only.
