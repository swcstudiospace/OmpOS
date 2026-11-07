# Phase 68 plan check (after the 2026-10-29 integration re-plan)

Checker: general subagent in the gsd-plan-checker role. Scope: 68-01..68-07-PLAN.md, 68-CONTEXT.md (D-01..D-72), 68-VALIDATION.md, REQUIREMENTS, ROADMAP.
Method: read every plan, then verified cited anchors and symbols against the worktree with grep, sed and `ls`. No builds, tests, podman or git writes.

## Verdict: BLOCKERS (2). Both are small, local fixes; everything else passes or is a warning.

## Dimension results

| Dimension | Result |
|-----------|--------|
| 1 Requirement coverage | PASS. AG-68-1,2,9 (68-02); 3,4,5,6,14 (68-03); 7,9,10,12 (68-04); 11 (68-05); 3,13 (68-06, 68-07); 8 (68-01). All 14 ids are in some `requirements` list and match REQUIREMENTS.md lines 667-680. |
| 2 Task completeness | PASS with warnings. Every `<automated>` is followed by `<fails_when>`; commands use only `/tmp/ag-bin/ag-dotnet` and `-- --filter-class/--filter-not-trait`, container tests use the status-preserving `source ... up && { ...; rc=$?; down; exit $rc; }` shape. |
| 3 Dependencies and waves | PASS. 01,06 -> wave 1; 02,03 (dep 01) -> 2; 04 (02,03) -> 3; 05 (04) -> 4; 07 (05,06) -> 5. Acyclic, wave = max(deps)+1. |
| 3b File ownership | PASS. files_modified of 01 vs 06 and 02 vs 03 are disjoint; 68-05 edits one line in the 68-04-owned file (declared). `openapi.json` is edited by 03 (wave 2) then 04 (wave 3), ordered. |
| 4 Key links | PASS. Erasure seam via L1 `ITenantKeyEraser` + delegate keeps Sessions.Postgres / Coordination.Dragonfly unreferenced (both L2, no tuple at `LayerMap.cs:58,64`, `SameLayerAllowed` at `:86`). |
| 5 Scope | 68-03 (about 45 files, 3 tasks) and 68-04 (about 28 files) are large but have per-task commit boundaries. |
| 6 Context compliance | PASS. No package beyond the already pinned `Microsoft.Extensions.Logging.Abstractions` (`Directory.Packages.props:65`), no registry edit, no Railway mutation, no RLS, no M0010, no artifact code, one 503 type. |
| 7 Citations | 41 path:line anchors and 17 symbols checked; 2 wrong (see W2, W3). |

## BLOCKERS

