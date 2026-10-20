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
- **AC mapping source**: `.planning/PROJECT.md` (ultrathink specification acceptance criteria AC_1..AC_11).
</decisions>

<deferred>
## Known Deferred Items Going In

- Remote CI run green (no git remote configured) — resume at ship PR.
- macOS MAUI build job — first CI run on a macOS runner.
Both already recorded in STATE.md Deferred Items.
</deferred>
