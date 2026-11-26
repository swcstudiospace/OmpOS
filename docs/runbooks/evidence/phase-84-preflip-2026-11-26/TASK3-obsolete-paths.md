# Phase 84 pre-flip baseline — Task 3: obsolete-path enumeration vs plan O1–O3 (no deletions)

Commit: fdafc70b1b418793814d4d3984a51d75ee63d532
Date: 2026-11-26
Linear: SPE-37896 chain (https://linear.app/swcstudio/issue/SPE-37896/phase-84-sota-quality-and-cutover-gate); SC1 SPE-37897; SC2 SPE-37898; SC3 SPE-37899; Project P-SPE-2.
Notion payload root: 700bc1a0-c7ae-812c-abfb-00035fd38be2
Method (per plan): consumer grep per candidate, exact command + output quoted, never a paraphrase. No file deleted in this run; this report is the Task 5 deletion manifest candidate list for owner review. `docs/open-questions.md` untouched.

## Flip-site current values (for context, verified this run)

- `apps/Autonogrammer.Native/Engine/EngineCompositionOptions.cs:55,64`: "The default is EngineTransport.Acp." / `Transport = EngineTransport.Acp`.
- `apps/Autonogrammer.Native/Agents/SessionEngineSetOptions.cs:81,85`: "EngineTransport.Rpc is the Phase 80 opt-in…" / `Transport = EngineTransport.Acp`.
- `services/Autonogrammer.Api/appsettings.json:34`: `"Adapter": "InMemory"`; `.env.example:12`: `Harness__Adapter=InMemory` with the outside-Development `Acp or Rpc` comment at line 8.

## O1 — superseded adapter code

| # | Candidate | Consumer grep (exact command) | Observed | Classification | Delete manifest? |
|---|---|---|---|---|---|
| O1-a | `InMemoryHarnessSessionFactory` (`services/Autonogrammer.Api/Harness/InMemoryHarnessSessionFactory.cs`) | `grep -rn 'InMemory' services/Autonogrammer.Api/Program.cs services/Autonogrammer.Api/Options/HarnessOptions.cs services/Autonogrammer.Api/appsettings.json services/Autonogrammer.Api/appsettings.Development.json` | `Program.cs:63-64` validators ("InMemory, Acp or Rpc"; Development-only refusal outside Development), `Program.cs:114,118,132,134` DI registration + dispatch to `InMemoryHarnessSessionFactory`, `HarnessOptions.cs:18,26-27,39,44` constants/helpers. Loaded whenever `Harness:Adapter=InMemory` (Development echo) or the default appsettings value is used. | **Keep** — Development still needs the echo adapter; plan guardrail: "retained if Development still needs it" — it does. | No |
| O1-b | `ComposeRpc` refusal clauses (`apps/Autonogrammer.Native/Engine/EngineComposition.cs:191,196,201,206,211,216,221,226`) | `grep -n 'throw\|refus\|InvalidOperation\|ArgumentException' apps/Autonogrammer.Native/Engine/EngineComposition.cs` | 8 `ArgumentException` refusal sites: remote-host (Phase 83/ADR-0038 D2), approval kinds (OQ-525), run-mode/resolver, plan MCP servers (OQ-526), overlay without settings file, user MCP servers, hook gate, process wrapper. Each names a live OQ row / Phase 83 re-probe (2026-11-26). | **Keep** — each clause still serving a named OQ row; none is dead code at HEAD. Not candidates for deletion. | No |
| O1-c | ACP client / `AcpAgentSession` / `Harness.AgentLoop` / `OmpLaunchPlan` | `ls src/Autonogrammer.Harness.AgentLoop/Launch/` → `OmpLaunchPlan.cs`, `OmpLaunchPlanner.cs`, …; `grep -rn 'AcpAgentSession'` hits in `src/Autonogrammer.Harness.Acp/AcpAgentSession.cs` + tests | Production-grade remote + rollback (plan guardrail: never candidates). | **Keep** | No |

O1 Delete rows: **0**.

## O2 — dead options / config

| # | Candidate | Consumer grep | Observed | Classification | Delete manifest? |
|---|---|---|---|---|---|
| O2-a | `Harness:Acp:*` fields (ACP options) | `grep -rln 'Harness:Acp\|HarnessOptions.*Acp\|AcpSettings' services src apps --include='*.cs'` → `EngineIsolationSettings.cs`, `EngineWorkspace.cs`, `HarnessApprovalValidation.cs`, `HarnessToolApprovalSettings.cs`, `HarnessReadinessCheck.cs`, `HarnessApprovalOptions.cs`, `HarnessOptions.cs`, `Program.cs`, `ProvidersOptions.cs` | ACP rollback configuration needs these fields (plan: kept if Acp rollback needs them — it does). | **Keep** | No |
| O2-b | "Phase 80 opt-in" RPC refusal messages / doc sentences | `grep -rn 'Opt-in\|opt-in' services/Autonogrammer.Api/Options/HarnessOptions.cs apps/Autonogrammer.Native/Agents/SessionEngineSetOptions.cs` | `HarnessOptions.cs:10,23` ("Rpc … opt-in… Acp stays the shipped default"), `SessionEngineSetOptions.cs:81` ("Phase 80 opt-in"). These are **doc sentences the flip (Task 5) must rewrite**, not dead code — Modify at flip time, not deletions now. | **Modify (at Task 5 flip)** | No (rewrites, not deletions) |
| O2-c | Validators | `Program.cs:63-64` (above) | Exhaustive, fail-fast (unknown adapter → `InvalidOperationException` naming the key, `Program.cs:137`); no silent dispatch. | **Keep** | No |

O2 Delete rows: **0**.

## O3 — retired fixtures / docs relics

| # | Candidate | Consumer grep | Observed | Classification | Delete manifest? |
|---|---|---|---|---|---|
| O3-a | `RetiredTerminalRuntimeTests` (`tests/Autonogrammer.Native.Tests/Terminal/RetiredTerminalRuntimeTests.cs`) | `grep -rln 'RetiredTerminalRuntimeTests\|AgentPanelRetiredTerminalTests' tests` → the fixture itself only; `TerminalRetirementArchitectureTests.cs:11` pins retirement | Phase 81 Tasks 10/13 shipped this as the retired-rendering guard; it guards a retained path (persisted terminal records / retired rendering). Plan guardrail: "no fixture that still guards a retained path is deleted". | **Keep** | No |
| O3-b | `AgentPanelRetiredTerminalTests` (`tests/Autonogrammer.Presentation.Tests/AgentPanel/AgentPanelRetiredTerminalTests.cs`) | same grep (above) | Guards retained retired-terminal rendering in the web head; no production consumer beyond the test itself, but it is a live guard, not a dead fixture. | **Keep** | No |
| O3-c | Stale "ACP is production default" sentences in `docs/architecture.md`, `docs/handoff.md`, harness reference pages | `grep -rn 'Acp.*default is\|default.*Acp\|ACP.*default' docs/architecture.md docs/handoff.md services/Autonogrammer.Api/appsettings.json .env.example` | Only `docs/handoff.md:87` (AC_4 replay table — not a default sentence) and `.env.example:8` (validator comment) matched; no stale "ACP is production default" sentence was found in `docs/architecture.md` at HEAD. | **Keep** (no relic found; nothing to modify) | No |
| O3-d | `TerminalRetirementArchitectureTests` + `LayerMap.cs` consumer set | `tests/Autonogrammer.ArchitectureTests/TerminalRetirementArchitectureTests.cs:11` | Retirement pins stay; deletion would shrink the consumer set silently — forbidden. | **Keep** | No |

O3 Delete rows: **0**.

## Manifest summary

**Delete rows: 0. Modify rows (at Task 5 flip): 2 doc-sentence sites (O2-b). Keep rows: 9.**
The Task 5 cutover commit, on this evidence, deletes **nothing** beyond the default-flip docs. Every candidate examined is either still loaded (named loading site given per row), still serving a named OQ row, still guarding a retained path, or a doc sentence to rewrite at flip time. If the owner believes any candidate above should be deleted, the burden is a new consumer grep showing zero production references and a retiring guardrail — none is met at HEAD.

No deletion was performed. `git status` unchanged by this task (evidence only under `docs/runbooks/evidence/phase-84-preflip-2026-11-26/`).
