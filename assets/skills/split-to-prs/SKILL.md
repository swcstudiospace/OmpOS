---
name: split-to-prs
description: Split a large working change into a stack of small reviewable branches, one concern each, after you approve the plan.
disable-model-invocation: false
user-invocable: true
---

# Split a large change into reviewable pull requests

Takes one large set of changes and proposes a series of small branches, each with a single purpose that builds on the one before. It plans first, shows the plan, and changes nothing in git until the user approves it. The text the user typed after the command may give a base branch or a preferred size.

## 1. Understand the change

1. Run `git status --short` and `git branch --show-current`. Note whether the changes are uncommitted, committed on the current branch, or both.
2. Find the base: the branch the work should merge into (the text the user typed, else the remote default branch, else `main`). Run `git merge-base <base> HEAD`.
3. Read the full diff against the base (`git diff <merge-base>`, plus untracked files). Group the files and hunks by purpose, not by folder: refactor with no behaviour change, new interfaces, the feature itself, tests, documentation, configuration, generated files.
4. If the working tree has uncommitted changes, the plan has to cover them too. If there are changes the user probably did not mean to ship (debug code, personal files), list them separately and leave them out.

## 2. Propose the plan

Produce a numbered list of branches. Aim for each to be reviewable in one sitting (roughly 400 changed lines or fewer; fewer when the code is dense) and to compile and pass tests on its own. For each branch give:

- A branch name (`<current-branch>-1-extract-parser`, `<current-branch>-2-add-cache`, and so on) and a one-line pull request title.
- The files and hunks it contains, and why they belong together.
- What it depends on (usually the previous branch) and what a reviewer should look at first.
- Risk: anything that cannot be split cleanly (a hunk that mixes two concerns) and how you would handle it.

Order the list so refactors and preparatory changes come first and the behaviour change last. Say where the plan is a guess (for example whether an intermediate state builds) and how you would check it.

Then **stop and ask the user to approve, change, or cancel the plan**. Do not create branches, commits or stashes before the answer.

## 3. Carry out the approved plan

Only after approval, and only the branches the user approved:

1. Make a safety copy first: create a backup branch at the current commit (`git branch backup/<current-branch>-before-split`) and, when there are uncommitted changes, record them in a commit on that backup branch or save the full diff to a file outside the repository. Tell the user the name.
2. Create each branch from the previous one (the first from the base), stage exactly the files and hunks of the plan (`git add -p` equivalents, or `git apply` of a prepared patch), and commit with a message that explains the why. Do not use destructive commands: no `reset --hard`, no `checkout --` on files with changes, no force-push, no `clean`.
3. After each branch, run the project's build and fast tests if they are known and cheap, and report the result. If a branch does not build, fix the split (move a hunk) rather than the code, and tell the user.
4. When the last branch is done, check that the tip of the stack has the same tree as the original change: `git diff <original> <last-branch>` must be empty except for the files the user excluded.

## 4. Report

List the branches in order with their titles and commit ids, the check results, and the exact commands to push them and open the pull requests. **Do not push or open pull requests** unless the user asks; when they do, push without force and open each pull request against the previous branch (or the base for the first), with a description that links the stack.

## Never

- Never run a destructive git command, and never force-push.
- Never push, open pull requests or delete branches without being asked.
- Never drop a change: everything in the original diff ends up in some branch or in the user's explicit "left out" list.
