---
phase: 71-cowork-artifacts
plan: 04
subsystem: api
tags: [artifacts, tools, render-tickets, openapi]

requires:
  - phase: 71-01
    provides: ArtifactService and ICoworkToolProvider seam
  - phase: 71-02
    provides: ArtifactDocumentPolicy and HtmlDocumentWrapper
  - phase: 71-03
    provides: ArtifactWireContract and InternalsVisibleTo Autonogrammer.Api
provides:
  - Host-owned artifact tool family with exact-once edits, folder grants and a write limiter
  - Owner-scoped artifact routes, 60-second HMAC render tickets and version-bound grants
  - Counter autonogrammer.cowork.artifact.count and hash-only audit events
affects: [71-05, 71-06]

actuals:
  tokens: 0
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns:
    - "Artifact routes stay mapped while Cowork:Artifacts:Enabled is false and answer 404 cowork_disabled"
    - "The render route carries OwnResponseSecurityHeadersMetadata so the middleware writes only nosniff"
    - "PUT ceilings are counted in the handler because TestServer does not apply Kestrel's body limit"

key-files:
  created:
    - src/Autonogrammer.Cowork.Artifacts/Tools/ArtifactToolProvider.cs
    - services/Autonogrammer.Api/Cowork/Artifacts/ArtifactEndpoints.cs
    - services/Autonogrammer.Api/Cowork/Artifacts/ArtifactRenderTickets.cs
  modified:
    - services/Autonogrammer.Api/Program.cs
    - services/Autonogrammer.Api/Hosting/SecurityHeadersMiddleware.cs
    - docs/api/openapi.json

key-decisions:
  - "API version PUT accepts a caller-supplied id through the store mutation port because ArtifactService always mints a new id"
  - "Metadata-only PUT answers 200 with X-Artifact-Same-Hash and does not store the JSON body as version bytes"
  - "Service worker already listed /cowork/, so that file was left unchanged"

requirements-completed: [AG-71-8, AG-71-9, AG-71-17, AG-71-19, AG-71-20, AG-71-24]

metrics:
  tasks: 3
  files: 20
  tests: 32

duration: 0
completed: 2026-10-30
---

# Phase 71 Plan 04: Artifact tools and account routes

Host-owned `artifact_create`, `artifact_update`, `artifact_read` and `artifact_list` sit behind the MCP gateway configuration. The API maps owner-scoped artifact routes plus an anonymous 60-second HMAC render route.

## What shipped

- Tool provider returns `Preview.Grant = (artifact.write, cowork)` on create and update, wraps read content as untrusted text, refuses paths outside trusted folders and the storage folder, and rate-limits writes.
- Routes answer another owner's id with the same 404 as an absent id. Content is `application/octet-stream` with `attachment`. Render tickets bind account, artifact, version and hash for at most 60 seconds.
- Create, update, grant and revoke increment `autonogrammer.cowork.artifact.count` and write audit payloads that carry hashes and origins only.

## Deviations

- Extra files were required by MA0048 (one type per file): origin rules, account directory, audit writer, JSON documents and the two limiter policies.
- `HostileCorpusTests` gained two outcome cases so the tool fixtures it scans do not throw. `/cowork/` was already in the service worker prefix list.

## Gaps

- Title changes on update are not persisted. The store has no rename.
- Audit rows use `account_id` as the table key. The JSON payload does not.
- Cowork tool host registration is 71-05, not this plan.
