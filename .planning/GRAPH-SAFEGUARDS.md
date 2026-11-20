#GRAPH-SAFEGUARDS — Hard guards per risk class (SPE-35249, Wave 4 · n6)

Date: 2026-11-20. Binding on every unit in `.planning/GRAPH-UNITS.md`
(68, 71, 73, 74, 75, 76, 77) and on the stacked-increment approach in
`.planning/GRAPH-APPROACH.md`; satisfies the quality/verification design in
`.planning/GRAPH-DESIGN.md` §§6–10 and the verdicts/gap defaults in
`.planning/GRAPH-CRITIQUE.md` (repo scope = current monorepo only; verify
existing evidence; two-way sync is planning-state → Linear + Notion).
Every guard below names the concrete enforcement mechanism in this repo
(command, script, file, or field) — no guard is aspirational prose.

---

## G1 · CI state classification (passing / failing / pending / skipped-required / unknown / timed-out / flaked)

Any claim "CI is green/red/pending" in a VERIFICATION.md, SUMMARY.md, PR
body, or STATE line MUST be derived from the GitHub check-runs API for the
exact head SHA — never from the PR title, a local run, or memory.

Enforcement (run per unit, record output in `<phase>-VERIFICATION.md`):

```bash
# 1. Resolve the run set for the exact HEAD (no --limit trimming that drops a job)
gh run list --branch <pr-branch> --limit 20 --json databaseId,headSha,workflowName,status,conclusion,event,displayTitle
# 2. Per-check classification (authoritative)
gh api repos/{owner}/{repo}/commits/<HEAD_SHA>/check-runs --paginate \
  --jq '.check_runs[] | "\(.name) status=\(.status) conclusion=\(.conclusion) run_id=\(.details_url)"'
```

Classification table (map raw `status`/`conclusion` to exactly one label):

| Guard label | Raw condition (`status` / `conclusion`) | Meaning |
|---|---|---|
| `passing` | `status=completed` AND `conclusion=success` | Job/step passed at HEAD |
| `failing` | `status=completed` AND `conclusion=failure` | Real failure at HEAD (subject to G2 flake probe) |
| `pending` | `status != completed` (`queued` / `in_progress`) OR `conclusion` empty while run active | Still running; never record a verdict |
| `skipped-required` | `status=completed` AND `conclusion=skipped` AND the check name ∈ required-checks set for `main` | Skipped but blocks merge — treated as blocking, same as failing |
| `unknown` | No check-run row for an expected job, or API error/empty body | Indeterminate; never coerce to pass |
| `timed-out` | `conclusion=timed_out` | Runner/timeout kill; bounded-retry eligible only via G2 |
| `flaked` | `failing` on attempt N, then `passing` on attempt N+1 at the **same headSha** with zero code change (see G2) | Non-deterministic; the pass does NOT overwrite the recorded failure |

Expected job set (all-green = all of these classified, per GRAPH-CRITIQUE gap 4):
`build-test` (always) + `android` (needs `build-test`) + `desktop` (matrix
linux/windows/macos, `fail-fast: false`) on `pull_request`/push; plus
push-only `image-scan` (needs `build-test`). PR-only gates inside
`build-test` (OpenAPI breaking §G7, version-bump §G7) are part of
`build-test`'s verdict.

| Gate | How to confirm a check is "required" |
|---|---|
| `skipped-required` determination | `gh api repos/{owner}/{repo}/branches/main/protection/required_status_checks` lists the required contexts; a `skipped` conclusion on any context in that list → `skipped-required`. If the protection API returns 404/no ruleset (known repo state per GRAPH-CRITIQUE gap 5: no required review configured; branch-protection statuses unconfirmed), the required set is **unknown** → a `skipped` conclusion records `unknown`, and the VERIFICATION says so explicitly. |

Hard rules:
- `concurrency.cancel-in-progress: ${{ github.event_name == 'pull_request' }}`
  (`ci.yml:16-19`) means a superseded PR run is `cancelled`, not `failing`.
  A `cancelled` conclusion records `unknown — superseded`, never `failing`,
  and never counts as a pass.
- `continue-on-error: true` on the smoke step (`ci.yml:122`) means step
  failure surfaces as `steps.smoke.outcome == failure` and the follow-on
  `Browser smoke gate` step (`ci.yml:132`) fails the job. Classify by the
  **job** conclusion, not the smoke step's.
