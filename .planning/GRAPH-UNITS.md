# GRAPH-UNITS — v0.16.0 decautonogrammerition into execution units (SPE-35246, Wave 2 · n2)

Date: 2026-11-20. Sequencing source (SOLE): `.planning/GRAPH-STATE-discovery.md`
(Wave 1 output). Scope source (verbatim outstanding/blocked items):
`/tmp/gsd-recon/ALL.json` (recon 2026-11-19). ROADMAP order preserved.

## Ordering and exclusion rule

Units are listed in ROADMAP order: 68, 71, 73, 74, 75, 76, 77.
Excluded — not units of this graph (gates' own closeout order):
66.4 (closed via merge `f5d453d1`) and 67 (closing via PR #24).
Also out of this graph: 66, 66.1–66.3, 69, 70, 72, 74.1 (see Totals mapping).

Dependency-fixed order inside the graph: 74 gates 73 (Dispatch eligibility) and
74.1 gates 75-12; 75-07 account gate feeds 73 registration. 77 is last (it
regrades the parity audit the other units change).

Unit count: 7.

## Unit 1 — Phase 68: docs/M-id fixes, stage-closeout (SPE-34833)

Phase: `68-data-plane-names-schema-and-resilience` ("Data plane: names, schema and
resilience", substantially-complete; 9/11 plans done). 68-09/68-10 are superseded
(design built: M0012 erasure fence, SEC-77-07 fixed on main `7dadceb5`); 68-10 is
done in code (M0012/M0014, `PostgresAccountErasure`). Owner/external remainder
stays OPEN (see Blocked).

Scope — outstanding items (verbatim, 6):
1. `Mark ROADMAP 68-09/68-10 and plan files superseded; replace the 'SEC-77-07
   open' note; update STATE pause record (STATE.md:1099-1105)` (docs, S)
2. `Rewrite stale SC3 grep and 'head M0009' wording; the literal grep matches the
   new prefix, omp markers and the scheme` (docs, S)
3. `Run full phase gate (locked restore, -warnaserror build, licences, non-Live
   Linux tests, DecisionRecordTests) and write a fresh 68-VERIFICATION` (test, M)
4. `Live GreptimeDB telemetry test (SC4)` (human-verification, S)
5. `Production erasure entry point, credential/session revocation, resumable
   coordinator (68-11, SEC-77-09)` (owner-decision, L)
6. `Railway-hosted Dragonfly/Timescale edition and deploy` (external, M)

Blocked (stays open, with reason): `68-11 authorized account-erasure entry point`
— owner approval of Candidate A/B + IdP revocation/denial design + independent
security review (74.1). `Dragonfly keys outside erasure fence` — owner acceptance
of SEC-77-09 Low risk or fence decision. `Live Greptime and hosted deploy
evidence` — podman machine; Railway access (OQ-73).

Owners: agent (items 1–4) / owner (5) / external (6).
Files: `.planning/ROADMAP.md`, `.planning/STATE.md`, 68 plan files,
`68-VERIFICATION.md`, `docs/runbooks/data-plane.md`, `docs/runbooks/accounts.md`.

## Unit 2 — Phase 71: Cowork Artifacts (partial; 6/13 plans done)

Scope — outstanding items (verbatim, 8):
1. `Commit the Native ArtifactBlock producer wiring (ArtifactToolProvider card
   callback, ArtifactsCautonogrammerition.DeliverAsync, AgentSessionRuntime.ShowArtifactCard,
   panel DeliverAsync, ArtifactBlockDeliveryTests)` (code, S)
2. `Mount web artifact UI: route ArtifactBlock to ArtifactCard, open panel with
   ArtifactBody/ArtifactFrame via ArtifactWebClient in the Cowork web view` (code, M)
3. `Persist requested origins and reasons per version (71-07); wire to panel
   RequestedOrigins` (code, L; gated on 71-07 rollout gate)
4. `Durable server grant store replacing in-memory ArtifactGrants; sync producer
   for permissions (71-12)` (code, L; depends on 71-07 and rollout gate)
5. `Run the success-criteria commands on a .NET 10 host: Artifacts.Tests,
   Stores.Tests with test-services.sh, Api artifact tests, UI/Web/Native/
   Presentation artifact tests, ArchitectureTests` (test, S)
6. `Run web-smoke artifact half, check-licences.sh and
   generate-third-party-notices.py --check` (test, S)
7. `Write 71-13-SUMMARY.md, tick roadmap boxes 71-01..06 verified and 71-13,
   refresh 71-VERIFICATION.md` (docs, S)
8. `Live WebKitGTK surface test and desktop open-from-card check`
   (human-verification, S)

Blocked (stays open): OQ-400 mermaid vendoring licence (owner/legal); OQ-401
interactive Html via ticketed API render (owner + OD-03 origin); OQ-403 scripts
on open vs per-click (owner/security); network-origin enforcement 71-08 (design
decision; network stays denied); device/session proof-of-possession for grants
(accounts-phase identity); sync rollout gate 71-07/71-12 (owner confirmation of
deployed receiver versions).

Owners: agent (1, 2, 5–8) / owner (3, 4, all blocked).
Files: `ArtifactToolProvider.cs`, `ArtifactPanelModel.cs`, `ArtifactGrants.cs`,
`ArtifactVersionRecord.cs`, `ArtifactWebClient.cs`, `Components/Cowork/Artifacts/*`,
`CHANGELOG.md`, `71-VERIFICATION.md`, `.planning/ROADMAP.md`.

## Unit 3 — Phase 73: Cowork Computer Use (partial; 5/10 plans done)

Scope — outstanding items (verbatim, 9):
1. `Register ComputerToolProvider (ComputerUseToolsCautonogrammerition.AddTools) into the
   Cowork tool host in AgentHead/CoworkHead for enabled desktop Cowork; Build()
   is never called in production` (code, L; unblocked — 75-07 account gate landed)
2. `Replace the empty indicator chrome callback with the real indicator window
   bound to the shared KillSwitchCoordinator; wire hotkey, session Stop and tray
   Stop` (code, L)
3. `Mount local opt-in control and approval dialog in the head; local-only
   approvals (MustBeLocal) through CoworkApprovalBroker` (code, M)
4. `Local vs Dispatch eligibility: build DispatchComputerUseConfig from Cowork
   options, enforce AllowDispatchSessions, tool class and command-level local
   approval` (code, M; Phase 74 verification is gaps_found → follows Unit 4)
5. `Head-level integration tests: real cautonogrammered host, controllable backend, local
   opt-in, approval, audit before injection, negative matrix (Code/API/web/cloud,
   revoked, remote approve)` (test, L)
6. `Run the six ROADMAP criteria commands and record results; X11 live under
   Xvfb; ADR and licence checks` (test, S)
7. `Refresh ROADMAP Plans list (73-01..06 only) and VERIFICATION; write SUMMARY
   for 73-07..10 as superseded/closed` (docs, S)
8. `User-facing docs and CHANGELOG entry for Computer Use (off by default, kill
   chord, platforms)` (docs, S)
9. `Desktop smoke on a real X11 session; Windows and macOS device smoke`
   (human-verification, M)

Blocked (stays open): ADR-0035 acceptance (owner); Windows/macOS live validation
(machines + accessibility/screen-recording grants); Dispatch-facing closure
(needs Unit 4 verification closed); Wayland/AT-SPI (owner unsupported-or-fund).

Owners: agent (1–8) / owner+hardware (9, blocked).
Files: `ComputerUseHeadCautonogrammerition.cs`, `apps/.../AgentHead.cs`, `CoworkHead`,
`ComputerIndicatorViewModel`, `ComputerApprovalViewModel`, `DispatchComputerUseConfig`,
`73-VERIFICATION.md`, `.planning/ROADMAP.md`, `docs/reference/`, `CHANGELOG.md`.

## Unit 4 — Phase 74: Dispatch relay (partial; 8/11 plans done)

Scope — outstanding items (verbatim, 7):
1. `Native desktop command handlers (Code, Cowork), bind DispatchConnector in
   HeadServices, approval-prompt bridge, OS-backed ISecretStore` (code, L;
   needs Code prompt entry on SessionEngineSet + cowork seam, 74-04 gaps 1–2)
2. `Dispatch.Client: HttpDispatchHost and DispatchSseReader (HTTPS POST + SSE
   resume) with tests` (code, L; unblocked — new `Dispatch.Client` project)
3. `Web head WebDispatchHost, event feed, routes, registration; Native
   DispatchChromeBinding and HeadServices.DispatchRelay; Android factory`
   (code, L; depends on HttpDispatchHost)
4. `Plan 74-04 residual gaps: 403 PairedToAnotherAccount mapping, unknown-frame
   err plus 1008 after three, evt frames, handlers off the receive loop`
   (code, M)
5. `Stale statements: dispatch.md:9 and CHANGELOG:128 say M0010 not registered;
   74-VERIFICATION frontmatter claims hosts exist; ROADMAP 74-01..10 checked`
   (docs, S)
6. `Fresh verification of SC1-SC6 incl. services-up Api and Postgres Dispatch
   tests, then new 74-VERIFICATION.md` (human-verification, M)
7. `Hosted WebSocket drain on Railway edge; native pairing and platform
   keyrings on Windows/macOS; Android controller (compile only)` (external, M)

Blocked (stays open): 74-11 Task 3 legacy-row idempotency policy (owner decision
+ read-only production count access); enabling Dispatch outside Development
(owner approval after 74-11 + 74-09/04 land); OQ-420 hosted draining / OQ-429
Android controller (Railway + devices).

Owners: agent (1–6) / external+owner (7, blocked).
Files: `src/Autonogrammer.Dispatch.Client/` (new), `apps/.../Web/Services/Dispatch/`,
`apps/.../Native/Dispatch/`, `Dispatch.Desktop/` handlers, `PostgresDispatchStore.cs`,
`docs/runbooks/dispatch.md`, `CHANGELOG.md`, `74-VERIFICATION.md`,
`.planning/ROADMAP.md`.

## Unit 5 — Phase 75: Cowork parity A (partial; 4/13 plans done)

Scope — outstanding items (verbatim, 8):
1. `Port or merge the unmerged 75-07/08/09/11/13 Native wiring from the
   divergent branch onto main, then re-verify` (code, L; branch diverged at
   `66d5e95c`, 88 commits each side)
2. `75-10: register MemoryToolProvider in Native, session-scoped gateway
   leases, approval; saves must not revert settings` (code, L; needs 75-07)
3. `Populate CoworkEngineRequest.StandingContext from account and project; carry
   paragraph, first-prompt block and memory.backend off overlay to launch`
   (code, M; needs 75-07)
4. `Register ScheduledCoworkSessionStarter; add true AutomationScheduler tick
   test, cancellation and failed-send tests` (test, M)
5. `Connect project editor to ProjectsView, user-selectable memory mode,
   account-switch teardown test, rollback of Failed row` (code, M)
6. `Rewrite docs/parity/cowork-parity.md and 75-VERIFICATION.md against current
   tree; MET only with a passing named test class` (docs, M)
7. `Run roadmap success criteria 1-6 on a Linux SDK 10 host with throwaway
   services` (test, M)
8. `Add plans 75-07..13 to ROADMAP.md Phase 75 list; write missing 75-09, 75-10
   summaries` (docs, S)

Blocked (stays open): 74.1-03 human runbook review gates 75-12 (named reviewer);
durable project PUT idempotency (owner migration decision); OQ-440/441 omp
`memory.backend off` + `--config` overlay (live omp, OD-07); Auto approval mode
(owner decision); sleep-wake on Windows/macOS (hosts).

Owners: agent (1–8) / owner+external (blocked).
Files: branch `origin/gsd/v0.10.0-cross-platform-ui` port set,
`CoworkAccountContext.cs`, `CoworkFirstPromptStore.cs`, `CoworkEngineRequest.cs`,
`MemoryProviderRegistration.cs`, `ScheduledCoworkSessionStarter.cs`,
`ProjectsView`, `docs/parity/cowork-parity.md`, `75-VERIFICATION.md`,
`.planning/ROADMAP.md`, `.planning/REQUIREMENTS.md`.

## Unit 6 — Phase 76: Cowork parity B (partial; 1/9 plans done)

Scope — outstanding items (verbatim, 7):
1. `Add Bundles, CoworkPermissions, Isolation, Usage, Notifications settings pages
   to SettingsWindowOptions in HeadSettings.cs with sources from
   BundleCautonogrammerition, Activity and isolation` (code, M)
2. `Start Cowork sessions through IsolationCautonogrammerition.PrepareAsync: probe,
   consent prompt, chip, wrapper and grants to CoworkEngineRequest; Code stays
   None` (code, L)
3. `Wire notifications: build NotificationPolicy + OsNotificationSink
   (Linux/macOS), feed NotificationBridge from the event tee, host NotificationsView
   and the sub-agent panel` (code, L)
4. `Prove the bwrap wrapper with IsolationLiveTests (allowed write + denied
   outside write) and record result in runbook and CHANGELOG` (test, M;
   host with user namespaces; bwrap at `/usr/bin/bwrap`)
5. `Grade Phase 76 rows C3-C6, C11-C13, C23, C24, C26, K04, K10, K16, K19, K20
   with named test classes; fix stale text; run parity_counts --check` (docs, M)
6. `Run phase gate: locked restore, -warnaserror build, 11 test projects,
   repo-checks, licences, format; refresh VERIFICATION.md` (test, M)
7. `Mark AG-76-1..12 and AG-76-14 done in REQUIREMENTS.md and check 76-09 in
   ROADMAP only once the items above pass` (docs, S)

Blocked (stays open): macOS Seatbelt + osascript display (owner Mac host);
Windows job-object containment + notification display (runner + owner decision);
omp-flag-dependent rows (owner OD-07 live omp run); human desktop check of
notifications/isolation chip.

Owners: agent (1–7) / owner+hardware (blocked).
Files: `apps/.../Native/Settings/HeadSettings.cs`, `AgentHead.cs`,
`IsolationCautonogrammerition.cs`, `NotificationBridge`, `docs/parity/cowork-parity.md`,
`docs/runbooks/cowork-isolation.md`, `76-VERIFICATION.md`, `CHANGELOG.md`,
`.planning/REQUIREMENTS.md`, `.planning/ROADMAP.md`.

## Unit 7 — Phase 77: Hardening, docs and integration (partial; 1/11 plans done)

Scope — outstanding items (verbatim, 9):
1. `Regrade docs/parity/cowork-parity.md against the 0.19.x/0.20.0 tree; 0 MET
   rows look stale; each MET needs a named passing test class` (docs, M)
2. `Write missing runbooks cowork.md and computer-use.md, plus an owner-actions
   page; list in docs/SUMMARY.md` (docs, M)
3. `Cover the 23 .env.example variables absent from runbooks/reference
   (Dispatch__*, Worker__*, DataPlane__Ledger__Endpoint, Sandbox__Provider, ...)
   and add an audit test` (test, M)
4. `Add the 14 src/services projects missing from docs/architecture.md
   (Coordination, EngineHost, Harness.Plugins, Sessions.Postgres, ...) and a
   project-row audit test` (docs, S)
5. `Refresh docs/guides/status-and-roadmap.md (still v0.15.0, last verified
   2026-10-28) and README to the shipped version` (docs, S)
6. `Re-run and re-date the security review for the current tree; close or
   OQ-record SEC-77-04 (memory idempotency) and SEC-77-09 (erasure entry point)`
   (code, L; SEC-77-04 needs production cautonogrammerition)
7. `Close accessibility matrix Pending cells: run named bUnit/headless tests for
   Account menu, Devices, Dispatch; add browser keyboard walks` (test, M)
8. `Run the full gate (./bootstrap.sh) at the final head under the flock wrapper
   with no Live tests and commit a dated evidence page` (test, M)
9. `Screen-reader (NVDA/VoiceOver/TalkBack/Orca), Windows/macOS and device runs`
   (human-verification, L)

Blocked (stays open): live model run + production Dispatch enablement OD-07
(owner + Supabase/Railway credentials); release tag OQ-480 + Windows/macOS
runners; Mermaid vendoring licence OQ-400 (owner/legal); native OS keyring
pairing verification (desktop + human run).

Owners: agent (1–8) / owner+external (9, blocked).
Files: `docs/parity/cowork-parity.md`, `docs/runbooks/cowork.md`,
`computer-use.md`, owner-actions page, `docs/architecture.md`,
`docs/guides/status-and-roadmap.md`, `README`, `docs/quality/v0.16.0-security-review.md`,
`docs/quality/v0.16.0-accessibility-pass.md`, `CoworkAuditTests.*`, dated gate
evidence, `CHANGELOG.md`, `.planning/ROADMAP.md`.

## Serialization notes (shared files → run serially)

- `.planning/ROADMAP.md` + `.planning/STATE.md`: touched by ALL seven units
  (phase list + state updates). Plan-doc reconciliation must be owned by one
  unit at a time — never parallel ROADMAP edits. Natural order: unit N's
  planning edits land before unit N+1 starts, or a single docs pass at the end
  (Units 1, 7 are docs-heavy).
- `docs/parity/cowork-parity.md`: Units 5 (75), 6 (76) and 7 (77) all regrade it
  (0 MET baseline). SERIAL in order 75 → 76 → 77; 77 runs last and regrades
  everything the earlier units landed.
- `CHANGELOG.md`: Units 2 (71), 3 (73), 4 (74), 6 (76), 7 (77) append entries.
  Append-only → keep one writer per wave, or merge conflicts if parallel.
- Native head surface (`AgentHead.cs`, `CoworkHead`, `HeadSettings.cs`,
  `SettingsWindowOptions`): Unit 3 (73 registers tools in AgentHead/CoworkHead)
  and Unit 6 (76 populates five settings pages, AgentHead IsolationLevel)
  edit the same files → SERIAL 73 before 76.
- `CoworkEngineRequest` / launch seam: Unit 5 sets `StandingContext`; Unit 6
  passes wrapper + grants (ProcessWrapper) → SERIAL 75 before 76.
- Cross-unit gates (code dependencies, not just file overlap): 74 verification
  (Unit 4) gates 73 Dispatch eligibility (Unit 3, item 4) and, via 74.1, 75-12
  (Unit 5 blocked item). Order code units 71 → 74 → 73/75 → 76 → 77 so that
  74's fresh VERIFICATION exists before 73's eligibility wiring.
- Disjoint units MAY run in parallel: Unit 1 (68, docs-only + own gate) is
  disjoint from 71/73/74 code work except ROADMAP/STATE (serialize those
  files). Unit 2 (71 artifacts, own projects + web components) is disjoint from
  Unit 4 (74: new Dispatch.Client project + dispatch routes). Run-owned
  podman test services are per-gate (Unit 1 item 3, Unit 4 item 6, Unit 7
  item 8) — never shared containers between concurrent gates (68-06/77-07
  history).

Suggested execution waves: Wave A = {Unit 1, Unit 2, Unit 4-code} (disjoint
files); Wave B = {Unit 4-verify → Unit 3, Unit 5} ; Wave C = {Unit 6} ; Wave
D = {Unit 7}.

## Totals mapping (107 outstanding / 64 blocked, ALL.json)

In-scope units (68+71+73+74+75+76+77): **54 outstanding + 29 blocked**.
Excluded phases: 66 (4/6), 66.1 (2/0), 66.2 (5/5), 66.3 (6/5), 66.4 (6/4),
67 (7/5), 69 (4/2), 70 (6/3), 72 (9/3), 74.1 (4/2) → **53 outstanding +
35 blocked**. 54+29+53+35 = 107 + 64. ✓ (66.4/67 accounts 13/9 of the
excluded bucket — they are the gates' own closeout scope, not this graph.)

In-scope outstanding buckets (by blocker/kind): unblocked (no blocker) 20 ·
gated-on-another-unit 21 · host/test-infra 12 · owner-decision 1. All 29
in-scope blocked items stay OPEN in their units with the reason named above;
every Phase 74/75/73 item that depends on a still-gaps_found VERIFICATION is
carried as that unit's blocked row, not silently dropped.
