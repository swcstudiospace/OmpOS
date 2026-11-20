# GRAPH-PLAN — v0.16.0 Final Execution Plan (SPE-35250, Wave 5 · n7)

Date: 2026-11-20. Synthesizes: GRAPH-STATE-discovery (Wave 1 · n1, SPE-35244) →
GRAPH-UNITS (Wave 2 · n2, SPE-35246) → GRAPH-CRITIQUE (Wave 2 · n4, SPE-34833's
evidence) → GRAPH-APPROACH (Wave 3 · n3, SPE-35248) → GRAPH-DESIGN (Wave 3 · n5,
SPE-35247, quality/verification design S1–S10) → GRAPH-SAFEGUARDS (Wave 4 · n6,
SPE-35249, guards G1–G7).

Repo scope = current monorepo only (`swcstudiospace/OmpOS`); archived phases under
`.planning/milestones/` are closed history, never gate evidence. A phase is done
only when its success criteria are observable AND the gate passes at the phase
HEAD AND a fresh `<phase>-VERIFICATION.md` records `passed` at that HEAD
(GRAPH-CRITIQUE gap 2). Worktree
`/root/src/worktrees/ag-plugin-connector`, branch `docs/phase68-closeout`,
HEAD `94d64280`; main at `f5d453d1` (66.4 merged, PR #23); 67 closing via PR #24.

## Approach (binding)

**Stacked independently-verifiable increments** (GRAPH-APPROACH Approach A):
7 units, one phase / one branch / one PR / one VERIFICATION.md each, in Waves
A→D below. Single atomic exception: **68-11 production account-erasure entry
point** (schema/fence + composition, partial landing unsafe) — lands as one
non-splittable PR and merges only after explicit owner HITL approval recorded in
the PR (AskUserQuestion channel, `human_verify_mode: end-of-phase`). All other
units are PR-review + green-CI gated. Blocked rows stay OPEN per unit with the
reason named; nothing is silently dropped (in-scope: 54 outstanding + 29
blocked).

Serialization (GRAPH-UNITS): ROADMAP.md + STATE.md one writer at a time;
`docs/parity/cowork-parity.md` serial 75 → 76 → 77; CHANGELOG.md one writer per
wave; 73 before 76 (shared `AgentHead.cs` / `CoworkHead` / `HeadSettings.cs`);
75 before 76 (shared `CoworkEngineRequest`); 74 verification before 73
eligibility wiring and (via 74.1) before 75-12.

## Per-wave plan

### Wave A — disjoint starts (parallel, serial shared files)

| # | Unit | Tracker | Verification (exact, ci.yml / bootstrap.sh) | Hold points | Merge gate (G1–G7) |
|---|---|---|---|---|---|
| A1 | **Phase 68 docs-closeout PR** (Unit 1 items 1–4; already committed locally `94d64280`, merge after 67 / PR #24): mark 68-09/68-10 + plan files superseded, replace the "SEC-77-07 open" note (fixed on main `7dadceb5`), update STATE pause record (STATE.md:1099-1105), rewrite stale SC3 grep / "head M0009" wording, fresh full phase gate, fresh `68-VERIFICATION.md` | SPE-34833 | Locked restore + `-warnaserror` build + format (DESIGN §10.1–10.2), `bash scripts/ci/check-licences.sh`, non-Live `dotnet test ... -- --filter-not-trait "Category=Live"` with run-owned `bash scripts/test-services.sh up/down` (DESIGN §10.4), `DecisionRecordTests`, live GreptimeDB telemetry test SC4 (human-verification, may be deferred to hold) | **OQ-73** (Railway written confirmation / hosted-placement; evidence needs podman machine + Railway access) — hold, does not block A1 merge | G1 (classify `build-test` at HEAD) · G2 (≤1 flake retry, both attempts recorded) · G3 (partial → `verdict: partial`, artifacts kept) · G5 (five-way: same SHA + CI `passing` + merged PR + Linear Done + Notion `sync_status: ok`) · G6 (secret-scan gate; redaction rules) · G7 (no breaking change in A1 scope) |
| A2 | **Phase 71 Cowork Artifacts** (Unit 2): commit Native ArtifactBlock producer wiring, mount web artifact UI, then gated items 3–4 once rollout gate clears (71-07); run per-project tests + web-smoke + fresh VERIFICATION + 71-VERIFICATION refresh | SPE-34833 | Per-project `dotnet test tests/<Project>.Tests/... -c Release -- --filter-not-trait "Category=Live"` (DESIGN §10.4), `bash scripts/test-services.sh up` for Stores.Tests, `bash scripts/ci/check-licences.sh`, `python3 scripts/generate-third-party-notices.py --check`, `OUT_DIR="$(mktemp -d)" bash scripts/web-smoke/run-smoke.sh` (DESIGN §10.6) | OQ-400 (mermaid vendoring licence, owner/legal) · OQ-401 (interactive Html, ticketed API render) · OQ-403 (scripts on open vs per-click) · network-origin enforcement 71-08 (design decision) · device/session proof-of-possession · sync rollout gate 71-07/71-12 (owner confirms deployed receiver versions) | G1 · G2 · G3 · G5 · G6 (grant-store secrets) · G7 (no breaking change) |
| A3 | **Phase 74 Dispatch relay, code** (Unit 4 items 1–5): `Dispatch.Client` (HttpDispatchHost + DispatchSseReader + tests), Web/Native heads, 74-04 residual gaps (403 PairedToAnotherAccount, unknown-frame err + 1008 after three, evt frames, handlers off receive loop), stale statements (dispatch.md:9, CHANGELOG:128, 74-VERIFICATION frontmatter) | SPE-34833 | `bash scripts/test-services.sh up` → services-up Api tests + Postgres Dispatch tests (Unit 4 item 6 runs in Wave B verify step), same non-Live test + build trio, `python3 scripts/ci/check-openapi-breaking.py --base origin/main` (PR-only, DESIGN §10.7) if routes change the API | OQ-420 (hosted draining, Railway) · OQ-429 (Android controller, devices) · 74-11 Task 3 legacy-row idempotency policy (owner decision + read-only production count) · enabling Dispatch outside Development (owner approval after 74-11 + 74-09/04 land) | G1 · G2 (SSE/host flake budget) · G3 (drain/external stays open) · G5 · G6 · **G7 (no enable-outside-Development without owner approval)** |

Wave A code work (71, 74) is file-disjoint from 68 (docs-only + own gate) except
ROADMAP/STATE/CHANGELOG — those files are written by one writer at a time.

### Wave B — 74 verify → 73, 75 (parallel after 74-verify)

| # | Unit | Tracker | Verification (exact) | Hold points | Merge gate (G1–G7) |
|---|---|---|---|---|---|
| B1 | **Phase 74 verify** (Unit 4 item 6): fresh verification of SC1–SC6 incl. services-up Api and Postgres Dispatch tests, then new `74-VERIFICATION.md` | SPE-34833 | `env_file="$(bash scripts/test-services.sh up)"; source "$env_file"` → `dotnet test Autonogrammer.Linux.slnf -c Release --no-build -- --filter-not-trait "Category=Live"` → `bash scripts/test-services.sh down` (DESIGN §10.4) | OQ-420 / OQ-429 carried; **Live WebSocket drain on Railway edge** (external) | G1 (fresh gate at HEAD) · G2 · G3 · G5 (fresh VERIFICATION at HEAD is a leg) |
| B2 | **Phase 73 Cowork Computer Use** (Unit 3 items 1–8): register ComputerToolProvider in AgentHead/CoworkHead, real indicator window bound to KillSwitchCoordinator (hotkey + Stop + tray), local opt-in + approval via CoworkApprovalBroker, Dispatch eligibility from 74 verify, head-level integration tests, six ROADMAP criteria, docs + CHANGELOG | SPE-34833 | Non-Live test trio (§10.1–10.4), six ROADMAP criteria commands + X11 under Xvfb, ADR + licence checks (`bash scripts/ci/check-licences.sh`), web-smoke (§10.6) | ADR-0035 acceptance (owner) · Windows/macOS live validation (machines + accessibility/screen-recording grants) · Wayland/AT-SPI (owner unsupported-or-fund) · desktop smoke on real X11 (human-verification, item 9) | G1 · G2 · **G3 (X11/HITL remainder stays open)** · **G5 (74 verification leg before eligibility)** · G6 · G7 |
| B3 | **Phase 75 Cowork parity A** (Unit 5 items 1–8): port/merge unmerged 75-07/08/09/11/13 Native wiring from divergent branch (diverged at `66d5e95c`, 88 commits each side) then re-verify, 75-10 MemoryToolProvider registration, StandingContext from account+project, ScheduledCoworkSessionStarter + AutomationScheduler tick tests, project editor + memory mode + teardown tests, rewrite parity doc + 75-VERIFICATION (MET only with named passing test class), add plans 75-07..13 to ROADMAP, write 75-09/75-10 summaries | SPE-34833 | Roadmap success criteria 1–6 on .NET 10 host + throwaway podman services (`bash scripts/test-services.sh up/down`, DESIGN §10.4), parity-counts check, `python3 scripts/ci/check-openapi-breaking.py --base origin/main` + `bash scripts/ci/check-version-bump.sh --base origin/main` (PR-only, DESIGN §10.7) | 74.1-03 human runbook review gates 75-12 (named reviewer) · durable project PUT idempotency (owner migration decision) · OQ-440/441 omp `memory.backend off` + `--config` overlay (live omp, OD-07) · Auto approval mode (owner decision) · sleep-wake on Windows/macOS (hosts) · **live Supabase smoke** (credential-gated sign-in, 67's `human_needed` remainder) | G1 · G2 · **G3 (divergent-branch port re-verified before grading)** · **G5 (74.1 gates 75-12)** · G6 · G7 |

### Wave C — 76 alone (serial after 75 on shared seams)

| # | Unit | Tracker | Verification (exact) | Hold points | Merge gate (G1–G7) |
|---|---|---|---|---|---|
| C1 | **Phase 76 Cowork parity B** (Unit 6 items 1–7): five SettingsWindowOptions pages in HeadSettings.cs, IsolationComposition.PrepareAsync + chip + grants (Code stays None), NotificationBridge + OsNotificationSink + NotificationsView + sub-agent panel, IsolationLiveTests (bwrap positive control), grade C3–C6, C11–C13, C23, C24, C26, K04, K10, K16, K19, K20 with named test classes, phase gate, mark AG-76-1..12 + AG-76-14 in REQUIREMENTS, check 76-09 in ROADMAP | SPE-34833 | Locked restore `-p:TargetLatestRuntimePatch=false -p:RuntimeFrameworkVersion=10.0.13` + `-warnaserror` build (DESIGN §10.1), `dotnet format ... --verify-no-changes`, 11 test projects non-Live (§10.4), `bash scripts/ci/repo-checks.sh`, `bash scripts/ci/check-licences.sh`, IsolationLiveTests on a host with user namespaces (`/usr/bin/bwrap`) | macOS Seatbelt + osascript (owner Mac host) · Windows job-object containment + notification display (runner + owner decision) · omp-flag-dependent rows (owner OD-07 live omp run) · human desktop check of notifications / isolation chip | G1 · G2 · **G3 (bwrap positive-control failure preserved, never graded MET)** · **G5 (75 before 76 on `CoworkEngineRequest`)** · G6 · G7 |

### Wave D — 77 last, then milestone closeout (SPE-34836)

| # | Unit | Tracker | Verification (exact) | Hold points | Merge gate (G1–G7) |
|---|---|---|---|---|---|
| D1 | **Phase 77 Hardening, docs and integration** (Unit 7 items 1–8): regrade parity doc (0 MET rows stale; each MET needs a named passing test class) against the 0.19.x/0.20.0 tree, write runbooks cowork.md + computer-use.md + owner-actions page, cover 23 missing `.env.example` vars + audit test, add 14 missing projects to architecture.md + audit test, refresh status-and-roadmap + README, re-run and re-date security review (close/OQ-record SEC-77-04, SEC-77-09), close accessibility matrix (named bUnit/headless tests + browser keyboard walks), full gate at final HEAD under flock with dated evidence page | SPE-34836 | **`flock` + `./bootstrap.sh`** (DESIGN §10.8) at final HEAD, no Live tests; `python3 -m unittest scripts/ci/test_check_skip_markers.py scripts/legal/test_generate_sbom.py`, `node --test scripts/web-smoke/a11y/*.test.mjs`, `node scripts/web-smoke/a11y/fixture-selftest.mjs` (DESIGN §10.5), `OUT_DIR="$(mktemp -d)" bash scripts/web-smoke/run-smoke.sh` (DESIGN §10.6) | **OQ-105 / OQ-106** (trademark + code ownership, owner/legal) · **OQ-480** (release tag) · Mermaid vendoring licence OQ-400 (owner/legal) · live model run + production Dispatch enablement OD-07 (owner + Supabase/Railway credentials) · Windows/macOS runners · native OS keyring pairing verification · screen-reader runs NVDA/VoiceOver/TalkBack/Orca (human-verification, item 9) | G1 (classify `build-test` + `android` + `desktop` + push-only `image-scan` at HEAD) · G2 (final gate both attempts recorded) · G3 · G4 (sync drained before close) · G5 (all five legs; `flaked` ≠ `passing`) · **G6 (23 undocumented env vars = gate failure)** · G7 |
| D2 | **Milestone audit + complete + cleanup** (SPE-34836): milestone audit against this plan, close v0.16.0, archive phase files, sync Linear/Notion to final state | SPE-34836 | Fresh `./bootstrap.sh` at the merge HEAD, `gh run list` + check-runs API per G1, five-way agreement re-checked at the final SHA | AUDIT/WARNING-only items enumerated in D1 (OQ-105/106, OQ-480, OD-07) carried as documented remainder, never silently closed | G4 (queue drained; `sync_status: ok`) · G5 (all five legs at final SHA) · G6 · G7 |

### 68-11 atomic erasure PR (the only HITL exception — can land any wave, gated)

Separate, non-splittable PR for `PostgresAccountErasure` entry point /
credential-session revocation / resumable coordinator. Owner decision required:
Candidate A/B + IdP revocation/denial design + independent security review via
74.1 (SEC-77-09); owner acceptance of SEC-77-09 Low risk or the fence decision
for Dragonfly keys outside the erasure fence. Gate: G5 + G7 (schema/composition):
additive-only migration, `DecisionRecordTests`, rollback = previous migration +
previous image, plus **mandatory owner HITL approval recorded in the PR before
merge**. Blocked rows on this unit stay OPEN until the decision lands.

## Hold points (owner / external — never silently dropped)

- **OQ-73** — Railway written confirmation (remote desktops / acceptable-use) +
  hosted placement; live GreptimeDB telemetry (SC4) and hosted Dragonfly/
  Timescale deploy evidence need a podman machine + Railway access.
- **OQ-105 / OQ-106** — trademark + code ownership (owner/legal); also licence
  rows OQ-93, OQ-102 (AGPL variant; agent-swarm / agent-substrate licences).
- **Live Supabase smoke** — credential-gated live sign-in (67's `human_needed`
  remainder); the Wave B3 75-07 account gate / Phase 73 registration stays
  blocked until it lands.
- **Credential / placement choices** — `SUBMODULE_TOKEN` (private
  `plugins/agent-substrate`), Railway deploy scope beyond the staged worker,
  billed `ompos-staging` project (OQ-171), live model turn OD-07.
- 74-11 legacy-row idempotency (owner + read-only production counts); enabling
  Dispatch outside Development (owner, after 74-11 + 74-09/04); OQ-420
  (Railway) / OQ-429 (Android devices); ADR-0035 (owner); 74.1-03 runbook
  review (named reviewer) gates 75-12; OQ-440/441 live omp (OD-07);
  OQ-480 release tag; macOS/Windows host runs (Seatbelt, job objects,
  notification display, screen readers).

## Sync direction (GRAPH-CRITIQUE gap 3)

Planning state → Linear (authoritative tracker per LINEAR-MAP, project
"Autonogrammer GSD roadmap (v0.16.0 phases 66-77)"), gated on VERIFICATION
`passed` at phase HEAD; Notion is historical mirror until write quota is
resolved. On outage: local queue (`.planning/sync-queue/<sink>-queue.jsonl`),
bounded backoff 1m → 5m → 30m → 3h, STATE `sync_status: degraded` marker with
`sync_degraded_sink`, `sync_degraded_since`, `sync_queue_depth`,
`sync_last_success` (G4). No phase moves to Done while degraded.

## Verify (closing tail)

```bash
# Per phase, at its HEAD, in order (DESIGN §10.1–10.8, ci.yml build-test):
dotnet restore Autonogrammer.Linux.slnf --locked-mode -p:TargetLatestRuntimePatch=false -p:RuntimeFrameworkVersion=10.0.13
dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror -p:TargetLatestRuntimePatch=false -p:RuntimeFrameworkVersion=10.0.13
dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore
bash scripts/ci/secret-scan.sh
bash scripts/ci/plugin-node-tests.sh
bash scripts/ci/repo-checks.sh
bash scripts/ci/check-licences.sh
bash scripts/ci/check-sbom.sh
python3 -m unittest scripts/ci/test_check_skip_markers.py scripts/legal/test_generate_sbom.py
node --test scripts/web-smoke/a11y/*.test.mjs && node scripts/web-smoke/a11y/fixture-selftest.mjs
OUT_DIR="$(mktemp -d)" bash scripts/web-smoke/run-smoke.sh
env_file="$(bash scripts/test-services.sh up)"; source "$env_file"
dotnet test Autonogrammer.Linux.slnf -c Release --no-build --max-parallel-test-modules 2 --report-trx --coverlet --coverlet-output-format cobertura --results-directory TestResults -- --filter-not-trait "Category=Live"
bash scripts/test-services.sh down
python3 scripts/ci/test_check_openapi_breaking.py
python3 scripts/ci/check-openapi-breaking.py --base origin/main
bash scripts/ci/check-version-bump.sh --base origin/main
podman-compose --profile backend -f deploy/compose.yaml config -q
gh run list --branch <pr-branch> --limit 20 --json databaseId,headSha,workflowName,status,conclusion,event,displayTitle
gh api repos/{owner}/{repo}/commits/<HEAD_SHA>/check-runs --paginate --jq '.check_runs[] | "\(.name) status=\(.status) conclusion=\(.conclusion) run_id=\(.details_url)"'
./bootstrap.sh
```
