# 68-10 summary: all-writer fence for account erasure (SEC-77-07)

status: partial. Waves 3 to 5 are implemented and verified for the writers that can be fenced. Three writers cannot be fenced without an owner decision, so the plan is not complete. Phase 68-11 depends on this plan and has not started.

verified_on: 2026-11-13. Gate commit: `74238161` (plus `da8339b1` for wave 3).

## Implemented and verified

- Wave 3, producers and entry points: SessionRunReaper, DispatchDeliveryLoop and CloudIdleSweeper skip an erasing account's unit and continue with other accounts (`AccountErasingException` caught per unit). Writes in the other producers and entry points already run in admitted transactions, so no change was needed there.
- API boundary: `AccountUnavailableProblemMiddleware` maps `AccountErasingException` to 409 `application/problem+json`, type `/problems/account-unavailable`, fixed detail, no account id and no SQL.
- Wave 4, erasure: lifecycle-first erasure with `FOR UPDATE` on `account_lifecycle` and a 30 s `lock_timeout`. Trigger migration M0014 is in place. The operator command is `services/Autonogrammer.Api/Operations/EraseAccountCommand.cs`; the plan named `Cli/`, and this is a recorded path deviation.
- Wave 5, proof: `AccountErasureFenceTests` (two tests added: refused write while erasing, and lock timeout surfacing as `AccountErasureLockTimeoutException` with nothing deleted). `AccountErasureAdmissionTests` (new, three tests through the API).

Gate on `74238161`: format verify clean; build warnings-as-errors 0 warnings and 0 errors; Api 1564/1564; Sessions.Postgres 126/126; architecture 110/111, where the one failure is SEC-77-07 correctly open.

## Not placed: owner decision required

1. Ledger outbox `MarkSent` and `Schedule` (`services/Autonogrammer.Api/Audit/PostgresLedgerOutboxStore.cs`). These UPDATE `ledger_outbox` in autocommit and carry only a tenant hash, not the account id. The admission key is missing, so they cannot be placed in an admitted transaction without a hash-to-account mapping.
2. Automation store (`src/Autonogrammer.Harness.Automations`, `FileAutomationStore` and `InMemoryAutomationStore`). It is host-wide and not Postgres-backed, so erased owners' automations and run history are not fenced. Closing it needs a Postgres store in a project that references `Autonogrammer.Sessions.Postgres`, or erasure-side removal of that owner's automations.
3. Dispatch frames. `DispatchDeliveryLoop` writes the signed frame to the socket before the transition's admission can refuse it, so one command can reach an erasing account's device. Fixing it needs an admission check before the send, or moving the send after the transition.

## Not claimed

- SEC-77-07 remains open. The release gate fails until the three items above are placed or accepted by the owner. The D-16 record is a partial acceptance of the purge side only.
- The private-network reach test for Railway workers is untested until the API is deployed (OD-03).
- `IndependentHostsSerializeSameKey` fails under full-suite load and passes 14/14 when its class runs alone. It is a known timing-sensitive test; it is not claimed as fixed.
