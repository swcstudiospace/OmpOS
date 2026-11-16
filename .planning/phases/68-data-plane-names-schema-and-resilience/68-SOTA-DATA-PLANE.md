# Phase 68 SOTA for Data Plane: Names, Schema, Resilience and Cutover

**Date:** 2026-11-10
**Refs:** 68-CONTEXT.md (D-01..D-72 locked), 68-RESEARCH.md, 68-VALIDATION.md, 68-VERIFICATION.md (gaps_found), 68-PLAN-CHECK.md, 68-01..68-08 PLANs/SUMMARYs, ROADMAP AG-68-1..14, ADR-0031, 66-SOTA-QUALITY-REQUIREMENTS.md, prior phase SOTAs.

## Objectives (from CONTEXT + ROADMAP + brief)
- Cut every Autonogrammer-owned physical data name (Dragonfly keys/channels, Postgres objects/schemas/locks/roles/DB, Greptime, test/deploy vars, OTel) over to `autonogrammer` (now, nothing deployed).
- Additive reversible PostgreSQL/TimescaleDB schema (M0001-M0010) with hand-written migration runner, checksums, advisory lock, least-privilege roles (`autonogrammer_app` DML-only, migrator, owner, ro).
- Telemetry to GreptimeDB database `autonogrammer` (source `Autonogrammer.Api`, `autonogrammer.*` instruments) using pinned OTel 1.20.0.
- Bounded resilience (deadline + jitter retry + breaker + bulkhead on TimeProvider) around every store/ledger call with explicit degraded states (Healthy/Degraded/Unavailable/Disabled; reasons closed enum).
- Optional default-off audit path to Substrate ledger (outbox-fed, no personal data, no Solana/RPC by Autonogrammer).
- Plugin/Substrate namespaces untouched; no Railway mutations (OQ-73); no RLS, no new packages (Polly/DbUp etc rejected).
- 14 requirements AG-68-1 to 14 delivered in 8 plans/waves.
- Data cutover TODOs: rename constants (v1: prefix), merge locks, roles/DB creation in bootstrap.sql, no old names in first-party source, corrected criterion-3 check (defective literal grep retired).

## TODOs from Criteria + VERIFICATION + REVIEW (addressed + residual gaps)
- All 14 AG-68 covered across plans (see 68-PLAN-CHECK.md).
- Data cutover TODOs (pre-68 constants like "autonogrammer:" without v1, old locks, EnsureCreated, ompos* names): addressed by 68-02 (Dragonfly v1: + Keyspace cutover + delete Keys.cs), 68-03 (migrations + delete schemas), 68-07 (guards + corrected check).
- REVIEW/VERIF gaps (from 68-VERIFICATION.md): 
  - Phase gate (criteria 1/2) never run: 68-07 task 4 owns; 08 closes.
  - ArchitectureTests build blocked by other phase: prerequisite for guards/decision tests.
  - Criterion 3 grep defective (matches mandated new names + instruments): corrected shell check + runbook documented in 68-07; 08 embeds/runs.
  - Erasure incomplete (lease/idem/auth keys lack tenant; D-11 conflict): 08 owner decision + fix per D-11.
  - Presence/rl/auth stores missing (only builders): degraded not fully wired.
  - M0010 out-of-phase in 68-03: 08 removes registration.
  - SUP rows unreconciled: 08 + 68-07.
- SOTA states embedded per assignment: explicit loading (migration wait, schema probe, guard scans), error (outage/violations/old names), success (verified names/telemetry/roles/resilience).
- Security: fail-closed (roles DML-only, no secret in exceptions/logs, 503 only data_plane_unavailable, owner-scoped erasure), validation (checksums, TTL guards, runtime needles), graceful (degrade to in-process, memory-only where safe, no-deploy).
- Sandbox: read as precedent (SandboxLifecycle jitter, OpenShell isolation); 68-08 edits OpenShellProvider.cs for data-plane isolation probe (allowlist check).
- Deploy: blocked (OQ-73); runbook only, no mutations; health /ready per D-56.
- Verification/tests/gate: subsets claimed; full gate + live + corrected check + ArchitectureTests filtered in 08/VERIF human items.

## SOTA States for Data Plane / Cutover / Resilience (applied from 66-SOTA + phase specific)
1. **Explicit States** (loading/empty/error/success) in all data-plane paths:
   - LOADING: data-state=loading + timeout (schema wait 120s default, probe 1s, guard scan); progress, no silent hang. E.g. SchemaHeadWaitService, MigrationRunner apply, DataPlaneGuardTests scans, resilience half-open probe.
   - EMPTY: guidance + CTA (no data-plane configured = disabled state; first migrate).
   - ERROR: recovery (retry, fallback), error boundary + announcer (role=alert), redacted log. DataPlaneTimeoutException (component only), 503 data_plane_unavailable (closed reason), MigrationChecksumMismatch, old-name guard violation.
   - SUCCESS: explicit "first-attempt-success" log + count; state machine logged. Healthy after cutover + telemetry + roles verified; schema at head.
   - Implementation: DataPlaneStatus (Component/State/Reason), ResiliencePipeline states (via DependencyState gauge), SchemaHeadWait, guards, status endpoint, runbook probes.

