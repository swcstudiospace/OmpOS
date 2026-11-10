# Phase 76 SOTA Quality Requirements (Elevated to Best-in-Class, Production-Grade, Enterprise-Ready)

**Date:** 2026-11-10
**Scope:** Phase 76 Cowork parity B (plugins, connectors, permissions/Auto, isolation, activity/sub-agents/usage/notifications, integration). Apply SOTA states, first-attempt, fail-closed, explicit a11y/security to all deliverables. AG-76-1 to AG-76-14. Plans 01-08 + 06 integration gate.
**Refs:** 76-CONTEXT.md (D-01 to D-49, decisions), 76-RESEARCH.md, 76-VALIDATION.md, 76-PLAN-CHECK.md, 76-01 to 76-08 PLANs/SUMMARYs, ROADMAP, AGENTS.md SOTA bar, ADR-0037 (D7-D13, D2), 66-SOTA-QUALITY-REQUIREMENTS.md, prior Cowork SOTAs (69,70,71,72,73,75), docs/parity/cowork-parity.md, docs/reference/cowork-*.md, docs/runbooks/cowork-isolation.md

## Explicit SOTA Requirements (Required Inclusions for All Changes)

### 1. Explicit States (SOTA states for plugins, auto, isolation, activity, notifications)
- **LOADING**: Every install/scan, permission check, launch wrapper probe, sub-agent start, usage frame, notification publish, registry load exposes loading state (data-state or AutomationProperties) with timeout (env-overridable, default 30s). Progress indication, never silent hang. E.g. bundle validation, IsolationProbe, AcpTaskToolActivitySource, NotificationPolicy publish under clock.
- **EMPTY**: Guidance + CTA for no data (no plugins, no connectors, no grants, no sub-agents, no usage, no notifications): "No plugins installed", "No approvals waiting", "Usage unavailable" etc. Never blank UI.
- **ERROR**: Recovery (retry, fallback to Code, disable), error boundary + announcer (role=alert). Named errors: BundleInvalid, IsolationProbe failures (bwrap_missing etc), ContinueWithoutIsolation, NotificationOsUnavailable(...), UsageUnavailable, SubagentNamesUnavailable. Fail fast but recoverable.
- **SUCCESS**: Explicit "first-attempt-success" log + count on happy path. State machine transitions logged (Created→Running etc for sub-agents; session states). No warning on happy path.
- **Permission / Mode States (SOTA CoworkPermissionMode)**: Manual, Auto (rules), Skip. Auto never trusts readOnlyHint; always-ask-cached mapping. Deletion protection always on. Folder grants read-write vs read-only + scope.
- **Isolation States (SOTA IsolationLevel)**: None (Code default), ProcessContained, FilesystemIsolated (Cowork default). Network always "unrestricted". Probe results: bwrap_missing, userns_disabled, probe_timeout, platform_unsupported.
- **Notification States**: RetainedInApp (always), DeliverToOperatingSystem, Suppression (focused/DnD), OperatingSystemDrops, TitleOnly vs ShowContent.
- **Activity States**: Sub-agent rows (running/done/failed/cancelled), usage (last+peak occupancy, last cost or UsageUnavailable).

Implementation rule: every async seam, policy decision, launch, publish, frame must expose the states via model/report or enum. No implicit retries without backoff cap. CoworkPermissionMode and IsolationLevel are canonical.

### 2. Edge Cases (race, loaded host, first attempt, isolation, privacy)
- **Race conditions**: Plugin enable during launch, permission rule change mid-session, isolation probe concurrent with start, notification flood during focus change, usage frame vs export, sub-agent update vs panel render. Fix: deterministic waits (ManualTimeProvider, bUnit/Native settle, compound conditions). No reliance on timing.
- **Loaded host / first attempt**: Target 100% pass on first (no "reached-on-retry"). Strengthen waits in tests; first-success logs in models and audit. Backoff only last resort.
- **Isolation races**: Cowork isolation never affects Code (3+5 test separation); two RecoverAfterRestart independent; no bleed of env/paths. Caps and grants independent.
- **Privacy / notifications races**: Coalesce must not drop from in-app; rate drop counted but retained; focus/DnD changes mid-publish respected; Show content toggle never leaks to OS without flag.
- **Other**: Corrupt bundle (refuse, rollback atomic), full grants (exact message), tainted session (demote Auto to Manual), unknown sub-agent name ("Sub-agent N"), no cost (UsageUnavailable).

