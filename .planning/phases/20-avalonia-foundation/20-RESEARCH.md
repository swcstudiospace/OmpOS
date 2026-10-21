# Phase 20: Avalonia Foundation - Research

**Researched:** 2026-10-21
**Domain:** Avalonia 12.1.4 native head; Avalonia.Headless and xUnit v3 on Microsoft.Testing.Platform (MTP); AT-SPI2 accessible names; WCAG 2.x contrast; plugin contracts
**Confidence:** HIGH for package IDs, versions, dependency sets and member names (read from 12.1.4 source or NuGet API). MEDIUM for `[AvaloniaFact]` under MTP with xunit.v3 4.0.2 (upstream evidence only; nothing was run). LOW for analyzer behaviour on Avalonia XAML-generated code (no evidence found).

**Method:** Read-only. No `dotnet` build, test, restore or format was run, and no repo source or docs were edited. Facts come from the NuGet API, the GitHub REST API (tag and tree), raw source at tag `12.1.4` (the two places marked "master" are called out), `docs.avaloniaui.net` `.md` pages, xunit.net docs, W3C WCAG pages, and repo files. Every URL used is listed under `## Sources`.

## User Constraints

Locked decisions D-20-01 to D-20-14 in `20-CONTEXT.md` (Planning decisions) are taken as given and are not re-decided here. Index by ID (full text is in `20-CONTEXT.md`, Planning decisions, lines 38-54):

| ID | Locked decision (short title) |
|---|---|
| D-20-01 | Native project `apps/OmpOS.Native`, namespace `OmpOS.Native`, `net10.0`, layer L4 |
| D-20-02 | Presentation layer `src/OmpOS.Presentation`, L2; references Contracts (L0) and GenUi (L2); no Avalonia, MAUI or AspNetCore.Components reference; Markdig only |
| D-20-03 | Razor state moves to `OmpOS.Presentation`; `OmpOS.UI` references it (second same-layer allow-list entry) |
| D-20-04 | Clean caller migration including `apps/OmpOS.App` usings; no shims or aliases |
| D-20-05 | Plugin contracts in `src/OmpOS.Contracts/Plugins/` (no UI reference); host in `OmpOS.Presentation/Plugins/`; sample plugin is test code |
| D-20-06 | Test project `tests/OmpOS.Native.Tests` with `Avalonia.Headless.XUnit` 12.1.4 and `Avalonia.Themes.Fluent` 12.1.4; `xunit.v3` 4.0.2 stays; fallback is `Avalonia.Headless` with plain `[Fact]` and an explicit dispatcher; `PerTest` isolation |
| D-20-07 | One AXAML token dictionary in `apps/OmpOS.Native`; contrast test covers every foreground with every background at 4.5:1 |
| D-20-08 | `MotionPreference` (`System`, `Reduced`, `Full`) owned by presentation; `System` resolves to `Full` on Avalonia; `Reduced` removes `Transitions` from shared controls; default recorded as an open question |
| D-20-09 | `AutomationProperties.Name` set explicitly on every control; defaults not relied on |
| D-20-10 | Shared controls keep default `Focusable` and `IsTabStop`; `:focus-visible` style reads token colour; `FocusAdorner` null where a custom indicator replaces the default |
| D-20-11 | Explicit view mapping; no reflection-based `ViewLocator` |
| D-20-12 | Wiring: `OmpOS.slnx`, `OmpOS.Linux.slnf`, `LayerMap.cs`, `ProjectReference` in `tests/OmpOS.ArchitectureTests`; `packages.lock.json` committed (restore happens in execution, not planning) |
| D-20-13 | Evidence under `docs/runbooks/evidence/` in a dated Phase 20 directory; spike directory referenced, not edited |
| D-20-14 | No Avalonia.Browser build in this phase |

**Deferred (out of scope):** platform heads for Linux, Windows, macOS (Phase 22), iOS and Android (Phase 24); parity features (Phase 21); Avalonia.Browser and G-WEB re-measurement (Phase 23); MAUI and Razor removal (Phase 25); OS-level reduced-motion detection (open question); Tier 1 Linux CI.

## Summary

- **Package set is complete for both new projects.** `Avalonia.Desktop` 12.1.4 exists. Its `net10.0` dependencies (`Avalonia.Native`, `Avalonia`, `Avalonia.X11`, `Avalonia.HarfBuzz`, `Avalonia.Skia`, `Avalonia.Win32`) are all `[12.1.4, )`. `Avalonia.Headless` and `Avalonia.Headless.XUnit` are 12.1.4 and depend on `[12.1.4, )` Avalonia packages. `Avalonia.Headless.XUnit` declares `xunit.v3.extensibility.core` `[3.2.2, )`; the repo's pinned `xunit.v3` 4.0.2 brings `xunit.v3.mtp-v2` 4.0.2, and `xunit.v3.extensibility.core` 4.0.2 exists. Binary compatibility of the 12.1.4 build against 4.0.2 is not verified (U-01).
- **The MTP route has upstream evidence.** At tag `12.1.4`, Avalonia's own `Avalonia.Headless.XUnit` test projects are `Exe` test projects that import `build/XUnit.props`. That file adds `xunit.v3.mtp-v2`, and the tests use `[AvaloniaFact]` with `[assembly: AvaloniaTestApplication(...)]` and `[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]`. Nothing was executed in this repo, so the D-20-06 primary path is supported upstream but not verified here (U-02).
- **Dispatch API is verified.** `Dispatcher.UIThread` is a public static property and `Dispatcher.RunJobs(DispatcherPriority? priority = null)` is public. The non-XUnit headless path is `HeadlessUnitTestSession.StartNew(typeof(App))` followed by `Dispatch(...)`, both verified in source.
- **A race must be designed around.** Avalonia issue #22021 (reported against 12.1.1; no closed state visible on the fetched page) shows that a plain `[Fact]` constructing any `AvaloniaObject` (for example `SolidColorBrush`) on an xUnit worker thread can claim the UI thread while an `[AvaloniaFact]` session starts, and then the session throws `VerifyAccess`. The reporter's controlled run: 5 of 16 processes hung with default parallelism, 0 of 80 with `[assembly: CollectionBehavior(DisableTestParallelization = true)]`. This applies to the D-20-06 fallback directly (U-03).
- **Input and accessibility API names are verified in source:** `Key.Tab`, `Key.Enter` (alias of `Key.Return`) and `Key.Escape` (read from `master`, not the 12.1.4 tag); `PhysicalKey.Tab`, `PhysicalKey.Enter`, `PhysicalKey.Escape` (12.1.4); extension methods `KeyPress`, `KeyPressQwerty`, `KeyRelease` and `KeyTextInput` on `TopLevel` in namespace `Avalonia.Headless` (12.1.4); `AutomationProperties.SetName` and `GetName` on `StyledElement` in namespace `Avalonia.Automation` (12.1.4).
- **Reduced motion:** `IPlatformSettings` at 12.1.4 declares no motion or animation member. Issue #19405 ("Respect system animation settings", reported on Windows) shows no closed state on the fetched page. The D-20-08 default stands.
- **Tier:** Ubuntu 24.x is Tier 2 on the supported-platforms page; Ubuntu 25.x is Tier 1 (verified).
- **Contrast:** the WCAG 2.2 relative-luminance and contrast-ratio definitions are verified, including the constants and the 4.5:1 threshold for normal text (SC 1.4.3).
- **Gate scope is confirmed.** `OmpOS.Linux.slnf` already includes `src/OmpOS.UI`, `tests/OmpOS.UI.Tests`, `apps/OmpOS.Web` and `tests/OmpOS.ArchitectureTests`, and excludes `apps/OmpOS.App` (consistent with D-20-04 and D-20-12).

