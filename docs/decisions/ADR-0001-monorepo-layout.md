# ADR-0001: Monorepo layout and dependency layers

- Status: accepted
- Date: 2026-10-20
- Deciders: ompos-maintainers
- Scope: Phase 1 (Foundation)

## Context

OmpOS is one full-stack product (contracts, harness, backend, worker, web and native shells) built by a small team. Split repositories would let the shared contracts drift between surfaces; a monorepo with a mechanically enforced layer direction keeps one source of truth and one bootstrap command.

## Decision

Layout (D-01): build governance at the root (`OmpOS.slnx`, `OmpOS.Linux.slnf`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.editorconfig`, `bootstrap.sh`, `.env.example`, `.github/workflows/ci.yml`); libraries under `src/`; hosts under `services/`; heads under `apps/` (`Web` now, MAUI `App` in Phase 4); tests under `tests/`; docs under `docs/`; podman-compose stack under `deploy/`. OCI build files and hosting configs are not created until a service exists (Phase 2/3).

Layers (D-02): L0 `Contracts`; L1 `Parser`, `Harness.Abstractions`, `Sandbox.Abstractions` → Contracts only; L2 harness implementations, `Sessions`, `GenUi`, `Sandbox`, `Workflows`, `UI` → L0/L1 (`Sessions` may use `GenUi`; `UI` uses `Contracts` + `GenUi`); L3 `Api`, `Workflows.Worker` → L0–L2; L4 `Web` → Contracts + UI (+ GenUi), `App` → Contracts + UI + Harness (desktop in-process harness). Nothing references `OmpOS.Api` except tests; `OmpOS.Web` never references `Harness.Acp`/`Harness.Rpc`.

Ownership (D-03): `OmpOS.Contracts` owns Session, Message, SessionStatus, IntentEnvelope, IntentKind, Diagnostic, all UiBlock variants, sequence-numbered StreamEvent, SandboxDescriptor, SandboxStatus, WorkflowRun, WorkflowRunStatus, OmpOSError/ErrorCode, and ContractVersion. JSON uses source-generated `System.Text.Json` with reflection disabled.

Enforcement: `tests/OmpOS.ArchitectureTests` encodes the map as data (one-line change per new project) and checks both the `.csproj` graph and the type-level references, with synthetic-violation tests proving the gates fire.

## Volume deviation

D-05 wrote the Temporal volume as `/data/temporal.db`. The `temporalio/temporal:1.9.1` image runs as uid 1000 and `/data` does not exist in the image, so a write there may fail under rootless Podman. Following the vendor's own compose file, the volume mounts at `/home/temporal` (owned by the image user) with `--db-filename /home/temporal/temporal.db`. Recorded here and in `docs/runbooks/sandbox.md`.

## Rejected alternatives

- Split repositories per surface: rejected, contract drift between WASM, MAUI, and API with no mechanical check.
- `.sln` plus per-head solutions: rejected, the XML `.slnx` with one `.slnf` Linux filter expresses the same split with less duplication.
- Per-project package versions: rejected, central package management with transitive pinning and committed lock files makes restore reproducible.
- Roslyn-analyzer-only layer check: rejected for Phase 1, the csproj-graph test catches unused references ArchUnitNET cannot see; both run.
