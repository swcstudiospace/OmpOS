# Phase 3: Platform - Research

**Researched:** 2026-10-20
**Domain:** sandbox provider abstraction and lifecycle (fake, podman-compose, OpenShell/agentOS/Railway/dstack stubs), durable session-run workflows (Temporal .NET vs direct runner), deterministic workflow rules and replay testing
**Confidence:** HIGH — sandbox-layer facts come from the project's own verified research corpus (`.planning/research/ARCHITECTURE.md` Pattern 5, `.planning/research/PITFALLS.md` Pitfall 14, both provenance-tagged) plus live local verification of the podman toolchain this session; Temporal facts are `[CITED]` from official docs/NuGet read this session.

Provenance tags: `[VERIFIED: local]` = executed on this host. `[CITED: source]` = official docs/NuGet. `[REPO: path]` = read from this repo's provenance-tagged research corpus. `[INFERENCE]` = reasoned.

<verified_facts>
## Verified Facts

### VF-1 — Local podman toolchain (SBOX-03) — `[VERIFIED: local, this session]`

- Host: Linux, cgroups **v2**, `podman 4.9.3` (`podman info`), `podman-compose 1.6.0` (pip-installed at `/opt/podman-compose`).
- `podman-compose --profile workflows config` on `deploy/compose.yaml` exits 0; service `temporal` present (FOUND-08 evidence, Phase 1).
- **Gap to flag:** OpenShell v0.1.4's podman compute driver requires **Podman 5.x** (`[REPO: PITFALLS.md:290]`) — this host has 4.9.3, so `OpenShellProvider` local provisioning MUST surface the typed `PrerequisiteMissing` failure here; do not "fix" by upgrading the host inside the phase.

### VF-2 — Sandbox layer truths (SBOX-01..05) — `[REPO: ARCHITECTURE.md:260-280, PITFALLS.md:283-300]`

- `ISandboxProvider { ProvisionAsync(spec, idempotencyKey), AttachAsync, ExecAsync, StreamLogsAsync, SnapshotAsync, TeardownAsync }`; `SandboxStatus` enum already in Contracts (Requested, Provisioning, Ready, Attached, Degraded, TearingDown, Destroyed, Failed).
- Composition order (outer→inner target): `EnclaveBoundary(OpenShell(podman)(agentOS(omp)))`; each layer independently stubbable; the Phase 3 vertical slice uses `FakeSandboxProvider` only.
- **OpenShell v0.1.4** (NVIDIA): gateway (control plane: lifecycle, policy, credentials, logs) → compute driver provisions workload + separate supervisor; per-run gateway JWT + sandbox JWT; supervisor disconnect freezes agent. Podman driver prerequisites: Podman 5.x, cgroups v2, `systemctl --user start podman.socket`; gateway TOML `[openshell] version = 2` + `[openshell.gateway] compute_driver = "podman"` with `[openshell.drivers.podman]` options (`socket_path, network_name, sandbox_runtime_image, supervisor_image, image_pull_policy, stop_timeout_secs, grpc_endpoint`); policy **YAML `version: 1`** with `filesystem_policy{include_workdir, read_only, read_write}`; auto-detect order Kubernetes → Podman → Docker, **VM never auto-detected**; Landlock/seccomp probes fail closed; bind mounts need `enable_bind_mounts` + disabling resource admission; podman volumes lack `subpath`. Verified CLI invocation pattern from a sibling repo's sandbox policy checker: `openshell sandbox create --name X --policy <file> --no-auto-providers`; **no verified delete/exec/logs verb** — the stub must NOT invent them.
- **agentOS (rivet-dev)**: `@rivet-dev/agentos-core` is a **library VM inside a trusted Node process (V8 isolates + Wasm) — NOT a microVM** (no nested virt); hosted `@rivet-dev/agentos` drives a durable VM over RivetKit/HTTP `POST /gateway/{vm_id}/action/v1.<group>.<action>`; groups `vm, config, process, terminal, filesystem (mount/unmount/listMounts), javascript, python, network, software, cron`; `permissions.network.default: "deny"` with allow rules. Any prose saying "agentOS microVM" is wrong.
- **dstack (attested enclave shape)**: guest agent ≥0.6 serves `dstack.guest.v1` on Unix socket `/var/run/dstack.sock` at `/v1/{IssueCert,GetKey,Attest,AttestGpu,Info,Version}` (prpc: POST JSON/protobuf, GET query); external Worker service never hands out keys; `/v1/Version` is the cheapest liveness probe; pre-0.6 agents answer non-JSON 404. Attestation proves what booted — the Phala post-mortem (2026-10-19) shows exfiltration paths (tenant logs, `Config.Env`, user-mode networking) that attestation does NOT prevent. "Sandboxed" and "attested" are orthogonal; the descriptor must carry real `Isolation`/`Attestation` capability values.
- **Railway**: no attestation offered on Railway sandboxes; `RailwaySwarmProvider` reports `Attestation: None` honestly.

### VF-3 — Landlock policy knob (golden policy model) — `[VERIFIED: live probe by Phase3Research agent, 2026-10-20]`

`landlock.compatibility` defaults to `best_effort`; `hard_requirement` is the fail-closed option. The omega-prime policy fleet uses `best_effort`; OmpOS's generated OpenShell policy golden should pin **`hard_requirement`** (isolation defaults are explicit per SBOX-05, and fail-open isolation violates it).

