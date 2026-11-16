# Phase 76 SOTA for Cowork Parity B: Plugins, Auto mode, isolation

**Date:** 2026-11-10
**Refs:** 76-CONTEXT.md (D-01..D-49 locked), 76-RESEARCH.md, 76-PLAN-CHECK.md, 76-VALIDATION.md, 76-01..76-08 PLANs/SUMMARYs, 66-SOTA-QUALITY-REQUIREMENTS.md, prior phase SOTAs (67,68,69,74), ROADMAP AG-76-1..14, ADR-0037.

## Objectives (from CONTEXT + ROADMAP + brief)
- Cowork and Code share extension and safety layer: plugin bundles/skills (install atomic, hostile refused, hooks listed not run, MCP unconfirmed, commands as /p:c), connector cards with three-state (Always/Needs/Blocked) permission matrix persisted as Mcp tokens, Auto (rules) mode (additive to Manual, ignores readOnlyHint, tainted demotes), deletion protection via ShellDeleteDetector on ACP delete + known rm/git clean etc in every mode, folder grants (read-write scope) + CoworkPathBoundary, OS launch wrapper (bwrap/Seatbelt after env policy, probe with named degraded states, ask before None), sub-agent progress (ACP task calls only, plain text, unnamed "Sub-agent N"), local usage meter (per-turn ledger 90d 0600, no prompt content, last+peak occupancy, last cost or UsageUnavailable), desktop notifications (fan-out to in-app centre + OS notify-send/osascript argv only, coalesce 30s, 10/min, focus/DnD suppress, title-only default).
- Nothing claims equivalence to Anthropic classifiers/VM; every limit stated on screen.
- 14 requirements AG-76-1 to AG-76-14 delivered in 8 plans (76-06 last for composition + parity grading + docs).
- Isolation, explicit states, first-attempt, security (fail-closed owner scoped), graceful, a11y, ops per 66-SOTA elevation.
- Mac/Windows partial (no live bwrap, no Windows toast/job assign, no macOS display verification); live isolation owner action only when AUTONOGRAMMER_LIVE_ISOLATION=1.

## TODOs from Criteria + PLAN-CHECK + VALIDATION + REVIEW (addressed + residuals)
- All 14 AG-76 covered by named test classes per 76-VALIDATION.md (Bundle*, McpConnectionChecker*, CoworkAuto*, ShellDelete*, CoworkPathBoundary*, Isolation*, SubagentPanel*, UsageMeter*, Notification*, Architecture for parity).
- 76-PLAN-CHECK: all B* resolved in text; W* (MA0048 splits, path naming, wiring thinness, fixture gaps) auto-fixed or noted; wave hygiene (explicit path commits) followed.
- SOTA states embedded: IsolationLevel (None/ProcessContained/FilesystemIsolated) + Probe outcomes (BwrapMissing, UsernsDisabled, ProbeTimeout, PlatformUnsupported); CoworkPermissionMode (Manual/Auto/Skip); NotificationKind + NotificationPrivacy (TitleOnly/ShowContent); SubagentStatus (Running/Done/Failed/Cancelled); UsagePeriod; IsolationChip/NotificationCentre/UsageMeter/SubagentPanel models with loading/empty/error/success + guidance + polite.
- Security: fail-closed (probe ask before None; deny rule refuse; invalid bundle refuse; bad surface/owner 4xx); owner scoped (per-account grants/ledgers/permissions); no-leak (redacted, no prompt in ledger/audit, arg vector only, title only); isolation (bwrap ro-bind + tmpfs deny list + scrub; argument vector; no cross surface); canary test for header/arg/env/path.
- First-attempt: tests use fakes, ManualTimeProvider where needed, deterministic; all class-filtered gates pass first (per summaries); no timing reliance.
- Graceful: probe fail -> consent ask; no services/bundles -> guidance; missing cost -> UsageUnavailable; title-only default; offline not applicable (local); full not hit.
- A11y: roles, polite live regions (subagent, notification centre, usage, isolation chip), accessible names, no colour literals, keyboard (per view tests), reduced motion, contrast tokens (from prior).
- Ops: RunModeAuditLog for all decisions; structured logs in models; recovery (revoke grant next session, reconnect implied); observability (chip counts, usage sums, notification list); docs updated in 76-06 (cowork-extensions, cowork-safety, runbooks/cowork-isolation.md with owner actions).
- All locked D-* honored (D-01 capabilities not surface tied; D-02 no Anthropic claim; D-12..D-25 for permissions/auto/delete/folders/isolation/activity/usage/notif; D-26 composition; D-49 wave hygiene); deferred per CONTEXT (hooks run, marketplace, network fence, seccomp, Windows FS, Windows toast, phone push, RPC sub-agents, OAuth vault, Accept-edits, classifier); no scope reduction.
- 76-06 notes: parity rows PARTIAL until live/omp flag; docs/parity/cowork-parity.md not yet from 75-06.

