# Plan 71-05 SUMMARY: Presentation models and desktop panel (HALTED at Task 2)

Status: Task 1 complete. Task 2 and Task 3 are NOT started. Execution halted on two prerequisite gaps that the plan does not resolve. Plan 71-05 is not complete; 71-06 must not be treated as unblocked by this summary.

## Results

| Task | Commit | Result |
| --- | --- | --- |
| 1. Surface contract, panel models, access review | `eab88d11` | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*Artifact*"`: total 26, succeeded 26, failed 0, skipped 0 |
| 2. Desktop panel views, NativeWebView surface, table | not started | blocked (see Gaps 1 and 2) |
| 3. Live surface probe | not started | depends on Task 2 |
| Relock (amendment 9) | not started | depends on the Native.Views ProjectReference, which Task 2 adds |

## What landed (Task 1)

- `Presentation/Cowork/Artifacts/`: `IArtifactSurface` with the six states, `ArtifactSurfaceReason`, `ArtifactSurfaceRequest` (identity only, no bytes), `ArtifactNavigationBlockedEventArgs`, and `FakeArtifactSurface` recording every transition, load, blocked URL and destroy.
- `ArtifactPanelModel`: destroys the surface on hide, stop, close and sign-out; state is not preserved; every blocked navigation is kept and announced; first artifact auto-opens only under `AutoOpen`; a new version never moves the shown item; session grants live only while the surface does; version grants live in `ArtifactGrantLedger` and bind to `(ArtifactId, Number, ContentSha256)`; Access revokes and rebuilds with no network.
- `ArtifactListModel` with toolbar rules (Diff needs a previous version, Copy and Edit need text, Access needs an origin, Stop needs a running surface), `ArtifactDiffModel` with `+`/`-` markers and Added/Removed words, and `ArtifactAccessReviewModel` (Deny focused, Escape denies, timeout denies, first answer final, storage answered and never granted).
- `ArtifactHostNames`: punycode and decoded forms through `IdnMapping`; mixed-script marking is an approximation over common letter blocks and never proves a host safe.
- Tests: `ArtifactPanelModelTests`, `ArtifactAccessReviewTests`, `ArtifactListModelTests`, `ArtifactDiffModelTests` (26 cases, all green).

## Deviations (Task 1)

1. One top-level type per file (MA0048 under `latest-all` with `TreatWarningsAsErrors`). The plan named six files; the commit has 27.
2. Blocked navigation target is `System.Uri`, not `string` (CA1054/CA1056). The WebView reports navigations as URIs.
3. `ArtifactAccessReviewModel.FocusedChoice`, `Reason` and `Warning` copy is static (CA1822). `Reason` and `Warning` text is exposed as constants.
4. A rebuild after an approval destroyed the session grant before reloading. Fixed: a rebuild destroys the surface without clearing the session grant. Found by a failing test, then corrected.
5. Analyzer fixes in tests (CA1861 constant arrays moved to static fields).

## Gaps (blockers, in the order they stop Task 2)

1. **Cowork.Artifacts helpers the desktop views need are `internal`.** `CsvReader.Read` (Rendering/CsvReader.cs:64), `ImageValidator.Validate` (Rendering/ImageValidator.cs:48) and `SvgSanitizer.Sanitize` (Rendering/SvgSanitizer.cs:87, :96) are `internal`. `Autonogrammer.Cowork.Artifacts.csproj` exposes internals only to `Autonogrammer.Cowork.Artifacts.Tests`. The plan's table view (D-14), image view (D-15) and SVG path (D-23) need them from `Autonogrammer.Native.Views`. Fixing this means editing `Cowork.Artifacts` (a 71-02 file, not in 71-05's list) or adding a public facade. The orchestrator must choose.
2. **The Cowork tool host never reaches the gateway.** `CoworkToolServer.Start` calls the two-argument `McpGatewayServer.Start(registry, invoker)` (CoworkToolServer.cs:46). The three-argument overload that takes `ICoworkToolHost` (McpGatewayServer.cs:84) is never called. `CoworkHead.CreateAsync` builds `new CoworkToolServer()` with no host (CoworkHead.cs:68). The only `CoworkToolHost` is built in `BrowserUseComposition` (line 226) and is not handed to any gateway. So "register the provider with the tool host" (amendment 5 and 11) has no seam. Connecting it is an architectural change to the Phase 70 composition (Rule 4), not a hook line.

Also not verified before the halt: the `Avalonia.Controls.WebView` 12.1.1 `NativeWebView` option and event-argument member names. The DLL string table shows `IsInPrivateModeEnabled`, `IsWebMessageEnabled`, `AreDevToolsEnabled`, `AreDefaultContextMenusEnabled`, `Cancel`, but the public types were not confirmed by compile. Confirm them when Task 2 resumes.

## Plan drift noted

- Amendment 11 and the Task 2 text say `src/Autonogrammer.Presentation/Cowork` "does not exist yet". It exists (phase 69). Used as is.
- The plan's status line text ("No network. or the approved hosts") is garbled. Used the ADR-0033 copy: `Running in a sandbox. No network.`, and `Running in a sandbox. Requests go only to the approved hosts.` when an origin is approved.
- `src/Autonogrammer.UI/wwwroot/lib/mermaid/` is absent, as expected until 71-06.

## Next step for the orchestrator

Decide Gap 1 (expose the three `Cowork.Artifacts` helpers to `Native.Views`, or add a public facade) and Gap 2 (how `ICoworkToolHost` reaches `CoworkToolServer`, probably a Phase 70 follow-up). Then resume at Task 2 with the Native.Views ProjectReference, the relock, and the Native.Tests classes.
