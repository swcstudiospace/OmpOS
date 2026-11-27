# Phase 84 cutover — Tasks 4-5: owner decisions + one-commit flip (2026-11-26)

Commits: `c3ac51ae` (Task 4), `69b844f0` (Task 5 flip). Base: `184c5500`. Decider for Task 4: product owner.

## Task 4 — owner decisions

- **OQ-528 (realm) = init.** Evidence (verbatim): `git submodule status` → `f28a3acc319d9805c2d2487c05d12a075a2c8ad7 plugins/agent-realm (v0.3.38)`; `git ls-tree HEAD plugins/agent-realm` → `160000 commit f28a3acc...`; `git -C plugins/agent-realm rev-parse HEAD` → `f28a3acc319d9805c2d2487c05d12a075a2c8ad7`; `git -C plugins/agent-realm status --porcelain` → clean (empty). Disposition: already initialised, no-op (no re-init executed). Recorded: OQ-528 row → `closed 2026-11-26 (owner init call; submodule initialised at f28a3acc)`.
- **ADR-0039 Gate A = A1** (reuse Dispatch ES256 device credential with artifact-specific audience + server-validated session id). **Gate B = B4** (no-network default for v0.16.0; grants recorded but network-inert). Status stays **Proposed** — the 71-08 Tasks 2/3 code is not yet written; the recorded calls unblock it. Recorded: ADR-0039 Status header + Candidate chosen + `docs/decisions/README.md` ADR-0039 row.
- **SEC-77-01**: carried open at 0 waivers; no identifier cleared, waived or renamed.
- Linear: SPE-37896 chain (SC1 SPE-37897, SC2 SPE-37898, SC3 SPE-37899, Project P-SPE-2). Notion payload root: `700bc1a0-c7ae-812c-abfb-00035fd38be2`.

## Task 5 — flip (commit `69b844f0`, 8 files)

Precondition (baseline green): `bash scripts/ci/repo-checks.sh` exit 0 at `c3ac51ae`; `check-docs.sh` 0 errors; `check-skip-markers.sh` ok (93 kinds, 90 files); `check-sbom.sh` ok. Failing-gate rule: no red → flip attempted.

| # | Site | Before | After |
|---|---|---|---|
| F1 | `services/Autonogrammer.Api/appsettings.json` | `Harness:Adapter = InMemory` | `Rpc` |
| F1 | `services/Autonogrammer.Api/Options/HarnessOptions.cs`, `Program.cs` | "Acp stays the shipped default / Rpc opt-in" | "Rpc default (Phase 84); Acp rollback + remote" (validators unchanged) |
| F2 | `apps/Autonogrammer.Native/Engine/EngineCompositionOptions.cs` | `Transport = Acp` | `Rpc` + doc |
| F3 | `apps/Autonogrammer.Native/Agents/SessionEngineSetOptions.cs` | `Transport = Acp` | `Rpc` + doc (child/remote stay ACP) |
| F4 | `.env.example` | `Harness__Adapter=InMemory` + "opt-in" | `Rpc` + `Harness__Adapter=Acp` named rollback |
| F5 | `docs/decisions/ADR-0038-omp-rpc-primary-transport.md` + `docs/decisions/README.md` | Proposed 2026-11-23 | Accepted 2026-11-26 (same commit) |

Deletion manifest: 0 files/types (TASK3: every O1-O3 candidate Keep or Modify-at-flip). Validators: exhaustive/fail-fast unchanged (unknown adapter still throws naming the key, `Program.cs` dispatch).
Compile-check: `dotnet build services/Autonogrammer.Api/Autonogrammer.Api.csproj -c Release --no-restore -warnaserror` → 30 projects, 0 errors, 0 warnings, exit 0. `dotnet build apps/Autonogrammer.Native/Autonogrammer.Native.csproj -c Release --no-restore -warnaserror` → 44 projects, 0/0, exit 0. (Host load 44–67 from parallel waves; earlier attempts timed out at 600/900s before these final runs.)
No new suppression/NoWarn/pragma/severity-none in the flip diff (grep-verified). Tests pinning the old default: none (no `EngineTransport.Acp` in tests/; adapter tests set config explicitly).
