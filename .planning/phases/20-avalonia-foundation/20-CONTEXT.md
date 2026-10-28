# Phase 20: Avalonia Foundation - Context

**Gathered:** 2026-10-21
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss). The ROADMAP goal and success criteria are the spec. Constraints from the approved run are recorded below as locked decisions.

<domain>
## Phase Boundary

One shared Avalonia native project, a framework-neutral presentation layer, an accessibility baseline and plugin host contracts form the base that every head builds on.

Requirements: XP-01, XP-08, XP-09. Depends on: nothing (first phase of v0.10.0). Mac channel: No.

Success criteria (verbatim from `.planning/ROADMAP.md` lines 397-404):

1. One Avalonia 12.1.4 native project is in the solution and builds with warnings as errors. Verify: gate at phase HEAD.
2. A presentation layer project holds the view models and state extracted from the Razor components, with no UI-framework types, and references no Avalonia, MAUI or ASP.NET Core Components assembly. Verify: contract test in the gate that fails on any such reference or UI-framework type.
3. The presentation layer is consumed by both the Avalonia head and the Blazor web head; both build against it and its tests pass in the gate. Verify: gate build of both heads and presentation-layer tests.
4. Every control in the Avalonia head has an explicit AutomationProperties name, and the names of the prompt input, send control and transcript are exposed through AT-SPI2 on Linux. Verify: headless UI test in the gate that fails on an unnamed control; AT-SPI2 query output in the runbook.
5. Shared controls are reachable by keyboard, have a visible focus style, and disable transitions when reduced motion is on. Verify: headless UI test in the gate over every shared control.
6. Theme tokens for colour, type and spacing live in one resource dictionary, and a contract test asserts at least 4.5:1 text contrast for every foreground and background pair the dictionary defines. Verify: contract test in the gate.
7. Plugin host contracts in the contracts layer define commands, panels, tools and agent modes with no UI-framework reference, and an in-process sample plugin registers one of each through the host. Verify: contract test in the gate that fails if the contracts assembly references a UI framework assembly, and asserts each registration.

</domain>

<decisions>
## Implementation Decisions

### Locked constraints (from the approved run; not open for re-discussion)

- **Host:** Linux, Ubuntu 24.04 (Avalonia Tier 2). No Mac host, no Android emulator or SDK, no Windows host, no Railway or Vercel deploy, no Podman 5 install. A criterion that needs one of these is recorded as blocked with the missing prerequisite, never as passed.
- **Architecture:** ADR-0004 (`docs/decisions/ADR-0004-cross-platform-ui.md`). Avalonia 12.1.4 for the native heads. The web head stays Blazor WebAssembly on the shared presentation layer. Avalonia.Browser is NOT adopted (gate G-WEB failed on 12.1.4). The MAUI head retires by clean cutover in Phase 25, not here. Plugin host contracts live in the contracts layer with no UI-framework reference.
- **Quality bar:** warnings as errors, nullable, analyzers (`AnalysisLevel latest-all`, Meziantou, BannedSymbols), central package management (`Directory.Packages.props`), committed `packages.lock.json` per project, System.Text.Json source generation only, no reflection-based serialization, no `TODO` without an owner and an `docs/open-questions.md` link (`RepoHygieneTests`, `TodoMarkerScanner`), no secrets, Podman only (no Docker files or syntax), no invented vendor APIs or package versions. Every package ID and version is verified against NuGet or primary docs.
- **Conventions win:** layers L0 to L4 in `docs/architecture.md`, enforced by `tests/Autonogrammer.ArchitectureTests/LayerMap.cs` (single source), `ProjectGraphChecker`, `ArchUnitLayerTests`. New projects follow the five steps in `CONTRIBUTING.md` "Adding a project". Reuse existing patterns; do not establish a second convention.
- **Verification policy for this run:** subagents NEVER run builds, tests, linters or formatters. The orchestrator runs the gate (`./bootstrap.sh`, which builds and tests `Autonogrammer.Linux.slnf` and runs `dotnet format --verify-no-changes`) once per phase. Subagents do read-only inspection, edits, and web or NuGet fact checks.
- **Scope:** Phase 20 only. Phases 21 to 30 and v0.9.2 phases are not planned, closed or edited here. v0.11.0 is out of scope.

### Planning decisions (locked for this phase; each carries its reason)

