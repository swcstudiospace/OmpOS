---
phase: 02-core
asvs_level: undefined (no threat_model block in any 02-0N-PLAN.md)
block_on: none defined; every OPEN or PARTIAL item is treated as blocking
threats_total: 9
threats_open: 0
verdict: SECURED
audited: 2026-10-21
head: 144ca10 (working tree has uncommitted changes; findings describe the working-tree contents as read; HEAD not re-checked in this pass)
scope: the nine Phase 2 mitigations named in the security-gate brief, re-verified after the change list of 2026-10-21
---

# Phase 2 Core: Security Verification

**Verdict: SECURED. `threats_open: 0`.** All nine mitigations are MITIGATED. P2-08 was closed on 2026-10-21 with 404 and 415 body-hygiene tests; no production change was needed.

Changes since the previous audit (3 PARTIAL to 1):
- P2-01 (CORS default-deny) moved PARTIAL to MITIGATED. `Cors_DefaultDeny_BlocksSimpleAndPreflight` (`tests/OmpOS.Api.IntegrationTests/SecurityMitigationsTests.cs:19`) now runs on the default host with no CORS override.
- P2-08 moved PARTIAL to MITIGATED. The 404 and 415 bodies are covered by `ProblemDetails_NotFound_HidesInternals` and `ProblemDetails_UnsupportedMediaType_HidesInternals` (`tests/OmpOS.Api.IntegrationTests/SecurityMitigationsTests.cs`). Both bodies are fixed strings plus a trace id.
- P2-04 moved PARTIAL to MITIGATED. `CorrelationId_ParsableButOversizedValue_IsMinted` (`SecurityMitigationsTests.cs:132`) reaches the length arm (see row).

## Register source

`02-01-PLAN.md` through `02-06-PLAN.md` contain no `<threat_model>` block. The register is therefore the nine Phase 2 mitigations named in the gate brief. Each is one row below.

## Status rules

- **MITIGATED**: code reference at file:line, and a named test that exercises the behaviour.
- **PARTIAL**: code present, but a named behaviour (or a separate code branch) has no test.
- **OPEN**: no mitigation found in code.
- **threats_open** = count of OPEN plus PARTIAL items. A PARTIAL item is unverified, so it blocks SECURED.

## Threat table

