# ADR-0005: Workspace conventions for rules, skills, modes, search and setup

- Status: Accepted 2026-10-22
- Date: 2026-10-22
- Deciders: ompos-maintainers
- Scope: project-local files that shape agent runs (ADE-17), codebase text and regex search (ADE-18) and the setup file that runs before an agent run (ADE-19). Implemented in `src/OmpOS.Harness.Workspace`. Requirements: `docs/plans/ade-harness-plan.md` and `.planning/REQUIREMENTS.md`.

## Context

Phase 28 of the ADE harness needs a project-local convention for four things: rules and skills that load into session context, custom agent modes that appear in the mode picker, a text and regex search over the workspace, and a setup step that prepares the environment before an agent run. The plan fixes the behaviour (ADE-17, ADE-18, ADE-19) but not the file layout, the file formats, or how a malformed file or a failing step is treated. This ADR decides those points.

The workspace is user-owned content. Files in it can be edited, committed by others, or produced by tools. Two rules follow: a malformed convention file must not take the session down, and no convention read or search may reach outside the workspace root.

## Decision

### 1. Layout

All convention files live under `.ompos/` at the workspace root.

| Kind | Path | Format |
| --- | --- | --- |
| Rule | `.ompos/rules/<id>.md` | Markdown text |
| Skill | `.ompos/skills/<id>.md` | Markdown text |
| Mode | `.ompos/modes/<id>.json` | JSON object |
| Setup | `.ompos/setup.json` | JSON object |

Only direct children of the three folders are read. Subfolders are not searched. Files in these folders whose extension does not match the kind are ignored without a diagnostic.

### 2. Rules and skills

- The id is the file name without `.md`.
- The id must match 1 to 64 ASCII characters: lowercase letters, digits, `-` or `_`, starting with a letter or digit. Otherwise the file is malformed.
- The body is the whole file as UTF-8 text. A leading UTF-8 byte order mark is removed. There is no front matter and no metadata in this version.
- A file is malformed when it is not valid UTF-8, is empty or whitespace only, or is larger than 256 KiB.

### 3. Modes

A mode file is a JSON object with these members:

```json
{ "id": "review", "displayName": "Review", "rules": ["style"], "skills": ["triage"] }
```

- `id` is required and follows the id rule in section 2.
- `displayName` is required, is trimmed, and is at most 100 characters.
- `rules` and `skills` are optional arrays of ids. Each id must name a rule or skill that loaded. Duplicates are dropped.
- Unknown members are malformed. A mode id may be defined by only one file; the later file, in ordinal file-name order, is malformed.

A mode that references an unknown rule or skill is malformed and is skipped. Modes are not partially loaded.

### 4. Setup

```json
{
  "steps": [
    { "command": "/bin/sh", "arguments": ["-c", "make deps"], "workingDirectory": "tools" }
  ],
  "environment": { "OMPOS_SETUP_MARKER": "ready" }
}
```

- `steps` is required and is an ordered array. It may be empty.
- Each step has a `command` (required), an `arguments` array of strings (optional, default empty), and a `workingDirectory` relative to the root (optional, default `.`).
- `environment` is optional. Its names match ASCII letters, digits and `_`, not starting with a digit. Its values are strings.
- The environment is added to every step and is returned to the caller as the environment the agent run must use. Steps inherit the host environment with the declared map added on top. Applying the map to the agent process is the launcher's job.
- The setup file is validated on read. A `workingDirectory` or a relative `command` path that leaves the root is rejected at validation time, and it is checked again at run time.

### 5. Path containment

Every workspace-relative path goes through `WorkspaceRoot.Resolve`. It rejects:

- an empty path, a rooted path, or a path with NUL characters;
- a lexical path that leaves the root after normalisation;
- a path whose real location leaves the root because a symbolic link in any segment points outside it, including a dangling link whose target does not yet exist.

Symbolic links that point inside the root are followed. Search does not follow symbolic links or reparse points at all, so a search never reads outside the root.

Command paths follow a separate rule. A bare command name (`sh`) is resolved through `PATH`. A relative command with a directory part (`tools/run.sh`) must resolve inside the root. An absolute command (`/bin/sh`) names a host executable and is used as given. This is deliberate: the setup file is code the workspace owner wrote, and it runs with the user's privileges. Open risk 1 records the consequence.

### 6. Loading and reload

`WorkspaceCatalog` loads the snapshot in its constructor and on every `Reload()` call. `Reload()` is synchronous and is the deterministic entry point.

Changes are also picked up automatically. A watcher on the root detects creation and removal of `.ompos`. A second watcher covers the `.ompos` subtree only, so the watch count does not grow with the size of the repository. Events are debounced by 250 ms. A watcher error triggers a reload.

A malformed file produces exactly one `WorkspaceDiagnostic` with its workspace-relative path and a reason. The file is skipped; the rest of the snapshot loads. Diagnostics are ordered by path, then reason.

### 7. Search

`WorkspaceSearch` searches literal text or regular expressions over every file under the root, excluding any folder named `.git`.

- Results are `SearchHit(RelativePath, LineNumber, MatchText)`: one hit per match, one-based line numbers, paths with `/` separators. Hits are ordered by path, then line, then position.
- Files larger than 1 MiB are skipped. Files that contain a NUL byte are treated as binary and skipped. Files that disappear or cannot be read during the search are skipped.
- A regular expression is compiled with a 2-second match timeout per line. An invalid pattern and a timeout are both error results, never exceptions. An empty pattern is an error result.
- Zero-length regular expression matches are not reported.
- `MaxHits` (default 1000) bounds the output. A search that finds more sets `Truncated`.
- Semantic indexing is not part of this decision.

### 8. Setup run semantics

