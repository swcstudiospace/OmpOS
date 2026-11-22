---
phase: 71-cowork-artifacts
verified: 2026-11-22T01:53:17Z
status: gaps_found
score: "7/7 runnable pass; web access-flow gap remains"
covered_files:
  - ".planning/phases/71-cowork-artifacts/71-01-PLAN.md"
  - ".planning/phases/71-cowork-artifacts/71-01-SUMMARY.md"
  - ".planning/phases/71-cowork-artifacts/71-02-PLAN.md"
  - ".planning/phases/71-cowork-artifacts/71-02-SUMMARY.md"
  - ".planning/phases/71-cowork-artifacts/71-03-PLAN.md"
  - ".planning/phases/71-cowork-artifacts/71-03-SUMMARY.md"
  - ".planning/phases/71-cowork-artifacts/71-04-PLAN.md"
  - ".planning/phases/71-cowork-artifacts/71-04-SUMMARY.md"
  - ".planning/phases/71-cowork-artifacts/71-05-PLAN.md"
  - ".planning/phases/71-cowork-artifacts/71-05-SUMMARY.md"
  - ".planning/phases/71-cowork-artifacts/71-06-PLAN.md"
  - ".planning/phases/71-cowork-artifacts/71-06-SUMMARY.md"
  - ".planning/phases/71-cowork-artifacts/71-07-PLAN.md"
  - ".planning/phases/71-cowork-artifacts/71-07-SUMMARY.md"
  - ".planning/phases/71-cowork-artifacts/71-08-PLAN.md"
  - ".planning/phases/71-cowork-artifacts/71-08-SUMMARY.md"
  - ".planning/phases/71-cowork-artifacts/71-09-PLAN.md"
  - ".planning/phases/71-cowork-artifacts/71-09-SUMMARY.md"
  - ".planning/phases/71-cowork-artifacts/71-10-PLAN.md"
  - ".planning/phases/71-cowork-artifacts/71-10-SUMMARY.md"
  - ".planning/phases/71-cowork-artifacts/71-11-PLAN.md"
  - ".planning/phases/71-cowork-artifacts/71-11-SUMMARY.md"
  - ".planning/phases/71-cowork-artifacts/71-12-PLAN.md"
  - ".planning/phases/71-cowork-artifacts/71-12-SUMMARY.md"
  - ".planning/phases/71-cowork-artifacts/71-13-PLAN.md"
  - ".planning/phases/71-cowork-artifacts/71-13-SUMMARY.md"
  - ".planning/phases/71-cowork-artifacts/71-CONTEXT.md"
  - ".planning/phases/71-cowork-artifacts/71-PLAN-CHECK.md"
  - ".planning/phases/71-cowork-artifacts/71-RESEARCH.md"
  - ".planning/phases/71-cowork-artifacts/71-SOTA-COWORK-ARTIFACTS.md"
  - ".planning/phases/71-cowork-artifacts/71-SOTA-SUMMARY.md"
  - ".planning/phases/71-cowork-artifacts/71-VALIDATION.md"
  - ".planning/phases/71-cowork-artifacts/71-VERIFICATION.md"
covered_digest: "v1:sha256:20ec3f3dd26f148e6c30d522d098354e02efbace563810908fbd5c4e0b1255bd"
behavior_unverified: 1
overrides_applied: 0
re_verification: true
previous_status: gaps_found
previous_score: "7/7 runnable roadmap criteria executed 2026-11-21; integrated access-flow gap remains"
commits: ["b46fd81f"]
gaps:
  - truth: "No production web opener for artifact access dialog; 71-07..71-12 blocked (rollout, identity proof, DNS-rebinding). AG-71-17 open."
    status: unverified
    closing_step: "Land 71-07..71-12; add opener; record browser proof."
human_verification:
  - "Live surface hide/stop/signout + panel visual/a11y; OQ-400/401 decisions."
---

---
## Refresh 2026-11-22 (T855b-be, HEAD b46fd81f, SDK 10.0.402)

