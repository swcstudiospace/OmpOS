# ADR-0021: Hosted single-operator deployment: owner-token sign-in, engine isolation and an approval policy for the direct run path

- Status: Accepted 2026-10-29
- Date: 2026-10-29
- Owner: autonogrammer-maintainers
- Deciders: autonogrammer-maintainers
- Scope: what must be true before the API and the web head run as a real, internet-facing application for one operator (milestone v0.16.0, phases 66.2 to 66.4): how the operator signs in, what the engine child process can see, which tool requests can reach the operator, what the API does at its edge, and what the web head does about routes, titles, focus and sign-in. It closes OD-03 for the single-operator case and narrows HOST-04.
- Related: ADR-0017 (Demo mode stays a build-time variant), ADR-0020 (cloud agents on Railway; this record is its tier T0, the engine inside the API container), ADR-0030 (accounts with Supabase passwordless sign-in, Proposed: the multi-user mode that sits next to this one).

## Context

The web head published at the Vercel URL is a Demo: every API route is answered in the browser from scripted fixtures (ADR-0017), so nobody can use the product there. The real path exists in code (a REST and SSE client, the API, `omp acp` through the ACP adapter) but four things stopped it from being deployed:

1. **Sign-in (OD-03).** The API accepted only OIDC bearer tokens and no identity provider authority was configured. The multi-user design (ADR-0030, Supabase) is planned but not built, and its live verification needs owner actions that are outside this repository. A deployment for one operator needs something smaller that is still safe on the internet.
2. **The engine sees the host.** `AcpStdioTransport` started `omp acp` with the API's whole environment, so database passwords, token digests and provider keys were readable by the agent (`printenv`, `/proc/self/environ`), and a same-user agent could read the API's own `/proc/<pid>/environ`. With no worktree repository configured the engine also started in `/app`.
3. **No tool request could be decided.** The direct run path used `AcpApprovalPolicy.DenyAll` and registered approvals without a ceiling, so the hosted agent could not edit a file or run a command and the Approvals UI never received an actionable request.
4. **No production edge.** No forwarded-header handling, HSTS or security headers, request limits, startup probe, SSE keep-alive, graceful drain or service resource in telemetry; every rate limiter had one global bucket.

The web head also had one route, static titles, no focus management, no error boundary and no sign-in, so it could neither be deep-linked nor operated against an authenticated API.

## Decision

### D1. Two authentication modes, one contract

`Api:Auth:Mode` is `Oidc` (default) or `OwnerToken`; `Api:Auth:AllowDevelopmentPrincipal` stays a Development-only switch.

- **OwnerToken** is for one operator. The operator generates a token of at least 32 random bytes, stores the SHA-256 digest (lowercase hex, one to three digests so a token can be rotated) in `Api:Auth:OwnerToken:Sha256`, and types the token into the web head once per tab. The API hashes the presented bearer token and compares it with every digest in constant time. The token is never accepted in a URL, never logged and never stored by the browser (memory only). Wrong tokens are throttled per client address (`MaxFailuresPerMinute`, default 10, answers 429 with `Retry-After`); a correct token is never throttled. The principal is `Api:Auth:OwnerToken:Subject` (default `owner`).
- **Oidc** keeps today's rules (absolute https authority and audience). Outside Development it now also requires `Api:Auth:AllowedSubjects` (exact `sub` values) or the explicit `Api:Auth:AllowAnyAuthenticatedUser=true`, because a valid token from a shared identity provider is not permission to run an agent that executes commands on this host. An authenticated subject that is not listed gets 403.
- `GET /auth/config` (anonymous, `Cache-Control: no-store`, own per-address rate limit) returns `{ mode, authority, audience }` with `mode` `owner-token`, `oidc` or `development`, so a head knows which sign-in to show. ADR-0030 adds its project URL and publishable key to the same response; the `mode` field is the shared part.

Public exposure rule (OD-03 for this case): the API may get a public domain only with `Mode=OwnerToken` and a digest, or `Mode=Oidc` with an allow-list. Start-up fails in every other shape outside Development. This does not make the product multi-user: the agent still runs as the API's user (D3).

### D2. Per-subject rate limits

Every `IRateLimiterPolicy<string>` partitions by the authenticated subject (`sub:`), else the client address (`ip:`), else `anonymous`, with the configured budget per key. Idle limiters are disposed by the framework and keys are truncated, so memory stays bounded.

### D3. The engine child process is not the API

