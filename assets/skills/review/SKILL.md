---
name: review
description: Review the working diff (or a named change) for bugs, regressions, missing tests and unclear code, and report findings without editing files.
disable-model-invocation: false
user-invocable: true
---

# Review the current change

A general code review of a change. It reads and reports. It does not edit files, commit, push or run anything that changes the repository unless the user asks for a fix afterwards. For a dedicated pass over a whole branch with severity ranking use `/agent-review`; for vulnerabilities use `/review-security`.

## 1. Find the change

The text the user typed after the command may name a target. Resolve it in this order:

1. A file, folder, commit, range or branch named in the request: review exactly that.
2. Otherwise the working tree: staged and unstaged changes (`git diff HEAD`) plus untracked files that are not ignored (`git status --short`).
3. If the working tree is clean, review the latest commit (`git show HEAD`).

State which change you are reviewing (the command you used and the number of files and changed lines). If the diff is very large (more than about 1500 changed lines), say so, review the files with the most logic first, and list what you did not reach.

## 2. Read for context

For every changed file read enough of the surrounding code to understand what the change touches: the callers of a changed function, the tests next to it, the types it uses. Do not review a hunk in isolation. Read repository guidance that applies (`AGENTS.md`, rules) and judge the change against it.

## 3. What to look for

- **Correctness**: wrong conditions, off-by-one, null or empty inputs, error paths that are swallowed, resource leaks, concurrency and ordering problems, behaviour that differs from what the title, comments or tests claim.
- **Regressions**: callers, configuration, serialisation formats and public contracts that the change alters without updating the other side.
- **Tests**: new behaviour with no test, tests that cannot fail (they assert what the code does, not what it should do), tests that were weakened or deleted.
- **Maintainability**: duplicated logic, dead code left behind, names that mislead, comments that contradict the code, needless complexity.
- **Leftovers**: debug output, commented-out code, unfinished markers, files that do not belong in the change.

Skip style choices a formatter or linter owns, and personal taste. Do not report a problem you could not tie to a line.

## 4. Report

Write the result in this shape:

1. **Verdict** in one line: ready, ready with comments, or needs changes.
2. **Findings**, most serious first. For each: severity (`high`, `medium`, `low`), `path:line`, what is wrong, why it matters, and a concrete suggestion (a short patch sketch is fine, in a code block).
3. **Questions** for the author, if anything cannot be decided from the code.
4. **What you checked**: files read, commands run, and anything you could not verify (for example "did not run the tests").

If there are no findings, say so plainly and list what you checked. Do not invent findings to look thorough.

## Never

- Never modify, stage, commit or push files as part of the review.
- Never run commands with side effects (installs, migrations, deploys). Running the project's own tests or build is fine when asked or clearly safe.
- Never paste secrets you find in the diff into the report; name the file and line and say a secret is there.
