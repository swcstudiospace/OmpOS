# OmpOS

OmpOS is a chat-style agent workspace — a Grok Chat / Cursor Agent–class client whose engine is **omp** rather than Cursor. It orchestrates omp sessions and renders their streamed output as **Generative UI** (structured, interactive blocks, not plain text). It is a **Desktop ACP Harness built in C#** that grows into one full-stack product: a Blazor WebAssembly web app hosted on Vercel, Avalonia native heads for desktop and Android, and a unified ASP.NET Core backend on Railway (session runs execute in-process in the API; no Temporal and no worker service) and sandboxed agent execution.

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
| `OmpOS.slnx` | Solution (all projects, including the Android head) |
| `OmpOS.Linux.slnf` | Solution filter for Linux/CI (everything except the Android head) |
| `Directory.Build.props` | Compiler, analyzer, determinism, and lock-file policy for every project |
| `Directory.Packages.props` | Central package versions (CPM with transitive pinning) |
| `global.json` | Pinned .NET SDK plus the test runner mode |
| `.editorconfig` | Single rule source for formatting and code style |
| `bootstrap.sh` | One-command bootstrap (restore, build, test, format) |
| `.env.example` | Every configuration variable, documented |
| `src/` | Libraries L0–L2 (`Contracts`, `Parser`, `Harness.*`, `Sandbox.*`, `Sessions`, `GenUi`, `Workflows`, `UI`) |
| `services/` | L3 hosts (`Api`) |
| `apps/` | L4 heads (`Web` Blazor WASM, `Native` Avalonia desktop, `Android` Avalonia; `App` MAUI removed in v0.10.0, Phase 25) |
| `tests/` | Per-project suites plus contract fixtures and `ArchitectureTests` |
| `plugins/` | Agent loop plugins: three pinned submodules (`claude-ultrathink`, `agent-swarm`, `agent-substrate`) and the tracked `gsd-core-omp`; see [docs/plugins.md](docs/plugins.md) |
| `docs/` | Architecture, ADRs, open questions, runbooks |
| `docs/decisions/` | Architecture Decision Records |
| `docs/runbooks/` | Operator and developer runbooks |
| `deploy/` | `compose.yaml` for podman-compose (the Temporal dev server was removed on 2026-10-21) |
| `.github/workflows/` | `ci.yml` (build, test, format, audit, scan, compose, Android) |

Plugins: `plugins/` holds the omp plugins the agent loop runs: `claude-ultrathink`, `agent-swarm` and `agent-substrate` as pinned git submodules (`agent-substrate` is private), and `gsd-core-omp`, tracked in this repository. Clone with `git clone --recurse-submodules`, or run `git submodule update --init --recursive` in an existing clone. See [docs/plugins.md](docs/plugins.md).

## What is verified where

| Surface | Verifier | Scope |
| ------- | -------- | ----- |
| Linux (Omega CI pool, `[self-hosted, linux, x64, omega-ci]`) | `ci.yml` job `build-test` | Everything in `OmpOS.Linux.slnf`: API, harness, sandbox, sessions, workflows, web head, EngineHost — libraries and all test suites. The worker project was removed on 2026-10-21 ([decision](docs/handoff.md#temporal-removal-and-railway-topology-2026-10-21)) |
| Linux dev box | `./bootstrap.sh` | Same scope as Linux CI; the Android head is outside the Linux filter, so it is not built locally |
| Linux (Omega CI pool) | `ci.yml` job `android` | `apps/OmpOS.Android` (Android head) only: restore and `-warnaserror` build; no device or emulator needed |
| macOS (human-deferred) | none in CI | The Avalonia macOS head and the iOS head need macOS and Xcode; there is no macOS runner in the pool, so they are not built in CI (Mac channel, v0.10.0 Phases 22 and 24) |
| Inspection only (Linux) | Code review | Removed in v0.10.0 (Phase 25): the MAUI head's structure (project file, RCL sharing, `IEngineHost` registration) was inspected; its binaries were never produced on Linux |

### Android workload requirement

Restoring the full `OmpOS.slnx` needs the .NET Android workload
(`dotnet workload install android` on a developer machine; the Omega CI runner image
pre-installs the Android workloads and its SDK directory is read-only, so the CI job
`android` only lists them). Without the workload, restore of `apps/OmpOS.Android`
(which targets `net10.0-android36.0`) fails with a missing-workload error, so the
supported Linux path is the solution filter, which excludes the Android head:

```sh
dotnet test OmpOS.Linux.slnf -c Release
```

## Before any public deployment

Three blockers must clear before the API or web head is exposed publicly:

- **Auth provider.** The API validates OIDC bearer tokens fail-closed. Production needs `Api:Auth:Authority` (https) and `Api:Auth:Audience`. Do not expose the API publicly until an identity provider is configured for it (OD-03).
- **Production API origin.** The web head's `connect-src` is generated at build time from one validated origin (`OMPOS_API_ORIGIN`, see `vercel.ts` and `scripts/vercel/render-csp.sh`). Set it to the production API origin, then rebuild the web head.
- **Native macOS head (human).** The Avalonia macOS head's first warning-free build and spot-check run on a macOS host (v0.10.0 Phase 22); there is no macOS runner in the Omega CI pool, so they stay with a human on a Mac. The MAUI head (`apps/OmpOS.App`) and its `ci.yml` `shells` job were removed in v0.10.0 (Phase 25) after parity; the Android head (`apps/OmpOS.Android`) is built by CI job `android` instead. See [ADR-0004](docs/decisions/ADR-0004-cross-platform-ui.md).

Full per-AC verdicts and the human verification steps: [docs/handoff.md](docs/handoff.md).


## Heads

| Head | Project | Status |
| ---- | ------- | ------ |
| Web (Blazor WASM) | `apps/OmpOS.Web` | Verified on Linux and in a browser |
| Desktop (MAUI Blazor Hybrid, removed in v0.10.0) | `apps/OmpOS.App` | Removed in v0.10.0 (Phase 25); was structure verified, binaries deferred to macOS CI |
| Android (Avalonia) | `apps/OmpOS.Android` | Built by CI job `android` on the Linux runner; no device or emulator needed |

## Docs

- [docs/architecture.md](docs/architecture.md) — layers, components, data flow.
- [docs/decisions/ADR-0001-monorepo-layout.md](docs/decisions/ADR-0001-monorepo-layout.md) — repo layout and dependency layers.
- [docs/decisions/ADR-0002-surface-technology.md](docs/decisions/ADR-0002-surface-technology.md) — Blazor, MAUI, SSE, harness, workflow, and sandbox choices.
- [docs/open-questions.md](docs/open-questions.md) — every unverified external fact, with owner.
- [docs/runbooks/local-dev.md](docs/runbooks/local-dev.md) — bootstrap, local dependency stack, troubleshooting.
- [docs/runbooks/sandbox.md](docs/runbooks/sandbox.md) — sandbox prerequisites and providers.
- [CONTRIBUTING.md](CONTRIBUTING.md) — branches, commits, formatting, lock files, TODO policy.