- `Harness:Acp:Environment:Mode` is `Inherit` or `Allowlist`; outside Development the default is `Allowlist` and `Inherit` needs `Harness:Acp:AllowInheritedEnvironment=true`. `Allowlist` clears the child's inherited variables and keeps `PATH`, `HOME`, `USER`, `LOGNAME`, `LANG`, `LC_*`, `TERM`, `TZ`, `TMPDIR`, `SHELL`, `PI_CODING_AGENT_DIR`, `OMP_*`, the CA and proxy variables, the explicit variables of the session and `Harness:Acp:Environment:Allow` (exact names, or a prefix ending in `*`). The stdio MCP launcher takes the same option.
- `Harness:Acp:WorkspaceRoot`: each session gets `<root>/<sessionId>` (mode 0700, id charset checked) as the engine's working directory; without a worktree repository and without a root, start-up fails outside Development.
- `ProcessHardening.Apply` calls `prctl(PR_SET_DUMPABLE, 0)` on Linux before the first engine process (`Hosting:RestrictProcessInspection`, default on outside Development), so the engine and its tools cannot read the API's `/proc/<pid>` entries; `execve` resets the flag, so the engine stays debuggable by its user.
- **Residual risk, accepted for a single operator:** the engine still runs as the API's user, the registered sandbox provider is the in-memory fake (HOST-04), and a compromised token is a shell in the container. `docs/security/hosted-threat-model.md` lists the rest.

### D4. An approval policy for the direct run path

`Harness:Acp:Approval` has `Kinds` (ACP tool kinds that may reach the operator, default none), `GrantableTools` (tool names a decision may allow, `*` for any admitted tool, default none) and `DecisionTimeoutSeconds`. Defaults keep today's behaviour: deny everything. The single-operator recipe is `Kinds` = `read, search, edit, delete, move, execute` and `GrantableTools` = `*`: every such request becomes a pending approval in the UI, nothing is auto-approved, and an undecided request is denied on timeout.

### D5. The edge

`Hosting:*`: forwarded headers (`None`, `TrustAllSingleHop`, `KnownNetworks`; one hop only), HSTS outside Development, security headers on every response (`nosniff`, `no-referrer`, `X-Frame-Options: DENY`, a restrictive `Permissions-Policy`, `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` on API responses, `Cache-Control: no-store` for authenticated JSON), Kestrel limits, a default request timeout with the SSE routes exempt, `/health/startup`, a readiness check that turns unhealthy when shutdown starts, an SSE keep-alive comment (`: keepalive`, default every 15 seconds) with `Cache-Control: no-store, no-transform` and `X-Accel-Buffering: no`, a clean stream end with `retry: 2000` when the host stops, and an OpenTelemetry resource (`service.name=autonogrammer-api`).

### D6. The web head

One workbench page with routes `/`, `/kanban`, `/sessions/{id}`, `/settings[/{page}]`, `/about` and `/connect`; a title per view, one `h1` per view, focus moved to the heading after a page change (never on the first page, so the skip link stays the first Tab stop), a route announcer, a root error boundary with a plain-language fallback, and an `AuthGate`: Demo and `development` pass through, `owner-token` shows the connect screen until the API accepts a token, `oidc` explains that this build cannot sign in, an unreachable `/auth/config` shows an alert with Retry. A 401 on a request that carried the token ends the session and returns to `/connect`. The Demo stays a build-time variant (`AutonogrammerDemo`), now deployed as its own bundle; `scripts/vercel/build-web.sh` publishes the real bundle and `scripts/vercel/check-no-demo.sh` fails when it carries Demo code.

## Consequences

- A single operator can run the real product on the internet with one secret that the API stores only as a digest. Multi-user sign-in stays ADR-0030's job and must keep the `/auth/config` contract.
- The engine's environment is no longer the API's. Operators who relied on inherited variables must list them in `Harness:Acp:Environment:Allow` or set `Inherit` explicitly.
- A deployment that enables approvals still trusts the operator's judgement: the engine runs approved commands as the API user.
- Breaking: an Oidc deployment outside Development fails to start without `AllowedSubjects` or `AllowAnyAuthenticatedUser`; `ApiFactory` and the Production-shaped fixtures set the latter.
- The a11y checks added to the web smoke fail until a real defect they find is fixed; that is intended.

## Verification

Recorded in `.planning/phases/66.2-hosted-real-web-app/66.2-VERIFICATION.md` at closeout: what was built and run, and what was only written. Nothing in this record was run against a real model, on Windows, macOS or Android, against a deployed Railway API, or with a screen reader.

## Open items

OQ-172 to OQ-190 in `docs/open-questions.md`, notably the sandbox provider (HOST-04), the model credential, the hosted run of the scripted-model harness, and the screen-reader pass.
