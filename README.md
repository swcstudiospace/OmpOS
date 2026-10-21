# OmpOS

OmpOS is a chat-style agent workspace — a Grok Chat / Cursor Agent–class client whose engine is **omp** rather than Cursor. It orchestrates omp sessions and renders their streamed output as **Generative UI** (structured, interactive blocks, not plain text). It is a **Desktop ACP Harness built in C#** that grows into one full-stack product: a Blazor WebAssembly web app hosted on Vercel, .NET MAUI Blazor Hybrid native shells for macOS/Windows/iOS/Android, and a unified ASP.NET Core backend on Railway (session runs execute in-process in the API; no Temporal and no worker service) and sandboxed agent execution.

A prompt typed on any surface becomes an omp session whose streamed events render as live Generative UI — reliably, resumably, and inside an isolation boundary the operator controls.

## Prerequisites

- `bash`, `curl`, `git`, `python3` on `PATH`.
- .NET is **not** a prerequisite: `./bootstrap.sh` installs the SDK pinned in `global.json` into `~/.dotnet` when it is missing.
- `podman` plus `podman-compose` are optional (only the local sandbox provider needs them; the `workflows` profile is removed). Podman is the only supported container runtime; Docker is not used.

## Bootstrap

One command builds and tests everything from a clean clone:

```sh
./bootstrap.sh
```

It installs the pinned SDK when missing, then runs a locked restore, a warnings-as-errors Release build, the test suite, and a format verification. Any failing step exits non-zero. See [docs/runbooks/local-dev.md](docs/runbooks/local-dev.md) for troubleshooting.

## Run and test

- Run the API backend: added in Phase 2 (`services/OmpOS.Api`).
- Run the web surface: added in Phase 4 (`apps/OmpOS.Web`, Blazor WebAssembly).
- Run the full test suite now: `dotnet test OmpOS.Linux.slnf -c Release`.
- Verify formatting: `dotnet format OmpOS.Linux.slnf --verify-no-changes --no-restore`.

## Repo map

| Path | Contents |
| ---- | -------- |
| `OmpOS.slnx` | Solution (all projects, including the MAUI head) |
| `OmpOS.Linux.slnf` | Solution filter for Linux/CI (everything except the MAUI head) |
| `Directory.Build.props` | Compiler, analyzer, determinism, and lock-file policy for every project |
| `Directory.Packages.props` | Central package versions (CPM with transitive pinning) |
| `global.json` | Pinned .NET SDK plus the test runner mode |
| `.editorconfig` | Single rule source for formatting and code style |
| `bootstrap.sh` | One-command bootstrap (restore, build, test, format) |
| `.env.example` | Every configuration variable, documented |
| `src/` | Libraries L0–L2 (`Contracts`, `Parser`, `Harness.*`, `Sandbox.*`, `Sessions`, `GenUi`, `Workflows`, `UI`) |
| `services/` | L3 hosts (`Api`) |
| `apps/` | L4 heads (`Web` Blazor WASM now, `App` MAUI in Phase 4) |
| `tests/` | Per-project suites plus contract fixtures and `ArchitectureTests` |
| `docs/` | Architecture, ADRs, open questions, runbooks |
| `docs/decisions/` | Architecture Decision Records |
| `docs/runbooks/` | Operator and developer runbooks |
| `deploy/` | `compose.yaml` for podman-compose (the Temporal dev server was removed on 2026-10-21) |
| `.github/workflows/` | `ci.yml` (build, test, format, audit, scan, compose, MAUI) |

## What is verified where

| Surface | Verifier | Scope |
| ------- | -------- | ----- |
| Linux (`ubuntu-latest`) | `ci.yml` job `build-test` | Everything in `OmpOS.Linux.slnf`: API, harness, sandbox, sessions, workflows, web head, EngineHost — libraries and all test suites. The worker project was removed on 2026-10-21 ([decision](docs/handoff.md#temporal-removal-and-railway-topology-2026-10-21)) |
| Linux dev box | `./bootstrap.sh` | Same scope as Linux CI; the MAUI head is never built locally on Linux |
| macOS (`macos-15`, human-deferred) | `ci.yml` job `shells` | `apps/OmpOS.App` (MAUI head) only — first run pending, see [docs/handoff.md](docs/handoff.md) |
| Inspection only (Linux) | Code review | The MAUI head's structure (project file, RCL sharing, `IEngineHost` registration) — its binaries are not produced on Linux |

### MAUI workload requirement

Restoring the full `OmpOS.slnx` needs the .NET MAUI workloads installed
(`dotnet workload install maui`, as the macOS CI `shells` job does). On Linux,
without those workloads, restore of `apps/OmpOS.App` (which targets
`net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst` with `UseMaui`) fails
(missing-workload `NETSDK1147`, `maui-tizen` chain) — so the supported Linux path
is the solution filter, which excludes the MAUI head:

```sh
dotnet test OmpOS.Linux.slnf -c Release
```

## Before any public deployment

Three blockers must clear before the API or web head is exposed publicly:

- **Auth provider.** The API validates OIDC bearer tokens fail-closed. Production needs `Api:Auth:Authority` (https) and `Api:Auth:Audience`. Do not expose the API publicly until an identity provider is configured for it (OD-03).
- **Production API origin.** The web head's `connect-src` is generated at build time from one validated origin (`OMPOS_API_ORIGIN`, see `vercel.ts` and `scripts/vercel/render-csp.sh`). Set it to the production API origin, then rebuild the web head.
- **Native macOS head (human).** The Avalonia macOS head's first warning-free build and spot-check run on a macOS runner (v0.10.0 Phase 22). The MAUI head (`apps/OmpOS.App`) builds only in the `ci.yml` `shells` job until it retires after parity (v0.10.0 Phase 25). See [ADR-0004](docs/decisions/ADR-0004-cross-platform-ui.md).

Full per-AC verdicts and the human verification steps: [docs/handoff.md](docs/handoff.md).


## Heads

| Head | Project | Status |
| ---- | ------- | ------ |
| Web (Blazor WASM) | `apps/OmpOS.Web` | Verified on Linux and in a browser |
| Desktop (MAUI Blazor Hybrid) | `apps/OmpOS.App` | Structure verified; binaries deferred to macOS CI |
| Mobile (MAUI Blazor Hybrid) | `apps/OmpOS.App` | Structure verified; binaries deferred to macOS CI |

## Docs

- [docs/architecture.md](docs/architecture.md) — layers, components, data flow.
- [docs/decisions/ADR-0001-monorepo-layout.md](docs/decisions/ADR-0001-monorepo-layout.md) — repo layout and dependency layers.
- [docs/decisions/ADR-0002-surface-technology.md](docs/decisions/ADR-0002-surface-technology.md) — Blazor, MAUI, SSE, harness, workflow, and sandbox choices.
- [docs/open-questions.md](docs/open-questions.md) — every unverified external fact, with owner.
- [docs/runbooks/local-dev.md](docs/runbooks/local-dev.md) — bootstrap, local dependency stack, troubleshooting.
- [docs/runbooks/sandbox.md](docs/runbooks/sandbox.md) — sandbox prerequisites and providers.
- [CONTRIBUTING.md](CONTRIBUTING.md) — branches, commits, formatting, lock files, TODO policy.