| ID | Mitigation | Status | Code (file:line) | Test | Fix required / residual |
|---|---|---|---|---|---|
| P2-01 | CORS default-deny when `Api:Cors:Origins` is empty | MITIGATED | `services/OmpOS.Api/appsettings.json:3-5` (`"Cors": { "Origins": [] }`); `tests/OmpOS.Api.IntegrationTests/OmpApiFactory.cs` adds no CORS override; `services/OmpOS.Api/Security/ConfigureCorsOptions.cs:35-38` (carried forward, not re-read this pass) | `Cors_DefaultDeny_BlocksSimpleAndPreflight` (`tests/OmpOS.Api.IntegrationTests/SecurityMitigationsTests.cs:19`): a cross-origin simple GET and a preflight from `https://evil.example` both lack `Access-Control-Allow-Origin`. The non-empty branch is covered separately by `Cors_DisallowedOriginGetsNoHeaders` (`tests/OmpOS.Api.IntegrationTests/SessionEndpointTests.cs:206`). | None. [INFERENCE: the test relies on the test host loading the API `appsettings.json`, the `WebApplicationFactory` default; not executed in this pass.] |
| P2-02 | Body size limit (`Api:MaxRequestBodyBytes`, 413) | MITIGATED | `services/OmpOS.Api/Endpoints/SessionEndpoints.cs:297-318` (`ReadLimitedAsync`, carried forward); 413 at `:74-76` and `:250-254`; default 1048576 at `services/OmpOS.Api/appsettings.json:7` | `Prompt_OversizedBody_Returns413` (`tests/OmpOS.Api.IntegrationTests/SessionEndpointTests.cs:103`) | None |
| P2-03 | Content-type enforcement (`application/json` or `application/*+json`, 415) | MITIGATED | `services/OmpOS.Api/Endpoints/SessionEndpoints.cs:284-295` (`IsJsonContentType`); 415 at `:245-248` (prompt) and `:83-86` (create); carried forward | `Prompt_WrongContentType_Returns415` (`tests/OmpOS.Api.IntegrationTests/SessionEndpointTests.cs:89`) | None. The create path has no direct test; it calls the same predicate. |
| P2-04 | Correlation id accepted only if it parses as a GUID and is at most 128 characters; otherwise a GUIDv7 is minted | MITIGATED | `services/OmpOS.Api/Middleware/CorrelationIdMiddleware.cs:33-35` (`Guid.TryParse(inbound, out _) && inbound!.Length <= 128`; otherwise `Guid.CreateVersion7()`) | `CorrelationId_ValidGuid_IsEchoed` (`tests/OmpOS.Api.IntegrationTests/SecurityMitigationsTests.cs`) and `CorrelationId_RoundTrips` (`tests/OmpOS.Api.IntegrationTests/SessionEndpointTests.cs:225`) cover the echo path. `CorrelationId_NonGuidValue_IsMinted` covers the parse arm. `CorrelationId_OversizedValue_IsMinted` (129 `x`, `SecurityMitigationsTests.cs:55`) fails the parse arm. `CorrelationId_ParsableButOversizedValue_IsMinted` (`SecurityMitigationsTests.cs:132`) sends a 32-character GUID padded with 97 leading spaces to 129 characters. The padded value passes `Guid.TryParse` and fails `Length <= 128`, so it exercises the length arm, and the test asserts a minted GUID. [INFERENCE: `Guid.TryParse` accepts surrounding whitespace, and the in-memory test server passes the header through without HTTP trimming; neither was executed.] | None. The length arm is covered. |
| P2-05 | Prompt text never logged; redacted length only | MITIGATED | `services/OmpOS.Api/Services/PromptRedactor.cs:16-19`; `services/OmpOS.Api/Services/PromptRunner.cs:138`, `:161`, `:117`, `:171`; default `services/OmpOS.Api/appsettings.json:23` (`LogPrompts: false`); carried forward | `Redaction_PromptNeverLogged` (`tests/OmpOS.Api.IntegrationTests/GuardTests.cs:98`) | Holds for the shipped default. See O-5: `OmpOS:Debug:LogPrompts=true` logs verbatim by design. |
| P2-06 | SSE resume position (`Last-Event-ID` header and `?since=`) validated and clamped | MITIGATED | `services/OmpOS.Api/Endpoints/EventStreamEndpoints.cs:46-57` (header), `:59-68` (query), `:70-73` (negative rejected with 400), `:75` (`Math.Max(0, ...)`); `services/OmpOS.Api/Sse/SessionEventStreamer.cs:74`, `:163`; carried forward | `Events_NegativeResume_Returns400` (`tests/OmpOS.Api.IntegrationTests/ReviewFindingsTests.cs:80`); resume-position tests in `tests/OmpOS.Api.IntegrationTests/SseResumeTests.cs` (names carried forward from the previous audit, not re-grepped) | None required. Recommended: add a non-integer case (`abc`) to the 400 theory; the branches at `EventStreamEndpoints.cs:51-54` and `:62-65` are untested. There is no upper clamp; a position above the newest seq yields no replay until the log catches up. |
| P2-07 | Rate limiting on prompt submission and session creation (429 with Retry-After) | MITIGATED | `services/OmpOS.Api/Security/SessionRateLimiterPolicy.cs:33-48`; `services/OmpOS.Api/Security/SessionCreationRateLimiterPolicy.cs:31-46`; wiring `services/OmpOS.Api/Program.cs:59-64,106`; `services/OmpOS.Api/Endpoints/SessionEndpoints.cs:44`, `:55`; `services/OmpOS.Api/Security/RateLimitRejection.cs:20-35`; carried forward | `Prompt_OverBudget_Returns429WithRetryAfter` (`tests/OmpOS.Api.IntegrationTests/SessionEndpointTests.cs:184`); `Create_OverBudget_Returns429WithRetryAfter` (`tests/OmpOS.Api.IntegrationTests/ReviewFindingsTests.cs:60`); `Prompt_GlobalBucket_SecondSessionOverBudgetReturns429` (`tests/OmpOS.Api.IntegrationTests/ReviewFindingsTests.cs:42`) | None. The bucket is global (see O-4). |
| P2-08 | ProblemDetails responses carry no stack traces | MITIGATED | `services/OmpOS.Api/Program.cs:75` (`AddProblemDetails`; `CustomizeProblemDetails` at `:80` replaces the 500 detail with the fixed text "An unexpected error occurred."); `services/OmpOS.Api/Program.cs:136-138` (`UseExceptionHandler` outside Development; the developer exception page is Development-only, by framework default); `services/OmpOS.Api/Endpoints/SessionEndpoints.cs:281-282` (`ProblemResponse`, fixed detail string); `services/OmpOS.Api/Endpoints/EventStreamEndpoints.cs:43,53,64,72` (fixed detail strings) | `ProblemDetails_BadRequest_HidesInternals` covers 400. `ProblemDetails_UnhandledException_HidesInternals` (`SecurityMitigationsTests.cs:154`) covers the unhandled branch in Production. `ProblemDetails_NotFound_HidesInternals` and `ProblemDetails_UnsupportedMediaType_HidesInternals` (`SecurityMitigationsTests.cs`) cover 404 and 415; each asserts the status first, then no line beginning `at `, no `System.`, no `Exception` and no `StackTrace` text. | None |
| P2-09 | OwnerId cap of 256 characters | MITIGATED | `services/OmpOS.Api/Endpoints/SessionEndpoints.cs:99-101` (`ownerId.Length > 256` gives 400); `services/OmpOS.Api/Endpoints/CreateSessionRequest.cs:7-14`; carried forward | `Create_OwnerIdLength_BoundaryEnforced` (`tests/OmpOS.Api.IntegrationTests/ReviewFindingsTests.cs:120`; 256 gives 201, 257 gives 400) | None |