2. **Edge/Race/First-Attempt**:
   - Races: concurrent migration, breaker open during startup probe (D-57: 15s Postgres open holds gate), tenant hash under owner vs device, erasure scan + live keys.
   - Loaded host: fresh context, deterministic waits (ManualTimeProvider in tests).
   - First attempt: 100% pass target; strengthened waits; no reliance on timing. Guards use runtime needles.

3. **Validation/Sanitization/Auth**:
   - Inputs sanitized (TTL bounds, checksum SHA-256 of UpSql, advisory lock strings no dots in session ids).
   - Auth: tenant via ITenantHasher (HMAC pepper over sub); owner scoped in erasure; no credential in client/head/log.
   - Schema: additive only (IF NOT EXISTS, ADD COLUMN IF NOT EXISTS); no DROP/ALTER TYPE in ups; runtime-built needles in guards.

4. **Security (fail-closed, no leak, owner scoped, roles least-privilege)**:
   - Fail closed: definitive failures (old name, checksum mismatch, DDL by app role, Solana literal) clear to error/503; refuse service_role etc; https pin in hosted; no partial state.
   - No leak: DataPlaneTimeoutException carries Component only; validators name key not value; LoggerMessage only enums/fixed; log hygiene; tokens/pepper never in logs/args.
   - Owner scoped: erasure per account_id/tenant hash; 404 foreign rows; dispatch_audit INSERT/SELECT only.
   - Roles: runtime `autonogrammer_app` DML-only (bootstrap.sql grants + default privileges); migrator separate one-shot; no RLS.
   - STRIDE from plans (T-68-*) + 68-07 guards: mitigated (no tampering of names/migrations, no disclosure of secrets, DoS bounded by caps/budgets).
   - A11y/ops not primary (no UI) but status has live region per UX ADR.
   - Graceful: no-deploy (OQ-73), in-process rate fallback, presence "unknown", signals poll safety net.

5. **Graceful Degradation**:
   - Fallbacks: Dragonfly outage -> in-process rl; schema behind -> "service updating"; ledger disabled -> no-op; telemetry exporter failing visible in /status only.
   - Retries: decorrelated jitter (D-41 bounds), 2 max for reads/idempotent; breaker 5 consec/50% 20-in-30s; bulkhead caps.
   - Cutover: old names cause guard fail (ERROR state); v1: enforced.

6. **Operational**:
   - Logs: structured EventIds, fixed vocab.
   - Recovery: operator `migrate --down`, ALTER SCHEMA rename (pre-first-migrate), one-shot retention if no licence.
   - Observability: /status/data-plane, dataplane.* instruments + gauges, runbook probes, DataPlaneGuardTests.
   - Deploy: runbook + bootstrap.sql only; variables named, no values; private network.

## Plan Steps (8 plans, tracer-first where applicable, waves per 68-PLAN-CHECK + CONTEXT)
Wave 1 (parallel, no deps, tracer resilience):
- 68-01 (Resilience L1): hand-written pipeline (deadline, jitter retry, breaker, bulkhead on TimeProvider + ManualTimeProvider tests); options validators; telemetry meter. SOTA: states in pipeline (via gauge), no package, no secret leak, cancellation passthrough. AG-68-8.
- 68-02 (Dragonfly names/cutover): DragonflyKeyspace (v1: prefix + TTL-in-command), ITenantHasher (HMAC), fail modes (closed/open), erasure helper, delete old Keys.cs. SOTA: explicit cutover states, tenant isolation, fail-closed for critical domains. AG-68-1,2,9.

Wave 2:
- 68-03 (Postgres migrations/roles/stores): M0001-M0009 (additive, down, checksum), MigrationRunner, roles (DML-only app), Session.Surface/ClosedAt + ListByOwner/SetClosed, erasure, pipeline wiring. SOTA: migration loading/success/error, role security, owner-scoped. AG-68-3-6,14.

Wave 3:
- 68-04 (API composition/telemetry/states): AddDataPlane, status/data-plane, 503 middleware (data_plane_unavailable only), migrate verb + SchemaHeadWait (D-58), telemetry to autonogrammer DB, DataPlaneOptions, problem shape, isolation. SOTA: loading (wait), error (outage/schema), success (status); security validators. AG-68-7,9,10,12.

Wave 4:
- 68-05 (Ledger sink): outbox + pump (default off), SubstrateGateway only, closed AuditLedgerEvent (no PII), breaker. SOTA: disabled state, no direct ledger. AG-68-11.

