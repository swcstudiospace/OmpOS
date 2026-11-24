# v0.16.0 outstanding-phase gap inventory — 2026-11-24

Lane V1 (read-only inventory). Repo `autonogrammer` @ `b0568d95`, branch
`gsd/v0160-wave4-computer`. No tests, builds or live turns were run for this
inventory (OD-07 respected); nothing below is a new verification result.

## Sources and counting rule

- One `<N>-VERIFICATION.md` per phase for phases **67, 68, 70, 71, 72, 73, 74,
  74.1, 75, 76, 77, 78** (`.planning/phases/<dir>/`), read at their latest
  refreshed state (frontmatter `verified: 2026-11-22`, HEADs `b46fd81f` /
  `52eb2c55` as recorded in each file).
- `.planning/STATE.md` — "Reconciliation 2026-11-19" and "Deferred
  Verification" sections (see Cross-checks).
- **Counting rule.** One inventory row = one distinct open gap, taken from the
  frontmatter `gaps:` list, plus the frontmatter `human_verification:` list,
  plus body-only gap items (success-criteria / Gaps sections) that are not
  already in the frontmatter. Overlaps between `gaps:` and
  `human_verification:` are counted once and marked. Frontmatter totals across
  the 12 files: **19 `gaps:` rows** and **14 `human_verification:` strings**;
  after de-duplication and body-only additions the inventory has **34 open
  rows** (plus 3 accounted-but-not-open notes: 70-H note, 68-09 hold note,
  77 body gap 5).
- **Classes.** `Code-fixable` = closable by code/docs/test-run work on this
  Linux host (includes pure evidence runs: the defect is the missing run, not
  missing code — marked "evidence run"). `Human-only` = needs a screen
  reader, Windows/macOS/Android host, live credential/model, real display, or
  an owner decision/approval. `Held` = blocked on a standing hold
  (68-09, 74.1 independent human runbook review, 75-12, SEC-77-01 /
  SEC-77-07 / SEC-77-08).
- Closing steps are quoted/paraphrased from the VERIFICATION file's own
  `closing_step` / Gaps text, not invented here.

## Summary counts

| Phase | VERIFICATION status | Open rows | Code-fixable | Human-only | Held |
|---|---|---:|---:|---:|---:|
| 67 Accounts (Supabase sign-in) | `human_needed` | 3 | 0 | 3 | 0 |
| 68 Data plane | `gaps_found` | 3 | 1 | 2 | 0 |
| 70 Cowork chat core | `passed` (5/5) | 0 | 0 | 0 | 0 |
| 71 Cowork Artifacts | `gaps_found` | 2 | 1 | 1 | 0 |
| 72 Cowork Browser Use | `gaps_found` | 3 | 2 | 1 | 0 |
| 73 Cowork Computer Use | `gaps_found` | 2 | 1 | 1 | 0 |
| 74 Dispatch relay | `gaps_found` | 4 | 2 | 2 | 0 |
| 74.1 Memory idempotency | `gaps_found` | 2 | 0 | 0 | 2 |
| 75 Projects/memory/schedules | `gaps_found` | 5 | 3 | 1 | 1 |
| 76 Plugins/auto/isolation | `gaps_found` | 4 | 2 | 1 | 1 |
| 77 Hardening/docs/integration | `gaps_found` | 5 | 3 | 1 | 1 |
| 78 Desktop release pipeline | `gaps_found` (SC6 only) | 1 | 0 | 1 | 0 |
| **Total** | 1 passed, 1 human_needed, 10 gaps_found | **34** | **15** | **14** | **5** |

Code-fixable shortlist (15), ordered by phase: **68-G1, 71-G1, 72-G1, 72-G2,
73-G1, 74-G1, 74-H1, 75-G1, 75-G3, 75-G4, 76-G2, 76-G3, 77-G2, 77-G3, 77-G4**
— full one-line list at the end of this report.

## Phase 67 — Accounts: Supabase passwordless sign-in (`human_needed`, 3 rows)

