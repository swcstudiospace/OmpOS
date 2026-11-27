# Phase 84: SOTA quality and cutover gate - Context
Source: task T9013-arch, ADR-0038, upstream US-307 to US-310, NFR-S3/NFR-C3
**Gathered:** 2026-11-23
**Status:** Planned (Waves 2+: plan-and-return). Last phase; executes after 79-83 land.

<domain>
## Phase Boundary

The cutover: `Harness:Adapter` default flips to Rpc for local sessions, obsolete paths left behind by the rearchitecture are deleted, documentation is brought to the new truth, ADR-0038 moves to Accepted, and the tracker sync for phases 79-84 runs from payload files. Quality bar: the full gate, the security pass over the milestone diff (NFR-S3: secret scan 0 findings; SEC-77-01 carried open, never waived), and the parity suites of Phases 80/83 all green at the cutover commit. If any gate fails, the default does not flip — the milestone closes with ACP still default and says so.

Not delivered: archival of v0.16.0 (never; ROADMAP stays cumulative, US-307), clearance of SEC-77-01, 68-09 SEC-77-07, the 74.1 review, the 75-12 hold or deferred-human 66.1-66.4/69 (US-308), any live model turn, any release tag.
</domain>

<decisions>
## Implementation Decisions

- **D-01:** The default flip is one commit touching the option default and its docs; rollback is setting `Harness:Adapter=Acp`, which stays a supported configuration after the milestone (remote depends on the ACP client remaining production-grade).
- **D-02:** "Delete obsolete" is enumerated, not vibes: the cutover change lists every type/file removed (superseded adapters, dead options, retired fixtures) in its verification; anything not listed stays.
- **D-03:** ADR-0038 acceptance is recorded in the record header and the decisions index in the same commit as the flip; if the flip does not happen, ADR-0038 stays Proposed and the close-out states which phases did not land.
- **D-04:** Tracker payloads are authored as files first (one per phase, Linear SPE / project P-SPE-2 and Notion 700bc1a0-c7ae-812c-abfb-00035fd38be2); on any tracker error the exact error text is stored beside its payload and 0 identifiers are fabricated (US-310, NFR-C3).
- **D-05:** Every carried identifier (66.1, 66.2, 66.3, 66.4, 69, 68/68-09, 74.1, 75-12, SEC-77-01) appears in the close-out with its state unchanged (AC-308).
</decisions>

<verification_posture>
## What "done" means here

`./bootstrap.sh` exits 0 at the cutover commit with RPC as default, docs and changelog checks pass, the secret scan reports 0 findings, and each RPC-* traceability row matches its phase verification. Anything less is reported as the actual state.
</verification_posture>

<canonical_refs>
## Canonical References

- `docs/decisions/ADR-0038-omp-rpc-primary-transport.md`
- `docs/requirements/nfr-T9013.md` (NFR-S3, NFR-C3)
- `.planning/REQUIREMENTS.md` (RPC-84-1 to RPC-84-4)
- `.planning/LINEAR-MAP.md`
</canonical_refs>
