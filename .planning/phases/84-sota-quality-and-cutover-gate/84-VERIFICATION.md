# Phase 84: SOTA quality and cutover gate — verification record

Executed 2026-11-26 (wave13-1, item 1: Tasks 4–7). Tasks 1–3 evidence: `docs/runbooks/evidence/phase-84-preflip-2026-11-26/` (TASK1-baseline.md, TASK2-suppression-ledger.md, TASK3-obsolete-paths.md). This file is the close-out record; evidence for Tasks 4–7: `docs/runbooks/evidence/phase-84-cutover-2026-11-26/`.

Flip commit: `69b844f0` (one commit, F1–F5, ADR-0038 Accepted same commit; 8 files, 14+/14-). Task 4 commit: `c3ac51ae` (owner decisions). Deletion manifest (Task 3): **0 deletions** — the cutover commit deletes nothing.

## Baseline gate (pre-flip, TASK1 + hygiene)

- At `fdafc70b`: 3 red gates (check-docs 3 orphans; check-skip-markers 3 unlisted + 7 orphan rows; check-sbom stale) + 1 ArchTests failure (`RepoHygiene_DocsSkeletonExists`, OQ-526-83 five-column row); `-warnaserror` build green (0/0), locked restore green, licence check green (TASK1-baseline.md).
- Hygiene `d8fea8f0` (OQ-526-83 six columns; SUP-890..892; 7 orphan rows reworded; SUMMARY links), `9737cf01` (SBOM regenerated), `184c5500` (74/83 test fix + OQ-529).
- Pre-flip re-run at `c3ac51ae` (this assignment): `bash scripts/ci/repo-checks.sh` exit 0; `check-docs.sh` 0 errors; `check-skip-markers.sh` ok (93 kinds, 90 files); `check-sbom.sh` ok. Baseline verdict: green (flip precondition met).

## Suppression ledger (TASK2)

Summary: scanner/ledger completeness oracle (`SuppressionLedgerTests`) green at baseline; 154 raw pragma lines, 276 kept suppression-form SUP rows; `<NoWarn>` 0; S1 severity-none 4 rules (CA1303, CA1707, CS1591, CA1062) keep-with-justification; 1 pragma without reason comment flagged pre-existing (`WebAccountSession.cs:552` CA5399, not added here); 3 unlisted skip markers → SUP-890..892 added in hygiene commit; 7 orphan rows retired by rewording, not deletion. No new suppression added in Tasks 4–7 (flip diff checked: 0 NoWarn/pragma/severity-none additions).

## Owner decisions (Task 4, 2026-11-26, decider: product owner)

- OQ-528 (realm): `init`. Verified: `git submodule status` shows `f28a3acc319d9805c2d2487c05d12a075a2c8ad7 plugins/agent-realm (v0.3.38)`; gitlink matches; working tree populated; no re-init executed (no-op). Disposition recorded in `docs/open-questions.md` OQ-528 (closed 2026-11-26).
- ADR-0039 Gate A: **A1** (reuse Dispatch ES256 device credential with artifact-specific audience + server-validated session id). Gate B: **B4** (no-network default for v0.16.0; grants recorded but network-inert). Status stays **Proposed** (71-08 Tasks 2/3 code not yet written; decisions unblock that code). Recorded in ADR-0039 Status header + Candidate chosen + index row.
- SEC-77-01: carried open at 0 waivers (NFR-S3). No identifier cleared or waived.

## Flip (Task 5, commit 69b844f0)

- F1: `services/Autonogrammer.Api/appsettings.json` `Harness:Adapter`: `InMemory` → `Rpc`; `HarnessOptions.cs` + `Program.cs` comments updated (validators unchanged).
- F2: `apps/Autonogrammer.Native/Engine/EngineCompositionOptions.cs` `Transport`: `Acp` → `Rpc` + doc.
- F3: `apps/Autonogrammer.Native/Agents/SessionEngineSetOptions.cs` `Transport`: `Acp` → `Rpc` + doc (child/remote stay ACP by construction).
- F4: `.env.example` `Harness__Adapter=Rpc` with `Acp` named as rollback.
- F5: `docs/decisions/ADR-0038-omp-rpc-primary-transport.md` Accepted 2026-11-26 + `docs/decisions/README.md` row (same commit).
- Compile-check: `dotnet build Autonogrammer.Api` (30 projects, 0 errors 0 warnings, `-warnaserror`) exit 0; `dotnet build Autonogrammer.Native` (44 projects, 0/0) exit 0. Host load 44–67 (parallel waves) made earlier attempts time out; final runs above are the verdicts.
- Tests pinning the old default: none found (grep: zero `EngineTransport.Acp` references in tests/; adapter-selection tests set config explicitly).

## Post-flip gate + rollback (Task 6, at 69b844f0)

Full per-suite table: `docs/runbooks/evidence/phase-84-cutover-2026-11-26/TASK6-postflip.md`. Headlines: DecisionRecordTests 3/3; ArchitectureTests 124/124 (one `RegexMatchTimeoutException` flake proven by isolated re-run 3/3); SuppressionLedgerTests 7/7; RepoHygieneTests 8/8; ContractTests 32/32; Api selection 2/2 (rollback: Production+Acp starts, Production+InMemory fails naming `Harness:Adapter`); readiness probe (Rs) 6/6; Rpc adapter 11/11 + factory 5/5; parity 2/2. Secret scan (gitleaks) not run here (needs network download; main agent's full-gate step); SEC-77-01 open at 0 waivers.

## Docs sweep, trackers, close-out (Task 7)

- Reference docs at new truth: `docs/architecture.md` (engine starts `omp --mode rpc` per session; ACP = remote + rollback); `.planning/PROJECT.md` update line + key-decision row → Accepted 2026-11-26; `.planning/ROADMAP.md` Phase 84 Complete; `.planning/STATE.md` current focus; `.planning/LINEAR-MAP.md` Phase 84 status. `docs/runbooks/hosting.md` recipe intentionally unchanged (explicit Acp for approval-configured flows, OQ-525 — not a default statement).
- Six tracker payloads (file-first, 0 identifiers fabricated, no tracker write attempted): `79-TRACKER-PAYLOAD.md`, `80-TRACKER-PAYLOAD.md`, `81-TRACKER-PAYLOAD.md`, `82-TRACKER-PAYLOAD.md`, `83-TRACKER-PAYLOAD.md`, `84-TRACKER-PAYLOAD.md` under each phase directory.
- Carried identifiers, states unchanged: 66.1, 66.2, 66.3, 66.4, 69 (deferred-human); 68 / 68-09 (SEC-77-07, paused); 74.1 review (open); 75-12 hold; SEC-77-01 (open, 0 waivers). v0.16.0 is not archived (ROADMAP stays cumulative, US-307).
- Verify: `bash scripts/ci/repo-checks.sh` exit 0 (T7) + ArchitectureTests as above.
