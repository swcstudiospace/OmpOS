# Phase 74 plan check, round 2 (2026-10-29)

Checked: 74-01..74-10-PLAN.md, 74-CONTEXT.md (D-01..D-57), 74-RESEARCH.md, 74-VALIDATION.md against the tree at `/root/src/repos/ompos-autonogrammer`. Read-only: no plan, source or build touched. Round 1 findings were re-verified by reading the revised plan text; new findings come from the revision itself.

**Verdict: ISSUES** (1 BLOCKER, 4 WARNING, 4 INFO). All four round 1 blockers and W1-W9 are resolved except where listed below.

## Round 1 findings, re-checked

| Round 1 | Status | Evidence |
|---------|--------|----------|
| B1 hub has no public Dragonfly seam | Fixed | New 74-10 (wave 1, no deps): `IDispatchSignals`, `IDispatchPresence` (Set, Refresh as Lua CAS declaring KEYS, Remove, Get returning Online/Offline/Unknown), `IDispatchAuthNonces` (TryClaim `SET NX PX`, TryConsume), each with an in-memory twin and a Dragonfly implementation, one registration line per seam in both `AddInMemoryCoordination` and `AddDragonflyCoordination`. 74-05 lines 101/145: the hub uses only the public seams, "no Api file references StackExchange.Redis or a Dragonfly key"; depends_on adds 74-10. D-33/D-57 state `DragonflyKeyspace` is internal. Closed (nonce) and open (presence, signal) fail modes match 68-02. |
| B2 store has no feed | Fixed | 74-01 tasks 2-3: `AppendFeedEventAsync`, `ReadFeedAsync` (Resync page, 30-day prune), `DispatchStoreLimits`, closed result enums, feed cases in `DispatchStoreContractTests`; 74-02 implements them over `dispatch_events`/`accounts.feed_seq` and links the same oracle; 74-07 streams through the store. |
| B3 PairingTests asserted a 74-07 route | Fixed | `capability_unavailable` no longer appears in 74-03; only 74-07 (lines 92, 96). |
| B4 strip and prompt never mounted | Fixed | 74-09 adds `apps/Autonogrammer.Native/Dispatch/DispatchChromeBinding.cs` and `DispatchChromeBindingTests.cs` (strip visible while accepted/running with Stop; prompt at every computer_use start); the phase 69 registration member is marked `[INFERENCE]`, acceptable. |
| W1 env not exported | Fixed (residue W4) | `source "$(bash scripts/test-services.sh up)" && ...` in 74-02 task 1, 74-05 task 2, 74-10 task 2, VALIDATION lines 24, 27, 48, 49, 65 and RESEARCH:50. |
| W2 migration naming and registry | Fixed | `M0010_DispatchDeviceState.cs` (matches 68-03 `M0009_CoworkProjectsMemory.cs`); `Migrations/MigrationRegistry.cs` in `files_modified`; tenant hash is a `Func<string,string>` delegate (68-03 erasure pattern). |
| W3 close codes | Fixed | 74-04 `DispatchCloseStatus.cs` (in `files_modified`) parses `(NNNN reason)` from `DisconnectedHarnessException` (`AcpWebSocketTransport.cs:370-380`, verified). |
| W4 code.prompt tool classes | Fixed | 74-04: code.prompt and every session.* refuse a payload naming toolClasses, PolicyTests assert it; 74-07 line 92 rejects `toolClasses` outside `cowork.task`. |
| W5 CSS contract | Fixed | 74-06 read_first and verify: `--filter-class "*CssContract*"`; size budget noted as browser smoke (not run here). |
| W6 74-09 -> 74-07 | Fixed | `depends_on: [74-01, 74-04, 74-06, 74-07, 74-08]`. |
| W7 scope | Open (W2 below) | |
| W8 device cap, queue-full status | Fixed | store results `DeviceLimitReached`/`QueueFull` (74-01), pinned 409 `device_limit_reached` (74-03:145) and 409 `queue_full` (74-07:96); key unified as `Dispatch:Limits:DevicesPerAccount`. |
| W9 IJitterSource | Fixed | `Contracts/Dispatch/IJitterSource.cs` in 74-01 `files_modified`; 74-04 and 74-09 use it; `Dispatch.Client` never references `Dispatch.Desktop`. |

## Graph, ownership, coverage (re-run)

Waves: 74-01, 74-10 (w1) -> 74-02, 74-03, 74-04, 74-06 (w2, dep 01) -> 74-07 (w3, dep 01,03,10), 74-08 (w3, dep 01,06) -> 74-05 (w4, dep 01,02,03,07,10), 74-09 (w4, dep 01,04,06,07,08). Acyclic; wave = max(deps)+1 for all ten; matches D-56. No same-wave `files_modified` overlap (74-01 and 74-10 are disjoint). Cross-wave shared files stay consistent: `Program.cs` (03 three statements, 05 two lines), `openapi.json` (03, 07), `DispatchApiHost.cs` (03, 05), `DeviceEndpoints.cs`/`DispatchRateLimits.cs`/`DispatchModule.cs` (03, 07), `HeadServices.cs` (04 w2, 09 w4); the two Coordination registration files are now touched only by 74-10. No registry file, no package edit. Requirements: the union of `requirements` is AG-74-1..18 (74-10 adds AG-74-4, AG-74-14). Every `<automated>` is followed by a `<fails_when>` in all ten plans (counts equal, adjacent). Each plan has the D-52 gates line, Files owned, Shared files touched and Artifacts this phase produces. No bare `dotnet`. No reference to `IDispatchPresenceReader` remains; the `DragonflyKeyspace` mentions in 74-05 say the Api does not use it. VALIDATION has 26 rows (74-10-01 and 74-10-02 added, waves match).

