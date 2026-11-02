# Plan 71-05 SUMMARY: Presentation models and desktop artifact panel

Status: Tasks 1, 2 and 3 are implemented and committed. The desktop composition is not registered (out of scope, see Gaps). The pinned Mermaid hash check fails until 71-06 embeds the bundle, as the plan designs. The architecture gates were not run.

## Commits

| Commit | Content |
| --- | --- |
| `eab88d11` | Task 1: surface contract, panel, list, diff and access review models (Presentation) |
| `c04f75db` | Gap 1 resolved: `Cowork.Artifacts` friend assembly for `Native.Views` (orchestrator) |
| `9d26aa29` | Gap 2 resolved: browser tool host reaches the gateway (orchestrator, Phase 70) |
| `6e0b1da5` | Task 2: `ProjectReference` to `Cowork.Artifacts` from `Native.Views`, with relock |
| `143b7108` | Task 2: desktop panel, NativeWebView surface, content views, Mermaid asset provider, ViewMapping |
| `ff34ca5f` | Relock: Native app and Native.Tests lock files |
| `f6833310` | Presentation fix proven by a test: access origins described by host (see Deviation 6) |
| `22512411` | Panel fixes: skip control, access focus, `NavigationBlocked` state |
| `d833ba24` | Task 2 and 3 tests: panel, renderer, live class |
| `bba3738e` | Document-level removal of the adapter's bridge names (see Finding 1) |
| `97ad088a` | Live probe collects all findings and records the real navigation behaviour |

## Results

Targeted runs (Release):

- `tests/Autonogrammer.Presentation.Tests --filter-class "*Artifact*"`: total 26, succeeded 26, failed 0, skipped 0.
- `tests/Autonogrammer.Native.Tests --filter-class "*Artifact*"` (final run): total 20, succeeded 17, failed 0, skipped 3 (the three `ArtifactSurfaceLiveTests` cases, skipped without `AUTONOGRAMMER_LIVE_ARTIFACT_SURFACE=1`). Each skip message names the variable.
- `--filter-class "*ColourLiteral*"`: total 7, succeeded 7.
- `--filter-class "*ThemeContrast*"`: total 189, succeeded 189.
- Live, `AUTONOGRAMMER_LIVE_ARTIFACT_SURFACE=1 xvfb-run -a ... --filter-class "*ArtifactSurfaceLive*"`:
  - Hostile Html probe: passes (7 fixtures, see Findings).
  - Mermaid fixture fallback: passes (Unavailable with `MermaidAssetMissing`, bundle absent).
  - Pinned hash `a8a6a6f8...c3b9`: fails with "mermaid.min.js is not embedded", as designed until 71-06.
- Not run: `tests/Autonogrammer.ArchitectureTests` (LayerMap and HeadMotion gates). The project reference is L3 to L2, which amendment 2 permits. The full suite was not run, per the targeted-run rule.

Locked restore (`RestoreLockedMode=true`), all exit 0:

- `src/Autonogrammer.Native.Views`
- `apps/Autonogrammer.Native`
- `tests/Autonogrammer.Native.Tests`

Unlocked relock (`--force-evaluate -p:RestoreLockedMode=false`) added only lines: Native.Views +55, Native app +50, Native.Tests +40. No entries were removed. Native app relock was needed after all (its graph gains DiffPlex through Native.Views); the orchestrator note said to relock only if it failed. Npgsql was not added, because no `Cowork.Artifacts.Stores` reference exists yet.

Not verified on this host: `apps/Autonogrammer.Android` lock (DiffPlex entry expected when the Android job restores it).

## Plan deviations

