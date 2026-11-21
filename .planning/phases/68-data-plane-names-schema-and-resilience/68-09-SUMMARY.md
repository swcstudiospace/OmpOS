# 68-09 Summary: erasure architecture and authority decision (SEC-77-07)

Status: **decision step completed; implementation in 68-10/68-11.** This plan (68-09-PLAN.md,
`autonomous: false`, wave 7) authorized no source edit, migration, execution, deployment, test,
staging, commit, or shared STATE/ROADMAP change on its own. It paused for human approval on
2026-11-12 (STATE.md pause record, commit `95fbfee2`, ancestor of `c04095ba`), and it carries a
`SUPERSEDED 2026-11-19` banner pointing at ROADMAP Phase 68 and 68-VERIFICATION.md. No file was
written by an executor of this plan; the lines below trace who decided what, from committed
records only.

## Decision recorded

The candidate in 68-09-PLAN.md Task 1 was an all-writer fence so that account erasure cannot race
a concurrent account-owned writer: a durable per-account barrier that every writer holds through
its commit, drained before the first delete, covering PostgreSQL stores, Dragonfly coordination
keys and background producers — including `PostgresArtifactStore` (protected from edits) without
editing it, which required a database-side gate. The plan made the migration (D-18 closing
phase-68 migration ownership; D-34 limiting up migrations to `CREATE ... IF NOT EXISTS` /
`ADD COLUMN IF NOT EXISTS`) and the D-34 exception for any trigger/function separate owner
approvals, and it left SEC-77-07 High/open until a complete writer inventory, exact migration
scope, and independent security review existed. It also held 74.1 independent human review and
75-12 as separate holds.

How that decision landed is recorded in the security review, `docs/quality/v0.16.0-security-review.md`
(SEC-77-07 design notes, §"SEC-77-07 fence design notes (68-10 style)"), shipped with `7dadceb5`
(ancestor of `c04095ba`):

1. "Gate in the database, not in each store." The owner approved the all-writer fence design "in
   chat" (review's words); no D-34 text was amended, and the up steps contain no `DROP`, so the
   additive-migration guard applies unchanged. Only a database gate covers the protected artifact
   store, direct SQL and an older binary.
2. Names/versions fixed by the owner reservation: M0012 `account_erasure_fence`; the unreleased
   memory-idempotency migration renumbered to M0013 (checksum unchanged); M0014
   `memory_idempotency_fence` puts the same trigger on `cowork_memory_idempotency`. SQLSTATEs
   AE001–AE005 as recorded there (AE001 erasing/erased, AE002 owner unresolvable, AE003
   transaction not at READ COMMITTED, AE004 fence row altered, AE005 rollback refused).
3. Admission-lock-then-state-check, fail-closed owner resolution, digest-only tombstone
   (SHA-256 of a namespaced account identifier, never the identifier), least-privilege
   `SECURITY DEFINER` check, family transactions kept separate behind the fence (D-31 order
   kept), 20 s lock wait then `AccountErasureBusyException`, runner-transaction rollback guard,
   explicit non-fenced set (`dispatch_audit`, `ledger_outbox`, Dragonfly keys) tracked as
   SEC-77-09 — all as recorded in that section. This summary does not restate the design; it
   points at the record that carries it.
4. Invocation/caller choice (68-09 Candidate A operator command vs Candidate B self-service
   route) was NOT decided by this plan, and to this day there is no production caller: SEC-77-09
   (Low, open) records "no route, no operator command, no identity-provider session revocation"
   and an erased subject can still authenticate (writes refused by the fence).

## Files

- Created/modified by this plan: none (decision only). The `SUPERSEDED 2026-11-19` banner in
  68-09-PLAN.md:22 was added on `main` by `e5f57667` ("Phase 68 closeout: planning
  reconciliation (0.20.7)", PR #25); the parallel `gsd/v0.10.0-cross-platform-ui` line carries
  the same banner from `e880faf8` but that line is not an ancestor of `c04095ba` and its
  implementation commits (e.g. `30afe342`, `da8339b1`, `74238161`, `7548213a`) never landed
  on `main` — main's fence shipped as `c4e6fe2e`/`332660ee` (see 68-10-SUMMARY.md).
- Implementation of the approved boundary: `68-10-SUMMARY.md` (`c4e6fe2e`, `332660ee`).
- Erasure completion and the caller gap: `68-11-SUMMARY.md` + `7dadceb5` and SEC-77-09.

## UNVERIFIED / held

- The "in chat" owner approval has no committed decision record in this tree; it is carried by
  the security-review sentence quoted above. A formal owner decision record remains untracked.
- 74.1 independent human review and the 75-12 hold remain in force (security review, §"No
  Critical or High finding is open…"). SEC-77-07 itself is fixed on `main` (see 68-10-SUMMARY).