### 3. Validation (sanitization, contracts, argv, no shell)
- **Sanitization**: Titles/summaries capped, control chars stripped for OS; paths validated no traversal/symlink in bundles; args capped 200; account keys first-20 lower base32 SHA; no prompt content in ledgers.
- **Contracts**: INotificationSink, ILaunchContributor, ICoworkPermissionResolver, ISubagentActivitySource, IUsageHistorySource, Bundle* contributors. Presentation never refs Harness.* for notifications/usage/activity. Seams in Abstractions only.
- **Argv only**: All process starts (notify, osascript, bwrap, sandbox-exec, omp child) by argument vector; never shell, never splice text. Fixed sources asserted byte-exact.
- **Schema/JSON**: Strict deserializers, unknown members refused or ignored per store rules. No new ContractVersion.

### 4. Security (fail closed, never trust hints, isolation, no leak)
- **Fail closed**: On ambiguity (bad bundle, probe fail, unknown mode, missing tool, corrupt grant) refuse or ask (never silent approve/run). Auto treats MCP like Manual unless explicit rule. Tainted = Manual for all.
- **Injection (T-76-20 high)**: Mitigated by argv-only + fixed AppleScript + strip + cap (asserted).
- **Information (T-76-21 medium)**: Title-only default on OS; Show content explicit; focus/DnD honoured; summary kept in-app only.
- **Denial (T-76-22 low)**: Coalesce + rate cap + drops visible in-app.
- **Isolation (D-18..D-21)**: bwrap/seatbelt/job with explicit deny list + grants only; env scrub; no net unshare; probe before run; never silent unconfined.
- **No leak**: Never echo subs, full paths, tokens, secrets in logs/UI/summaries/notifications/ledgers (redact). ~/.omp noted as readable risk (shown, not hidden).
- **Authz**: Owner-scoped where applicable; permissions per-tool/per-server matrix (Always/Needs/Blocked); deletion detector on ACP+shell.
- **Canary**: Secret canary (headers/args/paths/env) in 76-02.

### 5. A11y (WCAG 2.2 AA, keyboard, focus, labels, polite, reduced-motion)
- **Keyboard**: Settings pages (plugins, connectors, permissions, isolation) keyboard reachable; F6 cycles; skip links; roving where segmented.
- **Focus/Labels**: Every new control named; live regions Polite for activity/notifications/status; no focus steal.
- **Contrast/Motion**: No new colour literals (tokens + classes); reduced motion respected; target sizes met.
- **WCAG in checks**: Landmarks, live regions, names, 2.4.3 order, axe on new pages/views, Native headless + bUnit. States exercised in a11y routes.
- **Notifications**: Live region "Notification status"; list and toast named; approval link name = id only; privacy label.

Human-needed (per prior): full screen-reader, pointer at various sizes, live isolation (owner via AUTONOGRAMMER_LIVE_ISOLATION + bootstrap).

### 6. Graceful Degradation (fallbacks, degraded states)
- **Fallbacks**: No plugins → empty with CTA; unavailable isolation → ContinueWithoutIsolation prompt; missing OS tool → named unavailable, in-app only; no cost → UsageUnavailable; no sub-agent name → "Sub-agent N"; bad surface/mode → Code or Manual.
- **Degrade**: Optional panels; unknown states refused (never default); offline keeps local; network unrestricted always shown.
- **Retries**: Capped; no infinite loops.