Frontmatter: 0 `gaps:`, 3 `human_verification:` ("Gaps: None beyond
human_verification"). Offline 7/8 criteria re-proven 2026-11-22.

| ID | One-line defect | Closing step stated in file | Class |
|---|---|---|---|
| 67-H1 | Live Supabase sign-in never run (fakes only) | "Live Supabase sign-in (real key + test email) or recorded skip" | Human-only (live credential) |
| 67-H2 | Keyring round-trip SC3 FAILED 1/1 (no Secret Service daemon; neither passed nor skipped) | "Keyring round-trip (`AUTONOGRAMMER_LIVE_KEYRING=1`) or skip reason" | Human-only (desktop Secret Service host / recorded skip) |
| 67-H3 | Full CI-shape gate + account UI visual/a11y session not recorded | "Full gate vs throwaway services (CI shape) + account UI visual/a11y session" | Human-only as filed (file lists under "Human (never automated)"); the full-gate half alone is a Linux evidence run |

## Phase 68 — Data plane: names, schema and resilience (`gaps_found`, 3 rows + 1 hold note)

Frontmatter: 3 `gaps:`, 1 `human_verification:` (overlaps 68-G1/68-G2, counted
once). `behavior_unverified: 3` matches the 3 rows.

| ID | One-line defect | Closing step stated in file | Class |
|---|---|---|---|
| 68-G1 | Full 61-project Linux set not run; targeted only | "test-services up; source; dotnet test slnf --filter-not-trait Category=Live; down" | Code-fixable (evidence run) |
| 68-G2 | Live Greptime OTLP to `autonogrammer` DB not proven (SC4) | "`AUTONOGRAMMER_LIVE_GREPTIME=1` dotnet test --filter-trait Category=Live on podman host" | Human-only as filed (live-service host run) |
| 68-G3 | Erasure production entry point not proven end-to-end; no production caller (SEC-77-09, Low): no route/command/IdP revocation, Dragonfly keys unfenced, typed AE001 refusal mapping missing in 4 stores | "Wire + prove authorized erasure entry point vs throwaway services; record run" — body adds: owner decision on entry point + credential revocation first | Human-only (owner decision blocks the wiring; wiring itself is then Linux code work) |

Hold note (accounted, not counted open): **68-09** (erasure architecture /
SEC-77-07 authority) — `68-09-SUMMARY.md` records the decision step completed
(all-writer fence approved, M0012/M0014), implementation in 68-10/68-11, and
the 2026-11-21 re-verification records **SEC-77-07 fixed in code** (fresh
re-execution inconclusive, disk-full). What remains of the 68-09 thread is the
undecided caller choice, which is row 68-G3. STATE.md's older 68-09 pause
record (2026-11-12) is superseded by these records.

## Phase 70 — Cowork chat core and tool host (`passed`, 0 rows)

Frontmatter: `status: passed`, 5/5 ROADMAP criteria re-proven executed
2026-11-22 at `52eb2c55`; "Gaps: None beyond human_verification."

| ID | One-line defect | Closing step stated in file | Class |
|---|---|---|---|
| 70-H (note) | Live omp turn with real model never run | None — file states "(NOT a ROADMAP criterion; never claimed)" | Human-only (live model, OD-07) — non-blocking, not counted as an open gap |

## Phase 71 — Cowork Artifacts (`gaps_found`, 2 rows)

Frontmatter: 1 `gaps:`, 1 `human_verification:`. 7/7 runnable ROADMAP criteria
pass as executed; phase goal "partially achieved".

| ID | One-line defect | Closing step stated in file | Class |
|---|---|---|---|
| 71-G1 | No production web opener for artifact access dialog; plans 71-07..71-12 blocked/unexecuted (rollout gate, device/session proof protocol, private-address/DNS-rebinding enforcement); AG-71-17 open | "Land 71-07..71-12; add opener; record browser proof" | Code-fixable (large: 6 plans of design + code on Linux) |
| 71-H1 | Live surface destroy (hide/stop/signout) + panel visual/keyboard/a11y unconfirmed; OQ-400 (Mermaid admission) and OQ-401 undecided | "Live surface hide/stop/signout + panel visual/a11y; OQ-400/401 decisions" | Human-only (live platform run + owner decisions) |

## Phase 72 — Cowork Browser Use (`gaps_found`, 3 rows)

Frontmatter: 3 `gaps:`, 1 `human_verification:` (overlaps 72-G2). Offline
124/124 pass; live criteria 2/6/7 fail, 3 skipped (2026-11-21 executed).

| ID | One-line defect | Closing step stated in file | Class |
|---|---|---|---|
| 72-G1 | Production `Wire` omits Configure/Pages; `browser_*` tools not offered; egress proxy absent (72-12); real broker/audit unreachable for browser tools | "Pass Configure+Pages/broker/audit; wire egress; re-run heads" | Code-fixable |
| 72-G2 | Live CDP tests fail on host (2/2, PuppeteerSharp connection); headful runs zero tests without `CHROME_BIN`; live Site/Corpus/Screenshot classes missing (72-09) | "Run live/headful with CHROME_BIN on network host; record" | Code-fixable (Linux + Chrome defect/run; no foreign OS or credential needed) |
| 72-G3 | 72-13/72-14 unexecuted: D-12 BlockList vs D-13 Strict Allowlist are mutually exclusive in pinned PuppeteerSharp 25.15.0 and owner retained both | "Resolve D-12/D-13; execute 72-13/72-14" (candidate must prove both boundaries compose, or one requirement is relaxed) | Human-only (owner policy decision; implementation proof follows on Linux) |

Cross-reference: SEC-77-01 (Phase 72 browser/tool policy) is inventoried under
77-G1 as Held; it is the security face of 72-G1's wiring.

## Phase 73 — Cowork Computer Use (`gaps_found`, 2 rows)

Frontmatter: 1 `gaps:`, 1 `human_verification:`. ROADMAP criteria 1–6 all pass
as executed (core 90/90, X11 live 7/7, Win/macOS compile-only).

| ID | One-line defect | Closing step stated in file | Class |
|---|---|---|---|
| 73-G1 | No Computer Use provider registered from the running head; kill-switch/indicator not a verified kill action; plans 73-07/73-09/73-10 blocked (73-09/10 on Phase 75 prerequisites) | "Register provider + kill wiring; record gate; desktop smoke" | Code-fixable (Linux wiring; sequenced after its Phase 75 prerequisites) |
| 73-H1 | Real-display topmost/approval/Stop behaviour and Windows/macOS runtime behaviour unproven (compile-only) | "Real-display topmost/approval/Stop; Win/macOS runtime" — desktop smoke on a real Cowork session after 73-G1 | Human-only (real display + Windows/macOS hosts) |

## Phase 74 — Dispatch relay (`gaps_found`, 4 rows)

Frontmatter: 1 `gaps:`, 1 `human_verification:` string carrying two items
(74-H1, 74-H2 below); 74-H3 is body-only ("Human verification required").

| ID | One-line defect | Closing step stated in file | Class |
|---|---|---|---|
| 74-G1 | 74-11 store idempotency projection not shipped: `InsertCommandAsync` projects `IdempotencyKey ?? CommandId`, conflating keyed/unkeyed identity; plan's M0010 text stale vs registered M0010–M0015 | "Ship 74-11; record projection run" (74-11-SUMMARY: nullable `idempotency_key`, ambiguous legacy rows fail, regression cases, throwaway-services DB gate, rewrite migration section) | Code-fixable |
| 74-H1 | Full `bootstrap.sh` CI gate on PR not recorded for this phase | "Full bootstrap.sh CI gate on PR" | Code-fixable (evidence run; filed under human_verification but needs no human-only resource) |
| 74-H2 | Ambiguous legacy-row policy undecided (owner kept affected production path disabled, STATE.md 2026-11-16) | "legacy-row owner decision" | Human-only (owner decision) |
| 74-H3 | Real phone/Android controller and Windows/macOS credential-store behaviour unverified | Body: "remain OS-verification items; never claimed passed here" | Human-only (Android + Windows/macOS hosts) |

Note: the body's earlier SC2/SC6 "not executed" items (DB-backed Dispatch
suites, GuardTests) are superseded by the 2026-11-22 refresh ("DB execution
now proven", Dispatch 127/127, Guard 12/12) and are not open rows.

## Phase 74.1 — Durable memory idempotency (`gaps_found`, 2 rows, both Held)

Frontmatter: 1 `gaps:`, 1 `human_verification:`. Production migration never
authorized.

| ID | One-line defect | Closing step stated in file | Class |
|---|---|---|---|
| 74.1-G1 | Plan 03 (retention/erasure/rollback/operations) unticked; no shipped implementation | "Execute plan 03 or descope in ROADMAP; re-verify" | Held (74.1 independent human runbook review hold governs this phase; execution/descope follows it) |
| 74.1-H1 | Integrated migration/rollback/isolation/concurrency rerun (plans 01/02/04) not done; independent human runbook review still pending | "Integrated migration/rollback rerun + human runbook review hold" | Held (74.1 review; the rerun itself is Linux-runnable once the review lands) |

## Phase 75 — Cowork parity: projects, memory, schedules (`gaps_found`, 5 rows)

Frontmatter: 2 `gaps:`, 1 `human_verification:`; 75-G3/75-G4 are body-only
success criteria 8 and 6.

| ID | One-line defect | Closing step stated in file | Class |
|---|---|---|---|
| 75-G1 | Memory.Postgres depth thin (1/1); phase-gate DB depth not proven | "Run PG + Api Cowork/Project/Memory/Schedule filters vs services; record" | Code-fixable (evidence run) |
| 75-G2 | SEC-77-02 (75-12, project-scoped memory) recorded open in this file | "Close SEC-77-02 with owner; record live run" | Held (75-12 hold — held until 74.1 verification per STATE.md; see discrepancy note) |
| 75-G3 | Scheduled Cowork tick (75-11) not shipped: `ScheduledCoworkSessionStarter` + tests absent | Body SC8: "NOT SHIPPED" (land 75-11 starter + tests per 75-11-PLAN) | Code-fixable |
| 75-G4 | Parity rows for 75-07/75-09/75-10/75-11/75-13 lack the named passing tests their plans require; must stay out of MET | Body SC6: rows stay un-MET "until their tests land" | Code-fixable (land named tests; overlaps 75-G3 for the 75-11 row) |
| 75-H1 | Live omp `memory.backend: off` consumption + wake-from-real-sleep unverified (OD-07); owner runbook review open | "Live memory/schedule + owner runbook review" | Human-only (live model + real-sleep host + owner review) |

Note: body's SC3/SC5 partials (Api Cowork suites, repo-checks timeout) are
superseded as runs by the 2026-11-22 refresh (suites pass); their residual
substance is 75-G1's depth point, counted once.

## Phase 76 — Cowork parity: plugins, auto, isolation (`gaps_found`, 4 rows)

Frontmatter: 2 `gaps:` (76-G1, and the vague "two partial criteria" 76-G2 —
mapped here to body SC1's named un-run suites), 1 `human_verification:`;
76-G3 is body SC5.

| ID | One-line defect | Closing step stated in file | Class |
|---|---|---|---|
| 76-G1 | Live Bubblewrap isolation not executed; wrapper defects remain (deny-mask precedence, missing-target/socket handling, nested read-only grants, /tmp ordering; 76-09); SEC-77-08 (probe promoted to `FilesystemIsolated` before session policy exercised) | "Run live isolation suites on provisioned host; record" — body: `AUTONOGRAMMER_LIVE_ISOLATION=1` positive control in disposable-/etc outer Bubblewrap, after the wrapper design lands (76-09) | Held (SEC-77-08; see discrepancy note — wrapper work itself is Linux code) |
| 76-G2 | Named suites not run by the verification lane: PluginBundles, Notifications, Mcp, Acp, Workspace, ContractTests, Abstractions ("two partial criteria need host proof") | "Complete composed run; record" | Code-fixable (evidence runs) |
| 76-G3 | Parity MET audit held: omp-consumed rows and rows depending on unshipped 75-07..13 / 76-09 must remain PARTIAL | Body SC5: audit against current test names + live-run records for omp-consumed rows | Code-fixable (audit + tests; depends on 75-G3/G4 and 76-G1 landing first) |
| 76-H1 | Auto-mode rules behaviour source-verified only (live model, OD-07); `./bootstrap.sh` owner run not done | "Live isolation + on-host auto runs"; body SC4: repo-checks on idle disk + bootstrap owner run | Human-only (live model + owner gate run) |

## Phase 77 — Hardening, docs and integration (`gaps_found`, 5 rows + 1 accounted)

Frontmatter: 4 `gaps:`, 1 `human_verification:`; body lists 6 numbered gaps —
body gap 5 is accounted below as not-open, body gap 6 folds into 77-H1.

| ID | One-line defect | Closing step stated in file | Class |
|---|---|---|---|
| 77-G1 | Security review open per this file: SEC-77-01 / SEC-77-02 / SEC-77-04 (AG-77-10/11); architecture security audit fails while any High is open | "Close with owners; never downgrade while High open" (owners: 72, 75-12, 74.1/75) | Held (SEC-77-01 held; SEC-77-02 via 75-12 hold; SEC-77-04 via 74.1 review hold — see discrepancy note) |
| 77-G2 | Runbooks `cowork.md`, `computer-use.md`, `v0.16.0-owner-actions.md` absent; `dispatch.md`/`browser-use.md` are stubs (AG-77-5) | "Author pages; complete stubs; run CoworkAuditTests.Security" | Code-fixable (docs authoring) |
| 77-G3 | `v0.16.0-parity-audit-elevation.md` missing + no SOTA Gap Closure Elevation section (77-09), as stated in the file | "Write elevation file + SOTA section" | Code-fixable (docs authoring) |
| 77-G4 | A11y matrix cells Pending/Not established; no a11y-report bundle recorded on HEAD (AG-77-13, OQ-483) | "Record bundle on HEAD; fill matrix; keep screen-reader human" (`run-smoke.sh --a11y-report <dir>`) | Code-fixable for the bundle/matrix; the screen-reader remainder is 77-H1 |
| 77-H1 | Screen-reader pass (OQ-483), populated-device a11y, OS keyring + native pairing on real desktop, `Accounts:Enabled=true` operator transition, owner review of the three runbooks (incl. body gap 6 / 77-11 live items) | Frontmatter human_verification string, verbatim items | Human-only (screen reader, real desktop/device, operator + owner actions) |

Accounted, not open: body gap 5 (77-06 "exactly 1 version-history commit"
assertion) — the file itself records it retired by superseding cuts
(VersionPrefix now 0.20.x); no closing action stated or needed.

## Phase 78 — Desktop release pipeline / Windows installer (`gaps_found`, SC6 only, 1 row)

Frontmatter: 1 `gaps:`, 1 `human_verification:` (same item, counted once).
SC1–SC5 re-proven 2026-11-22 (clean-HEAD recheck); the 2026-11-21 prose PASS
is superseded by the gaps_found frontmatter because SC6 is open.

| ID | One-line defect | Closing step stated in file | Class |
|---|---|---|---|
| 78-G1 | SC6 human checklist for Windows/macOS installs not recorded (AG-78-11); v0.20.0 asset check not re-run (only v0.20.0-rc.1 downloaded + verified) | "A person records docs/guides/install.md checklist result; then gh release download v0.20.0 and verify_desktop_assets.py + sha256sum -c pass" | Human-only (Windows/macOS install hosts; the asset re-download half is Linux-runnable after the checklist) |

## Cross-checks against STATE.md

- **Reconciliation 2026-11-19** (phases 66–77, 149 plans): verdicts for this
  inventory's phases — substantially complete: 67, 68, 74.1; partial: 70, 71,
  73, 74, 75, 76, 77; blocked on an owner action: 72. Its totals (107
  outstanding items, 64 blocked items, plan granularity across 17 phases) are
  a different counting unit from this report's VERIFICATION gap rows and are
  not expected to reconcile arithmetically. It explicitly verified no phase
  (`completed_phases` stays 0); the later 2026-11-22 refreshes supersede its
  per-phase readings where they differ (e.g. 70 now `passed`).
- **Deferred Verification table** lists only 66, 66.1, 66.2, 66.3, 66.4 and 69
  — none of phases 67–78 in this inventory appears there, so no gap row above
  is covered by a deferred-verification entry.
- **Standing holds as recorded in STATE.md**: 74.1 independent human runbook
  review not recorded (holds 75-12, which is "held until its verification");
  owner kept the Phase 74 production path disabled with the legacy-row policy
  undecided (74-H2); no live model turn approved (OD-07 — 70-H note, 75-H1,
  76-H1); no Windows/macOS/Android host (73-H1, 74-H3, 78-G1); no live
  Supabase key (67-H1).
- **Discrepancy flagged, not resolved here:** the VERIFICATION files
  (2026-11-22) record SEC-77-01/02/04 (77), SEC-77-02 (75) as open and live
  isolation (76) as not executed, while `docs/quality/v0.16.0-security-review.md`
  currently marks SEC-77-01, SEC-77-02, SEC-77-07 and SEC-77-08 `fixed`
  (SEC-77-04 Medium and SEC-77-09 Low open; "No Critical or High finding is
  open"), and 68's own 2026-11-21 tail records SEC-77-07 fixed. Per the
  assignment, SEC-77-01/SEC-77-07/SEC-77-08, 68-09, the 74.1 review and 75-12
  are classified **Held** above regardless; reconciling the stale
  VERIFICATION wording against the security review is follow-up work for the
  owning lanes, not done by this inventory. (Also observed while listing the
  target directory: `docs/quality/v0.16.0-parity-audit-elevation.md` exists on
  disk today although 77-VERIFICATION calls it missing — contents not assessed
  here; 77-G3 stands as filed until re-verified.)

## Code-fixable shortlist (15, ordered by phase)

1. **68-G1** — run the full 61-project non-Live Linux set vs throwaway services and record it (evidence run).
2. **71-G1** — land 71-07..71-12 (grant store, identity proof, destination/DNS-rebinding enforcement, web client/composition) + production web opener + browser proof (AG-71-17).
3. **72-G1** — production Browser Use wiring: Configure+Pages/broker/audit through `Wire`, egress proxy, offer `browser_*` tools (72-12).
4. **72-G2** — fix/record live CDP + headful runs with `CHROME_BIN` (incl. missing 72-09 live classes).
5. **73-G1** — register Computer Use provider in the head + verified kill-switch/indicator wiring (73-07/73-09/73-10).
6. **74-G1** — ship 74-11: separate keyed/unkeyed idempotency identity in `PostgresDispatchStore`, regression cases, DB gate.
7. **74-H1** — record a full `bootstrap.sh` CI gate run on the PR (evidence run).
8. **75-G1** — run PG + Api Cowork/Project/Memory/Schedule filters vs services to phase-gate depth (evidence run).
9. **75-G3** — ship 75-11 scheduled Cowork tick (`ScheduledCoworkSessionStarter` + tests).
10. **75-G4** — land the named parity tests for 75-07/75-09/75-10/75-11/75-13 rows.
11. **76-G2** — run the un-run named suites (PluginBundles, Notifications, Mcp, Acp, Workspace, ContractTests, Abstractions) (evidence runs).
12. **76-G3** — parity MET audit against current test names (after 75-G3/G4, 76-G1).
13. **77-G2** — author `cowork.md`, `computer-use.md`, `v0.16.0-owner-actions.md`; complete `dispatch.md`/`browser-use.md` stubs.
14. **77-G3** — write the parity-audit elevation file + SOTA elevation section (as filed; see discrepancy note).
15. **77-G4** — record the a11y-report bundle on HEAD and fill the Pending matrix cells (screen-reader part stays human, 77-H1).