*Evidence first, then verdict (ADR-0002). No product code touched. Fresh test-services up failed twice at Dragonfly readiness (90 s, loaded host); DB suites ran vs existing healthy throwaway set (ag67-8382). No live-model, live-CDP, Win/macOS install, Railway, sign-in or screen-reader run claimed.*

*Status: gaps_found - 7/7 runnable pass; web access-flow gap remains*

| Check | Command | Result |
|---|---|---|
| restore | `dotnet restore Autonogrammer.Linux.slnf --locked-mode` | exit 0 |
| build | `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` | 124 projects, 0 errors, 0 warnings, exit 0 |
| format | `dotnet format Autonogrammer.Linux.slnf --verify-no-changes` | exit 0 |
| rebrand | `python3 -m unittest scripts/rebrand/test_rebrand.py; rebrand.py verify` | 61 OK; verify OK |
| checks | `check-licences/brand/changelog/openapi-breaking/sbom/skip-markers/parity` | all exit 0 |
| arch | `ArchitectureTests -c Release --no-build` | 107/107 |
| contracts | `Contracts.Tests non-Live` | 179/179 |
| accounts | `Accounts 215/215; Secrets.Os 83/83; Desktop 47/47` | all exit 0 |
| coord | `Coordination.Tests with services` | 87/87 |
| postgres | `Sessions.Postgres.Tests with services` | 148/148 |
| dataplane | `Api *DataPlane* 70/70; *Dispatch* 127/127; *Cowork* 44/44; *Guard* 12/12` | all exit 0 |
| cowork | `Cowork 123/123; McpGateway 79/79; AgentLoop 1328/1328` | all exit 0 |
| artifacts | `Artifacts 32/32; Stores 17/17; Memory 35/35; Memory.PG 1/1` | all exit 0 |
| browser | `BrowserUse.Tests 124/124` | exit 0 |
| compute | `ComputerUse 90/90; X11 9/9` | exit 0 |
| dispatch | `Client 1/1; Desktop 80/80` | exit 0 |
| resil | `Resilience 62/62; Sessions 124/124` | exit 0 |
| heads | `Presentation 2646/2646; UI 882/882; Web 444/444; Native 2353/2353` | exit 0 |
| a11y | `node --test a11y 48/48; fixture PASS; size ok; StyleA11y 17/17` | exit 0 |
| audit | `CoworkAudit 16/16` | exit 0 |

*Per-criterion: Artifacts 32/32 Stores 17/17 Api Cowork 44/44.*

*Gaps:*
- No production web opener for artifact access dialog; 71-07..71-12 blocked (rollout, identity proof, DNS-rebinding). AG-71-17 open. -> Land 71-07..71-12; add opener; record browser proof.

*Human (never automated):*
- Live surface hide/stop/signout + panel visual/a11y; OQ-400/401 decisions.

*Digest: v1:sha256 over concatenated bytes of covered_files in listed order excluding this VERIFICATION file (self-reference not computable). Prior content below retained as history.*

# Phase 71: Cowork Artifacts Verification Report

**Re-verification (2026-11-21, SDK 10.0.402, worktree auto-78 @ c04095ba):** every runnable ROADMAP success criterion was executed; all 7 pass (table below). Plans 71-07..71-12 were not executed (blocked: rollout gate, identity proof, destination enforcement — see their SUMMARYs); 71-13 shipped and is verified. The standalone artifact-access dialog still has no production web opener, so the integrated access flow remains the single open gap. The 2026-11-10 static inspection below is retained as historical record; its "not executed" verdicts are superseded by the executed results in this re-verification.

**Sync-off privacy repair:** The local sender no longer serializes the `grants` field or retains the unused internal grant list. `ArtifactSyncServiceTests` passed 3/3, including the actual sync-off sender and retry; `ArtifactWireRoundTripTests` passed 2/2 against the current authenticated API, including preservation of an existing grant. The endpoint's metadata-only response does not prove listing persistence, and compatibility with older deployed receivers is unverified. These focused results do not close Phase 71. Rolling out new per-version requests before compatible senders and receivers can silently lose them; plans 71-07 and 71-12 remain blocked on a safe rollout boundary.