**Primary recommendation:** Use `[AvaloniaFact]` on xUnit v3 4.0.2 with `[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]` and `[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]`. Do not add plain `[Fact]` tests that construct Avalonia objects to `OmpOS.Native.Tests` unless `DisableTestParallelization` is set in that assembly. Treat the xUnit binary-compatibility question (U-01) as a build-time check in the execution phase.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|---|---|---|---|
| View models, session state, motion preference, plugin host | `OmpOS.Presentation` (L2) | `OmpOS.UI` (Razor, L2) and `OmpOS.Native` (L4) consume it | D-20-02 and D-20-03: one framework-neutral state layer for both heads |
| Rendering of controls, focus visuals, token resources | `apps/OmpOS.Native` (Avalonia, L4) | Web head uses its own Razor markup and CSS | Native head owns Avalonia visuals; the web head stays Blazor WebAssembly (ADR-0004) |
| Accessible names and AT-SPI2 exposure | `apps/OmpOS.Native` controls (`AutomationProperties`) | OS AT-SPI2 bus (Linux) | D-20-09; Avalonia exposes the tree to AT-SPI2 when a D-Bus session bus and accessibility service are present |
| Plugin contracts | `src/OmpOS.Contracts/Plugins` (L0) | Host in `OmpOS.Presentation/Plugins` | D-20-05; contracts must not reference a UI framework |

## Standard Stack

### Core

| Package ID | Version | Role | Source URL | Verified (this session) |
|---|---|---|---|---|
| Avalonia | 12.1.4 | UI framework: `StyledElement`, `AutomationProperties`, `Dispatcher`, `Application` | https://www.nuget.org/packages/Avalonia | yes |
| Avalonia.Desktop | 12.1.4 | Desktop entry point; provides `AppBuilder.UsePlatformDetect()` (namespace `Avalonia`) | https://www.nuget.org/packages/Avalonia.Desktop | yes |
| Avalonia.Themes.Fluent | 12.1.4 | Fluent theme for the native head and the headless test app | https://www.nuget.org/packages/Avalonia.Themes.Fluent | yes |
| Avalonia.Headless | 12.1.4 | Headless platform, input simulation extensions, `HeadlessUnitTestSession` | https://www.nuget.org/packages/Avalonia.Headless | yes |
| Avalonia.Headless.XUnit | 12.1.4 | `[AvaloniaFact]`, `[AvaloniaTheory]`, `AvaloniaTestFrameworkAttribute` | https://www.nuget.org/packages/Avalonia.Headless.XUnit | yes |
| xunit.v3 | 4.0.2 (repo pin in `Directory.Packages.props`) | Test framework; installs `xunit.v3.mtp-v2` 4.0.2 | https://www.nuget.org/packages/xunit.v3 | yes |

### Supporting

| Package ID | Version | Role | Source URL | Verified (this session) |
|---|---|---|---|---|
| xunit.v3.mtp-v2 | 4.0.2 (exact, transitive from `xunit.v3`) | MTP v2 runner; Avalonia's own XUnit test projects reference it directly | https://www.nuget.org/packages/xunit.v3.mtp-v2 | yes |
| xunit.v3.extensibility.core | 4.0.2 (satisfies Avalonia.Headless.XUnit range `[3.2.2, )`) | Base types that `Avalonia.Headless.XUnit` compiles against | https://www.nuget.org/packages/xunit.v3.extensibility.core | yes for existence and version; binary compatibility no (U-01) |
| Avalonia.Fonts.Inter | 12.1.4 (transitive from Avalonia.Headless) | Font used by headless text measurement | https://www.nuget.org/packages/Avalonia.Headless | yes (dependency list) |
| Avalonia.HarfBuzz | 12.1.4 (transitive from Avalonia.Headless and Avalonia.Desktop) | Text shaping | https://www.nuget.org/packages/Avalonia.Desktop | yes (dependency list) |
| Avalonia.X11, Avalonia.Skia, Avalonia.Native, Avalonia.Win32 | 12.1.4 (transitive from Avalonia.Desktop) | Platform backends pulled by `UsePlatformDetect` | https://www.nuget.org/packages/Avalonia.Desktop | yes (dependency list) |
| Markdig | 1.4.2 (repo pin) | Markdown in `OmpOS.Presentation` (already used by `OmpOS.UI`) | repo `Directory.Packages.props` | no (repo pin read; NuGet not re-fetched) |
| Meziantou.Analyzer | 3.0.315 (repo global analyzer) | Analyzer on every project | repo `Directory.Packages.props` | no (repo pin read; NuGet not re-fetched) |
| Microsoft.Testing.Platform | 2.5.1 (repo pin) | MTP host already pinned | repo `Directory.Packages.props` | no (repo pin read) |