## BLOCKER

**B5 - 74-05 / 74-07: D-34 "command POST 503 data_plane_unavailable with Retry-After" has no implementing code.**
Evidence: 74-05 lines 44 and 137 (and `DataPlaneDownTests`, line 139) assert it, but the POST route is owned by 74-07, which never mentions 503 or `data_plane_unavailable`. The only closed Dragonfly path is `IDispatchAuthNonces` (74-10); the presence read and the signal publish are open paths that never throw; D-25 says the Dragonfly idem domain is not used. No code path raises `DataPlaneTimeoutException` or a 503 on the command POST, and 74-05 owns no `CommandEndpoints.cs`, so its test asserts behaviour no plan builds.
Fix hint: in 74-07 task 1 define the rule (for example the enqueue answers 503 `data_plane_unavailable` with `Retry-After` when `IDispatchPresence.GetAsync` reports Unknown because Dragonfly is unreachable, or when a closed seam call throws `DataPlaneTimeoutException`), with a CommandRouteTests case using a faulting `IDispatchPresence` fake; 74-05 `DataPlaneDownTests` then only verifies it end to end. If only hellos should fail closed, amend D-34 instead.

## WARNING

**W1 - 74-03 / 74-05: nobody wires `DispatchStoreLimits` and the tenant-hash delegate into the stores.** `DispatchStoreLimits` appears only in 74-01 and 74-02; 74-03 registers `InMemoryDispatchStore` (line 170) and 74-05 `DispatchHubRegistration` selects `PostgresDispatchStore` (line 113) without saying that `Dispatch:Limits:*` and the retention values build the record, or that `ITenantHasher.HashTenant` is bound as the `Func<string,string>` (74-02 line 95 says the Api composition root does it). Add both to the two registration descriptions with one assertion each.

**W2 - scope (files per plan; role table: target 5-8, warning 10, blocker 15).** Files: 74-01 21, 74-02 5, 74-03 29, 74-04 20, 74-05 23, 74-06 23, 74-07 11, 74-08 14, 74-09 14, 74-10 15. Tasks: 3, 3, 3, 3, 3, 2, 2, 2, 3, 2. Estimated tokens (all confidence low): 55k, 50k, 60k, 60k, 60k, 55k, 50k, 45k, 55k, 40k. Reported as WARNING per the assignment; the role table would call 15 or more files a blocker for 74-01, 74-03, 74-04, 74-05, 74-06 and 74-10. 74-03 (29 files: five shared-file edits plus eight test files) is the largest risk. `estimate-check` not run (`workflow.smart_zone_tokens` unset).

**W3 - 74-05: stale ownership sentence and unstated fault injection.** Line 113 still says "74-01 registers IDispatchSignals inside AddInMemoryCoordination and AddDragonflyCoordination"; it is 74-10 now. The plan also adds a Dragonfly/Postgres service mode to `DispatchApiHost`, but how "Dragonfly down" is simulated for `DataPlaneDownTests` (faulting fakes over the 74-10 interfaces) is not stated.

**W4 - 74-02 tasks 2 and 3 verify without `source "$(bash scripts/test-services.sh up)"`** (lines 101, 119). `PostgresServerFixture` falls back to its own podman container when the variable is unset, so these pass or fail on podman availability, not on the throwaway server; use the same form as task 1.

## INFO

- I1: 74-01 task 1 acceptance says "holds the 11 files"; `files_modified` now lists 12 with `IJitterSource.cs`. `SystemJitterSource` has no file named.
- I2: 74-01 `IDispatchStore` text lists device create/rename/revoke/list but not the update of `credential_jti`, capability snapshot, `last_seen_at` and `paired_session_id` that 74-03 and 74-05 rely on; name them so the shared oracle covers jti rotation.
- I3: 74-CONTEXT lists D-57 before D-56 (cosmetic). 74-02:95 says Sessions.Postgres "gains no reference to ... Resilience" although its csproj already references Resilience (wording only).
- I4: Open from round 1: `SessionEndpoints.cs:445` is a call site (definition `private static` at :497; 74-07 now says so); the Devices chrome entry of D-46 stays with phase 69; ROADMAP `**Plans**` lists one plan (ten exist); VALIDATION frontmatter is `status: draft`, `nyquist_compliant: false`.

## Result

Fix B5 (74-07, with a pointer in 74-05) and W1; W3 and W4 are wording fixes. Nothing else contradicts CONTEXT or another plan.

## Planner follow-up after round 2 (2026-10-29)

Round 2 left one BLOCKER (B5) and four warnings; no third check round was run (limit of two). Fixes applied afterwards:
- B5: 74-07 task 1 now answers 503 `data_plane_unavailable` with `Retry-After` when `IDispatchPresence` reports Unknown or a closed seam call throws `DataPlaneTimeoutException`, with a data-plane-down case in `CommandRouteTests`; 74-05 `DataPlaneDownTests` simulate Dragonfly down with fault-injecting decorators of the 74-10 seams (ungated).
- W1: 74-03 registers `DispatchStoreLimits` from the Dispatch options; 74-05 passes it and the `ITenantHasher.HashTenant` delegate to `PostgresDispatchStore`.
- W3: 74-05 names 74-10 as the registrar of the three seams. W4: 74-02 tasks 2 and 3 and the VALIDATION rows use the `source "$(bash scripts/test-services.sh up)"` form.
- W2 (scope, files per plan above the role table's warning line) stays a WARNING: 74-01 21, 74-03 29, 74-04 20, 74-05 23, 74-06 23 files, driven by tests, one-line registry edits and the lock tests of D-48; plans were split (74-07 to 74-10) where a file-disjoint cut existed.
