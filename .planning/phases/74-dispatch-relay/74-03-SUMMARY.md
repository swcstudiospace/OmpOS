---
phase: 74-dispatch-relay
plan: 74-03
subsystem: api
tags: [dispatch, pairing, credential, openapi]
requires:
  - 74-01 IDispatchStore and InMemoryDispatchStore
provides:
  - Dispatch options, disabled middleware, DispatchDevice scheme and pairing
  - Connect seam that answers 503 hub_not_ready until 74-05
affects: [74-05, 74-07]
actuals:
  tokens: 0
  tasks: 3
  commits: []
tech-stack:
  added: []
  patterns:
    - Device credential uses Dispatch:Credentials:SigningKeyPem and audience autonogrammer-dispatch-device
    - Connect authenticates only through scheme DispatchDevice; the owner claim is re-checked with AllowedSubjectRequirement
    - In-memory IDispatchStore is registered only in Development
key-files:
  created:
    - services/Autonogrammer.Api/Dispatch/DispatchModule.cs
    - services/Autonogrammer.Api/Dispatch/Devices/DeviceEndpoints.cs
    - tests/Autonogrammer.Api.IntegrationTests/Dispatch/DispatchApiHost.cs
  modified:
    - services/Autonogrammer.Api/Program.cs
    - services/Autonogrammer.Api/OpenApi/OpenApiConfig.cs
    - docs/api/openapi.json
    - apps/Autonogrammer.Web/wwwroot/service-worker.published.js
key-decisions:
  - "Pairing wire types live in DeviceEndpoints because the landed PairDispatchDeviceRequest does not carry platform, protocolVersions or a public JWK."
  - "Dispatch:Enabled outside Development is refused until a durable store is registered. This plan registers InMemoryDispatchStore only in Development."
requirements-completed: [AG-74-1, AG-74-2, AG-74-14, AG-74-17]
# Phase 74 plan 03 summary
Account-bound pairing, the device credential, and the connect seam. The feature is off by default and answers 404 dispatch_disabled after authorization.
## Tests
`/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*"`
34 passed, 0 failed.
## Gaps
PostgresDispatchStore is not wired here. 74-05 replaces NotReadyDispatchConnectHandler and must not regenerate openapi.json.