- `if: always()` cleanup steps (`ci.yml:156,161,208,213`) never decide the
  verdict.
- Fork PRs have no secrets and skip jobs by design (`ci.yml:38`,
  `ci.yml:253`, `ci.yml:300`): classify as `unknown — fork skip`, never
  `passing`.

## G2 · Flaky CI → bounded retry with failure-record distinction

A `failing` result is retried at most **once**, and only when the failure
is classified as a flake. Unbounded retry ("run until green") is forbidden.

Enforcement:
1. First classify per G1. If the raw conclusion is `failure` (or
   `timed_out`), attempt budget = 1 retry. Any other label gets no retry
   (`pending` → wait; `skipped-required`/`unknown` → investigate, not retry).
2. Flake probe (all three required to label `flaked`):
   - `gh run rerun <run_id> --failed` (rerun failed jobs only), same headSha.
   - `git diff <base>...<HEAD> --stat` between attempt N and N+1 = empty
     (no code change; runner-only rerun).
   - The retried result is `passing` at the identical headSha.
3. Failure-record distinction is mandatory: the VERIFICATION records both
   attempts, e.g.
   `build-test attempt 1: failing (dotnet test 3/214 failed — TestA) →
   rerun attempt 2: passing at same SHA → verdict: flaked (1 retry used,
   0 remaining)`.
   The original failure stays in the record; the gate verdict uses the
   final classification `flaked`, which is **not** `passing` for the
   purpose of G5 false-completion (G5 requires `passing`, never `flaked`,
   on the final attempt).
4. If the retry also fails, or the diff is non-empty, the label reverts to
   `failing` and the retry budget is exhausted. No second retry.
5. `timed_out` gets the same single-retry budget; a second `timed_out` is
   `failing` with reason `timeout`, never `flaked`.

## G3 · Partial phase failure → preserve artifacts, true partial state

When a phase gate fails part-way, the partial work is preserved and the
state is recorded as partial — never as pass, never discarded.

Enforcement:
- The gate run leaves every artifact in the phase directory
  (`.planning/phases/<phase>/`): test results, coverage
  (`cobertura` output in `TestResults/`), logs, and the refreshed
  `<phase>-VERIFICATION.md` with per-command exit codes and a
  `verdict: partial` line naming exactly which gate steps passed/failed
  (ran in GRAPH-DESIGN §10 order 10.1→10.8).
- STATE/ROADMAP update for a partial gate: `completed_phases` does NOT
  increment; the phase row records `status: partial` with the failing
  step named; the open items stay OPEN with owners (per GRAPH-UNITS
  `Blocked (stays open)` rows). ROADMAP checkboxes for that phase stay
  unchecked (checkboxes move only on the G5 five-way agreement).
- No artifact deletion: failed-run logs, TRX files, and the failing
  VERIFICATION stay in the phase dir and in the PR (attached or linked).
  The next attempt appends a new dated section; it never rewrites history.
- Artifact-preservation rule (applies to every retention scenario in this
  doc): keep the artifact name, the head SHA it was produced at, and the
  command that produced it on the artifact itself (header of the
  VERIFICATION section, or a `.meta.json` sibling for logs). An artifact
  without a SHA is not evidence.

## G4 · Sync outage (Linear/Notion) → queue + backoff + degraded marker in STATE

Two-way sync direction (GRAPH-CRITIQUE gap 3): planning state → Linear is
gated on `VERIFICATION passed` at phase HEAD (GRAPH-DESIGN §7.4); Notion
mirrors task/step rows. When either sink is unreachable, work queues
locally with bounded backoff and STATE declares degraded mode — it never
silently drops a sync, and never fabricates a sync result.

Enforcement:
1. Queue file: `.planning/sync-queue/<sink>-queue.jsonl` (one JSON object
   per line: `{queued_at, head_sha, phase, target_id, op, payload_summary}`).
   Every sync write that fails appends one line; the line is removed only
   after a confirmed successful write.
2. Backoff: retry on a bounded exponential schedule (1m → 5m → 30m → 3h,
   then once per subsequent unit gate). Each attempt appends, never
   overwrites. After 3h (3 retries), the entry remains queued with
   `retry_count` visible in the queue file; the unit gate does NOT block
   on the sync backlog — it blocks on the code gate (G1/G2) only.
