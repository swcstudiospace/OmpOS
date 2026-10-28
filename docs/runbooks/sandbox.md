---
description: "Host prerequisites and local provider notes for the OmpOS sandbox, which runs agent processes under podman."
---

# Sandbox runbook

Status: Partial (the fake provider, the local podman-compose provider and the cloud environment clients (`FakeCloudEnvironmentClient`, `StaticPoolEnvironmentClient`, `RailwayCloudEnvironmentClient`) are built and tested; vendor providers are blocked stubs; nothing was run on Railway)

Last verified: 2026-10-28

Applies to: v0.15.0

The sandbox abstraction, a fake provider and a local podman-compose provider exist (Phase 3 and later); vendor providers stay blocked on external criteria. This runbook records the host prerequisites and the local provider notes so the machine can be prepared ahead of time. Podman is the only supported container runtime.

**Status of this release** (recorded 2026-10-28, tree 7535859). `OmpOS.Sandbox.Tests` passed 329 tests on the final tree. The cloud control plane (`services/OmpOS.Api`) uses `ICloudEnvironmentClient`; `RailwayCloudEnvironmentClient` is built for Railway's GraphQL API and was never run against Railway (nothing of OmpOS is deployed there; the operations are unverified, ADR-0020 D8). Which tests drive a real podman (`LocalPodmanComposeProviderContractTests` uses `CommandProbe`) was not re-checked for this page: UNVERIFIED. No model turn, nothing on Windows, macOS or Android.

## Host prerequisites (OpenShell podman driver)

- Podman 5.x or newer.
- cgroups v2.
- An active per-user socket:

```sh
systemctl --user start podman.socket
```

Note: Ubuntu 24.04 apt ships Podman 4.9.3, which is enough for `podman-compose` but not for the OpenShell podman driver. Install Podman 5.x from the upstream repo on machines that run the OpenShell provider. Until then the provider reports typed `PrerequisiteMissing`; Docker is not used as a substitute.

## Local compose provider

- The local provider shells out to `podman-compose` with the stack in `deploy/compose.yaml`.
- The Temporal dev server and its `workflows` profile were removed on 2026-10-21. There is no `temporal` service and no `/home/temporal` volume; ADR-0001's volume note is superseded. See [handoff decision](../handoff.md#temporal-removal-and-railway-topology-2026-10-21).

## Provider posture

- Fake provider: in-memory, always available, used by tests and the vertical slice.
- Local podman-compose provider: real once podman is present.
- OpenShell, agentOS, Railway Swarm, and attested-enclave providers: interface-conforming stubs returning typed `NotAvailable` or `Unverified`, each linked to an entry in `docs/open-questions.md`. Vendor APIs are never invented. The Railway cloud environment client is a separate seam (`RailwayCloudEnvironmentClient`, see [Cloud agents](cloud-agents.md)).
