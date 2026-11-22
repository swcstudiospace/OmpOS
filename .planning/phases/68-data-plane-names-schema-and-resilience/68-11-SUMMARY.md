# 68-11 Summary: authorized erasure and recovery — eraser complete, entry point still absent

Status: **Part B done; caller gap open as SEC-77-09 (Low).** This plan (68-11-PLAN.md,
`autonomous: false`, wave 9, depends on 68-10) required the 68-09 invocation decision, an exact
entry/DI/subject-to-hash binding, post-implementation fence/drain proof, and an independent
security review before SEC-77-07 could close. What ships on `main` satisfies the fence and the
erasure-coordinator parts; the invocation part was deliberately left unexposed.

## Done on `main` (via 68-10 commits `c4e6fe2e`, `332660ee`, `7dadceb5`; see 68-10-SUMMARY.md)

- `PostgresAccountErasure` erases per D-31 family order behind the fence, is resumable: a
  repeated call on an `erased` account skips SQL and retries the Dragonfly tenant-key deletion,
  and the call completes only when the keys are gone (security review design note 7). Account B
  isolation and concurrent-eraser serialization are proven by `AccountErasureFenceTests` 10/10
  (admitted-writer, busy, bypass cases — the review confirms they fail when the shared lock is
  removed).
- Migration safety proven by `AccountErasureFenceMigrationTests` 7/7 (upgrade, checksum
  readiness, down/re-up, unsafe-rollback refusal AE005, app/migrator role grants).
- Runbook updated (`docs/runbooks/data-plane.md`, `docs/runbooks/accounts.md` in `7dadceb5`)
  with the fence behavior and operator notes; CHANGELOG entry in the same commit.

## NOT done (plan Task 1 precondition unmet — correctly left blocking)

- **No production caller.** 68-11-PLAN.md: "There is no demonstrated or approved production
  caller today." A grep at `c04095ba` over `src/ services/` for `PostgresAccountErasure`
  matches only `src/Autonogrammer.Sessions.Postgres/PostgresAccountErasure.cs` itself —
  no route in `services/Autonogrammer.Api/Accounts/AccountsModule.cs`, no operator command,
  no DI composition, no identity-provider session/refresh revocation path. SEC-77-09 records
  exactly this as Low/open: no erasure can be requested, an erased subject can still authenticate,
  and every write for it is refused by the fence. The plan's Task 3 file list (entry-point
  composition, `docs/runbooks/data-plane.md` ops procedure, changelog closure) therefore exists
  only in the parts `7dadceb5` wrote; no external hold was lifted by this work.
- **Dragonfly keys not fenced** (presence, rate limit, lease, idempotency, nonce, signal): ≤24 h
  TTL, no user content; tenant scan removes tenant-carrying keys; a live heartbeat can rewrite
  one after erasure. Stores surfacing plain AE001 `PostgresException` (memory, projects,
  artifacts, accounts) instead of the typed `AccountErasingException` is recorded in SEC-77-09
  as the missing API mapping — not implemented here.
- **Independent security review:** SEC-77-07's row was closed by `7dadceb5` with the review
  itself carrying the evidence and the 12-item design note for the independent reviewer; the
  review explicitly does not pass the release gate on its own (SEC-77-04 Medium, SEC-77-09
  Low, 74.1 review, 75-12 hold, Mermaid licence OQ-400 all open). This summary does not treat
  the row closure as that review.

## Files

- Shipped (above): `src/Autonogrammer.Sessions.Postgres/{PostgresAccountErasure,AccountErasureFence}.cs`,
  `Migrations/{M0012,M0013,M0014,Registry}.cs`, Resilience wiring,
  `tests/.../{AccountErasureFenceTests,AccountErasureFenceMigrationTests,PostgresAccountErasureTests}.cs`,
  runbooks, CHANGELOG, security review.
- Not created (per plan): no erasure route, no operator command, no `AccountErasureRecoveryTests`
  beyond `PostgresAccountErasureTests`, no revocation integration. 68-11-PLAN.md:22 quotes the
  candidate composition files; none was added by this phase.

## UNVERIFIED

Production erasure, cross-host drain against real Dragonfly, and IdP revocation were never
exercised (security review §Verification). SEC-77-04 (memory idempotency, Medium) and the
74.1/75-12 holds are untouched by this plan.