3. STATE degraded marker (frontmatter, exact format):
   ```yaml
   sync_status: degraded
   sync_degraded_sink: linear        # or notion
   sync_degraded_since: 2026-11-20T14:05Z
   sync_queue_depth: 3               # lines across both queue files
   sync_last_success: linear=2026-11-20T12:00Z notion=2026-11-20T14:03Z
   ```
   `sync_status` is `ok` (both sinks reachable, queue empty) or `degraded`
   (any sink failing or queue non-empty). The marker is updated on every
   sync attempt outcome, not only at phase close.
4. No false sync claims: a Linear issue moves to Done only after the
   phase's VERIFICATION says `passed` (LINEAR-MAP "How to keep this in
   step"); while `sync_status: degraded`, the phase row records
   `pending-sync` and the Done move waits for queue drain.

## G5 · False-completion prevention (five-way agreement)

No phase is complete, no issue is Done, no checkbox is ticked, and no
CHANGELOG entry claims shipping until all five evidence legs agree —
independently acquired, at the same head SHA.

| # | Leg | Concrete evidence (all required) |
|---|---|---|
| 1 | Code | `git rev-parse HEAD` == the SHA named in `<phase>-VERIFICATION.md`; `git status --porcelain` clean for the unit's files |
| 2 | CI | G1 classification = `passing` for every expected job at that SHA (per GRAPH-APPROACH per-unit gate: `build-test` + `android`/`desktop` where touched), PR-only gates (G7) green when the unit includes them |
| 3 | Merge | PR merged to `main` (`gh pr view <n> --json mergeCommit,state` → state `MERGED`, mergeCommit present) |
| 4 | Linear | Issue state = Done via the gated write in G4 (only after leg 2 at HEAD, never on checkbox) |
| 5 | Notion | Mirror row updated AND `gh`-visible agreement… Notion is not queryable from `gh`; evidence leg = the queue-drained confirmation + `sync_status: ok` (G4) |

Hard rules:
- Disagreement = not complete. If any leg is missing, stale (wrong SHA),
  or contradicts another (e.g. ROADMAP `[x]` while CI at HEAD is
  `failing`), the phase records `status: partial` and the reconciliation
  note names the disagreeing legs. This mirrors the STATE reconciliation
  rule (GRAPH-CRITIQUE gap 2): reconciliation verdicts/substantially-
  complete/partial and ROADMAP checkboxes are not gate results.
- `completed_phases` in STATE frontmatter increments only when all five
  legs hold (WT baseline is `0`: no gate run = 0 completed).
- "Done" in Linear is never set from a reconciliation record; "Done"
  requires the VERIFICATION `passed` line at HEAD (GRAPH-DESIGN §7.4).
- Archived phases (`.planning/milestones/`) are closed history and never
  count as a leg (GRAPH-DESIGN §8.1).
- `flaked` (G2) is not `passing` for leg 2: the final attempt must itself
  classify `passing`.

## G6 · Secrets prohibition + redaction

No secret (token, password, API key, connection string) may appear in
code, fixtures, docs, logs, PR bodies, VERIFICATION/SUMMARY files, or
queue payloads. Detection is a gate; prohibition is absolute.

Enforcement:
1. Gate: `bash scripts/ci/secret-scan.sh` (pinned gitleaks; fails closed
   offline) runs in `build-test` and via
   `bootstrap.sh` (`ci.yml:81-82`); `AUTONOGRAMMER_SKIP_SECRET_SCAN=1` is
   an explicit recorded operator choice, never a default (GRAPH-DESIGN
   §6.1). A skipped scan records `unknown` per G1, never `passing`.
2. Redaction rules for any artifact written by the gate:
   - Values of env keys matching `*PASSWORD*`, `*TOKEN*`, `*SECRET*`,
     `*KEY*` (e.g. `SUBMODULE_TOKEN`, `GH_TOKEN`, `GITHUB_TOKEN`,
     `AUTONOGRAMMER_*_PASSWORD`) are redacted to `***` in logs, TRX
     attachments, and VERIFICATION output. The test-services env file
     (`AUTONOGRAMMER_TEST_SERVICES_*`, run-owned `gate-$$`) is removed on
     exit (`test-services.sh down`) and never committed.
   - CA1031 boundary logging (`AccountSessionManager.cs:419-421`):
     log `exception.GetType().Name` only — never the message or stack
     into a wire result or log line (GRAPH-DESIGN §3).
   - Queue payload summaries (G4) carry counts and IDs only, never
     payload bodies that could hold secrets.