## Observations (not counted; outside the named mitigations)

- **O-1 No authentication.** `services/OmpOS.Api/Middleware/AuthBoundaryMiddleware.cs` publishes a fixed principal. `services/OmpOS.Api/Security/SingleDevPrincipal.cs` returns `local-dev` for every request. Any caller that can reach the API is the same user.
- **O-2 Client-chosen OwnerId.** `services/OmpOS.Api/Endpoints/SessionEndpoints.cs:105` honours the client's `OwnerId` unconditionally. `services/OmpOS.Api/Endpoints/CreateSessionRequest.cs:7-9` says it is "only honored while the host serves its single dev principal". The code does not check that condition.
- **O-3 No owner scoping on reads.** `ListSessionsAsync` (`SessionEndpoints.cs:112`) returns every session. `GetSessionAsync` does not check the owner. Retention is unbounded (`SessionEndpoints.cs:61-64`, documented as accepted for Phase 2).
- **O-4 Global rate-limit bucket.** `SessionRateLimiterPolicy.cs:9-12` documents one shared bucket. One client can exhaust the budget for all callers.
- **O-5 Verbatim prompt logging is an opt-in.** `services/OmpOS.Api/Options/DebugOptions.cs:15` and `appsettings.json:23`. P2-05 holds only while `LogPrompts` stays false. Decide whether this switch stays in production builds.

## Method

Read and grep only. No build, test, format or git write command was run. Re-verification in this pass covered P2-04 and P2-08 only (code and test bodies re-read); other rows are unchanged from the prior record. Behaviour claims (CORS denial with no allowed origins, `Guid.TryParse` whitespace handling, in-memory test-server header handling) come from ASP.NET and .NET semantics and are marked `[INFERENCE]` where not observed. Line numbers for the rows touched in this pass refer to the working tree as read.
