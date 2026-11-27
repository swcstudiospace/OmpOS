# Phase 81: Terminal engine retirement - Context
Source: task T9013-arch, ADR-0038 (Proposed), owner directive, upstream US-304/US-305, A04 terminal-retirement UX spec (session UI states)
**Gathered:** 2026-11-23
**Status:** Planned (Waves 2+: plan-and-return).

<domain>
## Phase Boundary

Remove the omp-TUI-in-PTY engine: `SessionKind.Terminal` creation in `apps/Autonogrammer.Native/Agents/SessionEngineSet.cs`, `PlannedOmpTerminalStarter` and the terminal launcher path over `Harness.OmpLauncher`, the Web catalog mapping and Demo simulation branches. Delete `Harness.Herdr` and its tests in the same phase (dead code, isolated in the C4 check). Persisted terminal records get the read-only, non-startable retired state. `Harness.OmpLauncher` provisioning (`OmpManagedInstaller`, `OmpLocator`) stays — both kept transports depend on it.

Not delivered: a replacement embedded terminal, any chat/Cowork session change, deletion of user session data.
</domain>

<decisions>
## Implementation Decisions

- **D-01:** One owner change, here only: `SessionEngineSet` stops creating terminal records; the starter is deleted; the phase diff is the AC-304 evidence for the sequenced change (Wave 1 changed 0 owner assignments).
- **D-02:** Herdr deletion recounts zero production consumers first (same count discipline as Phase 79), deregisters both Herdr projects with one relock, and closes its suppression rows (SUP-873 to SUP-875 class) as removed-with-the-project in `docs/open-questions.md`.
- **D-03:** Persisted terminal records are never deleted and never startable again; they render in the retired state the A04 UX spec names (OQ-527 mapping decided before execution), under NFR-A3 (axe) and NFR-I3 (strings) rules. Demo follows the same states.
- **D-04:** Contract gate: the served OpenAPI document is re-checked against `docs/api/openapi.json`; if removing the kind changes it, the breaking-change gate applies before merge.
- **D-05:** Provisioning control (AC-305): after both deletions `OmpManagedInstaller` and `OmpLocator` are present (count 2) and their tests pass.
</decisions>

<verification_posture>
## What "done" means here

Native, Web, Presentation and Architecture tests pass; a persisted terminal fixture renders retired with no start action; 0 new axe errors at 1440/1024/390 px; `./bootstrap.sh` exits 0.
</verification_posture>

<canonical_refs>
## Canonical References

- `docs/decisions/ADR-0038-omp-rpc-primary-transport.md`
- `apps/Autonogrammer.Native/Agents/SessionEngineSet.cs`, `apps/Autonogrammer.Native/Agents/PlannedOmpTerminalStarter.cs`
- `docs/open-questions.md` (OQ-05, OQ-527)
- `.planning/REQUIREMENTS.md` (RPC-81-1 to RPC-81-4)
</canonical_refs>