3. If a scan finds a secret in a committed file: the unit stops; the file
   is redacted; the committed secret is treated as compromised
   (rotation ticket opened, owner-notified) — redaction alone is not
   remediation. The VERIFICATION records the incident, not the secret.

## G7 · Breaking changes / schema / migrations → PLAN justification + tests + rollback + HITL

Change-controlled categories. The gate differs per category; the
common core (justification → test → rollback → human decision) is always
present.

| Category | Required justification (in PLAN) | Required test | Required rollback | HITL |
|---|---|---|---|---|
| OpenAPI breaking change | `ALLOW_BREAKING_API` env or a `BREAKING-API` commit line + CHANGELOG entry (enforced by `scripts/ci/check-openapi-breaking.py --base origin/main` + `check-version-bump.sh`, `ci.yml:103-111`) | `python3 scripts/ci/test_check_openapi_breaking.py` negative control (bypass fails without the flag) | Version-bump check proves the CHANGELOG + version carry the break; revert = the PR revert | Owner approval recorded in PR (AskUserQuestion channel, GRAPH-CRITIQUE gap 6) |
| Schema / migration | Migration number (M0012/M0014 precedent), additive-only by default (GRAPH-DESIGN §7.2); destructive/truncating needs explicit owner decision (e.g. 74-11 legacy-row policy, 68-11 erasure) | `DecisionRecordTests` for the 68 gate; Postgres Dispatch tests run with test-services up (Unit 4 item 6) | Migration must be re-runnable against a restored DB (locked restore, §10.1); rollback = previous migration + previous image | Owner decision recorded; erasure entry point additionally requires independent security review (74.1, SEC-77-09) |
| Atomic compositions (schema + composition change, partial landing unsafe) | PLAN names the composition boundary and why it cannot split (Unit 1 item 5: `PostgresAccountErasure` fence + composition) | Phase gate at HEAD (GRAPH-DESIGN §10) | Revert the one atomic PR; no partial revert path exists — state this in the PLAN | **Mandatory owner HITL before merge** — the single atomic-unit exception (GRAPH-APPROACH): recorded explicit owner approval in the PR; `human_verify_mode: end-of-phase` |

Hard rules:
- A breaking change without the PLAN justification line, its test, and
  its rollback note is a gate failure — the unit does not merge, even if
  CI is green. The OpenAPI scripts are the enforcement: they must run on
  the PR (PR-only gates), and their output goes into the VERIFICATION.
- No destructive migration lands as part of a stacked increment riding
  alongside unrelated units (GRAPH-APPROACH: per-PR revert only works when
  each unit is independently revertible). The erasure-entry-point PR is
  the only atomic, non-splittable unit, and it carries its HITL record.

---

## Guard × unit quick reference

| Unit (GRAPH-UNITS) | Most-critical guards |
|---|---|
| 1 — Phase 68 (docs + erasure) | G7 (atomic erasure PR, HITL), G5 (SEC-77-07 fix evidence at HEAD), G1 (fresh full gate, item 3) |
| 2 — Phase 71 (artifacts) | G5 (rollout gate 71-07 before persist), G6 (grant-store secrets), G1 (per-project tests + web-smoke) |
| 3 — Phase 73 (computer use) | G5 (74 verification before eligibility), G3 (X11/HITL remainder stays open) |
| 4 — Phase 74 (dispatch) | G7 (no enable-outside-Development without owner approval), G2 (SSE/host flake budget), G3 (drain/external stays open) |
| 5 — Phase 75 (parity A) | G5 (74.1 gates 75-12), G3 (divergent-branch port re-verified before grading) |
| 6 — Phase 76 (parity B) | G3 (bwrap positive-control failure preserved, not graded MET), G5 (75 before 76 on `CoworkEngineRequest`) |
| 7 — Phase 77 (hardening) | G2/G1 (final `bootstrap.sh` flock gate, dated evidence), G6 (23 undocumented env vars = gate failure, §6.4), G4 (sync drained before close) |

## Non-goals of this document

It defines guards, not the workflow that executes them (GRAPH-APPROACH
owns stacking/serialization), and not the quality baseline they enforce
(GRAPH-DESIGN owns typing, error handling, a11y, and the §10 command
list). Where a later phase must deviate (e.g. a new flake class), the
deviation and reason are recorded in that phase's PLAN — the guard
interaction (G1–G7) must still hold.
