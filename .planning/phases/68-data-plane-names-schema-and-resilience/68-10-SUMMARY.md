# 68-10 summary: all-writer fence for account erasure (SEC-77-07)

status: waves 3 to 5 implemented and proven. Two owner decisions remain before the plan can close. Phase 68-11 depends on this plan and has not started.

verified_on: 2026-11-13. Gate commit: `a18713cf`.

## Implemented and proven

- Producers and entry points: SessionRunReaper, DispatchDeliveryLoop and CloudIdleSweeper skip an erasing account's unit. Other producers and entry points already write in admitted transactions.
- API boundary: AccountUnavailableProblemMiddleware maps AccountErasingException to 409 `application/problem+json` (`/problems/account-unavailable`), with a fixed detail and no account id or SQL.
- Erasure: lifecycle-first, `FOR UPDATE` with a 30 s `lock_timeout`. Trigger migration M0014. Operator command in `services/Autonogrammer.Api/Operations/EraseAccountCommand.cs` (plan named `Cli/`; recorded deviation).
- Tenant bindings (M0015): erasure deletes the binding inside its lifecycle transaction. Touch binds only active accounts. A startup backfill binds accounts provisioned before M0015.
- Ledger outbox: MarkSent and Schedule run in one admitted transaction keyed by the tenant hash. A row with no tenant or no binding is refused without a write.
- Dispatch: the Queued to Delivered transition is applied before the signed frame is sent. A refused transition sends nothing.
- Automation store (M0016): PostgresAutomationStore, selected only for the Postgres store, with admitted writes. Reflection-free JSON via PostgresAutomationJsonContext. Erasure deletes the owner's definitions and runs.
- Proof tests: LedgerOutboxFenceTests (3), AutomationStoreFenceTests (3), DispatchSendOrderTests (2), AccountErasureAdmissionTests (3), AccountErasureFenceTests (2 added).

Gate on `a18713cf`: format verify clean; build warnings-as-errors 0 warnings and 0 errors; Api 1572/1572; Sessions.Postgres 126/126; architecture 110/111, the one failure is SEC-77-07 open.

## Open: owner decisions

1. Tenant pepper stability. The tenant hasher uses a random per-process pepper unless the coordination store is Dragonfly (services/Autonogrammer.Api/DataPlane/DataPlaneServiceCollectionExtensions.cs). Bindings persist in Postgres, so after a restart a bound account's EnsureAsync throws 'bound to a different hash', breaking touches and ledger writes. Needs a configured stable pepper, or a Postgres-driven hasher.
2. Stranded delivered command. If the signed frame's socket send fails after the transition is applied, the command stays delivered until it expires, because no delivered-to-queued edge exists. Needs a compensating transition (a state-machine change), or acceptance as a residual.

## Not claimed

- SEC-77-07 remains open. The release gate fails until both decisions are settled and the owner signs the closure.
- The Railway private-network reach test is untested until the API is deployed (OD-03).
- `IndependentHostsSerializeSameKey` fails under full-suite load and passes 14/14 when its class runs alone (timing-sensitive; not claimed as fixed).