### Not used

| Package ID | Reason | Source URL | Verified (this session) |
|---|---|---|---|
| Avalonia.Browser | Not adopted in this phase (D-20-14; G-WEB failed on 12.1.4 per ADR-0004) | https://www.nuget.org/packages/Avalonia.Browser | no (cited from ADR-0004 only) |

**Version verification:** Every 12.1.4 package and `xunit.v3` 4.0.2 is published 2026-10-09 on NuGet (NuGet API pages above). Tag `12.1.4` resolves to commit `2d79ba55fae1b1e71c69f93a668080c9029e6a31` (GitHub refs API).

**Central package additions (execution phase, not done here):** add `PackageVersion` entries for `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Headless` and `Avalonia.Headless.XUnit` at `12.1.4` in `Directory.Packages.props`. Do not add an entry for `xunit.v3.mtp-v2`; it arrives transitively from the pinned `xunit.v3` (the MTP doc says v2 is the default for 4.0.0 and later).

## Architecture Patterns

### Dependency shape

```
OmpOS.Contracts (L0)  <--  OmpOS.Presentation (L2)  <--  OmpOS.UI (L2, Razor)  <--  OmpOS.Web (L4)
  Plugins/ contracts        view models, state,            (Blazor WASM head)
                            plugin host, MotionPreference
                                    ^
                                    |
                            OmpOS.Native (L4, Avalonia)   [new]
OmpOS.GenUi (L2)  <--  OmpOS.Presentation   (new same-layer edge: add to SameLayerAllowed)
```

### Per new or changed project

| Path | Layer | References | Why |
|---|---|---|---|
| `apps/OmpOS.Native` (new, namespace `OmpOS.Native`, `net10.0`) | L4 | `OmpOS.Presentation` (L2, downward); `Avalonia.Desktop`; `Avalonia.Themes.Fluent`. Ships `Themes/OmpOSTokens.axaml`. | D-20-01, D-20-07. The Avalonia packages are the only UI framework references in the native tree; L4 may depend downward. |
| `src/OmpOS.Presentation` (new, namespace `OmpOS.Presentation`) | L2 | `OmpOS.Contracts` (L0); `OmpOS.GenUi` (L2, same-layer, needs `SameLayerAllowed` entry); `Markdig` 1.4.2 | D-20-02. Must not reference `Avalonia*`, `Microsoft.Maui*` or `Microsoft.AspNetCore.Components*`. Holds `MotionPreference`, plugin host (`Plugins/`), and VMs moved from `OmpOS.UI` (D-20-03). |
| `tests/OmpOS.Native.Tests` (new) | test | `OmpOS.Native`; `OmpOS.Presentation`; `Avalonia.Headless.XUnit` 12.1.4; `Avalonia.Themes.Fluent` 12.1.4; `xunit.v3` (central) | D-20-06. `Directory.Build.props` already makes every `*Tests` project `Exe`, MTP runner, not trimmable. |
| `tests/OmpOS.Presentation.Tests` (new) | test | `OmpOS.Presentation`; `OmpOS.Contracts`; `xunit.v3` | Plain xUnit with no UI framework (D-20-02). Hosts the in-process sample plugin (D-20-05). |
| `src/OmpOS.UI` (changed) | L2 | adds `OmpOS.Presentation` (second same-layer edge) | D-20-03. Needs `("OmpOS.UI", "OmpOS.Presentation")` in `LayerMap.SameLayerAllowed`. `("OmpOS.UI", "OmpOS.GenUi")` already exists. |
| `src/OmpOS.Contracts/Plugins/` (changed, not a new project) | L0 | none new | D-20-05. Must not reference any UI framework assembly. |
| `apps/OmpOS.App` (changed, usings only) | L4 | unchanged (already references `OmpOS.UI`) | D-20-04. `Components/SessionHost.razor` moves to `OmpOS.Presentation` namespaces. Compile verification is blocked on this host (no MAUI workload). |
| `tests/OmpOS.ArchitectureTests` (changed) | test | `ProjectReference` to each new product assembly | D-20-12. `LayerMap.Layers` must gain `OmpOS.Presentation = 2` and `OmpOS.Native = 4`. |

**Wiring not yet in the repo (execution phase):** `LayerMap.Layers` entries for `OmpOS.Presentation` (2) and `OmpOS.Native` (4); `SameLayerAllowed` entries `("OmpOS.Presentation", "OmpOS.GenUi")` and `("OmpOS.UI", "OmpOS.Presentation")`; `OmpOS.slnx` and `OmpOS.Linux.slnf` entries for the new projects, including both test projects.

**Scope note on D-20-12:** `LayerMap.cs` is documented as mapping product assemblies only. Read literally, D-20-12 asks for a `ProjectReference` in `OmpOS.ArchitectureTests` for each new project. The planner should decide whether test projects also get that reference; this research does not decide it.

### Pattern: explicit view mapping (D-20-11)

Map each presentation VM to its Avalonia control in code (for example, a `switch` on the VM type in `OmpOS.Native`). Do not use the default `ViewLocator`; the spike recorded trim-analysis IL2026 under warnings-as-errors. This is a design rule from the locked decisions, not a verified Avalonia API claim.

### Pattern: tokens in one dictionary (D-20-07)

Keep colour, type and spacing tokens in `apps/OmpOS.Native/Themes/OmpOSTokens.axaml`. The contrast test parses that file as XML (see Validation Architecture row 6). Parsing as XML keeps the test free of any Avalonia runtime and free of reflection.

### Anti-patterns to avoid

