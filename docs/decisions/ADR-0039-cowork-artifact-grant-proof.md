# ADR-0039: Cowork artifact grants — device/session proof and approved-destination enforcement (decision inputs)

- Status: Proposed 2026-11-26 (decision inputs only; nothing in this record is built, and no code was changed for this record). Owner decisions recorded 2026-11-26 (Phase 84 Task 4, decider: product owner): Gate A = A1 (reuse the Dispatch ES256 device credential with an artifact-specific audience + server-validated session id) and Gate B = B4 (no-network default for v0.16.0; grants recorded but network-inert). Status stays Proposed because the 71-08 Tasks 2/3 code (grant proof, browser implementation, destination-evidence route) is not yet written; the decisions above are the recorded calls that unblock that code, not a claim it exists.
- Date: 2026-11-26
- Owner: autonogrammer-maintainers
- Deciders: autonogrammer-maintainers (the integrator; the two decisions in this record need explicit approval before 71-08 Tasks 2/3 code lands)
- Scope: the two gate-class decisions that block v0.16.0 gap 71-G1 rows 7 and 8 (`docs/quality/71-g1-scope-2026-11-25.md:30-31`, work orders `:57-58`; 71-08-PLAN.md Tasks 2 and 3 preconditions): (A) the device/session proof-of-possession protocol that binds artifact grants, and (B) the evidenced destination-enforcement mechanism for approved origins. This record amends ADR-0033 D8 and its open risk on DNS rebinding once a decision is taken; until then it changes nothing.
- Naming: post-rename spelling; on-disk names are `Autonogrammer.*` today.

## Context

Rows 7 and 8 are gate-class by the audit's own classification. 71-08 Task 2 states its precondition verbatim: "The integrator has approved and evidenced the precise device/session proof protocol and its browser implementation... Neither a caller-supplied device nor session string is identity. This protocol is currently unresolved; halt rather than fabricate it." Task 3 states: "A documented and tested mechanism exists for enforcing the browser artifact's exact approved destinations against private-address resolution and DNS rebinding... CSP hostname matching alone does not prove that property... If this evidence is absent, stop and escalate." Both preconditions are still absent at HEAD, and both rows depend on earlier landings that have since shipped (rows 1-3, 5, 6 in commit `10fb9933`: public `IArtifactGrantStore`, per-version origin requests on the wire and in both stores, API both-modes). What remains is exactly the two decisions.

Facts read in this pass (`file:line` at HEAD):

