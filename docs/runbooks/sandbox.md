# Sandbox runbook

Sandbox providers arrive in Phase 3. This runbook records the host prerequisites and the local provider notes so the machine can be prepared ahead of time.

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
- OpenShell, agentOS, Railway Swarm, and attested-enclave providers: interface-conforming stubs returning typed `NotAvailable` or `Unverified`, each linked to an entry in `docs/open-questions.md`. Vendor APIs are never invented.
