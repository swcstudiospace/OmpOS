# Requirements specification T77d9-req

Status: Record (requirements specification for T77d9-req)

## 1. Goal

Close milestone v0.16.0 phases 66 to 78 on origin main at 434e214e.
Preserve the 10 dirty auth and secrets files onto a branch from origin
main. Keep main clean. Harden the top State-of-the-Art gaps from phases
25.1 to 25.9 only, with no new conventions and no scope shrink. Map every
phase to one Linear parent and its sub-issues with Given/When/Then
criteria, no duplicates, every mapping linked to its GSD phase, and
blocked phases staying blocked. Merge to main only when every CI job
is green. Update STATE.md and ROADMAP.md at the end.

Baseline facts, verified by reads: origin STATE records phase 78 plan 06
next, umbrella SPE-34831, reconciliation 2 complete, 7 substantially
complete, 7 partial, 1 blocked, 107 outstanding and 64 blocked items,
no phase verified by a gate run. LINEAR-MAP covers 66 to 77 with 17
parents and 58 sub-issues; phase 78 maps to SPE-34831 with steps
SPE-34768, SPE-34769, SPE-34800, SPE-34803, SPE-34809, SPE-34834 and
SPE-34835. Phase 72 is blocked on an owner action and stays blocked.
Local main is 93 commits behind origin main with 10 modified files.
The .NET 10.0.402 SDK is absent locally, so all proof runs in CI.

## 2. Traceability matrix (story to GSD phase to Linear)

US-101 maps to phase 66, parent SPE-34890, children SPE-34891,
SPE-34892, SPE-34952.
US-102 maps to phase 66.1, parent SPE-34953, children SPE-34954,
SPE-34955.
US-103 maps to phase 66.2, parent SPE-34956, children SPE-34957,
SPE-34958, SPE-34959.
US-104 maps to phase 66.3, parent SPE-34960, children SPE-34961,
SPE-34962, SPE-34963.
US-105 maps to phase 66.4, parent SPE-34893, children SPE-34894,
SPE-34895, SPE-34896.
US-106 maps to phase 67, parent SPE-34964, children SPE-34965,
SPE-34966, SPE-34967, SPE-34968.
US-107 maps to phase 68, parent SPE-34897, children SPE-34898,
SPE-34899, SPE-34969.
US-108 maps to phase 69, parent SPE-34900, children SPE-34901,
SPE-34902, SPE-34903, SPE-34904.
US-109 maps to phase 70, parent SPE-34905, children SPE-34906,
SPE-34907, SPE-34908, SPE-34909.
US-110 maps to phase 71, parent SPE-34910, children SPE-34911,
SPE-34912, SPE-34913, SPE-34914.
US-111 maps to phase 72, parent SPE-34915, children SPE-34916,
SPE-34917, SPE-34918, SPE-34970, SPE-34919.
US-112 maps to phase 73, parent SPE-34920, children SPE-34921,
SPE-34971, SPE-34922, SPE-34923, SPE-34924.
US-113 maps to phase 74, parent SPE-34925, children SPE-34926,
SPE-34927, SPE-34928, SPE-34929, SPE-34930.
US-114 maps to phase 74.1, parent SPE-34931, children SPE-34932,
SPE-34933.
US-115 maps to phase 75, parent SPE-34934, children SPE-34935,
SPE-34936, SPE-34937, SPE-34938, SPE-34939.
US-116 maps to phase 76, parent SPE-34940, children SPE-34941,
SPE-34942, SPE-34943, SPE-34944, SPE-34945.
US-117 maps to phase 77, parent SPE-34946, children SPE-34947,
SPE-34948, SPE-34949, SPE-34950, SPE-34951.
US-118 maps to phase 78, parent SPE-34831, children SPE-34768,
SPE-34769, SPE-34800, SPE-34803, SPE-34809, SPE-34834, SPE-34835.
US-119, US-120, US-121 and US-122 are infrastructure outcomes. They
create no new Linear parents. Their evidence is the branch, the quality
gate, the pull request and the STATE and ROADMAP update.

Owner register: SPE-34972 holds one line per blocked item tagged by
phase. Phase 72 stays Blocked. No issue moves to Done until the phase
VERIFICATION file says passed.

## 3. Non-functional requirements

NFR-P1 (perf), NFR-S1 (security), NFR-A1 (a11y), NFR-I1 (i18n) and
NFR-C1 (compliance) are defined in nfr.md with numeric targets and
checkers. The compliance entry is non-negotiable. No gate may waive
it. PII scan of the brief found 0 matches.

## 4. Assumptions

A1: The 10 dirty files merge onto origin main without conflict. If
they conflict, the conflict is reported and the branch still ships
the preserved content plus the conflict record.
A2: CI on the branch runs every job including image scan, CodeQL,
Scorecard, build-test, desktop and android. All green is required
before merge.
A3: Phase 72 stays blocked for this run. Its criterion records the
blocked state rather than production behavior.
A4: SDK proof stays in CI. No local build claim is made.

## 5. Out of scope

Architecture design, production code, schema design, IaC and release
commands belong to downstream agents. This spec defines what and how
well, never how. Scheduling belongs to A01. Security-risk acceptance
belongs to A10.

## 6. Stability

Identifiers US-101 to US-122, NFR-P1, NFR-S1, NFR-A1, NFR-I1, NFR-C1
and the Linear IDs are stable across revisions and are never reused
for another meaning.