**Phase Goal:** Cowork shows what the agent makes (markdown, code, HTML pages and small apps, SVG, Mermaid, CSV, images) in a panel beside the chat, as immutable hash-chained versions the user can diff, copy, edit, export and list from any signed-in head. Generated content is untrusted: it renders only in-process through renderers that cannot execute it, or in an isolated surface with a restrictive CSP, no network by default, no navigation and no bridge to the app.

**Verified:** 2026-11-10T14:30:00Z
**Initial status (historical):** human_needed
**Re-verification:** No — initial verification below; current-tree findings above supersede its verdict.
**Historical SOTA assessment:** Yes (71-SOTA-COWORK-ARTIFACTS.md + 71-SOTA-SUMMARY.md overlay post 01-06); it does not establish the current integrated flow or privacy boundary.

## Goal Achievement

### Observable Truths (Roadmap Success Criteria + Plan must-haves + SOTA)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Every hostile corpus entry is rendered inert and the artifact library passes. Verify: `dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Tests -c Release --filter-not-trait "Category=Live"` passes, including every hostile corpus entry. | ✅ VERIFIED (executed 2026-11-21) | Passed: total 32, failed 0, skipped 0 (includes HostileCorpusTests + per-renderer hostile tests). |
| 2 | The Postgres artifact stores pass against the throwaway services. Verify: `dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Stores.Tests -c Release --filter-not-trait "Category=Live"` passes with `scripts/test-services.sh up` sourced. | ✅ VERIFIED (executed 2026-11-21) | Passed: total 17, failed 0, skipped 0 against throwaway TimescaleDB/Dragonfly (same containers/env as test-services.sh; /dev/tcp probe unavailable in this sandbox so containers started manually with distinct suffix). Includes 71-13 `ArtifactSyncServiceTests` 3/3 (exact allowlist property set, no content/grant in body or headers). |
| 3 | The artifact endpoints pass and the OpenAPI document is fresh. Verify: `dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Artifact*"` passes and `docs/api/openapi.json` is fresh. | ✅ VERIFIED (executed 2026-11-21) | Artifact integration: total 22, failed 0, skipped 0. Freshness: `GuardTests` total 12, failed 0 (committed openapi.json matches generated document); `python3 -m json.tool docs/api/openapi.json` valid. Note: test builds regenerate openapi.json with `[]`→`[ ]` whitespace only; that churn was reverted, content unchanged. |
| 4 | The heads' artifact panels pass their tests. Verify: `dotnet test --project tests/Autonogrammer.Presentation.Tests`, `Native.Tests`, `UI.Tests`, `Web.Tests` with `-- --filter-class "*Artifact*"` pass. | ✅ VERIFIED (executed 2026-11-21) | Presentation Artifact: 26/26. Native Artifact: 31 passed + 3 skipped, 0 failed. UI Artifact: 20/20. Web Artifact: 15/15. |
| 5 | The architecture tests pass (LayerMap, graph, decision records, suppression ledger). Verify: `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release` passes (LayerMap, graph, decision records, suppression ledger). | ✅ VERIFIED (executed 2026-11-21) | Passed: total 107, failed 0, skipped 0 (includes LayerMap, ProjectGraph, CoworkSurfaceIsolation, DecisionRecord with ADR-0033). |
| 6 | The artifact page of the web smoke makes no outbound request, no navigation and no parent access from the hostile fixtures. Verify: `OUT_DIR="$(mktemp -d)" bash scripts/web-smoke/run-smoke.sh` passes with the artifact page: no outbound request, no navigation, no parent access from the hostile fixtures. | ✅ VERIFIED (executed 2026-11-21) | `OUT_DIR="$(mktemp -d)" SMOKE_TARGETS=artifact bash scripts/web-smoke/run-smoke.sh` → commit c04095ba, `artifact smoke exit=0`. |
| 7 | The licence check and the notices check exit 0. Verify: `bash scripts/ci/check-licences.sh` and `python3 scripts/legal/generate-third-party-notices.py --check` exit 0. | ✅ VERIFIED (executed 2026-11-21) | `licence checks passed` exit 0; `THIRD-PARTY-NOTICES.md is up to date` exit 0. |