### VF-4 — Temporal .NET SDK (WFLW-01..03) — `[CITED: nuget.org, github release notes, dotnet.temporal.io docs, read 2026-10-20]`

- `Temporalio` **1.20.0** (2026-09-28); keep `Temporalio.Extensions.Hosting` on the **same version** (profile pages may still show 1.18.0 — pin explicitly). Standalone Activities GA across SDKs 2026-09-15.
- Replay testing without a server: `Temporalio.Worker.WorkflowReplayer` — `new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<T>())` then `ReplayWorkflowAsync(WorkflowHistory.FromJson("id", historyJson))`; non-determinism throws. Histories for fixtures can be exported from CLI/UI JSON. This is how WFLW-02's "replay test passes" is verified with no Temporal server in CI.
- Worker hosting: `Temporalio.Extensions.Hosting` `AddHostedTemporalWorker(...)` (IHostedService over `ITemporalClient` from `AddTemporalClient`); deployment options must be set at registration, not later (obsolete overload warns). Recommended: worker and web app in separate processes — matches the existing `services/OmpOS.Workflows.Worker` split.
- Determinism rules to enforce in `SessionRunWorkflow`: no `DateTime.Now`/`Task.Run`/`Guid.NewGuid()`/`Random` inside workflow code — use `Workflow.UtcNow`, `Workflow.Random`, `Workflow.NewGuid`, `Workflow.Delay`; activities carry retry policies + heartbeats (`ActivityExecutionContext.Heartbeat`).
- Dev server for optional local runs: `temporal server start-dev` (the `temporal` CLI; the Phase 1 compose `temporal` service exists under the `workflows` profile for this).
- Routing criteria for the ADR (WFLW-02 doc): route through Temporal **only** when a run must survive process restarts or needs durable retries/timers; everything else stays on `DirectSessionRunner`.

### VF-5 — Secrets and isolation defaults (SBOX-05) — `[REPO: PITFALLS.md "How to avoid"]`

- Never pass provider API keys as env vars a logs/inspect surface can read; per-sandbox unique tokens; secrets via provider profiles/files with tight modes; no tokens in URLs; no bind mounts of `~/.omp`; compose files never mount host home dirs.
- Isolation defaults (locked, tested by a canary): no host paths mounted, no host secrets, per-sandbox credentials, outbound network **default-deny**; secret values must never leak into descriptors, logs, or workflow history.
</verified_facts>

<validation_architecture>
## Validation Architecture

| Req | Verification | Type |
|---|---|---|
| SBOX-01 | `dotnet test tests/OmpOS.Sandbox.Tests` — provider contract: timeouts + idempotency keys on every call; status transition coverage over the enum | unit/contract |
| SBOX-02 | `SandboxLifecycleTests` — TTL expiry triggers reaper; orphan reaper sweeps tagged-but-dead sandboxes; teardown retried with jittered backoff (fake clock); failed provision leaves session NOT Running | unit |
| SBOX-03 | `SandboxProviderContractTests<T>` against `FakeSandboxProvider` + `LocalPodmanComposeProvider` with injectable `IProcessRunner` (fake runner asserted; real `podman-compose config` invocation `[VERIFIED: local]` allowed as a skippable smoke — podman 4.9.3 present) | contract |
| SBOX-04 | stub conformance tests — each stub returns typed NotAvailable/Unverified; OpenShell stub asserts `PrerequisiteMissing` when podman < 5 (true on this host); golden TOML/YAML generated from the typed policy model pinned by golden tests (TOML `[openshell] version = 2`, YAML `version: 1`, `landlock.compatibility: hard_requirement`); open-questions rows owner-tagged | unit/golden |
| SBOX-05 | isolation-defaults tests + canary: provisioned spec never contains host paths/secrets; network policy default-deny; secret values absent from descriptor/log/history strings | unit |
| WFLW-01 | `SessionRunWorkflowContractTests<T>` against `DirectSessionRunner` (in-process; no Temporal types referenced by Contracts/abstractions) | contract |
| WFLW-02 | worker starts only when `Workflows:Enabled=true` (startup test); `WorkflowReplayer` replay test over a checked-in `WorkflowHistory` JSON fixture; determinism enforced by code-review rule + replay | test |
| WFLW-03 | same contract fixture generic over runner implementations | contract |

Phase-end gates mirror Phase 2: `./bootstrap.sh` green; new projects registered in `OmpOS.slnx` + `OmpOS.Linux.slnf`; new packages pinned in `Directory.Packages.props` (`Temporalio` 1.20.0 + `Temporalio.Extensions.Hosting` 1.20.0 — only in `OmpOS.Workflows`/worker projects, never referenced by API/UI layers).
</validation_architecture>

<assumptions_log>
## Assumptions Log

- `[INFERENCE]` OpenShell CLI surface beyond `sandbox create` is unverified; the provider is a stub this phase, so no invented verbs.
- `[INFERENCE]` RailwaySwarmProvider stub's "swarm" semantics (the user's existing Railway deployment) need no vendor calls this phase; typed NotAvailable only.
- Temporal 1.20.0 + Hosting must be same-version; exact Hosting version re-check at pin time (`dotnet package search`).
</assumptions_log>

## Summary

The sandbox contract, lifecycle rules, and vendor truths are fully specified from the repo's provenance-tagged corpus plus live podman verification; Phase 3 can implement fake + podman-compose providers and typed stubs without inventing vendor APIs, and the Temporal layer is defined down to the replay-test API — all verifiable without a Temporal server or any cloud account.
