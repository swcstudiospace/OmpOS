# 68-10 summary: all-writer fence for account erasure (SEC-77-07)

status: complete in code and proven by the gate below. SEC-77-07 stays open: the release gate fails until the owner signs the closure. Phase 68-11 (verification and crash windows) follows and has not started.

verified_on: 2026-11-14. Gate commit: `e2767298`.

## Implemented and proven

- Producers and entry points: SessionRunReaper, DispatchDeliveryLoop and CloudIdleSweeper skip an erasing account's unit. Other producers and entry points already write in admitted transactions.
- API boundary: AccountUnavailableProblemMiddleware maps AccountErasingException to 409 `application/problem+json` (`/problems/account-unavailable`), with a fixed detail and no account id or SQL.
- Erasure: lifecycle-first, `FOR UPDATE` with a 30 s `lock_timeout`. Trigger migration M0014. Operator command in `services/Autonogrammer.Api/Operations/EraseAccountCommand.cs` (plan named `Cli/`; recorded deviation).
- Tenant bindings (M0015): erasure deletes the binding inside its lifecycle transaction. Touch binds only active accounts. A startup backfill binds accounts provisioned before M0015. The pepper is required when the Postgres session store is used, so bindings survive a restart.
- Ledger outbox: MarkSent and Schedule run in one admitted transaction keyed by the tenant hash. A row with no tenant or no binding is refused without a write.
- Dispatch: the Queued to Delivered transition is applied before the signed frame is sent. A refused transition sends nothing. A failed send, including a send to a closed socket, returns the command to Queued (Delivered to Queued compensating edge) so it is redelivered. Revoke fan-out skips a closed session and continues.
- Automation store (M0016): PostgresAutomationStore, selected only for the Postgres store, with admitted writes and a source-generated JSON context. Erasure deletes the owner's definitions and runs.
- Proof tests: LedgerOutboxFenceTests (3), AutomationStoreFenceTests (3), DispatchSendOrderTests (3), AccountErasureAdmissionTests (3), AccountErasureFenceTests (2 added), TenantPepperRequirementTests (2).

Gate on `e2767298`: format verify clean; build warnings-as-errors 0 warnings and 0 errors; Api 1575/1575; Sessions.Postgres 126/126; architecture 110/111, the one failure is SEC-77-07 open.

## Residuals (recorded, not closed)

- A changed pepper value is not detected at startup. It surfaces at the first touch, where EnsureAsync refuses a different hash. Deployment docs say so.
- The revoke fan-out path has no test yet that proves a closed session is skipped while the others are revoked.
- The Railway private-network reach test is untested until the API is deployed (OD-03).
- `IndependentHostsSerializeSameKey` fails under full-suite load and passes 14/14 when its class runs alone (timing-sensitive; not claimed as fixed).
- The pepper must be set in every Postgres deployment before its next start (`DataPlane__TenantHashPepper`). This is a deployment change the owner must make.