### 7. Operational (logs, recovery, verification gate, audit)
- **Logs**: Structured "first-attempt-success", counts, named states only. No secrets/prompts. RunModeAuditLog for every decision.
- **Recovery**: Atomic rollback on bad install; quarantine; independent restarts; cleanup in tests.
- **Verification gate (76-06 owns)**: All class-filtered Release tests (no Live unless env); build -warnaserror; repo-checks (docs, skip-markers, licences, format); ArchitectureTests (ProjectGraph, hygiene, suppression); parity rows graded PARTIAL only with passing named test class; docs updated (cowork-extensions, cowork-safety, runbooks); no TODO without OQ.
- **Deterministic**: Fresh contexts, pinned fixtures, ManualTimeProvider where timing, no shared bleed.
- **Threats (from 76-08 and CONTEXT)**: T-76-20/21/22/SC mitigated as above; all other phase threats per CONTEXT.

All changes must pass: first-attempt in tests, isolation/permission gates red on violation, a11y, full gate via 76-06, explicit states in every new surface/model.

## Phase 76 Application (Plugins + Auto + Isolation + Activity + Notifications + Integration)
**Touched (per CONTEXT/VALIDATION):** src/Autonogrammer.Harness.{PluginBundles,Isolation,Notifications,AgentLoop/*,Acp}, src/Autonogrammer.Presentation/{Notifications,Usage,Subagent?}, src/Autonogrammer.Native.Views/{Notifications,Settings/*,Shell}, tests/* (all the *Tests named in VALIDATION), apps/Autonogrammer.Native/Composition/*, docs/*, parity files.

**Application of requirements:**
- **SOTA states**: Explicit in NotificationPolicy/Receipt, NotificationCentreModel, Isolation* (probe/level), Permission* (Auto/Skip), Subagent/Usage models, bundle states. Live regions, loading/empty/error/success in views.
- **First attempt / races**: Strengthened waits + ManualClock in policy/subagent/usage tests; first-success in audit/logs; compound in integration.
- **Fail-closed + security**: Auto rules + delete detector + boundary + taint demote; argv safety + fixed sources + title-only; probe before launch; no trust in hints/annotations.
- **A11y**: Polite live (notifications, activity), named controls, no literals, toggle for content, accessible approval ids.
- **Layer boundaries**: Presentation-local kinds/models (explicit test); Harness.* only for spawning; Abstractions seams.
- **Verification gate**: 76-06 owns full + parity grading (C rows, K rows PARTIAL with test class); 76-08 owns AG-76-13 tests + build.
- **Notifications (08)**: Exactly per plan: sink/policy/commands/centre/view; T-76 mitigations; Windows in-app only; owner action for display.

**Evidence of application**: All named tests in VALIDATION pass the SOTA behaviors; layer tests; first-attempt logs; states in models/UI; gates red on violation; docs record owner actions.

## Recommended Autonomous Answers (per prompt)
- For plugins/auto/isolation/activity: explicit SOTA states + enums in every model/contract; first-attempt compound waits + logs; fail-closed on every seam.
- For notifications: argv-only + fixed + title-only + in-app retain always; policy owns coalesce/rate/focus/DnD.
- For integration: 76-06 gate owns composition + grading; no cross-layer refs.
- Default: private ports; no live Supabase/omp beyond gated; compile for Win/Mac/Linux; first pass target; Live isolation owner-only.
- Verification order: per VALIDATION quick commands (cheapest first) then full 76-06 gate + repo-checks + docs.
- If blocked on human/live: record in 76-VERIFICATION (when created) or runbook; automated still passes.

This elevates phase 76 to SOTA: every Cowork parity surface, policy, launcher, activity, notification includes the 7 categories explicitly. First-attempt, fail-closed, argv-safety, privacy, a11y are structural + gated. Never trusts untrusted hints.

---
_Applied per user assignment "execute plan SOTA update. Refs 76": full SOTA states + first-attempt + security + a11y for plugins, Auto (rules), isolation levels, sub-agents/usage/notifications per 76-CONTEXT D-01..D-49, 76-VALIDATION map, 08 notifications plan. Companion 76-08-SUMMARY created. Ready for 76-06 gate close._