- **D-20-01 Shared Avalonia native project:** `apps/Autonogrammer.Native` (assembly and namespace `Autonogrammer.Native`), TFM `net10.0`, layer L4. Reason: the repo has `apps/Autonogrammer.Web` and `apps/Autonogrammer.App`, and "Native" matches ADR-0004 wording ("native heads") without colliding with the MAUI head that is removed in Phase 25. It is the one shared project that Phases 22 and 24 build the platform heads from.
- **D-20-02 Presentation layer:** `src/Autonogrammer.Presentation` (namespace `Autonogrammer.Presentation`), layer L2. It references `Autonogrammer.Contracts` (L0) and `Autonogrammer.GenUi` (L2, a same-layer edge that needs a `LayerMap.SameLayerAllowed` entry). Its only third-party reference is Markdig, which is already used by `src/Autonogrammer.UI`. It must NOT reference Avalonia, any `Microsoft.Maui` assembly, or any `Microsoft.AspNetCore.Components*` assembly. Its tests live in `tests/Autonogrammer.Presentation.Tests` (plain xUnit, no UI framework).
- **D-20-03 Razor state moves out:** view models and state currently inside `src/Autonogrammer.UI` move to `Autonogrammer.Presentation`. That includes `State/SessionViewModel.cs`, `SessionViewState.cs`, `Components/ConversationSummary.cs`, `Services/MarkdownRenderer.cs`, and the logic in the `@code` blocks of `Components/Composer.razor` (draft, send gating, Enter and Escape handling), `SessionView.razor`, `ConversationList.razor` and the block views under `Components/Blocks/`. The `.razor` files keep markup and bind to presentation types. `src/Autonogrammer.UI` then references `Autonogrammer.Presentation`, which needs a second same-layer allow-list entry. Reason: criterion 2 requires a project that holds the view models and state. The web head consumes them through `Autonogrammer.UI`, which keeps criterion 3 true.
- **D-20-04 Clean caller migration, MAUI included:** every caller of a moved type is updated in the same phase. This includes `apps/Autonogrammer.App/Components/SessionHost.razor` (`@using Autonogrammer.UI.State` and `Autonogrammer.UI.Components`) and `apps/Autonogrammer.App/Autonogrammer.App.csproj`. No shim, re-export or alias namespace is kept. The MAUI head cannot be built on this host (no MAUI SDK, see Q-WEB-REFLECTION evidence). The edit is a usings-only change, and its compile verification is recorded as blocked on a host with the MAUI workload.
- **D-20-05 Plugin contracts:** contribution contracts in `src/Autonogrammer.Contracts/Plugins/` (namespace `Autonogrammer.Contracts.Plugins`): one contribution type each for commands, panels, tools and agent modes, plus a plugin descriptor. None of them reference a UI framework. The host (registry that accepts those contributions and lists them) is framework-neutral and lives in `Autonogrammer.Presentation` under `Plugins/`. The sample plugin is test code only, in `tests/Autonogrammer.Presentation.Tests`. Reason: the contracts layer must stay UI-free (XP-09), and the host is a consumer, not a contract.
- **D-20-06 Avalonia test project:** `tests/Autonogrammer.Native.Tests`, referencing `Avalonia.Headless.XUnit` 12.1.4 and `Avalonia.Themes.Fluent` 12.1.4. Repo-pinned `xunit.v3` 4.0.2 stays. Avalonia.Headless.XUnit 12.1.4 declares `xunit.v3.extensibility.core` as `[3.2.2, )`, and the repo's 4.0.2 satisfies that range. Binary compatibility against 4.0.2 is unverified until restore and build, and it is a stated risk. Fallback when `[AvaloniaFact]` cannot run under Microsoft.Testing.Platform: use `Avalonia.Headless` directly with plain `[Fact]` and an explicit dispatcher. Headless isolation stays `PerTest` (the default) unless measured to be too slow. `PerAssembly` is avoided because of issue #22021.
- **D-20-07 One theme dictionary:** colour, type and spacing tokens live in one AXAML `ResourceDictionary` inside `apps/Autonogrammer.Native` (for example `Themes/AutonogrammerTokens.axaml`). The contrast contract test takes every foreground token and every background token from that dictionary and asserts at least 4.5:1 for each pair, using WCAG 2.x relative luminance. Pairing is exhaustive (every foreground with every background) so no defined pair can be skipped.
- **D-20-08 Reduced motion:** the presentation layer owns `MotionPreference` (`System`, `Reduced`, `Full`). Avalonia 12.1.4 exposes no reduced-motion setting on `IPlatformSettings` (issue AvaloniaUI/Avalonia#19405 is open). So `System` resolves to `Full` on Avalonia until a platform reader exists. That default is recorded as an open question with owner `autonogrammer-maintainers`. The native head applies a `reduced-motion` class on its root, and a style removes `Transitions` from the shared controls under that class. The headless test sets `Reduced` and asserts no transitions remain.
- **D-20-09 Accessible names:** every control in `Autonogrammer.Native` sets `AutomationProperties.Name` explicitly. Defaults are not relied on (the spike measured a default name of "TextBox"). The prompt input, send control and transcript carry the names used in the AT-SPI2 evidence.
- **D-20-10 Focus and keyboard:** shared controls keep `Focusable` and `IsTabStop` at their defaults. The focus indicator is a `:focus-visible` style that reads its colour from the token dictionary. `FocusAdorner` is set to null where a custom indicator replaces the default, so no duplicate ring appears.
- **D-20-11 Explicit view mapping:** no reflection-based `ViewLocator`. The spike recorded trim-analysis IL2026 warnings from the default locator, and warnings are errors here.
- **D-20-12 Gate wiring:** each new project is added to `Autonogrammer.slnx`, to `Autonogrammer.Linux.slnf` (not excluded like the MAUI head), to `LayerMap.cs`, and to a `ProjectReference` in `tests/Autonogrammer.ArchitectureTests`. Each new project commits its `packages.lock.json`, produced with `dotnet restore --force-evaluate`. Restore and locking happen in the execution phase, not in planning.
- **D-20-13 Evidence location:** Linux AT-SPI2 output and any runbook entry go under `docs/runbooks/evidence/` in a dated Phase 20 directory. The spike directory `docs/runbooks/evidence/avalonia-spike-2026-10-21/` is referenced, not edited.
- **D-20-14 No Avalonia.Browser:** no browser build of the native head is produced in this phase. Blazor WebAssembly publish output and Vercel behaviour are not changed.

### Claude's Discretion

Exact file names inside the new projects, the internal shape of the presentation view models, the order of per-criterion tests, and the choice between one or two plans in each wave, as long as the locked decisions above hold.

### Amendments from research and pattern mapping (2026-10-21)

These refine the locked decisions above. They do not reopen them. Sources: `20-RESEARCH.md`, `20-PATTERNS.md`, `20-UI-SPEC.md`.

- **A-01 (refines D-20-03): JS interop stays in Razor.** `Components/SessionView.razor` (JS scroll module, `[JSInvokable]`) and `Components/Blocks/CodeBlockView.razor` (clipboard JS) keep their JS interop in `Autonogrammer.UI`. Everything else in the `@code` blocks that is framework-neutral moves to `Autonogrammer.Presentation`: composer send gating and key handling, A2UI mapping, block projection, and scroll state that needs no JS. The criterion-2 requirement is met by the view models and state, not by moving markup or interop.
- **A-02 (refines D-20-03): namespace move.** `SessionViewState` lives in namespace `Autonogrammer.UI` today, not `Autonogrammer.UI.State`. Its move changes every `using` in `src/Autonogrammer.UI` (including `_Imports.razor`), `tests/Autonogrammer.UI.Tests`, and `apps/Autonogrammer.App/Components/SessionHost.razor`. All of them are migrated in the same phase (D-20-04).
- **A-03 (refines D-20-12): ArchitectureTests references.** `tests/Autonogrammer.ArchitectureTests` gets a `ProjectReference` only to the product assemblies that `LayerMap` maps and that must load for ArchUnit: `Autonogrammer.Presentation` and `Autonogrammer.Native`. Test projects get no such reference. Test projects are not in `LayerMap`.
- **A-04 (new requirement): TODO scanner covers AXAML.** `tests/Autonogrammer.ArchitectureTests/TodoMarkerScanner.cs` does not scan `.axaml`. An unlinked TODO in the new token dictionary or views would pass the gate. The scanner's extension list gains `.axaml` in this phase, with a test that proves an unlinked `.axaml` TODO is reported.
- **A-05 (refines D-20-06): test-assembly parallelism.** `tests/Autonogrammer.Native.Tests` sets `[assembly: CollectionBehavior(DisableTestParallelization = true)]` in addition to the `[AvaloniaFact]` setup. Reason: issue AvaloniaUI/Avalonia#22021, where plain `[Fact]` tests that build Avalonia objects race an Avalonia session. The setting holds whether or not plain facts are later added.
- **A-06 (new requirement): central package versions for the Avalonia transitive set.** `CentralPackageTransitivePinningEnabled` is on. Every Avalonia transitive package that restore resolves (for example `Avalonia.BuildServices`, `Avalonia.Remote.Protocol`, `MicroCom.Runtime`, `Avalonia.Native`, `Avalonia.X11`, `Avalonia.Skia`, `Avalonia.Win32`, `Avalonia.HarfBuzz`, `Avalonia.Fonts.Inter`, `xunit.v3.mtp-v2`) needs a `PackageVersion` in `Directory.Packages.props`. The exact set comes from restore output in the execution phase. Planning records the rule, not the list.
- **A-07 (UI-SPEC decisions, locked for Phase 20):** Send and Stop are always enabled, so they stay in the tab order. Pressing either with nothing to do is a no-op with a status message. The only non-essential animation is a 120 ms Button background transition, which reduced motion removes. The native accent is `#0b4fd6`, which differs from the web accent `#0b5fff` because the web accent measures 4.04:1 on the hover background. Accessible names are the web Composer labels (`Message`, `Send message`, `Stop response`) plus `Conversations`, `Session transcript` and `Session status`. The full contract is `20-UI-SPEC.md`.
- **A-08 (UI-SPEC, departure from D-20-10, flagged):** the transcript `ScrollViewer` is made focusable explicitly, because the default focusability of a `ScrollViewer` is unverified. Its runbook entry must confirm keyboard reach.

The plugin contract and host decisions (D-20-05), the reduced-motion decision (D-20-08), and every other locked decision stand unchanged.

</decisions>

<code_context>
## Existing Code Insights

Verified from the repository on 2026-10-21 (planning-time reads, no build):

- Solution filters: `Autonogrammer.slnx` lists `apps/Autonogrammer.Web`, `apps/Autonogrammer.App` (MAUI), `services/Autonogrammer.Api`, and the `src/` and `tests/` projects. `Autonogrammer.Linux.slnf` lists every Linux-buildable project and omits `apps/Autonogrammer.App`.
- Build defaults: `Directory.Build.props` sets net10.0, `TreatWarningsAsErrors`, `AnalysisLevel latest-all`, `ManagePackageVersionsCentrally`, `CentralPackageTransitivePinningEnabled`, `RestorePackagesWithLockFile`, `NuGetAuditMode all`, and `JsonSerializerIsReflectionEnabledByDefault false`. Any project whose name ends in `Tests` becomes an `Exe` on Microsoft.Testing.Platform and is not trimmable. `Directory.Packages.props` pins `xunit.v3` 4.0.2, `Shouldly` 4.3.0, `NSubstitute` 6.2.0, `TngTech.ArchUnitNET.xUnitV3` 0.13.4, `Microsoft.AspNetCore.Components.Web` 10.0.13, `Markdig` 1.4.2 and others. No Avalonia entry exists yet.
- `src/Autonogrammer.UI/Autonogrammer.UI.csproj` is a Razor class library (`Microsoft.NET.Sdk.Razor`, `SupportedPlatform browser`) referencing `Microsoft.AspNetCore.Components.Web` and `Markdig`, plus `Autonogrammer.Contracts` and `Autonogrammer.GenUi`. Its `State/SessionViewModel.cs` (124 lines) is already framework-neutral apart from `Autonogrammer.GenUi` and `Autonogrammer.Contracts` types.
- Logic still in Razor `@code` blocks: `Components/Composer.razor` holds the draft, the send-in-flight guard, the Enter and Escape key handling, and the `OnSend` contract (the draft clears only on acceptance). `SessionView.razor` (158 lines), `ConversationList.razor` and the `Blocks/*` views hold their own rendering helpers. `Services/MarkdownRenderer.cs` (92 lines) is pure C#.
- Existing UI tests: `tests/Autonogrammer.UI.Tests` (bUnit, `UiTestContext`) covers approval state, composer draft, conversation list, failed-turn folds, markdown sanitisation, scroll, replay tree and the UiBlock renderer. Logic-only tests move with their code to `tests/Autonogrammer.Presentation.Tests`. bUnit tests stay with the Razor shell.
- Web head: `apps/Autonogrammer.Web` (Blazor WebAssembly, `browser` platform) references `Autonogrammer.UI` and `Autonogrammer.Contracts`. Its `JsonSerializerIsReflectionEnabledByDefault` is set to true for the HeadOutlet interop (Q-WEB-REFLECTION, scoped override). Do not change it.
- MAUI head: `apps/Autonogrammer.App` references `Autonogrammer.UI`. `Components/SessionHost.razor` imports `Autonogrammer.UI.Components` and `Autonogrammer.UI.State`. Its platform `MauiProgram.*.cs` files reference `Autonogrammer.EngineHost`.
- Architecture rules: `tests/Autonogrammer.ArchitectureTests/LayerMap.cs` maps every product assembly to L0 to L4. `SameLayerAllowed` currently allows `Autonogrammer.UI` to `Autonogrammer.GenUi`. `ProjectGraphTests` rejects upward references and any reference to `Autonogrammer.Api` from a non-test project, and it keeps `Autonogrammer.Web` free of `Harness.Acp`, `Harness.Rpc` and `Sandbox`. `ArchUnitLayerTests` loads each mapped assembly from `AppContext.BaseDirectory`, and it tolerates only `Autonogrammer.App` being absent.
- Repo hygiene: `RepoHygieneTests` forbids Docker tooling in `bootstrap.sh`, `.github/workflows/ci.yml` and `deploy/compose.yaml`. It also checks docs size and the `docs/open-questions.md` table header. `TodoMarkerScanner` requires each TODO to name an owner and an OQ ID that exists.
- Spike evidence (`docs/runbooks/evidence/avalonia-spike-2026-10-21/README.md`): desktop Avalonia 12.1.4 built with warnings as errors (0 warnings) on Ubuntu 24.04. AT-SPI2 showed a TextBox as role "entry" named "TextBox" by default and as "Greeting" with `AutomationProperties.Name`. G-WEB failed with no textbox role in the Chromium tree. The browser publish needed the `wasm-tools` workload. The spike projects were throwaway and are not product code.
- Host tools already verified in the spike: Xvfb, xvfb-run, dbus-run-session, at-spi2-core 2.52.0, python3-pyatspi, google-chrome. These are needed for the Linux AT-SPI2 query in criterion 4.
- Open questions relevant to this phase: Q-WEB-REFLECTION (scoped JSON reflection override in the web head), OQ-17 (macOS MAUI workload on runners), OQ-09 (Vercel build specifics). A new open question is needed for the reduced-motion OS reader (see D-20-08) and for any other `[UNVERIFIED]` fact the research finds.
- Working tree at planning start: clean apart from an untracked `docs/runbooks/evidence/ho-03-podman-compose-config-2026-10-21.md`, which is not part of this phase and is left alone.

</code_context>

<specifics>
## Specific Ideas

- Keep the first Avalonia head small: one main window with a session list, a transcript region, a composer (prompt input and send control) and a stop control, all bound to the presentation layer. Phase 21 adds real session chat, approvals and steering on top.
- Tests in `Autonogrammer.Native.Tests` should use the same `Autonogrammer.Presentation` view models the web head uses, so the drift guard from ADR-0004 is real and not a copy.
- The AT-SPI2 query can run under `xvfb-run` plus `dbus-run-session` on this host. Its output is evidence, recorded with the command and the date.

</specifics>

<deferred>
## Deferred Ideas

- Platform heads for Linux, Windows, macOS (Phase 22), iOS and Android (Phase 24). Phase 20 provides only the shared project.
- Parity surface features: session chat, tool approvals, steering (Phase 21).
- Avalonia.Browser and G-WEB re-measurement (Phase 23).
- MAUI removal and Razor library removal (Phase 25).
- OS-level reduced-motion detection on each platform (open question, see D-20-08).
- Tier 1 Linux CI for Avalonia (Ubuntu 25.x, Fedora 43 or Debian 13). Ubuntu 24.04 is Tier 2 on this host.

</deferred>