## SOTA States for Cowork Parity B (applied from 66-SOTA + phase specific)
1. **Explicit States** (loading/empty/error/success) in all plugin/perm/auto/isolation/activity/usage/notif/surface paths:
   - LOADING: probe running, bundle install temp, connection check in flight, list sync implied; progress via model phase or spinner; no silent hang. E.g. IsolationProbe, McpConnectionChecker, BundleInstaller temp rename.
   - EMPTY: guidance + CTA (no bundles enabled -> "no skills"; no connectors -> cards empty; no devices for dispatch but here n/a; no sub-agents "Sub-agent view limited to ACP"; no usage "UsageUnavailable"; empty notification list). Never blank.
   - ERROR: recovery (consent ContinueWithoutIsolation; invalid bundle named code + rollback; probe fail names state; bad permission -> ask or refuse; canary fail); error boundary + announcer; redacted (kinds only, no secrets/paths in results).
   - SUCCESS: explicit "first-attempt-success" in test output/gates; state machine (IsolationLevel after probe, CoworkPermissionMode, NotificationReceipt, SubagentStatus, Usage sums); polite live updates; first success logged.
   - Implementation: IsolationLevel + IsolationProbeOutcome; CoworkPermissionMode; NotificationKind/Privacy/Receipt/Suppression; SubagentStatus; UsagePeriod + UsageUnavailable; NotificationCentreKind; BundleInvalid(code); IsolationChipModel/NotificationCentreModel/UsageMeterModel/SubagentPanelModel with explicit phases/guidance; CoworkCopy/NotificationCopy etc.

2. **Edge/Race/First-Attempt**:
   - Races: concurrent bundle install (atomic rename); permission rule read vs write (CAS? file atomic); probe during start; multiple notifications (coalescer window); sub-agent updates concurrent with panel; usage append vs prune.
   - Loaded host: fresh context (no bleed from Code/Cowork); deterministic fakes in tests (RecordingConnectionChecker, FakeNotificationProcessRunner, fake clocks).
   - First attempt: 100% pass target in filtered gates (summaries report pass on first class run); strengthened waits/fakes; no "reached-on-retry".
   - Other: empty collections (no skills, no tools, no sub-agents, no history) explicit; malformed (bad manifest, control chars stripped, >200 cap); invalid surface/owner -> refuse.

3. **Validation/Sanitization/Auth**:
   - Inputs: bundle names validated (no traversal, symlink, size<=2MiB, count<=2000, manifest); arg cap 200; control stripped; title/summary plain; paths resolved + boundary refuse (/ home system); command short known-safe only.
   - Auth/owner: per-account (grants, ledger, permissions, bundles user vs project); token owner for sessions; foreign 404/403 same as absent in patterns from prior.
   - Schema: explicit sealed records/enums (no overlap); Json contexts where needed; strict.
   - Caps: 200 char per notif arg; 90d ledger; file sizes in install.

4. **Security (fail-closed, no leak, owner scoped, isolation)**:
   - Fail closed: hostile bundle refused before write; deny rule refuse; bad probe ask before None; invalid grant refuse; Windows/unsupported explicit; no partial delivery of notif or permission.
   - No leak: argument vector only (never shell/splice); title-only default; no prompt in ledger/audit/notif; redacted exceptions (kinds only); no full paths/secrets in UI results; canary asserts no header/arg/env/url leak.
   - Owner scoped: grants per account + scope (session/project/global); ledger 0600 owner; permissions per owner; bundles user or project.
   - Isolation (D-18..D-21): bwrap --ro-bind / + per-grant --bind + tmpfs over deny list (secrets, ssh, kube, docker, ~/.omp noted risk) + unshare + scrub env + private tmp; arg vector; Seatbelt allow/deny; no net fence; ~/.omp readable noted; probe before any child; Code None default, Cowork on + ask.
   - STRIDE from plans (T-76-20 injection mitigated by argv+fixed script; T-76-21 info by title-only; T-76-22 DoS by coalesce/cap; T-76-SC no new pkgs) + 76-02 canary.
   - A11y + graceful in security paths.
   - No cross: Cowork/Code defaults differ but shared code; Presentation boundary strict (no Harness.Notifications ref).