1. Skip control is a `Button`, not a `HyperlinkButton`. The default `HyperlinkButton` template shares a static `TextDecoration` that another headless session's thread cannot draw, which broke parallel tests. `AutomationProperties.ControlTypeOverride` was not verified in this Avalonia build, so no link role is claimed.
2. Context-menu suppression is not implemented. The public Avalonia 12.1.1 API exposes no context-menu switch on any environment-args type. The string table names exist only in the WebView2 interop, so they cannot be set.
3. The post-message bridge option is not exposed. The surface never subscribes to `WebMessageReceived`, and it adds the host-document removal described in Finding 1.
4. A document-level bridge removal script was added (not in the plan), because the live probe showed the adapter installs the bridge names in every page.
5. `NavigationBlocked` state: a cancelled navigation after load moves the surface to `NavigationBlocked` (the plan's state machine implies this). Stop stays enabled while the displayed document is running or blocked.
6. Presentation fixes, which the orchestrator had said to avoid unless a test proved a bug (`TheAccessReviewDeniesOnEscapeOnceAndMarksMixedScripts` proved both): `ArtifactAccessReviewModel` passed whole origins (with scheme) to `ArtifactHostNames.Describe`, so the IDN decode never ran. Each origin's host is now described and its scheme and port kept. `ArtifactHostNames.Describe` computes the mixed-script mark on the decoded host. `ArtifactAccessReviewTests` still pass (26/26).
7. Access review view: the plan lists no file for it, but the objective names "access review". `ArtifactAccessReviewView` was added. Escape denies, Deny has focus (through the dispatcher), and the first answer is final.
8. Links in Markdown: `MarkdownAstBuilder` (shared) draws each link as underlined text with its full URL, and creates no anchor. Relative, `artifact:` and `javascript:` links are therefore text. No open-link action was built, so "links open only on explicit action" holds only vacuously.
9. Table: `DataGridTemplateColumn` with `FuncDataTemplate<string[]>` cells. A string `Binding` produced IL2026 under trimming-as-error.
10. Image: `ImageValidator.Validate` runs before any decode. The cap is `new ArtifactOptions().MaxImageBytes`.
11. Diff and Copy, Export and Edit: the panel raises `CommandRequested`. The host runs the models (the composition is out of scope).
12. Native.Views exposes `ArtifactDocumentBuilder` (policy-first host documents; Html, Svg and Mermaid) and `NativeWebViewArtifactSurface` takes a host document source. The surface refuses any document whose first element is not the policy meta.
13. Surface kinds that fail (SVG refused, Mermaid bundle missing, invalid UTF-8) show the source with the reason in the panel.
14. The Access review and version history use only the state the models expose. The panel has no version list (see Gaps).

## Findings from the live probe (Xvfb, WebKitGTK 4.1)

1. Bridge names are installed by the Avalonia adapter. In all seven hostile documents, `invokeCSharpAction` was a function and `window.webkit.messageHandlers` existed, although the surface subscribes to no message. The public API cannot turn this off. The host document now removes both names as its first script (`BridgeRemoval`, directly after the policy meta). With that removal, every displayed artifact document reports both names absent. `chrome.webview` and `external.postMessage` were absent throughout.
2. A cancelled navigation replaces the artifact with a blank `about:blank` page. For `location-assign` and `meta-refresh`, the probe read `href: about:blank` and `hostPolicyPresent: false`. The adapter's bridge names are present on that blank page, but no artifact code runs there. The live assertion is that the artifact is no longer displayed after the cancel. The panel reports "Navigation blocked: <target>".
3. `window.open` in a fixture produced no navigation that reached the surface, so no block was reported. It is recorded as an observation, not asserted.
4. No outbound resource request was observed in any document (`resourceEntries` 0), and no frame existed. Each document's parent was itself.

## Gaps (not built; state explicitly)

- Desktop composition is not built, per the orchestrator's out-of-scope list: `ArtifactsComposition.cs`, the hook lines in `CoworkSurfaceServicesFactory.cs` and `CoworkToolServer.cs`, `AddArtifacts()` and `Cowork:Artifacts` options, `FileArtifactStore`, `ArtifactSyncService`, `ArtifactToolProvider` registration, the `IDockPanel` registration with `CoworkDockHost`, and `ArtifactCompositionTests`. The sign-out teardown of the account-scoped store is therefore not wired on the desktop.
- Toolbar version picker: the panel model holds only the shown version, so there is no list to populate it from.
- F6 region cycle in `MainView`: not wired.
- Open-link action for Markdown: not built.
- Context-menu suppression: not achievable through the public API.
- Bridge names on the blank page after a cancelled navigation (Finding 2): upstream Avalonia behaviour.
- Mermaid pinned hash and live check 3: fail until 71-06 embeds `mermaid.min.js`. 71-06 owns the SUP row and the `EmbeddedResource` item.
- `ArchitectureTests` not run. `LayerMap`, `slnx`, `slnf` and `openapi.json` not touched, per instruction.
- Android lock not regenerated on this host.

## Checks for the main agent to run

- `tests/Autonogrammer.ArchitectureTests -c Release` (layer and HeadMotion gates for the new project reference).
- After 71-06: the live class with `AUTONOGRAMMER_LIVE_ARTIFACT_SURFACE=1 xvfb-run -a ... --filter-class "*ArtifactSurfaceLive*"`, expecting the hash check to pass.
- The composition work (gap list above) before the desktop panel is reachable from the shell.
