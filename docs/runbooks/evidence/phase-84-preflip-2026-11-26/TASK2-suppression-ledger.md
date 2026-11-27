# Phase 84 pre-flip baseline — Task 2: suppression ledger audit (report text, no source edits)

Commit: fdafc70b1b418793814d4d3984a51d75ee63d532 (working tree unchanged except untracked evidence + .bg-shell/.gsd/.mcp.json pre-existing)
Date: 2026-11-26
Linear: SPE-37896 chain (https://linear.app/swcstudio/issue/SPE-37896/phase-84-sota-quality-and-cutover-gate); SC1 SPE-37897; SC2 SPE-37898; SC3 SPE-37899; Project P-SPE-2.
Notion payload root: 700bc1a0-c7ae-812c-abfb-00035fd38be2
Rule: no new suppression, NoWarn, pragma disable without reason comment naming owning plan/decision, or severity-none rule in this phase; every occurrence classified with a named justification or removed; docs/open-questions.md edits forbidden for this run (all ledger work is report text here).

## Oracle

- `tests/Autonogrammer.ArchitectureTests/SuppressionLedgerTests.cs` (kept-row completeness oracle), `SuppressionScanner.cs`, `SuppressionOccurrence.cs`.
- Scanner scope: first-party `.cs`, `.razor`, `.axaml`, `.editorconfig` under repo root, skipping segments `.git, bin, obj, node_modules, TestResults, output, .planning, docs, TestData, .nuget, plugins`. Comments and string literals blanked before matching; match key is file path + rule id (line numbers not part of key).
- Kept rows: `docs/open-questions.md` rows `| SUP-… | Is the suppression `RULE` in `path` still needed? | … | kept |` (first backtick span = rule, second = path).

## Counts observed this run (grep-based, machine-generated commands quoted)

- `grep -rn '#pragma warning disable'` over `src apps services` + tests scan (first-party, bin/obj excluded by rglob segment skip): **154 pragma lines** (raw, comments not blanked; the SuppressionScanner blanked count and the SuppressionLedgerTests verdict are the authoritative numbers, pending `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release --filter "SuppressionLedgerTests" --filter-not-trait "Category=Live"`, recorded in `TASK1-baseline.md` once the build finishes).
- Rule distribution (raw): MA0048 ×48, CA2007 ×16(+1 in a multi-id pragma), CA1031 ×15, MA0002 ×15(+1), VSTHRD003 ×9, CA2000 ×7(+1), MA0004 ×7(+1), MA0006 ×6(+1), xUnit1051 ×5, CA2100 ×4(+3 in multi-id), CA1822 ×2, VSTHRD100 ×2, IDE0005 ×2(+1), CA1515 ×1, CA1003 ×1, CA1056 ×1, MA0051 ×1(+1), CA5399 ×1, plus multi-id pragmas (`CA1034+CA2000+CA1001+CA2007+CA2100+MA0004+MA0006+MA0051+IDE0005+CS8604` etc.).
- Kept SUP rows (SuppressionLedgerTests form only, status `kept`): **276** (`grep -c '^| SUP-' docs/open-questions.md` = 457 total SUP-prefixed rows incl. skip-marker rows; 420 lines carry `| kept |` overall; the suppression-form subset is 276).
- `dotnet test tests/Autonogrammer.ArchitectureTests -c Release --no-build -- --filter-not-trait "Category=Live"` → exit 2 overall, but the single failure is `RepoHygieneTests.RepoHygiene_DocsSkeletonExists` (an `OQ-526-83` row-shape issue in `docs/open-questions.md`, outside this task's ownership and edits-forbidden). `SuppressionLedgerTests` is **not** in the failed list: the scanner/ledger completeness oracle is green at `fdafc70b` (log `logs/16-arch-tests.log`: 124 total, 123 passed, 1 failed, 0 skipped). The grep counts in this file are the raw inventory; the scanner count is authoritative and the ledger test confirms every pragma/SuppressMessage/severity-none occurrence has exactly one kept row and no kept row is orphaned.
- `SuppressMessage` occurrences (`grep -rn 'SuppressMessage' src apps services tests` excluding bin/obj; many are the scanner's own test mentions): **118 lines** — the scanner's blanked count is authoritative; test-control mentions are not production suppressions.
- `<NoWarn>` in `*.csproj/*.props/*.targets` under src/apps/services/tests: **0** (grep exit 1, no matches). No `GlobalSuppressions` file. No new suppression added in this run (no source edits at all).
- S1 `.editorconfig` severity-none block (`.editorconfig:392-400`):
  - `CA1303` (`[*.{cs,vb}]`, line 395) — SUP-001 kept; justification comment block at `.editorconfig:392` ("single rule source for build and dotnet format (D-04)") plus SUP-001 rationale (English-only, no localization requirement). Disposition this run: **keep-with-justification (report only; no edit)**.
  - `CA1707`, `CS1591`, `CA1062` (`[tests/**.cs]`, lines 398-400) — SUP-002/003/004 kept; each row names the tests/** scope rationale. Disposition: **keep-with-justification (report only; no edit)**.
  - Justification-comment note: the bare `.editorconfig` block at lines 392-400 carries one shared comment ("single rule source… (D-04)"), not a per-rule comment; per-rule justification lives in the SUP rows. Classified S1, flag for the owner decision (Task 4) whether per-rule inline comments are required before the flip.

## S2 `#pragma warning disable` — classification by rule class (each occurrence in the machine list below; justification = file-adjacent reason comment)

Top classes:
- MA0048 (48, file-name vs type-name): overwhelmingly `#pragma warning disable MA0048 // The plan owns this file for …` (plan-owned reason comment present). Disposition: keep-with-justification **iff** the owning plan is named in the comment; the SampleSoup list in the evidence log (`task2-pragma-list.txt`) preserves each site so the owner can verify the named plan per file. Report: no occurrence in this sample lacks a `//` reason suffix except `apps/Autonogrammer.Web/Services/WebAccountSession.cs:552: #pragma warning disable CA5399` (no reason comment) — flagged as **justification is "none"** (report-only; no removal in this run given the docs/open-questions.md edit ban and the no-source-edit scope — the removal/decision is listed for the owner in §Orphans instead of being silently performed).
- CA1031 (15): each carries a boundary rationale comment (readiness probe never throws, settle is last-resort, etc.). Disposition: keep-with-justification.
- CA2007 / MA0002 / MA0004 / MA0006 / VSTHRD003 / CA2000 / CA2100 / xUnit1051 / CA1822 / CA5399 / CA1515 / CA1003 / CA1056: see per-occurrence list; majority carry reason comments. Any multi-id pragma is counted as one occurrence per rule by the scanner (the raw grep count 154 is therefore **not** the scanner count; the scanner count is one row per rule id per site).
- Reason-comment audit: searched for pragmas without `//` on the line; **1** found (`WebAccountSession.cs:552 CA5399`). All others carry a trailing `//` reason. The plan's "owning plan/decision" bar: MA0048 rows mostly name a plan ("The plan owns this file…"); CA1031 rows name a boundary, not a plan/decision id — flagged as **justification present but not plan/decision-named** for the owner to accept or require amendment (report text only).

## S3 ledger test rows / orphan rows (report classification, no edits)

Scanner/ledger mechanism (S3): `SuppressionLedgerTests.EveryPragmaSuppressMessageAndSeverityNoneHasARowInOpenQuestions` and `NoKeptSuppressionRowIsOrphaned` are the completeness oracle; the counts in this file reconcile against the scanner output once the targeted test run in TASK1 lands. Two additional orphan/unlisted findings from the baseline run (exit 1, full log `logs/05-check-skip-markers.log`):

- Unlisted (no SUP row): `Assert.Skip` in `tests/Autonogrammer.Harness.Rpc.Tests/RpcExtraArgumentsTests.cs:25`, `Assert.Skip` in `tests/Autonogrammer.Harness.Rpc.Tests/RpcPluginLoadCaptureTests.cs:51`, `Fact(Skip=…)` in `tests/Autonogrammer.Harness.Rpc.Tests/RpcPluginLoadCaptureTests.cs:42`. Classification: ledger gap, expected disposition = add SUP rows (forbidden in this run; recorded here as report text).
- Orphan kept rows (row names no live marker): SUP-873 (`tests/Autonogrammer.Harness.Herdr.Tests/HerdrLiveSessionTests.cs`), SUP-194/195/196 (`tests/Autonogrammer.Harness.OmpLauncher.Tests/Live/LiveOmpTerminalTests.cs`), SUP-860 (`tests/Autonogrammer.Harness.Terminal.Tests/WindowsLocalPtyBackendTests.cs`), SUP-215 (`tests/Autonogrammer.Native.Tests/AgentLoop/LiveSteeringWiringTests.cs`), SUP-216 (`tests/Autonogrammer.Native.Tests/AgentLoop/TerminalSessionTests.cs`). Classification: orphan kept rows, expected disposition = retire row (report text only; not edited).

No new suppression, NoWarn entry, pragma without reason comment (other than the one CA5399 flagged above, pre-existing and not added here), or severity-none rule was added in this run. The phase diff from this task is: evidence files only under `docs/runbooks/evidence/phase-84-preflip-2026-11-26/`.

## Per-occurrence list

Machine list: `task2-pragma-list.txt` (154 raw pragma lines, file:line:content) and the SUP-row table extract `task2-sup-rows.txt` (276 kept suppression-form rows, ID | rule | path). Per-occurrence class column: S1 for `.editorconfig` severity-none (4), S2 for pragmas/SuppressMessage (scanner-blanked count authoritative), S3 for ledger/test rows (457 total SUP-prefixed, 276 suppression-form kept).