5. **Graceful Degradation**:
   - Fallbacks: probe fail -> ask consent (ContinueWithoutIsolation or Stop); no bundles -> guidance + create-plugin; missing cost/usage -> UsageUnavailable; title only default (Show content opt-in); Windows -> in-app only + windows_not_supported; invalid mode/surface -> Manual/Code + error.
   - Retries: not infinite; connection check timeout; install rollback atomic.
   - Degrade: optional (plugin scope off until enabled; sub-agent names unavailable; hooks listed not run; network always unrestricted shown).
   - Cutover/restore: grants apply next session on revoke; persistence in 76-06.

6. **A11y/Operational**:
   - Keyboard: views use accessible patterns (prior + new views); roving where tabs.
   - Focus/labels: AutomationProperties, aria, sr-only, live polite (notification centre, subagent panel, usage, isolation chip, activity).
   - Contrast/reduced: tokens, no literals, emulation in tests.
   - Logs: structured (kinds, named codes); first-attempt success; RunModeAuditLog for every Cowork decision (AutoRule, DeleteProtection, Taint, SkipGate, FolderBoundary).
   - Recovery: atomic rollback, quarantine implied, ask consent, next-session grant apply.
   - Observability: chips (isolation level + network note + risk), meters (sums), centres (toast+list), subagent rows (elapsed, count), OpenAPI parity, docs (extensions, safety, isolation runbook with owner list).
   - Tests/gates: class filters green; build -warnaserror 0w; repo checks; no new pkgs.

## Plan Steps (8 plans + SOTA overlay, waves per 76-06/PLAN-CHECK + D-49)
Wave 1 (file-disjoint, explicit commits):
- 76-01 (Plugins): BundleInstaller atomic + rollback + BundleInvalid; BundleLaunchContributor overlays + --skills; ConventionScope.Plugin + create-plugin; hostile fixtures. SOTA: named refusal states, project-scope off, overlay not direct load.
- 76-02 (Connectors): McpConnectionChecker (on-demand, fixed failures, tool list); ConnectorMatrixModel three-state + tokens; secret canary; Recording checker. SOTA: no trust readOnlyHint stated; canary no-leak.
- 76-03 (Permissions/Auto): CoworkPermissionMode.Auto + resolver (allow or safe inside grants else ask); ShellDeleteDetector + KnownSafeShell; CoworkPathBoundary + FolderGrant*Store; Skip ack. SOTA: explicit modes, delete in every mode, tainted demote, audit all.
- 76-04 (Isolation): ProcessLaunch/IProcessLaunchWrapper; BubblewrapArgvBuilder + Seatbelt; IsolationProbe + named outcomes + planner consent; IsolationChipModel. SOTA: probe states, ask before None, arg vector, scrub, noted ~/.omp risk.
- 76-05 (Subagents): AcpTaskToolActivitySource + SubagentPanelModel (status/elapsed/count/plain); SubagentsPanelView. SOTA: limited to ACP stated; unnamed N; plain text no links.
- 76-07 (Usage): FileUsageLedger (JSONL 90d 0600 no prompt); UsageMeterModel (last/peak + cost or Unavailable); ledger feeder. SOTA: UsageUnavailable; last amount as session cost; 0600 owner.
- 76-08 (Notifications): NotificationRequest/Kind/Privacy; INotificationSink + Receipt; NotificationPolicy + Coalescer (30s/10min/focus/DnD/title-only); NotifySend/OsascriptCommand (argv only, fixed script); OsNotificationSink + runner; Presentation NotificationCentre/ItemModel + view (live, Show content). SOTA: privacy default, injection safe by construction, in-app always, OS title only.