**Score (re-verification):** 7/7 runnable ROADMAP criteria pass as executed. (The 2026-11-10 score "38/40 truths verified" counted SOTA/plan truths by inspection; those inspection findings are retained below and remain true, but the behavior-unverified labels on SC1/SC6 are superseded by the executed runs above.)

**SOTA overlay verified:** Yes. 71-SOTA-COWORK-ARTIFACTS.md accurately documents:
- States: Idle/Loading/Running/Stopped/NavigationBlocked/Unavailable + sync-off 404/static view + destroy (model Hide/Stop/Destroy + web/native impl)
- Sandbox: exact frame attrs + ArtifactDocumentPolicy (HtmlScriptsOn/Off + RenderResponseHeaders with sandbox allow-scripts + frame-ancestors) + no bridge + first-load only
- Destroy: on hide/stop/signout/second-load; nothing preserved
- A11y: ChromeLabel region, polite Announcement (LiveSetting), aria in AccessDialog, no colour literals in artifacts.css (uses tokens + forced-colors), import before a11y.css in autonogrammer.css
- Security: fail-closed (hostile corpus + renderers + CSP default none + owner 404 + ticket recheck + caps + audit hashes only + surface Cowork-only isolation)
- First-attempt/graceful: static view label, sync-off notice, unavailable fallback; smoke/tests assert clean

All match actual code reads.

### Deferred Items
None. (All AG-71-1..24 and roadmap SCs addressed in 01-06 + SOTA; no later-phase deferral in ROADMAP for this milestone.)

### Advisory (New Scope, Unevidenced)
- Full live surface (ArtifactSurfaceLiveTests) and full CI re-run of dotnet + smoke advised (env: .NET 10 SDK + services needed); recorded non-blocking per SOTA/VALIDATION/SUMMARY.
- OQ-400 (Mermaid legal admission default) and OQ-401 (interactive render availability) are owner decisions, not code gaps.