- Today the grant routes trust caller-supplied identity. `PutGrantAsync` reads the whole `ArtifactGrantDocument` — including `Device` — from the request body and stores it (`services/Autonogrammer.Api/Cowork/Artifacts/ArtifactEndpoints.cs:339-353`); `ArtifactGrants` is a process-local `ConcurrentDictionary` (`ArtifactGrants.cs:10`), so nothing survives a restart and nothing proves device or session. The durable `IArtifactGrantStore` (`src/Autonogrammer.Cowork.Artifacts/IArtifactGrantStore.cs`) exists but the API grant path does not use it yet — migrating caller-supplied device strings into durable storage before a proof decision would entrench the wrong authority, so the migration waits with the decision.
- Grants currently have no effect on rendering. `RenderAsync` lists grants and discards them (`ArtifactEndpoints.cs:379`, `_ = grants.List(...)`), then wraps with `scripts: true` and no approved origins (`:381`), so the policy is always the locked default (`ArtifactDocumentPolicy.cs:13,17`, `connect-src 'none'`). Tickets bind only (account, artifact, version, hash) (`ArtifactRenderTickets.cs:61-90`). ADR-0033 D8 already prescribes that approved hosts change the document policy and that the render route re-checks the grants — the gap is the identity those grants bind to and the enforcement that makes the approved hosts safe.
- Candidate infrastructure already in the tree. Dispatch has a server-verifiable device credential: `DeviceCredentialIssuer` signs a JWT device credential with proof of possession and `DeviceCredentialValidator` checks it (`services/Autonogrammer.Api/Dispatch/Devices/`; design in ADR-0036). It is audience-bound to the dispatch relay, not to artifacts. `ISessionRepository` (`src/Autonogrammer.Sessions/ISessionRepository.cs`) stores chat sessions (Cowork/Code, tool approvals, event log) — it is not an authenticated-browser-session store, so "session" in the grant sense (the user's authenticated web session) has no server-side identity today. `EgressGuardProxy` (`src/Autonogrammer.Harness.BrowserUse/Egress/EgressGuardProxy.cs`) pins and classifies egress for host-launched Chromium in Browser Use (ADR-0034); it sits outside the user's web iframe and cannot govern requests the iframe makes from the user's device. ADR-0033's open risks already record the residual: "A granted host can resolve to a private address (DNS rebinding); the surface cannot see resolved addresses" (`ADR-0033:230`).
- Authentication today: owner-token bearer (single operator, ADR-0021) or JWT subject; the web head attaches the bearer through `BearerTokenHandler` (`apps/Autonogrammer.Web/Services/Artifacts/ArtifactWebClient.cs:8`). There is no per-device or per-session server-issued credential attached to today'.

## Decision

No decision is taken by this record. The two gates below are recorded as decision inputs for the integrator; 71-08 Tasks 2 and 3 stay blocked until each is approved as marked.

### Gate A (row 7): device/session proof protocol

Options, with evidence:

- A1 — Reuse the Dispatch ES256 device credential with an artifact-specific audience. One device-identity primitive (key in OS store, proof of possession per use), already implemented and reviewed under ADR-0036. Risk: conflates relay trust with artifact trust; needs an explicit audience/scope amendment and a rule that a relay credential never authorises artifact grants without the artifact audience.
- A2 — New artifact device credential: per-device keypair enrolled after account authentication, server-issued short-lived JWT, proof of possession (challenge signature) required at every grant bind/revoke. Clean trust domain, but new enrollment UX, key storage on the web head (where the OS keystore is unavailable to a Blazor WASM client) and new code.
- A3 — Collapse to account identity: treat the authenticated account as the device (true of the single-operator owner-token deployment), persist version grants per account, hold session grants in memory keyed by the existing bearer. Simplest, no new primitive. Rejected as a general answer: it contradicts the multi-device semantics of ADR-0033 D8 ("device" column, cross-device revocation) and ADR-0030 accounts; acceptable only if the integrator explicitly scopes artifacts to single-operator deployments.

Session half (same gate): "session" must mean a server-issued, server-recognisable session identifier bound at authentication (e.g. a session id carried in the auth bearer/session cookie and validated server-side per request), not a string the caller asserts. `ISessionRepository` does not provide this and must not be assumed to.

Recommendation recorded for the decider: A1 with an artifact-specific audience for the device half, and a server-validated session id for the session half; A3 only as an explicit single-operator scope decision. Approval must name the browser implementation (how the web head holds and presents the credential) before any grant-binding code is written, per the Task 2 precondition.

### Gate B (row 8): approved-destination enforcement

Options, with evidence:

- B1 — CSP hostname matching only (`connect-src <approved hosts>`). Already writable today (`ArtifactDocumentPolicy.ApplyOrigins`, `:72-91`), and 71-08 Task 3 explicitly rules it insufficient: a hostname can rebind from public to private between the check and the fetch, and the surface cannot see resolved addresses (ADR-0033:230). Not a mechanism; available as a known-risk acceptance only.
- B2 — `EgressGuardProxy` (ADR-0034). Evidenced, but attached to host-launched Chromium, not to the user's web iframe (Task 3 precondition says so). Infeasible for the web render path without proxying the user's traffic through the host.
- B3 — API-hosted network proxy for approved hosts. Would enforce destination checks server-side, but changes the stated device-request semantics ("Requests are made from your device", ADR-0033 D8 dialog copy) and needs a user-approved amendment of that decision plus the data-leak surface of proxying user traffic. Must not be substituted silently.
- B4 — Keep the current no-network default (the present, wired behaviour) and record grants as audited decisions with no network effect until a mechanism is evidenced. Honest for v0.16.0: the approval dialog must then state that network stays off, so "Allow" is not cosmetic. This keeps AG-71-17's "default off" true and avoids claiming enforcement that does not exist.

Recommendation recorded for the decider: B4 for v0.16.0, with B1 as an explicit, owner-accepted risk amendment of ADR-0033's DNS-rebinding open risk only if the owner wants approved hosts enabled without an evidenced mechanism — never silently. Any of B1-B3 reopens Task 3's evidence requirement (controlled DNS responder tests: public-to-private flip, redirects, API-origin attempts, per the audit row 8 work order).

## Candidate chosen

Decided 2026-11-26 by the owner (Phase 84 Task 4): A1 (Dispatch ES256 device credential, artifact-specific audience) + server-validated session id for Gate A; B4 (no-network default, grants recorded but network-inert) for Gate B in v0.16.0. The integrator's approval (option per gate, browser implementation for A, evidence route for B) is recorded in the Status header above; 71-08 Tasks 2/3 proceed per the audit work orders under these calls. Alternatives and their costs are in the Decision section above.

## Licence

No third-party component is adopted by this record. Any proof mechanism built from it reuses this repository's own code, licensed `AGPL-3.0-or-later` (ADR-0018; licence text https://www.gnu.org/licenses/agpl-3.0.html). The existing JWT plumbing a Gate A implementation would reuse (`Microsoft.IdentityModel.JsonWebTokens`, Apache-2.0, https://github.com/AzureAD/azure-activesdk-for-dotnet) is already an adopted central package, not a new adoption by this record.

## Verification route

This record is verified by reading, not running: the audit evidence in `docs/quality/71-g1-scope-2026-11-25.md`, the preconditions in `.planning/phases/71-cowork-artifacts/71-08-PLAN.md`, and the current-state facts cited above in `services/Autonogrammer.Api/Cowork/Artifacts/` and `src/Autonogrammer.Cowork.Artifacts/Rendering/`. Structural gates: `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*DecisionRecordTests*"` covers this record and its index row. Once Gate A is approved, the tests it unblocks are `tests/Autonogrammer.Api.IntegrationTests/Artifacts/ArtifactEndpointsTests.cs`; once Gate B is approved, `ArtifactRenderTicketTests.cs` and `ArtifactDocumentPolicyTests.cs` (rebinding evidence). Neither suite is written by this record, and no row is marked complete by it.

## Open risks

- Gate A: A1 reuses a relay credential; if the audience separation is done carelessly, a dispatch device credential could authorise artifact grants across trust domains. A2's web-head key storage has no OS keystore equivalent in Blazor WASM and may end up weaker than the desktop story.
- Gate A: the session half has no existing server-side store; inventing "session id" semantics inside the artifact routes without the auth layer's cooperation would repeat the caller-asserted-string mistake under a new name.
- Gate B: B4 keeps grants network-inert; if the approval dialog copy is not changed to say network stays off, the feature misleads users into believing approval enables connectivity (AG-71-17 conformance risk).
- Gate B: B1-as-accepted-risk leaves DNS rebinding to private addresses exploitable from the user's device network; the acceptance must be explicit and recorded as an amendment of ADR-0033's open risk, not inferred from silence.
- Rows 7 and 8 also wait on the rollout-gate decision (audit row 0) for any code that writes permission metadata; this record does not substitute for it.