**B1. 68-01 task order: code uses `ILogger` before the package reference exists.**
Where: `68-01-PLAN.md` task 2 (GREEN, requires `ILogger`/`LoggerMessage.Define`) versus task 3 (adds the only `PackageReference`).
Evidence: `src/Autonogrammer.Resilience/Autonogrammer.Resilience.csproj` has only an `InternalsVisibleTo` item; `Directory.Build.props` has `TreatWarningsAsErrors`. Task 2's build fails with CS0246 before task 3 runs. Task 1 test code compiles against the same project.
Fix: move the `<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />` edit plus the non-locked restore and lock regeneration (task 3's files and its two `git status` lock commits) into task 1, committed first. Keep task 3 as the locked-mode and warning-free gate only.

**B2. 68-02 task 2 stops the shared Dragonfly container mid-test.**
Where: `68-02-PLAN.md` task 2 action ("Fail-mode tests run against a STOPPED container (stop the Dragonfly container mid-test ...)"), `DragonflyFailModeTests`.
Evidence: `scripts/test-services.sh up` starts one `autonogrammer-test-dragonfly` for the whole gate; `bootstrap.sh` and CI run test modules in parallel (`AUTONOGRAMMER_TEST_PARALLEL_MODULES`, comment at `bootstrap.sh:78-79`); `Coordination.Tests` classes and `Api.IntegrationTests` also use that Dragonfly. Stopping it makes unrelated tests fail intermittently, which breaks phase criterion 2 (whole Linux set green) and is not reproducible.
Fix: the outage must not touch the shared container. Build the failing client against an unreachable endpoint (a closed loopback port, `abortConnect=false`, short `connectTimeout`), or have the test start and stop its own throwaway container under a unique name. Say so in the action and add "the shared container is never stopped" to the acceptance criteria. [INFERENCE: the closed-port variant needs no podman.]

## WARNINGS

- W1. 68-03 task 1 action still says `Session gains required SessionSurface Surface`. The amendment (D-62) makes it `{ get; init; } = SessionSurface.Code`, not `required`. The amendment overrides, but fix the task text; `required` breaks `SessionService.cs:112`, `CloudAgentService.cs:437`, `RepositoryReadinessCheckTests.cs:48`. The same task says "write content into SessionSurface.cs": the file does not exist (`ls Contracts/Sessions` shows Message, Session, SessionStatus only), so it is a create.
- W2. Stale cite: 68-CONTEXT D-65 says the per-caller policy is "registered like `ApiAuthenticationExtensions.cs:75`". Line 75 is `app.UseAuthentication();`; the `AddRateLimiter(... AddPolicy<string, AuthConfigRateLimiterPolicy>(...))` registration is at `:62`.
- W3. `bootstrap.sh` anchors in D-69 / 68-06: `down` inside `cleanup` is `:68`, the closing brace `:75`, `trap cleanup EXIT` is `:76`, `up` is `:77`. The text pairs ":68, :75" with the trap. The GateParity assertion (trap precedes up) is still correct.
- W4. Docs that quote deleted code stay false: `docs/runbooks/backup-and-restore.md:45,159-165` (EnsureCreatedAsync at every start, "no version table"), `docs/privacy/data-retention.md:45`, `docs/security/hosted-threat-model.md` T3.4/T3.10 (schema step at every start) all name `PostgresSessionSchema`. 68-07 restricts those files to key text only (`D-68`) and omits `hosted-threat-model.md`. No gate catches it (`check_gitbook.py` checks only .md links). Add them to 68-07 task 2 (state the migrator/`migrate` flow) and files_modified.
- W5. 68-03 verifies only `PostgresSessionRepositoryContractTests`, `MigrationRunnerTests`, `PostgresRoleTests`. The edited `Api.IntegrationTests/Hosting/FailingSessionRepository.cs` and `Sessions.Tests/ObservingSessionRepository.cs` compile first in 68-04 (wave 3), and the in-memory contract class is not run. Add a build of `tests/Autonogrammer.Api.IntegrationTests` and the in-memory contract class to 68-03 task 1 verify. Also `ProductionConfig()` hosts are red between 68-03 and 68-04 (no EnsureCreated, helper arrives in wave 3); acceptable but say it in the SUMMARY.
- W6. 68-01 RED task does not list the `ResilienceTelemetry` MeterListener test that VALIDATION promises (it appears only in the GREEN action). Put it in task 1's files and action.
- W7. Tenant hasher wiring is underspecified: `TenantHasher` (L2) cannot bind the Api-level `DataPlaneOptions`; name its own options class bound to `DataPlane:TenantHashPepper`, and say which `ITenantHasher` 68-04 registers when `Coordination:Store=InMemory` (validator only requires the pepper for Dragonfly).
- W8. 68-03 lists `PostgresTestDatabase` creating `t_<guid>` databases `WITH TEMPLATE template_timescaledb` as [INFERENCE]; the first action must be the `\dx` check on `timescale/timescaledb:2.13.1-pg16` (`test-services.sh:11`) before relying on it. If the template is absent the whole Postgres test set fails.
- W9. ROADMAP "Plans" line for phase 68 (`.planning/ROADMAP.md:1377`) lists only `68-01-PLAN.md`; seven plans exist.
- W10. 68-02 frontmatter lists `DragonflyScripts.cs` but no task `<files>` names it; `AddLedgerSink` (68-05) has no named file. Name them.

## INFO

- Spot-check results: (a) telemetry is `Hosting/ApiTelemetryExtensions.cs:34-61` (`AddOtlpExporter` at `:47`, `:59`), `Program.cs:183` only calls it; (b) `Api.csproj:95-96` reference Resilience and Harness.AgentLoop, Resilience.csproj has no `PackageReference`; (c) the D-60 `EnsureCreated` caller list matches the tree exactly (2 Api files, Probe `:64`, 7 test classes, cref remarks); (d) layer rule confirmed; (e) `check-skip-markers.sh`, `SuppressionLedgerTests`, `check_gitbook.py`, `ImagePinTests` exist, last SUP row is SUP-239 (`open-questions.md:590`), `SUMMARY.md:78` Hosting; (f) `api.Containerfile:140` drops args, plan 06 handles it with a `$0` word, the acceptance grep matches `\"$@\"`; (g) Program.cs anchors `:37,:55-60,:92,:176-179,:183,:192,:196-199,:201,:203,:205,:206,:232-248` all hold; (h) `StartupGateHostedService.cs:39` probes `GetAsync("__health__")` and catches every non-cancellation exception, so a `DataPlaneTimeoutException` only delays opening; (i) wave order and file disjointness hold.
- The "old lock string" guard in 68-07 (b) names `autonogrammer.sessions.schema` / `autonogrammer.cloud-agents.schema`; those exist today only in the two classes 68-03 deletes, so the guard is consistent after the cutover. The old-name token scan finds only `scripts/rebrand/` (`rebrand.py`, `test_rebrand.py`), matching the single allow-list.
- ROADMAP criterion 3's literal grep matches the new `autonogrammer:v1:` prefix and cannot return nothing; 68-07's corrected check retires it with the reason recorded. Consider editing ROADMAP wording at phase close.

## Round 2 (after the fixes reported by replan-wave-2)

Verdict: PASS. B1 and B2 are closed; no new BLOCKER.

- B1 closed: `68-01-PLAN.md` now has four tasks; task 1 (package reference, lock files, `chore(68-01)` commit before any other commit) precedes RED and GREEN; the old task 3 is a build gate over the csproj only.
- B2 closed: `68-02-PLAN.md` lines 97, 102, 148 and `68-VALIDATION.md` row 24 use an unreachable loopback endpoint and state that the shared container is never stopped.
- Ownership: `files_modified` intersections (mechanical compare) are empty for 68-01 vs 68-06 and 68-02 vs 68-03. 68-03 vs 68-04 share only `docs/api/openapi.json`, which is ordered (wave 2 then wave 3). `JwtTestHost.cs` and `DataPlane/TestDatabaseMigrator.cs` are in 68-03's frontmatter only; 68-04 frontmatter and task `<files>` no longer list them, and its text says "created by 68-03 (D-60)" in task 1 and in the amendment. 68-03 and 68-04 agree.
- W1, W4 (07 frontmatter lists `hosted-threat-model.md`), W7 (`TenantHasherOptions` in 68-02, bound by 68-04, in-memory hasher), W10 (`LedgerSinkServiceCollectionExtensions.cs` in 68-05 frontmatter) verified present. 68-03 erasure takes the delegate and adds no ProjectReference.
- Open, non-blocking: W9 (ROADMAP Plans line lists only 68-01) is outside the phase directory and reported to the owner.