`SetupRunner.RunAsync(root, consentStore, consentPrompt, startAgent, ct)` checks consent, runs the setup, and then, only if every step passes, invokes `startAgent` with the declared environment. Its result is:

- `Passed`, with the environment the run used, when every step exits with status zero (or there is no setup file). `startAgent` has completed.
- `Failed`, with the zero-based index of the failing step and a reason, when a step exits non-zero, cannot start, has a working directory that is missing or escapes the root, when the setup file is malformed, or when an approval could not be saved. `startAgent` is never invoked, and the environment in the result is empty.
- `ConsentRefused`, with a reason, when the setup declares steps or environment variables and the user has not approved it (section 9). No step runs and `startAgent` is never invoked.

A missing setup file means no setup, and the run starts with an empty environment. A malformed setup file fails the run. This differs from the skip rule for rules, skills and modes (section 6), and it is deliberate: silently starting an agent without the setup its workspace requires would run it in an unprepared environment.

Steps run one at a time with `System.Diagnostics.Process`, an argument list, no shell, and an explicit working directory. Output is drained and bounded to 4096 characters. A failure reason keeps the first non-empty line of standard error, capped at 512 characters. Cancellation kills the step's process tree.

### 9. Setup consent

A setup file runs only after the user approved it for this repository. Consent is asked once per repository and is keyed by the canonical repository path plus the SHA-256 of the exact bytes of `.ompos/setup.json`, so a changed file asks again, and so does the same file in another repository. The hash is computed from the same bytes that were parsed, so the approval always describes what runs. A setup that declares no steps and no environment variables runs nothing and asks nothing; one that declares only environment variables still asks, because variables such as `LD_PRELOAD` change what the agent run executes.

- `ISetupConsentStore` answers `IsGrantedAsync(key)` and records `RecordGrantAsync(key)`. `FileSetupConsentStore` keeps one versioned JSON file (`OmpOS/setup-consent.json` under the per-user application data folder), reads it afresh on every call, writes through a temporary file renamed over the target, and keeps one approval per repository (the latest). A corrupt, empty, or unknown-version file is no consent: it is moved aside as `setup-consent.json.quarantined-*` and reading never throws.
- `ISetupConsentPrompt` asks the user; each head decides how (dialog, terminal question, web form). The default `DenyingSetupConsentPrompt` denies with a message that says no approval is recorded, so a head that cannot ask refuses the setup instead of running it.
- If the user approves but the approval cannot be saved, the run fails before any step runs.

## Consequences

- The convention is plain files that users can read, diff and commit. No schema registry or database is needed.
- Malformed files are visible as diagnostics; the session continues.
- The setup gate is fail-closed. A broken setup file blocks agent runs until it is fixed.
- The process-spawning code lives in `src/OmpOS.Harness.Workspace`, which the `BannedSymbols.NoProcess.txt` rule permits for `OmpOS.Harness.*` projects.
- The project registration in `OmpOS.slnx` and the layer map are done by the orchestrator; this decision does not change them.

## Open risks

1. **Setup executes workspace code.** An absolute command runs a host executable, and the setup file runs with the user's privileges. Section 9 asks the user to approve a setup file once per repository and file hash before its first run, but the approval shows the commands, not what they do; an approved file still runs with full user privileges. No head implements a prompt yet, so the default prompt denies every setup, and the run layer must supply a real `ISetupConsentPrompt` before it is wired to a real agent. The setup commands are still not routed through the ACP tool-approval path.
2. **Time of check to time of use.** `Resolve` validates a path, and the caller then opens it. A symbolic link swapped in between can redirect the open. The mitigation would be to open with no-follow semantics, which this version does not do.
3. **Platform coverage.** Containment and link handling are tested on Linux only. Windows junctions and case-insensitive volumes (default macOS) are not tested. A case-only difference from the root path is rejected as outside the root, which is safe but may reject valid paths. The ordinal-ignore-case comparison is used on Windows only.
4. **Setup steps have no timeout.** Only cancellation stops a step. A step that starts a background child which keeps the output pipe open can hold the run until that child exits. A per-step timeout is not included.
5. **Setup output may contain secrets.** The failure reason can include up to 512 characters of standard error, and environment values are passed as given. Nothing here redacts them.
6. **Search cost is bounded per line and per file, not per run.** The regex timeout applies to one line. There is no total deadline and no total byte cap, so a large tree with many matching lines can be slow. `MaxHits` limits output, not work.
7. **Watch limits.** On Linux the watcher uses inotify, so the `.ompos` subtree costs one watch per folder. Overflow is handled by a reload, which can be delayed by the debounce. Callers that need immediate consistency must call `Reload()`.
8. **Format is minimal.** Rules and skills are opaque text. Scope, priority, per-rule conditions and front matter are not defined. Modes reference rules and skills only by id.
9. **Strict ids reject some file names.** A rule file named `README.md` in `.ompos/rules` is reported as malformed, because its id is not lowercase. This is intentional, but users will see it.
10. **Search does not skip other VCS folders.** Only `.git` is excluded by name. `.hg`, `.svn` and similar folders are searched.

## Verification

The conventions are covered by `tests/OmpOS.Harness.Workspace.Tests` (29 tests, run on Linux with the Release configuration). The tests cover:

- loading of valid rule, skill and mode files;
- one diagnostic per malformed file while the valid files still load;
- reload after an edit, an addition and a removal, and watcher publication after an edit;
- text and regex hits with file, line and match; ignore-case; `MaxHits` truncation; invalid and timed-out regular expressions as error results; size, binary and symbolic-link exclusions;
- path containment for lexical escapes and for file and directory symbolic links, including a dangling link;
- a passing setup whose variable is visible to its step and to the run; a failing step that stops later steps and never starts the agent; a missing command; a malformed setup file; a working directory that escapes the root; and a missing setup file.
