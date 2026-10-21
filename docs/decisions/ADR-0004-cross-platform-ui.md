# ADR-0004: Cross-platform UI: Avalonia for native heads, Blazor WebAssembly for the web head

- Status: Accepted 2026-10-21, amended (web stays Blazor WebAssembly)
- Date: 2026-10-21
- Deciders: ompos-maintainers
- Scope: UI stack for Windows, macOS, Linux, iOS, Android and the web head. Supersedes the UI stack decisions in ADR-0002.

## Context

The request was to evaluate moving the cross-platform UI from .NET MAUI to Avalonia UI for Windows, macOS, Linux, iOS, Android and WebAssembly. The UI is the foundation for a Cursor-class agentic development environment (ADE).

Verified constraints, from primary sources on 2026-10-21:

- .NET MAUI 10 supports Android, iOS, Mac Catalyst and Windows. Tizen is provided by Samsung. It has no Linux target and no browser target. Source: Microsoft Learn, supported platforms for .NET MAUI, net-maui-10.0.
- Avalonia 12.1.4 (NuGet, published 2026-10-09, MIT) supports Windows, macOS, Linux (X11 by default, native Wayland opt-in from 12.1.0), iOS, Android and WebAssembly. Support tiers: Windows 11 24H2, macOS 26, iOS and iPadOS 26 and Android 16 are Tier 1. Ubuntu 25.x, Fedora 43 and Debian 13 are Tier 1, and Ubuntu 16.04 to 24.x is Tier 2. Source: docs.avaloniaui.net/docs/supported-platforms.
- Avalonia.Browser 12.1.4 declares the net10.0-browser1.0 target. Source: NuGet.

Host facts for the spike: Ubuntu 24.04.4 LTS (a Tier 2 Linux distribution), .NET SDK 10.0.402, no Android SDK installed.

## Spike, 2026-10-21

Throwaway projects were generated from Avalonia.Templates 12.1.4 (`avalonia.xplat`) outside the repository. Nothing from the spike is committed as product code. Evidence is in `docs/runbooks/evidence/avalonia-spike-2026-10-21/`.

| Check | Result |
|---|---|
| Desktop head builds with warnings as errors | Pass: 2 projects, 0 errors, 0 warnings |
| Browser head publishes with warnings as errors | Pass after installing the wasm-tools workload (10.0.13 packs). Without it the publish fails: the native file references are not linked, and trim analysis IL2026 is promoted to an error |
| Linux desktop AT-SPI2 accessible name | The text box is exposed as role "entry" named "TextBox" by default. With AutomationProperties.Name="Greeting" it is exposed as "Greeting" |
| G-WEB: the browser build exposes role and accessible name to Chromium | Fail, in two builds. Stock build and named-control build both show RootWebArea, generic, Canvas and generic nodes, with no textbox role and no name. roleNodes is 0. avalonia.js contains no ARIA code |
| Browser payload | Publish output is 33.8 MB on disk across 153 files, including precompressed .gz and .br copies. dotnet.native.wasm is 8.9 MB raw and 3.2 MB gzip-9 |

## Decision

1. Avalonia UI 12.1.4 (MIT) is the UI framework for the five native heads: Windows, macOS, Linux, iOS and Android.
2. The web head stays Blazor WebAssembly. Avalonia.Browser is adopted only when gate G-WEB passes: the browser build must expose role and accessible name for the prompt input, the send control and the transcript to the Chromium accessibility tree. G-WEB failed on Avalonia 12.1.4. It is re-measured on each Avalonia release with the procedure in the evidence README.
3. A framework-neutral presentation layer is extracted from the Razor components: view models and state with no UI-framework types. The Avalonia heads and the Blazor web head bind the same presentation state, and parity tests run against that layer. This bounds drift between the two view layers.
4. The .NET MAUI head (`apps/OmpOS.App`) retires by a clean cutover after parity tests exist (XP-10). The Razor component library (`src/OmpOS.UI`) stays as the web head's UI until the web head moves. No new MAUI-only or Razor-only feature code is written.
5. ADE capabilities are delivered through a plugin harness: UI-framework-neutral contracts in the contracts layer, in-process plugins first, MCP for out-of-process tools later.
6. iOS and macOS signing, notarization and device runs stay on the Mac channel and remain human-gated.

## Alternatives considered

- Keep MAUI and the Razor library, and add Linux through a third host. Rejected. MAUI has no Linux or browser target on .NET 10, and this leaves three UI stacks.
- Avalonia for all six targets, the original proposal. Rejected by the G-WEB measurement: no controls reach the browser accessibility tree on 12.1.4.
- Avalonia native heads plus a custom ARIA mirror over the canvas for the web. Not chosen for this milestone. It is new work with no verified behaviour for keyboard use or screen readers. Revisit if Avalonia.Browser still has no DOM accessibility on later releases.
- Avalonia native heads plus Blazor WebAssembly for the web, with a shared presentation layer. Chosen (decisions 1 to 4). The cost is two view layers over one presentation layer.

## Consequences

- Two UI stacks remain: Avalonia for the five native heads, and Blazor WebAssembly for the web head. Drift is bounded by the shared presentation layer and by parity tests.
- The web head keeps its verified accessibility route (axe) and CSP smoke route, and keeps Vercel static hosting.
- Linux CI for the Avalonia head should target a Tier 1 distribution (Ubuntu 25.x, Fedora 43 or Debian 13). The spike ran on Ubuntu 24.04, which is Tier 2.
- Windows, macOS, iOS and Android builds were not measured by this spike. Each has a verification route in `docs/plans/ade-harness-plan.md`.
- Milestones are v0.10.0 (Phases 20 to 25) and v0.11.0 (Phases 26 to 30) in `.planning/ROADMAP.md`. MAUI Phases 15 and 16 of v0.9.2 are superseded by v0.10.0 Phases 22 and 24.
- Reversal: if G-WEB passes on a later Avalonia release, decision 2 is reopened as a web head migration. Decisions 1 and 3 do not change.

## Evidence

- `docs/runbooks/evidence/avalonia-spike-2026-10-21/README.md`: versions, procedure, results and limitations.
- Microsoft Learn, supported platforms for .NET MAUI: https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms?view=net-maui-10.0
- Avalonia supported platforms: https://docs.avaloniaui.net/docs/supported-platforms
- Avalonia.Browser on NuGet: https://www.nuget.org/packages/Avalonia.Browser/
