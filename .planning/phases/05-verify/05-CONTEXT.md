# Phase 5: Verify & Handoff - Context

**Gathered:** 2026-10-20
**Status:** Ready for planning
**Mode:** Drafted during Phase 2 execution (orchestrator)

<domain>
## Phase Boundary

Prove the milestone end-to-end: clean-clone bootstrap (VERIFY-01), open-questions completeness (VERIFY-02), final implemented/stubbed/deferred handoff report against AC_1..AC_11 (VERIFY-03). No new product features.

Out of scope: fixing product bugs discovered here beyond small fix-ups (those go back to their phase owner plan), deploying anything.
</domain>

<decisions>
## Implementation Decisions

- **Clean-clone protocol**: `git clone` the repo into a fresh temp dir (not a copy of the working tree), run `./bootstrap.sh` with `DOTNET_ROOT` unset so the SDK-install path is exercised; record full output to `docs/runbooks/local-dev.md` § Clean clone (date, host, SDK version, duration, result). Podman present on this host → compose gate runs for real.
- **Open-questions sweep**: every unverified external fact in VERIFY-02's list gets a row with owner tag; every `TODO`/`HACK`/`FIXME` in tracked source greps clean against that list (RepoHygiene test enforces the linkage).
- **Handoff report**: `docs/handoff.md` table AC_1..AC_11 → status (Implemented | Stubbed | Deferred) → evidence (file paths, test names, or the deferred-verification STATE row). Source of truth for statuses: this milestone's plan SUMMARYs + gate reports, not memory.
- **AC mapping source**: `.planning/PROJECT.md` (ultrathink specification acceptance criteria AC_1..AC_11). **[GAP]** The original enumerated AC list could not be retrieved from the ultrathink plan store, Notion task graph, or git history (checked 2026-10-20: session transcripts, `last-plan.json`, swarm plans, Notion page `3ffbc1a0c7ae8112aa70d2e15b81929f`). It is reconstructed below from the ultrathink brief and the v1 requirement set; `docs/open-questions.md` carries the owner-tagged row. Phase 5 verifies against THIS list and states its provenance in `docs/handoff.md`.

| AC | Criterion (reconstructed 2026-10-20) |
|---|---|
| AC_1 | Monorepo scaffold boots green from one command (`bootstrap.sh`) with pinned SDK, locked restore, warnings-as-errors |
| AC_2 | Every prompt string yields exactly one `IntentEnvelope`; catch-all fallback is unremovable; omp keyword rules honoured (PARSE-01..04) |
| AC_3 | Typed harness with explicit state machine; scripted in-memory adapter drives UI work without omp; six failure modes covered (HARN-01..03, HARN-06) |
| AC_4 | Real ACP v1 and omp RPC JSONL clients pass replay + live smoke against installed omp, no model spend (HARN-04, HARN-05) |
| AC_5 | Append-only sequence-numbered session log; one-active-prompt enforcement; replay-equal projection (SESS-01..03) |
| AC_6 | Secured observable API: validated endpoints, per-session rate limit, locked CORS, committed OpenAPI 3.1, SSE resume without gaps/duplicates, health, fail-fast config, correlation, redaction, no-op auth boundary (API-01..03, OPS-01..04) |
| AC_7 | Sandbox provider abstraction: fake + local podman-compose providers under contract tests; explicit isolation defaults (SBOX-01..03, SBOX-05) |
| AC_8 | Vendor sandbox stubs (OpenShell, agentOS, Railway, dstack-shaped enclave) return typed NotAvailable/Unverified with owner-tagged open questions; no invented vendor APIs (SBOX-04) |
| AC_9 | Durable session-run workflow behind `Workflows:Enabled` with deterministic replay test; direct runner is the default path (WFLW-01..03) |
| AC_10 | Generative UI: all UiBlock variants + unknown-fallback render as a pure fold over the event log; Blazor WASM web head deploys statically to Vercel (SPA rewrite + CSP) and completes the vertical slice in a browser; MAUI head shares the RCL (desktop in-process, mobile remote-only) (UI-01..04, WEB-01..02, DESK-01, MOB-01, SHELL-01) |
| AC_11 | Milestone governance: architecture/security/format/vulnerability gates green, docs (architecture, ADRs, runbooks, handoff) reflect the final tree with implemented/stubbed/deferred fidelity (VERIFY-01..03) |
</decisions>

<deferred>
## Known Deferred Items Going In

- Remote CI run green (no git remote configured) — resume at ship PR.
- macOS MAUI build job — first CI run on a macOS runner.
Both already recorded in STATE.md Deferred Items.
</deferred>