- **Plain `[Fact]` tests that construct `AvaloniaObject`s in the same assembly as `[AvaloniaFact]` tests**, without `DisableTestParallelization` (issue #22021).
- **`AvaloniaTestIsolationLevel.PerAssembly`**: avoided by D-20-06 because of issue #22021 and the shared-state warning in the headless docs.
- **Reading the default `AutomationProperties` name and assuming it is correct**: the spike measured `"TextBox"` by default; D-20-09 requires explicit names.
- **Using `KeyTextInput` for Tab, Enter or Escape**: `KeyTextInput` is for text; use `KeyPress` with `Key` and `PhysicalKey` for those keys (docs table, verified).

## Don't Hand-Roll

| Problem | Don't build | Use instead | Why |
|---|---|---|---|
| Reduced-motion detection on Linux | P/Invoke or gsettings reader in phase 20 | `MotionPreference` enum (D-20-08), default `System` resolves to `Full` | No Avalonia API exists (IPlatformSettings has no such member); OS readers are a deferred open question |
| Keyboard focus order | Custom focus-walking logic | Default `IsTabStop`, `TabIndex`, visual-tree order; `KeyboardNavigation.TabNavigation` for containers (docs, cited) | Avalonia implements tab navigation; D-20-10 keeps defaults |
| Focus ring | Custom adorner or a second ring | `:focus-visible` style plus `FocusAdorner` set to `null` when a custom indicator replaces the default (docs, cited) | Avoids a duplicate indicator (D-20-10) |
| Accessible names | Deriving names from visual text | `AutomationProperties.SetName` / `GetName` explicitly on every control | Avalonia's accessibility page says Name is the property assistive technology reads; D-20-09 |
| Headless input | Calling `RaiseEvent` or internal input APIs | `Window.KeyPress`, `KeyPressQwerty`, `KeyRelease`, `KeyTextInput` (source-verified) | These raise the events real input raises |
| Test UI thread pumping | `Thread.Sleep` or hand-written loops | `[AvaloniaFact]`, or `HeadlessUnitTestSession.Dispatch`, and `Dispatcher.UIThread.RunJobs()` where needed | Source-verified dispatch paths |
| Contrast computation | HSL lightness or perceptual formulas | WCAG 2.x relative luminance and contrast ratio exactly as defined (see Code Examples) | D-20-07 and SC 1.4.3 define the test |
| Plugin discovery | Reflection scan of assemblies for plugin types | Explicit registration through the presentation host in tests | Keeps contracts UI-free and avoids reflection-based behaviour (repo rule); D-20-05 |

**Key insight:** The headless platform already raises the same events as real input and the test host already owns the UI thread. Hand-rolled pumping or input injection is where the #22021 race and flaky focus tests come from.

## Common Pitfalls

### Pitfall 1: Worker-thread `AvaloniaObject` construction crashes the headless session (issue #22021)
- **What goes wrong:** An `[AvaloniaFact]` session fails with "The calling thread cannot access this object because a different thread owns it", or the process hangs.
- **Why it happens:** `AvaloniaObject`'s `Dispatcher` property calls `Dispatcher.CurrentDispatcher`. The `Dispatcher` constructor sets `s_uiThread ??= this` (first dispatcher wins), and the session reset clears `s_uiThread` before the session thread reclaims it. A plain `[Fact]` constructing a brush in that window can take the UI thread. Source read: `src/Avalonia.Base/Threading/Dispatcher.cs` on master (not the 12.1.4 tag).
- **How to avoid:** Put `[assembly: CollectionBehavior(DisableTestParallelization = true)]` in `OmpOS.Native.Tests` if any plain `[Fact]` there touches Avalonia types; keep `PerTest` isolation.
- **Warning signs:** Intermittent `VerifyAccess` first-chance exceptions; hung test runs with `dotnet-stack` showing xUnit workers in `AvaloniaTestCase.Run`.

### Pitfall 2: Default accessible name is the control type
- **What goes wrong:** The text box is exposed as role "entry" named "TextBox".
- **Why it happens:** Avalonia falls back to a default name when `AutomationProperties.Name` is unset (spike measurement, repo evidence file).
- **How to avoid:** Set the name on every control (D-20-09) and assert it in a headless test (Validation row 4).

### Pitfall 3: Duplicate focus ring
- **What goes wrong:** Two focus indicators appear on one control.
- **Why it happens:** The default `FocusAdorner` remains active while a custom `:focus-visible` style also draws a ring.
- **How to avoid:** Set `FocusAdorner` to `null` on controls that carry a custom indicator (docs, cited). D-20-10.

### Pitfall 4: Trim analysis on the default ViewLocator
- **What goes wrong:** IL2026 promoted to an error under `TreatWarningsAsErrors`.
- **Why it happens:** The default `ViewLocator` is reflection-based. The spike recorded the warning (repo evidence README, cited through ADR-0004).
- **How to avoid:** Explicit view mapping (D-20-11).

### Pitfall 5: Using `PerAssembly` isolation with shared static state
- **What goes wrong:** Tests share styles, resources or static properties and leak state.
- **Why it happens:** The headless docs state that `PerAssembly` reuses one application and dispatcher and that concurrent test execution is not supported.
- **How to avoid:** Keep `[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]` (default and the locked choice).

### Pitfall 6: Contrast computed on gamma-encoded values
- **What goes wrong:** Ratios are wrong for mid-tones.
- **Why it happens:** The luminance formula is applied to 8-bit sRGB values without the linearising step.
- **How to avoid:** Apply the piecewise linearisation with the 0.04045 threshold before weighting (see Code Examples). Use the lighter colour as L1.

### Pitfall 7: `KeyTextInput` does not send Tab, Enter or Escape
- **What goes wrong:** Focus tests never move focus.
- **Why it happens:** `KeyTextInput` raises text input only (docs, cited).
- **How to avoid:** Use `KeyPress(Key, RawInputModifiers, PhysicalKey, string?)` for those keys (source-verified signature).

### Pitfall 8: Focus not set before typing
- **What goes wrong:** Text goes nowhere.
- **Why it happens:** Headless input goes to the focused element, and the window must be shown first (docs example calls `window.Show()` then `textBox.Focus()`).
- **How to avoid:** Show the window, focus the control, then send input.

### Pitfall 9: Linux AT-SPI2 query needs a session bus and accessibility service
- **What goes wrong:** The AT-SPI2 query shows no tree.
- **Why it happens:** Avalonia enables AT-SPI2 automatically only when a D-Bus session bus is available and an accessibility service is running (Linux guide, verified).
- **How to avoid:** Run the query under `dbus-run-session` and `xvfb-run`, as the spike did; record the command and date in the evidence directory (D-20-13).

## Code Examples

Only API names verified in this session are used. Comments give the source file and tag or the doc page.

### Program.cs entry point (native head)

```csharp
// Verified: AppBuilder.Configure<T>() and UsePlatformDetect() (namespace Avalonia, src/Avalonia.Desktop/AppBuilderDesktopExtensions.cs at 12.1.4).
// Docs pattern: docs.avaloniaui.net platform-specific-guides/linux (.UsePlatformDetect()).
public static AppBuilder BuildAvaloniaApp() =>
    AppBuilder.Configure<App>()
        .UsePlatformDetect();
```

### Test assembly wiring

```csharp
// Verified at 12.1.4: tests/Avalonia.Headless.XUnit.PerTest.UnitTests/AssemblyInfo.cs
// (AvaloniaTestApplicationAttribute and AvaloniaTestIsolationAttribute in namespace Avalonia.Headless).
global using Xunit;
global using Avalonia.Headless.XUnit;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]

public static class TestAppBuilder
{
    // Docs pattern (docs.avaloniaui.net testing/headless-xunit): UseHeadless(new AvaloniaHeadlessPlatformOptions()).
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
```

### Keyboard navigation test shape

```csharp
// Verified: Window extension methods KeyPress(this TopLevel, Key, RawInputModifiers, PhysicalKey, string?)
// and KeyTextInput(this TopLevel, string) in namespace Avalonia.Headless (src/Headless/Avalonia.Headless/HeadlessWindowExtensions.cs, 12.1.4).
// Verified: Key.Tab, Key.Enter, Key.Escape (src/Avalonia.Base/Input/Key.cs, master); PhysicalKey.Tab, PhysicalKey.Enter, PhysicalKey.Escape (12.1.4).
// Verified: RawInputModifiers lives in namespace Avalonia.Input.Raw (src/Avalonia.Base/Input/Raw/RawKeyEventArgs.cs, 12.1.4).
// "Shift" as a RawInputModifiers member is NOT verified (see U-04); this example uses default().
[AvaloniaFact]
public void Tab_moves_focus_to_next_shared_control()
{
    var first = new Button { Content = "First" };
    var second = new Button { Content = "Second" };
    var window = new Window { Content = new StackPanel { Children = { first, second } } };
    window.Show();

    first.Focus();
    window.KeyPress(Key.Tab, default, PhysicalKey.Tab, null);

    Assert.True(second.IsFocused);
}
```

Note: `Button.Content`, `StackPanel.Children` and `Window.Content` are standard Avalonia members used by the docs examples; `Focus()` and `IsFocused` appear in the docs focus page (docs, cited).

### Text input (not for Tab, Enter or Escape)

```csharp
// Verified: KeyTextInput(this TopLevel topLevel, string text) (src/Headless/Avalonia.Headless/HeadlessWindowExtensions.cs, 12.1.4).
window.KeyTextInput("Hello");
```

### Dispatch without XUnit integration (D-20-06 fallback)

```csharp
// Verified: HeadlessUnitTestSession.StartNew(Type), HeadlessUnitTestSession.Dispatch(Action, CancellationToken)
// (src/Headless/Avalonia.Headless/HeadlessUnitTestSession.cs, 12.1.4). Docs: headless "Run a headless session" example.
using var session = HeadlessUnitTestSession.StartNew(typeof(App));
await session.Dispatch(() =>
{
    var window = new Window { Content = new TextBox() };
    window.Show();
}, CancellationToken.None);
```

### UI-thread job pump

```csharp
// Verified: Dispatcher.UIThread (static property; the 12.1.4 grep hit is in src/Avalonia.Base/Threading/Dispatcher.cs or Dispatcher.ThreadStorage.cs, exact file not pinned)
// and Dispatcher.RunJobs(DispatcherPriority? priority = null) (src/Avalonia.Base/Threading/Dispatcher.Queue.cs, 12.1.4).
Dispatcher.UIThread.RunJobs();
```

### AutomationProperties name

```csharp
// Verified: class AutomationProperties in namespace Avalonia.Automation (src/Avalonia.Controls/Automation/AutomationProperties.cs, 12.1.4).
// SetName(StyledElement, string?) and GetName(StyledElement) returning string?.
using Avalonia.Automation;

AutomationProperties.SetName(promptInput, "Prompt");
string? accessibleName = AutomationProperties.GetName(promptInput);
```

### Focus visuals in XAML (docs, cited)

```xml
<!-- Docs: docs.avaloniaui.net input-interaction/focus. :focus-visible and FocusAdorner. -->
<Style Selector="Button:focus-visible">
  <Setter Property="FocusAdorner" Value="{x:Null}" />
</Style>
```

### Contrast ratio (WCAG 2.x; formula verified against the W3C text)

```csharp
// Source: W3C WCAG 2.2 glossary as reproduced in the SC 1.4.3 Understanding document.
//   R_sRGB = R8bit / 255; if c <= 0.04045 then c / 12.92 else ((c + 0.055) / 1.055)^2.4
//   L = 0.2126 R + 0.7152 G + 0.0722 B
//   contrast = (L1 + 0.05) / (L2 + 0.05), L1 = lighter, L2 = darker
static double RelativeLuminance(byte r, byte g, byte b)
{
    static double Linear(byte component)
    {
        double c = component / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
    return 0.2126 * Linear(r) + 0.7152 * Linear(g) + 0.0722 * Linear(b);
}

static double ContrastRatio(double luminanceA, double luminanceB)
{
    double lighter = Math.Max(luminanceA, luminanceB);
    double darker = Math.Min(luminanceA, luminanceB);
    return (lighter + 0.05) / (darker + 0.05);
}
```

### Plugin contract assembly check (no reflection serialization)

```csharp
// Uses Assembly.GetReferencedAssemblies() metadata only; no serializer involved.
var refs = typeof(OmpOS.Contracts.Plugins.PluginDescriptor).Assembly
    .GetReferencedAssemblies()
    .Select(a => a.Name ?? string.Empty);
Assert.DoesNotContain(refs, n => n.StartsWith("Avalonia", StringComparison.Ordinal));
```

Note: `OmpOS.Contracts.Plugins.PluginDescriptor` is a name chosen from D-20-05 (plugin descriptor type), not an existing type verified in this session.

## Validation Architecture

Gate command for every row: `./bootstrap.sh`, which builds and tests `OmpOS.Linux.slnf` and runs `dotnet format --verify-no-changes` (20-CONTEXT.md, Verification policy). The per-project narrowing command was not verified, so the planner should use the gate. Every new project must be added to `OmpOS.Linux.slnf` (D-20-12) for its tests to run in this gate.

| # | Success criterion | Test kind | Planned test project and file path | Gate command | Linux-only |
|---|---|---|---|---|---|
| 1 | One Avalonia 12.1.4 native project is in the solution and builds with warnings as errors | Build gate (no test file of its own) | `apps/OmpOS.Native/OmpOS.Native.csproj` (new); added to `OmpOS.slnx` and `OmpOS.Linux.slnf`. Graph rules are covered by existing `ProjectGraphTests`. | `./bootstrap.sh` | No (the project is cross-platform; this phase's gate host is Linux) |
| 2 | Presentation layer holds the VMs and state, with no UI-framework types, and references no Avalonia, MAUI or AspNetCore.Components assembly | Contract test (assembly metadata) | `tests/OmpOS.Presentation.Tests/PresentationIsolationTests.cs`: asserts no referenced assembly starts with `Avalonia`, `Microsoft.Maui` or `Microsoft.AspNetCore.Components`, and no public type in the assembly derives from or is declared in those namespaces | `./bootstrap.sh` | No |
| 3 | Presentation is consumed by both the Avalonia head and the Blazor web head; both build against it and its tests pass | Build gate plus unit tests | `tests/OmpOS.Presentation.Tests/` (VM tests moved from `tests/OmpOS.UI.Tests`, logic only); `tests/OmpOS.Native.Tests/PresentationBindingTests.cs` (native head binds the same VM types); existing `tests/OmpOS.UI.Tests` (bUnit) and `apps/OmpOS.Web` build via `OmpOS.Linux.slnf` (confirmed listed) | `./bootstrap.sh` | No |
| 4 | Every control in the Avalonia head has an explicit AutomationProperties name; prompt input, send control and transcript are exposed through AT-SPI2 on Linux | Headless UI test (names) plus runbook evidence (AT-SPI2 query, outside the gate) | Test: `tests/OmpOS.Native.Tests/AccessibilityNameTests.cs` (`[AvaloniaFact]`; walks the logical tree and asserts a non-empty `AutomationProperties.GetName` on every control and exact names for prompt, send and transcript). Evidence: a dated directory under `docs/runbooks/evidence/` (D-20-13) with the `xvfb-run` + `dbus-run-session` + AT-SPI2 query output. | Test: `./bootstrap.sh`. Evidence: manual run, recorded. | Test: No. AT-SPI2 query: Yes |
| 5 | Shared controls are reachable by keyboard, have a visible focus style, and disable transitions when reduced motion is on | Headless UI test over every shared control | `tests/OmpOS.Native.Tests/KeyboardFocusTests.cs` (Tab and Shift+Tab via `KeyPress`; Enter and Escape on the composer; `:focus-visible` style present on each shared control). `tests/OmpOS.Native.Tests/ReducedMotionTests.cs` (`MotionPreference.Reduced` removes `Transitions` from each shared control; `reduced-motion` class on root). Shift+Tab depends on U-04. | `./bootstrap.sh` | No |
| 6 | Theme tokens live in one resource dictionary; contract test asserts at least 4.5:1 text contrast for every foreground and background pair | Contract test (XML parse of AXAML, exhaustive pairs) | `tests/OmpOS.Native.Tests/ThemeContrastTests.cs`: loads `apps/OmpOS.Native/Themes/OmpOSTokens.axaml` with `System.Xml.Linq`, takes every foreground token and every background token, asserts ratio >= 4.5 for each pair | `./bootstrap.sh` | No |
| 7 | Plugin host contracts define commands, panels, tools and agent modes with no UI reference; an in-process sample plugin registers one of each through the host | Contract test plus in-process registration test | `tests/OmpOS.Presentation.Tests/PluginContractTests.cs`: asserts `OmpOS.Contracts` referenced assemblies contain no UI framework; sample plugin (test code) registers one command, one panel, one tool and one agent mode through the presentation host and the test asserts each is listed | `./bootstrap.sh` | No |

## UNVERIFIED

Each item below is not verified. The proposed open-question row uses a placeholder ID (`OQ-NEW-nn`) to be assigned when inserted into `docs/open-questions.md`. Owner is `ompos-maintainers` and status is `open` for every row. The default taken is what the plan may assume until the question is closed.

| ID placeholder | Item (not verified) | Question | Default taken | Owner | Status |
|---|---|---|---|---|---|
| OQ-NEW-01 (U-01) | Binary compatibility of Avalonia.Headless.XUnit 12.1.4 (compiled against the `xunit.v3.extensibility.core` `[3.2.2, )` range) with xunit.v3 4.0.2 | Does `Avalonia.Headless.XUnit` 12.1.4 compile and run unchanged against xunit.v3 4.0.2? | Yes; keep xunit.v3 4.0.2 (D-20-06). If the build fails in execution, record a blocker; do not silently downgrade. | ompos-maintainers | open |
| OQ-NEW-02 (U-02) | `[AvaloniaFact]` under MTP v2 in this repo. Upstream Avalonia tests use this route at 12.1.4, but nothing was run here. The custom `AvaloniaTestFrameworkAttribute` (xunit `ITestFramework`) under MTP is also unverified; this plan does not use it. | Do `[AvaloniaFact]` tests run under `Microsoft.Testing.Platform` in `OmpOS.Native.Tests`? | Yes. Fallback per D-20-06: `Avalonia.Headless` with plain `[Fact]`, `HeadlessUnitTestSession.StartNew` and `Dispatch`. | ompos-maintainers | open |
| OQ-NEW-03 (U-03) | Issue #22021 (worker-thread `AvaloniaObject` construction races the UI-thread claim). Reporter-run controlled result, not an Avalonia-verified fix status. The fetched page shows no close. | Is issue #22021 fixed in the pinned 12.1.4 package, and does `DisableTestParallelization` remove the hang in this suite? | Set `[assembly: CollectionBehavior(DisableTestParallelization = true)]` in `OmpOS.Native.Tests` if any plain `[Fact]` there touches Avalonia types. | ompos-maintainers | open |
| OQ-NEW-04 (U-04) | `RawInputModifiers.Shift` member name. The namespace `Avalonia.Input.Raw` is verified, but the file declaring the enum was not located in the 12.1.4 tree listing. | Is `RawInputModifiers.Shift` the member name, and does Shift+Tab reverse focus in a headless window? | Use `RawInputModifiers.Shift`; confirm at compile time. | ompos-maintainers | open |
| OQ-NEW-05 (U-05) | `FocusAdorner` property behaviour and `:focus-visible` rendering under the Fluent theme. Cited from the focus docs page only; the property and pseudoclass were not read in source. | Does `:focus-visible` apply in the Fluent theme under headless, and does setting `FocusAdorner` to `null` remove the default ring? | Follow the docs; the headless test asserts the style class is present and `FocusAdorner` is `null` on custom-indicator controls. | ompos-maintainers | open |
| OQ-NEW-06 (U-06) | `StartWithClassicDesktopLifetime` extension name. The type `ClassicDesktopStyleApplicationLifetime` exists at 12.1.4 (`src/Avalonia.Controls/ApplicationLifetimes/`), but the extension method was not read. | Is `StartWithClassicDesktopLifetime(args)` the extension used by the native `Program.cs`? | Use it, the usual template convention; confirm at compile time. | ompos-maintainers | open |
| OQ-NEW-07 (U-07) | OS reduced-motion reader. No such member on `IPlatformSettings` at 12.1.4 (verified absence in that interface only). Issue #19405 is about Windows and open on the fetched page. | Which platform reader resolves `MotionPreference.System` to `Reduced`, and on which OS? | `System` resolves to `Full` on Avalonia (D-20-08). | ompos-maintainers | open |
| OQ-NEW-08 (U-08) | Analyzer interaction: `AnalysisLevel latest-all`, Meziantou.Analyzer 3.0.315, Microsoft.VisualStudio.Threading.Analyzers 18.12.1 and BannedApiAnalyzers 5.6.0 against Avalonia XAML-generated partial classes and the C# source generator for Avalonia properties (PR #21797). A GitHub search returned no issue specific to these analyzers. | Do any of these analyzers fire on generated Avalonia code under `TreatWarningsAsErrors`, and how is each handled? | Fix at source; no blanket suppression. Any suppression requires its own open-question row. | ompos-maintainers | open |
| OQ-NEW-09 (U-09) | Avalonia XAML compiler `AVLN` diagnostics under `TreatWarningsAsErrors`. PR evidence exists for the family (PR #18132 "Warning AVLN2208"; PR #13447 "XAML warnings/diagnostics support"), but the full code list was not read. | Which `AVLN` codes can fire on the native AXAML, and do any fail the build? | Any `AVLN` warning blocks the build. | ompos-maintainers | open |
| OQ-NEW-10 (U-10) | AT-SPI2 names for controls other than TextBox. The spike measured only a TextBox's default name. | Do all native-head controls expose explicit names through AT-SPI2 on this host? | Explicit names on every control (D-20-09); the runbook records the query output. | ompos-maintainers | open |
| OQ-NEW-11 (U-11) | Live AT-SPI2 query and Orca behaviour for the native head on Ubuntu 24.04 (Tier 2). Not run in research. | Does the native head expose prompt input, send control and transcript names through AT-SPI2 on Ubuntu 24.04 under `xvfb-run` and `dbus-run-session`? | Assume yes until the runbook query shows otherwise; record the result either way. | ompos-maintainers | open |

## Sources

Primary, read this session:

- https://www.nuget.org/packages/Avalonia (Avalonia 12.1.4 package and dependencies)
- https://www.nuget.org/packages/Avalonia.Desktop (dependencies for net10.0 and net8.0)
- https://www.nuget.org/packages/Avalonia.Headless (dependencies: Avalonia, Avalonia.Fonts.Inter, Avalonia.HarfBuzz)
- https://www.nuget.org/packages/Avalonia.Headless.XUnit (dependencies: xunit.v3.extensibility.core `[3.2.2, )`, Avalonia.Headless)
- https://www.nuget.org/packages/Avalonia.Themes.Fluent (dependency: Avalonia)
- https://www.nuget.org/packages/xunit.v3 (4.0.2; installs xunit.v3.mtp-v2)
- https://www.nuget.org/packages/xunit.v3.mtp-v2 (4.0.2; pulls xunit.v3.core.mtp-v2 and xunit.analyzers)
- https://www.nuget.org/packages/xunit.v3.extensibility.core (4.0.2)
- https://api.github.com/repos/AvaloniaUI/Avalonia/git/refs/tags/12.1.4 (tag resolves to commit 2d79ba55fae1b1e71c69f93a668080c9029e6a31)
- https://api.github.com/repos/AvaloniaUI/Avalonia/git/trees/12.1.4?recursive=1 (repo tree at 12.1.4; source paths)
- https://api.github.com/repos/xunit/xunit/releases?per_page=12 (xunit release tags, including v3-4.0.2 published 2026-10-09)
- https://api.github.com/repos/xunit/xunit/releases/tags/v3-4.0.0 (release exists; body was empty, so no breaking-change notes were read)
- https://api.github.com/search/issues?q=repo:AvaloniaUI/Avalonia+... (issue and PR searches for analyzer, AVLN, focus-visible, Meziantou; titles only)
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Headless/Avalonia.Headless/HeadlessWindowExtensions.cs
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Headless/Avalonia.Headless/HeadlessUnitTestSession.cs
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Headless/Avalonia.Headless/AvaloniaTestApplicationAttribute.cs
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Headless/Avalonia.Headless/HeadlessUnitTestIsolationAttribute.cs
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Headless/Avalonia.Headless.XUnit/AvaloniaFact.cs
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Headless/Avalonia.Headless.XUnit/AvaloniaTestFramework.cs
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Headless/Avalonia.Headless.XUnit/AvaloniaTestFrameworkAttribute.cs
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Headless/Avalonia.Headless.XUnit/Avalonia.Headless.XUnit.csproj
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Avalonia.Base/Threading/Dispatcher.Queue.cs (RunJobs)
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Avalonia.Base/Threading/Dispatcher.MainLoop.cs
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/src/Avalonia.Base/Threading/Dispatcher.cs (master, not 12.1.4; constructor `s_uiThread ??= this` and `UIThread` property)
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Avalonia.Controls/Automation/AutomationProperties.cs (path shown as src/Avalonia.Controls/Automation/AutomationProperties.cs in the 12.1.4 tree)
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Avalonia.Desktop/AppBuilderDesktopExtensions.cs
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Avalonia.Base/Platform/IPlatformSettings.cs
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Avalonia.Base/Input/PhysicalKey.cs (Tab = 64, Enter = 58, Escape = 106)
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/src/Avalonia.Base/Input/Key.cs (master; Tab = 3, Return = 6, Enter = 6, Escape = 13)
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Avalonia.Base/Input/Raw/RawKeyEventArgs.cs (RawInputModifiers in namespace Avalonia.Input.Raw)
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Avalonia.Base/Input/Raw/RawInputHelpers.cs
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/build/XUnit.props (adds xunit.v3.mtp-v2 to Avalonia's test projects)
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/build/UnitTests.NetCore.targets
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/build/DevAnalyzers.props (Avalonia's own analyzer props; master)
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/tests/Avalonia.Headless.XUnit.PerTest.UnitTests/Avalonia.Headless.XUnit.PerTest.UnitTests.csproj (Exe, IsTestProject, XUnit.props import)
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/tests/Avalonia.Headless.XUnit.PerTest.UnitTests/AssemblyInfo.cs
- https://docs.avaloniaui.net/docs/supported-platforms.md (Linux rows: Ubuntu 25.x Tier 1; Ubuntu 16.04 to 24.x Tier 2)
- https://docs.avaloniaui.net/docs/platform-specific-guides/linux (UsePlatformDetect with X11PlatformOptions; AT-SPI2 enabled when D-Bus session bus and accessibility service are present)
- https://docs.avaloniaui.net/docs/testing/headless-xunit (AvaloniaFact, AvaloniaTheory, AvaloniaTestApplication, AvaloniaTestIsolation, UseHeadless pattern)
- https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform (Window.KeyPress, KeyPressQwerty, KeyRelease, KeyTextInput; HeadlessUnitTestSession example)
- https://docs.avaloniaui.net/docs/app-development/accessibility.md (AutomationProperties.Name is the most important accessibility property; platform support table)
- https://docs.avaloniaui.net/docs/input-interaction/focus.md (IsFocused, Focusable, :focus-visible, FocusAdorner, IsTabStop, KeyboardNavigation.TabNavigation)
- https://xunit.net/docs/getting-started/v3/microsoft-testing-platform (v2 default for 4.0.0 and later; UseMicrosoftTestingPlatformRunner)
- https://github.com/AvaloniaUI/Avalonia/issues/22021 (headless VerifyAccess race; controlled results)
- https://github.com/AvaloniaUI/Avalonia/issues/19405 (Respect system animation settings; open on the fetched page)
- https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html (SC 1.4.3: 4.5:1 normal text, 3:1 large text; glossary definitions of relative luminance and contrast ratio, including RsRGB = R8bit/255, threshold 0.04045, and the note on 0.03928)
- https://www.w3.org/TR/WCAG22/#dfn-relative-luminance (anchor cited; the fetch tool returned only the abstract, so the definition text is taken from the Understanding document above)
- https://www.w3.org/TR/WCAG22/#dfn-contrast-ratio (anchor cited; same note as above)

Repo files read this session:

- `.planning/phases/20-avalonia-foundation/20-CONTEXT.md` (locked decisions D-20-01 to D-20-14)
- `docs/decisions/ADR-0004-cross-platform-ui.md`
- `Directory.Packages.props` (xunit.v3 4.0.2; Meziantou.Analyzer 3.0.315; Microsoft.VisualStudio.Threading.Analyzers 18.12.1; Microsoft.CodeAnalysis.BannedApiAnalyzers 5.6.0; Microsoft.Testing.Platform 2.5.1; Markdig 1.4.2)
- `Directory.Build.props` (net10.0; TreatWarningsAsErrors; AnalysisLevel latest-all; test projects get Exe and UseMicrosoftTestingPlatformRunner)
- `tests/OmpOS.ArchitectureTests/LayerMap.cs` (layers; SameLayerAllowed)
- `OmpOS.Linux.slnf` (projects included in the Linux gate)

Attempted and not usable: `https://api-docs.avaloniaui.net/...` (Docusaurus shell only, no member text); `https://docs.avaloniaui.net/docs/concepts/input/focus` (moved to `docs/input-interaction/focus`); `https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Headless/...` paths guessed under `src/Avalonia.Headless` (404; correct path is `src/Headless/Avalonia.Headless`).