### Required Artifacts
| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| src/Autonogrammer.Cowork.Artifacts/* + .Stores/* + Tools/* | Core service, stores (file+pg), renderers (inert + policy), tool provider | ✓ VERIFIED | All substantive (hash verify, integrity, CSP, sanitizers, grants); no stubs/empty returns |
| services/Autonogrammer.Api/Cowork/Artifacts/* | Endpoints + tickets | ✓ VERIFIED | Owner scoped, HMAC tickets 60s, content as attachment, render with policy headers |
| src/Autonogrammer.Presentation/Cowork/Artifacts/* + Native + UI | PanelModel, IArtifactSurface, states, NativeWebView + web frame | ✓ VERIFIED | States enum, destroy contract, sandbox attrs, polite a11y, grants per version |
| scripts/web-smoke/* (artifact) + fixtures | Hostile smoke proof | ✓ VERIFIED | mjs + sh implement CDP checks against sandbox frames + fixtures |
| tests/.../Artifact* + Hostile + Architecture | Test coverage | ✓ VERIFIED | HostileCorpus, frame tests, layer map, integration exist and wired to impl |
| docs/api/openapi.json + CHANGELOG etc (71-06) | Fresh + owned | ✓ VERIFIED | Present, modified per summary |

### Key Link Verification
| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| ArtifactService | IArtifactStore (file/pg) | ctor + calls | WIRED | Pure core; stores injected |
| ArtifactBlock | UiBlockConverter | kind=artifact | WIRED | Degrades to UnknownBlock |
| ArtifactToolProvider | ICoworkToolProvider (phase 70) | MCP | WIRED | mcp__cowork_artifact_* |
| Render ticket | HMAC + anonymous render | 60s bound + recheck | WIRED | Tickets.cs + endpoints RenderAsync |
| PanelModel | IArtifactSurface (native/web) | StateChanged, Load/Stop/Destroy | WIRED | States flow, destroy on hide/signout |
| ArtifactFrame | sandbox iframe / srcdoc + ticket | onload destroy on 2nd | WIRED | Exact attrs per SOTA |
| Renderers | DocumentPolicy + sanitizers | ForHtml etc | WIRED | CSP meta first; no-exec |
| Store reads | Hash+parent re-verify | every Read* | WIRED | IntegrityException on fail |
| Web smoke | hostile fixtures | CDP checks | WIRED | artifact-page.mjs + run-smoke |

### Data-Flow Trace (Level 4)
| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| ArtifactPanelModel.Current / Surface | item bytes | ArtifactService.ReadVersion -> store | Yes (from Postgres/File) | ✓ FLOWING |
| Render/Content | bytes | directory.ContentAsync (account filtered) | Yes | ✓ FLOWING |
| Frame srcdoc/RenderUrl | document | HtmlDocumentWrapper + policy from stored | Yes (sanitized) | ✓ FLOWING |
| Ticket render | content | re-read after TryOpen HMAC+expiry | Yes | ✓ FLOWING |
| Grants/ApprovedOrigins | ledger | Grants + session | Yes | ✓ FLOWING |

No hollow/static fallbacks in prod paths.

### Behavioral Spot-Checks
Skipped (no runnable entry without .NET 10 SDK + services; see human_verification). Test files + logic paths exercised via grep/read instead.

### Probe Execution
- scripts/web-smoke/run-smoke.sh (artifact target): script present and implements the 3 checks (outbound/nav/parent) + CSP; not executed (browser/CDP needed).
- check-licences.sh / generate-third-party-notices.py --check: scripts present per 71-06; not executed.

### Requirements Coverage
All AG-71-1 to AG-71-24 covered (from 71-PLAN-CHECK + plan frontmatters + 71-SOTA-SUMMARY):
- Source plans claim complete; REQUIREMENTS.md lists them; code+tests map per 71-VALIDATION.
- No orphaned requirements for phase 71.
- Status: ✓ SATISFIED (by inspection + existence)

### Anti-Patterns Found
None blocking.
- No TODO/FIXME/XXX in artifact source (grep clean on phase files).
- No empty returns/stubs flowing to render (e.g. no `return null;` in service paths; all real queries + verify).
- Debt marker gate: none unreferenced.
- No new-scope unevidenced beyond the advisory (SOTA is doc overlay, accurate).

### Human Verification Required
See `human_verification:` frontmatter (1 item): live surface destroy + visual/keyboard/a11y check.

(The 1 behavior_unverified_item requires the live/platform run.)

### Gaps Summary
One open gap (frontmatter `gaps`): the integrated web Cowork access flow — the standalone dialog has no production web opener, and plans 71-07..71-12 (request metadata, grant store, identity, destination enforcement, web client, composition, browser proof) are blocked and unexecuted (summaries written 2026-11-21 record the blockers: rollout gate, device/session proof protocol, private-address/DNS-rebinding enforcement). Plan 71-13 (sync-off privacy) shipped and is verified above. AG-71-17 remains open. OQ-400 (Mermaid admission) and OQ-401 remain owner decisions.

Automated verification in the re-verification was executed on SDK 10.0.402 with throwaway services (see advisory). Remaining human confirmation is live/platform-only (WebView destroy, screen reader, contrast).

**Phase goal partially achieved:** the artifact library, stores, API, panels, architecture, hostile smoke and licence gates pass as executed; the signed-in access-grant flow is not delivered (gap above).

---
_Re-verified: 2026-11-21 (SDK 10.0.402, worktree auto-78 @ c04095ba; supersedes 2026-11-10 verdicts where they conflict)_
_Verifier: gsd-verify-71 (Claude per gsd-verifier process)_