Wave 5 (parallel infra + guards):
- 68-06 (Test/deploy infra): test-services.sh + --with-greptime + bootstrap-greptime.sh, GateParity, compose updates, env vars per D-04. SOTA: loading of services, graceful test down.
- 68-07 (Guards/docs/cutover lock + gate): DataPlaneGuardTests (prefix/schema/old-names/forbidden/UpSql/Solana/coordination_unavailable; corrected criterion-3), runbook data-plane.md, hosting/railway/README edits, ADR index, OQ rows, final filtered Architecture + full phase gate (locked restore, licences, build, whole Linux set). SOTA: guards embed states (LOADING scan, ERROR violation, SUCCESS clean); deploy notes. AG-68-3,13 + phase criteria.

Wave 6+ (gap closure 08 per assignment/VERIF):
- 68-08 (Close VERIF gaps + embed SOTA + Sandbox probe + final): fix M0010 registration, erasure per D-11 (owner decision), run corrected check + gate + ArchitectureTests, embed SOTA states in guards/tests/docs (LOADING/ERROR/SUCCESS for cutover/migration/resilience), edit src/Autonogrammer.Sandbox/OpenShellProvider.cs for isolation probe (data-plane env allowlist), reconcile SUPs, run full verification. Addresses all 7 gaps; SOTA states/security in touched artifacts. Files: deploy, docs, tests, src/Autonogrammer.Sandbox (per query).

All locked D-* honored (D-01..D-72); deferred per CONTEXT (OQ-340-350, no RLS etc); no scope reduction.

## Files
- New: src/Autonogrammer.Resilience/* (pipeline + policies + options + telemetry + exception + state), tests/Autonogrammer.Resilience.Tests/* + ManualTimeProvider, src/Autonogrammer.Coordination/{ITenantHasher,ITenantKeyEraser,NoOp...}, Dragonfly/* (Keyspace, TenantHasher, erasure, fail stores), src/Autonogrammer.Sessions.Postgres/Migrations/* (M0001-9), MigrationRunner etc, services/Autonogrammer.Api/{DataPlane/*,Audit/*,MigrateCommand}, deploy/data-plane/bootstrap.sql, scripts/data-plane/bootstrap-greptime.sh, docs/runbooks/data-plane.md, tests/Autonogrammer.ArchitectureTests/DataPlaneGuardTests.cs, apps/Autonogrammer.Web/Demo updates, docs updates (SUMMARY, hosting, railway/README, architecture etc), src/Autonogrammer.Sandbox/OpenShellProvider.cs (68-08 isolation).
- Edited: many existing (Dragonfly stores, Postgres stores/schemas deleted, Api composition, test-services.sh, compose, .env, openapi, GuardTests, ArchitectureTests, open-questions.md, ADR README).
- Read (precedent, no edit except 08): src/Autonogrammer.Sandbox/SandboxLifecycle.cs (jitter), OpenShellProvider (isolation in 08).
- Deploy: runbook + Containerfile entrypoint only (no mutations).
- Verification/tests/gate: Resilience.Tests, Coordination.Tests, Sessions.*.Tests, Api.IntegrationTests (incl OTLP), ArchitectureTests (guards + parity + decision + suppression + scaffold + image), full Linux slnf on services, corrected shell check, live (gated), bootstrap.sh gate items.

## Verification (tests, gate, SOTA proof)
Per 68-VERIFICATION + ROADMAP + 68-07/08:
- Offline: ag-dotnet test Resilience, Coordination, Sessions.*, Api.Integration (boundary/status/OTLP), Architecture (guards/DecisionRecord/Suppression/Scaffold/Image/GateParity), full filtered.
- Gate: repo-checks, CI=true locked restore, check-licences, build -warnaserror, full Linux set on `scripts/test-services.sh up`, format verify.
- Live: AUTONOGRAMMER_LIVE_GREPTIME=1, LIVE_DRAGONFLY_ACL=1, LIVE_SUBSTRATE=1 (gated, Assert.Skip).
- SOTA proof: states in resilience/status/migration/guards (loading/error/success), first-attempt clean cutover, no-leak (component only), owner-scope erasure, role DML tests, corrected check output, no old names.
- 08 closes: full gate, corrected check run, ArchitectureTests, erasure decision, M0010, SUPs.
- From VERIF: post-fix all criteria 1-5 + plan truths observable.

## Success
All 14 AG-68 observable (names cut to autonogrammer:v1:/autonogrammer schema/roles, migrations reversible, telemetry to autonogrammer DB, resilience bounded with states, ledger off-by-default), gate passes (locked, licences, build, full set), SOTA states (explicit loading/error/success for cutover/resilience/migration/schema/telemetry), security (roles least-priv, no leaks, fail-closed 503 only data_plane_unavailable, owner erasure), files (Sandbox precedent + probe), deploy notes only, verification/tests/gate complete per 08/VERIF. Data cutover TODOs closed (no old names, guards enforce).

(Plans 01-08 implement; 08 is the SOTA/gap-closure overlay per assignment.) All D- honored; gaps closed.