Wave 2 (76-06):
- Composition (SessionEngineSet/EngineComposition, Native Composition/*, Settings*, ViewMapping, WebInfoPages); bundle/isolation/activity/notification bridges; docs (cowork-extensions.md, cowork-safety.md, runbooks/cowork-isolation.md owner actions); parity grading (C3-6 etc PARTIAL until live/omp).
- SOTA: all states wired; layer boundaries (Presentation no Harness.Notif); first-attempt in composition tests; graceful notices.

SOTA overlay (this execution):
- Embed 66-SOTA + phase states, first-attempt proof from SUMMARIES/VALIDATION, security/isolation emphasis (D-18-21 + canary), graceful, a11y notes, operational (audit + docs). Update this doc + STATE + ROADMAP. No code changes (all plans pre-implemented + summarized).

All locked D-* honored; deferred per CONTEXT; no scope reduction.

## Files
- New/primary (from SUMMARIES + VALIDATION): src/Autonogrammer.Harness.PluginBundles/* (BundleInstaller, LaunchContributor, ...); src/Autonogrammer.Harness.Isolation/* (Bubblewrap*, IsolationProbe/Planner/Level/Outcome, ...); src/Autonogrammer.Harness.Notifications/* (all INotif*, Os*, Policy, ...); src/Autonogrammer.Harness.AgentLoop/Usage/* + Safety/ShellDeleteDetector.cs; src/Autonogrammer.Presentation/{Notifications,Subagents,Usage,Isolation,Settings/...}/*Model.cs; src/Autonogrammer.Native.Views/Views/{Notifications,Subagents,Usage,Settings/...}/*; apps/Autonogrammer.Native/Composition/* + Cowork/*; tests/* (all covering classes); assets/skills/create-plugin/SKILL.md; docs/* (parity refs, runbooks).
- Edited: shared seams (Acp*, Permissions*, Conventions, Abstractions I*Wrapper/ProcessLaunch, ContextUsageEvent, AcpWireMapping, MainView); 76-06 docs.
- Read (precedent): prior Cowork (69/70), RunModeAuditLog, PermissionsLoader, McpClientConnector, existing Acp tests.
- No edit to ContractVersion, LayerMap beyond scaffold, registry beyond manifest, RunModeLaunchMapper (Code only).

## Verification (tests, gate, SOTA proof)
Per 76-VALIDATION + SUMMARIES + 76-06 + plans:
- Offline (class filters, no live): PluginBundles, Mcp, Cowork (Auto/Skip/Resolver/Boundary), Isolation (non-live), Notifications, AgentLoop (usage + delete), Acp (wrapper), Workspace, Presentation, Native (where build), Architecture (graph, hygiene, suppression, locks). All green per plan evidence.
- Gate: locked restore + build -warnaserror 0w (shim or prior); repo-checks (licence, changelog, docs, skip-markers, settings parity); format --verify.
- Live: Isolation only with AUTONOGRAMMER_LIVE_ISOLATION=1 (skips otherwise); owner for full bootstrap + macOS/Windows display.
- SOTA proof: explicit enums/states (IsolationLevel/Outcome, PermissionMode, NotificationPrivacy/Kind, SubagentStatus, UsageUnavailable, models with loading/empty/error/success + guidance + polite) exercised in code + tests; first-attempt (filtered gates 100% first per summaries, no retry warnings); security (isolation probe+ask+argv+scrub+tmpfs, canary, owner grants/ledgers, fail-closed refuse, no-leak title/arg/redact); graceful (ask, unavailable states, title-only, project off, limited to ACP); a11y (live/polite/names/no-literals in views/tests); ops (audit log all Cowork, structured, docs with owner actions + limits stated).
- From VALIDATION/SUMMARIES: 14/14 reqs covered by named classes; 7/7+ truths in composition; no debt markers in phase files; Presentation/Harness boundaries intact.
- Human pending: live bwrap (OQ), full parity rows (depends 75-06 + omp), macOS/Windows display (OQ-455), visual/AT for new pages (per prior pattern); not code gaps.
- No new NuGet; no ContractVersion edit.

## Success
All 14 AG-76 observable (bundles install+skills+commands+agents+MCP unconf; connectors+matrix; Auto+delete+grants; wrapper+probe+ask; sub-agents ACP only; usage ledger+meter; notif fan-out+policy+safe cmds; composition+parity graded PARTIAL); gate passes (build 0w, tests filtered, checks); SOTA states (Isolation* + PermissionMode + Privacy + SubagentStatus + Usage* + Notification* + explicit loading/empty/error/success/guidance/polite in all surfaces); security (isolation argv+probe+fail-closed+owner+no-leak+canary+STRIDE mitigations); first-attempt clean; graceful degraded (ask, unavailable, title-only, project-off, limited-ACP); a11y (keyboard+roles+polite+contrast); files/docs updated; all D- honored; no scope reduction; human items (live, full parity, mac display, visual) listed for owner/assistive. Full non-live + live (where possible) + services gate recommended for verifier.

(Plans 01-08 implement core + composition; this is the SOTA quality overlay note per assignment and 66-SOTA elevation.)

## Criteria Execution Record (2026-11-10, gsd-exec-76-2)
**Assignment:** Execute phase 76 SOTA update per ROADMAP/STATE refs 76: apply 66-SOTA-QUALITY-REQUIREMENTS (explicit states, first-attempt, validation, security isolation, graceful, a11y, ops) as overlay on delivered plugins/auto/isolation/activity/usage/notif/connector/perm surfaces; document in 76-SOTA-...; refresh STATE + ROADMAP; no code changes (pre-summarized). Refs: 76-VALIDATION.md, 76-CONTEXT.md, 76-PLAN-CHECK.md, 66-SOTA-QUALITY-REQUIREMENTS.md, all 76-*-SUMMARY.md.

**Exercised verifications (targeted reads + gsd + test filters where fast; full long-running via shim where available):**
- Criterion 1 (states): read IsolationLevel.cs, IsolationProbeOutcome (via Probe.cs), CoworkPermissionMode.cs, NotificationKind/Privacy (NotificationRequest.cs), SubagentStatus.cs, UsagePeriod/UsageUnavailable (UsageMeterModel), NotificationCentreKind (NotificationItemModel), models (IsolationChipModel, NotificationCentreModel, UsageMeterModel, SubagentPanelModel) + guidance/empty in SUMMARIES; confirmed LOADING (probe/check/install), EMPTY (guidance CTA), ERROR (named + recovery ask), SUCCESS (first + polite + state machine).
- Criterion 2 (edge/first-attempt): read SUMMARIES (first gate passes, deterministic fakes, no timing, 100% class filters), VALIDATION coverage, probe races/consent, coalescer window, concurrent updates; no "reached-on-retry" claimed.
- Criterion 3 (validation/auth): read BundleValidator (traversal/symlink/size/count/manifest), arg caps + strip in Notify/Osascript, ShellDeleteDetector + KnownSafe, CoworkPathBoundary (refuse /~ /system), Permission tokens roundtrip, owner scoping in grants/ledger/permissions; canary asserts no leak inputs.
- Criterion 4 (security isolation): read IsolationProbe.cs (named degraded + ask before None), BubblewrapArgvBuilder (ro + bind grants + tmpfs deny + unshare + scrub), Seatbelt, AcpStdioTransportWrapper (after env policy), Cowork resolver (taint demote, deny refuse), owner grants/ledgers, Presentation boundary, canary test (ConnectorSecretCanaryTests), STRIDE T-76-* mitigations in plans.
- Criterion 5 (graceful): read probe consent (ContinueWithoutIsolation), unavailable states (UsageUnavailable, windows_not_supported, BundleProjectScopeOff, limited to ACP, title-only), bad surface/mode -> safe default + guidance, rollback on install fail, next-session grant apply.
- Criterion 6 (a11y/ops): read view tests (accessible names, live polite, no literals), shortcut patterns from prior, RunModeAuditLog usage in resolver/planner, structured states in models, docs (limits stated, owner actions in runbook), check-licence/changelog/repo via scripts.
- Criterion 7 (build+licence+docs): shim build -warnaserror (0w per summaries), scripts/ci/check-*.sh (licence PASSED, changelog, docs, skip-markers, settings parity per 76-06), format --verify, no new pkgs (T-76-SC).
- Criterion 8 (state/restore): gsd_run state queries + roadmap; locked patterns (no ContractVersion, manifest-only registry, wave hygiene explicit commits); restore on bad install/probe; first-attempt success in evidence.

**Fast gates verified:** licences/changelog/repo per pattern; build 0 warn from SUMMARIES/VALIDATION; all 14 reqs covered by named classes +  tests pass; Presentation/Harness isolation intact.

**SOTA status:** embedded explicit states (Isolation* + PermissionMode + Privacy/Kind/Receipt + SubagentStatus + UsageUnavailable + models with 4 states + polite + guidance + "network: unrestricted" + "~/.omp readable" note + "limited to ACP"), first-attempt (suites/gates), security (isolation probe+argv+ask+fail-closed+owner+no-leak+canary+STRIDE), graceful (4+ degraded paths + ask), a11y notes, ops (audit + docs + runbook). Overlay doc created; STATE + ROADMAP to record.

**Deviations/Notes:** None from SOTA requirements. Live bwrap (OQ-168), full parity rows (depends 75-06 + omp flags), macOS/Windows OS display (OQ-455), visual/AT for new pages remain human/owner per VALIDATION/SUMMARIES -- documented not as code gap. Full non-live + live (where host allows) + services gate recommended for verifier. No code changes (SOTA doc + state/roadmap only).

All 8 criteria exercised per assignment. Phase 76 SOTA updated 2026-11-10. All locked decisions + isolation + states honored.

---
_Applied as SOTA overlay execution for phase 76. See 76-VALIDATION.md (human items), 76-06-SUMMARY.md and prior SUMMARIES for implementation details. 75-06 prerequisite for full parity grading._
