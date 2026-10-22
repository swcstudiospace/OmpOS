# 3D anime-style pet (VRM) feasibility per OmpOS head

Research date: 2026-10-22 (all sources below were opened on this date unless marked otherwise).
Scope: rendering path, licences and sample assets only. No code, builds, tests or runs were performed.
Verdict definitions used below:
- feasible-verified: every required API, package or platform mechanism was confirmed from a primary source (vendor docs, official repo source, NuGet/npm registry metadata). Runtime rendering was NOT executed.
- feasible-unverified: a plausible path exists but a required link was not confirmed from a primary source; the reason is stated.
- not-feasible: a primary source or primary source code shows the mechanism is absent.

## Verdict

A rigged, animated VRM avatar is feasible on the Blazor WebAssembly head at API level: Blazor calls JavaScript through IJSRuntime and JS modules (Microsoft Learn), and three.js (MIT) with @pixiv/three-vrm (MIT, 3.5.5, VRM loader plugin) covers VRM 1.0 and 0.x loading. On the Avalonia desktop heads (Windows, macOS, Linux) the GPU path is feasible at API level through OpenGlControlBase and the per-platform GL/Vulkan/Metal backends, but no .NET library was found that renders VRM 1.0 with MToon and spring bones; a .NET desktop pet would require a custom glTF/MToon renderer on top of SharpGLTF (MIT) or Prowl.Clay (MIT, VRM capability unverified). Avalonia Android is feasible-unverified (EGL and Vulkan modes exist, but the OpenGlControlBase compositor path on Android was not confirmed). Avalonia iOS is feasible-unverified (Metal and EAGL modes exist; the iOS Metal source checked shows no GPU external-objects feature, and building needs macOS). Avalonia in the browser is not-feasible through OpenGlControlBase because Avalonia.Browser exposes no GPU interop feature; this does not affect the Blazor head. Asset licence: the two VRM samples found (Seed-san by VirtualCast, VRM1_Constraint_Twist_Sample by pixiv) are both under VRM Public License 1.0 with redistribution and commercial (corporation) use permitted; Seed-san requires credit display. The VRM specification repository has no LICENSE file at its root and no licence in its repository metadata, so the spec text licence is unverified. A 2D sprite fallback is feasible on every head with no licence dependency beyond the art itself; Live2D Cubism is an alternative with licence conditions for business entities.

## Table

### A. Per-head matrix

| Head | Rendering path (what was verified) | Verdict | Reason / caveat |
|---|---|---|---|
| Avalonia desktop, Windows | OpenGlControlBase lifecycle (OnOpenGlInit/OnOpenGlRender/OnOpenGlDeinit, RequestNextFrameRendering). Win32PlatformOptions RenderingMode: AngleEgl (default), Wgl, Vulkan, Software. | feasible-verified (API level) | Runtime not executed. Software fallback (remote desktop, VMs) cannot run GL; a 2D fallback is required for those sessions. |
| Avalonia desktop, macOS | OpenGlControlBase. AvaloniaNativePlatformOptions RenderingMode includes Metal and OpenGl. macOS 26 is Tier 1; macOS 14/15 Tier 2. | feasible-verified (API level) | Runtime not executed. Long-term status of the OpenGl mode on macOS not checked. |
| Avalonia desktop, Linux (X11) | OpenGlControlBase. X11PlatformOptions RenderingMode: Glx (default), Egl, Vulkan, Software. | feasible-verified (API level) | Runtime not executed. Software fallback on headless or VM sessions. |
| Avalonia desktop, Linux (Wayland) | Native Wayland backend (opt-in from 12.1.0) renders through EGL per supported-platforms page. | feasible-verified (API level) | Wayland default is XWayland; native Wayland is opt-in. |
| Avalonia Android | AndroidPlatformOptions RenderingMode: Egl (default), Vulkan, Software. Shared Avalonia.OpenGL/Egl contains an EGL external-objects feature. | feasible-unverified | OpenGlControlBase depends on Compositor.TryCreateCompatibleGlContextAsync returning a non-null context; the Android compositor interop path was not confirmed in source. Android 16 is Tier 1; Android 12 to 15 Tier 2. NuGet describes Android, iOS and WebAssembly as experimental. |
| Avalonia iOS | iOSPlatformOptions RenderingMode: Metal (default), OpenGl (EAGL; not on Mac Catalyst). EaglDisplay.cs has CanCreateSharedContext => true. | feasible-unverified | iOS Metal platform files checked (MetalPlatformGraphics.cs, MetalPlatformSurface.cs) do not show an external-objects feature; EAGL external-objects not confirmed; OpenGlControlBase on iOS therefore unconfirmed. Build and test needs macOS and Xcode. iOS 26 Tier 1; iOS 18.x Tier 2. |
| Avalonia WebAssembly (Avalonia.Browser), not the Blazor head | BrowserWebGlRenderTarget / WebGlContext: CanCreateSharedContext => false; WebGlContext.TryGetFeature returns null; BrowserPlatformGraphics exposes only a ready-state feature; no ICompositionGpuInterop or external-objects implementation found in the 40 Avalonia.Browser .cs files checked. | not-feasible (OpenGlControlBase) | Source-inferred: TryGetCompositionGpuInterop would return null and OpenGlControlBase would log that the compositor lacks GPU interop. Runtime not executed. Alternative (not verified): BrowserNativeControlHost hosting a canvas driven by three.js. Out of scope for the Blazor head. |
| Blazor WebAssembly (browser) | IJSRuntime.InvokeAsync and JavaScript modules (Microsoft Learn, aspnetcore-10.0). three.js WebGLRenderer in a canvas. @pixiv/three-vrm loads VRM files via a GLTFLoader plugin. | feasible-verified (API level) | Runtime not executed. Not checked: which three.js version @pixiv/three-vrm 3.5.5 requires as a peer, and which browsers provide WebGL2 (three.js relies on WebGL, WebGPU optional). |
| .NET VRM parsing and rendering in any desktop head (no existing .NET MToon renderer found) | SharpGLTF.Core 1.0.7 (MIT) reads glTF2/GLB. VRMC_vrm extension must be read by custom code. Prowl.Clay 3.9.0 (MIT) NuGet description says it loads glTF2/GLB/VRM. UniVRM (MIT) is Unity-only. | feasible-unverified | No primary-source proof that SharpGLTF exposes VRMC extensions, skins and MToon data, or that Prowl.Clay handles MToon and spring bones. Implementing MToon, spring bones, expressions and lookAt in a .NET renderer is a large custom effort. |

### B. Primary-source facts

| # | Claim | Source URL | Verified (yes/no) | Read date |
|---|---|---|---|---|
| 1 | Avalonia is MIT licensed; repository page licence line reads MIT. | https://github.com/AvaloniaUI/Avalonia | yes | 2026-10-22 |
| 2 | Avalonia 12.1.4 released 2026-10-09 (NuGet); NuGet description: experimental support for Android, iOS and WebAssembly. | https://www.nuget.org/packages/Avalonia | yes | 2026-10-22 |
| 3 | Avalonia 12.x release notes list 12.1.4 (2026-10-09) and 12.1.0 (2026-07-10). | https://github.com/AvaloniaUI/Avalonia/releases | yes | 2026-10-22 |
| 4 | Avalonia 12 supports only .NET 8 and later; recommended .NET 10; Android and iOS only .NET 10. | https://docs.avaloniaui.net/docs/avalonia12-breaking-changes | yes | 2026-10-22 |
| 5 | OpenGlControlBase is abstract, in Avalonia.OpenGL, and its lifecycle methods are OnOpenGlInit, OnOpenGlRender(GlInterface, int fb) and OnOpenGlDeinit; GL calls are only allowed inside those overrides. | https://docs.avaloniaui.net/api/avalonia/opengl/controls/openglcontrolbase | yes | 2026-10-22 |
| 6 | OpenGlControlBase source: EnsureInitialized/InitializeAsync calls Compositor.TryCreateCompatibleGlContextAsync; if null it logs "current platform doesn't support OpenGL interop with the compositor" and returns false. RequestNextFrameRendering queues a composition update. | https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/src/Avalonia.OpenGL/Controls/OpenGlControlBase.cs | yes | 2026-10-22 |
| 7 | OpenGlCompositionInterop.TryCreateCompatibleGlContextAsync returns null when the backend has no GPU interop (the log says "Expected on platforms without GPU interop support"). Compositor.TryGetCompositionGpuInterop depends on the IExternalObjectsRenderInterfaceContextFeature. | https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/src/Avalonia.OpenGL/Composition/OpenGlCompositionInterop.cs and https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/src/Avalonia.Base/Rendering/Composition/Compositor.cs | yes | 2026-10-22 |
| 8 | Custom rendering docs describe GPU interop for 3D engines via compositor.ImportGpuImage, ImportGpuSemaphore, UpdateWithKeyedMutexAsync and UpdateWithTimelineSemaphoresAsync. Docs example uses Avalonia.Skia 11.2 packages. | https://docs.avaloniaui.net/docs/graphics-animation/custom-rendering | yes | 2026-10-22 |
| 9 | Custom rendering docs: RenderTargetBitmap uses software rendering; controls relying on OpenGlControlBase may not render correctly when captured that way. | https://docs.avaloniaui.net/docs/graphics-animation/custom-rendering | yes | 2026-10-22 |
| 10 | Windows: RenderingMode AngleEgl (default), Wgl, Vulkan, Software; ANGLE translates to Direct3D 11. | https://docs.avaloniaui.net/docs/platform-specific-guides/windows.md | yes | 2026-10-22 |
| 11 | macOS: Avalonia ships its own Objective-C++ backend (libAvaloniaNative.dylib); the guide's example RenderingMode lists Metal, OpenGl; Metal and OpenGL rendering are named as provided. | https://docs.avaloniaui.net/docs/platform-specific-guides/macos.md | yes | 2026-10-22 |
| 12 | Linux: RenderingMode Glx (default), Egl, Vulkan, Software; Wayland native backend opt-in since 12.1.0. | https://docs.avaloniaui.net/docs/platform-specific-guides/linux.md | yes | 2026-10-22 |
| 13 | Android: RenderingMode Egl (default), Vulkan, Software; RenderingMode must include at least one mode. | https://docs.avaloniaui.net/docs/platform-specific-guides/android/ | yes | 2026-10-22 |
| 14 | iOS: RenderingMode Metal (default), OpenGl (EAGL on iOS/tvOS, not on Mac Catalyst). Building and simulator need macOS. | https://docs.avaloniaui.net/docs/platform-specific-guides/ios.md | yes | 2026-10-22 |
| 15 | iOS source: EaglDisplay.cs contains CanCreateSharedContext => true. | https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/src/iOS/Avalonia.iOS/Eagl/EaglDisplay.cs (directory listing via GitHub API) | yes (partial file check) | 2026-10-22 |
| 16 | Avalonia.Metal contains IMetalExternalObjectsFeature; Avalonia.Vulkan contains VulkanExternalObjectsFeature; Avalonia.OpenGL/Egl contains EglExternalObjectsFeature. iOS Metal platform files checked show no such feature. | https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/src/Avalonia.Metal/IMetalExternalObjectsFeature.cs ; https://github.com/AvaloniaUI/Avalonia (src/Avalonia.Vulkan, src/Avalonia.OpenGL/Egl listings via GitHub API) ; https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/src/iOS/Avalonia.iOS/Metal/MetalPlatformGraphics.cs | yes (partial; GitHub API rate-limited mid-run) | 2026-10-22 |
| 17 | Avalonia.Browser: WebGlContext.CanCreateSharedContext => false; WebGlContext.TryGetFeature returns null; BrowserPlatformGraphics.TryGetFeature exposes only IPlatformGraphicsReadyStateFeature; no external-objects or GPU-interop feature found in the Avalonia.Browser sources checked. | https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/src/Browser/Avalonia.Browser/Rendering/BrowserWebGlRenderTarget.cs ; .../Rendering/RenderTargetBrowserSurface.cs ; .../Rendering/* and root .cs files | yes (all 8 Rendering files and root .cs files checked by grep) | 2026-10-22 |
| 18 | Avalonia WebAssembly: RenderingMode WebGL2 (default), WebGL1, Software2D; JSImport/JSExport interop documented for Avalonia browser apps. | https://docs.avaloniaui.net/docs/platform-specific-guides/webassembly.md | yes | 2026-10-22 |
| 19 | Blazor: call JavaScript from .NET with IJSRuntime.InvokeAsync; JS isolation via JavaScript modules and IJSObjectReference. | https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/call-javascript-from-dotnet?view=aspnetcore-10.0 | yes (page opened; body partly truncated, module section located) | 2026-10-22 |
| 20 | three.js: MIT licence; npm latest 0.186.1. | https://github.com/mrdoob/three.js ; https://www.npmjs.com/package/three | yes | 2026-10-22 |
| 21 | @pixiv/three-vrm: MIT licence; npm latest 3.5.5; VRM file loader for three.js; depends on three-vrm-core, springbone, mtoon, node-constraint and v0compat sub-packages. | https://www.npmjs.com/package/@pixiv/three-vrm ; https://github.com/pixiv/three-vrm | yes | 2026-10-22 |
| 22 | three-vrm README: VRMLoaderPlugin registered on a GLTFLoader; WebGPU compatibility via MToonNodeMaterial (three.js r167 or later). | https://github.com/pixiv/three-vrm | yes | 2026-10-22 |
| 23 | VRM 1.0 specification (VRMC_vrm) status "Complete"; written against glTF 2.0; humanoid, meta, firstPerson, expressions, lookAt; extensions used: springBone, MToon, node constraint. | https://raw.githubusercontent.com/vrm-c/vrm-specification/master/specification/VRMC_vrm-1.0/README.md | yes | 2026-10-22 |
| 24 | VRM 1.0 meta licence: meta.licenseUrl must be https://vrm.dev/licenses/1.0/ (the only accepted value in VRM 1.0). Meta fields include avatarPermission, commercialUsage (personalNonProfit, personalProfit, corporation), allowRedistribution, modification, creditNotation. | https://raw.githubusercontent.com/vrm-c/vrm-specification/master/specification/VRMC_vrm-1.0/meta.md | yes | 2026-10-22 |
| 25 | VRM Public License 1.0 (English): worldwide, royalty-free, non-exclusive, irrevocable licence subject to License Settings; redistribution, avatar use and modified-data distribution are licensed rights; terms terminate on breach and reinstate after 30-day cure. Version 1.0 established 2021-08-05. | https://vrm.dev/en/licenses/1.0/index | yes | 2026-10-22 |
| 26 | VRM Public License 1.0 (Japanese) page exists at https://vrm.dev/licenses/1.0/ and matches the English terms on the points checked. | https://vrm.dev/licenses/1.0/ | yes (Japanese text; points compared only at summary level) | 2026-10-22 |
| 27 | vrm-c/vrm-specification repository: GitHub repository metadata shows no licence field (unlike three.js and UniVRM, which show MIT); LICENSE and LICENSE.md return HTTP 404 on master root. | https://github.com/vrm-c/vrm-specification ; https://raw.githubusercontent.com/vrm-c/vrm-specification/master/LICENSE (404) ; .../LICENSE.md (404) | yes | 2026-10-22 |
| 28 | UniVRM (Unity implementation of VRM and UniGLTF) licence MIT; supports Unity 2022.3 LTS or later per the search summary; distribution is Unity package only (not NuGet). | https://github.com/vrm-c/UniVRM ; https://github.com/vrm-c/UniVRM/tree/master/Packages/UniGLTF | yes (licence and Unity-only distribution from README and NuGet search; Unity version from search summary, not opened) | 2026-10-22 |
| 29 | SharpGLTF.Core 1.0.7: MIT licence; authors Vicente Penades; published 2026-09-12; depends on System.Text.Json; .NETStandard 2.0 and 2.1 targets. | https://www.nuget.org/packages/SharpGLTF.Core | yes | 2026-10-22 |
| 30 | Prowl.Clay 3.9.0: MIT licence; description: loads glTF2 / GLB / VRM, OBJ and FBX; depends on Prowl.Vector; published 2026-10-22. | https://www.nuget.org/packages/Prowl.Clay | yes (metadata only; VRM capabilities not confirmed) | 2026-10-22 |
| 31 | Silk.NET.OpenGL 2.23.0: MIT licence; published 2026-01-23; .NET Standard 2.0, 2.1 and .NETCoreApp 3.1 targets; provides OpenGL and Vulkan bindings. | https://www.nuget.org/packages/Silk.NET.OpenGL | yes (NuGet page; GitHub repo page URL returned 404) | 2026-10-22 |
| 32 | Live2D Cubism SDK Release License: business entities publishing content with the SDK need a licence; individuals and small-scale enterprises are exempt (except Expandable Application); plans A to G, with contact for other cases. | https://www.live2d.com/en/sdk/license/ | yes (plan detail pages not opened) | 2026-10-22 |
| 33 | Seed-san sample: VRM 1.0 (specVersion 1.0); licenseUrl https://vrm.dev/licenses/1.0/; authors VirtualCast, Inc.; avatarPermission everyone; commercialUsage corporation; allowRedistribution true; modification allowModificationRedistribution; creditNotation required; allowExcessivelyViolentUsage true; allowExcessivelySexualUsage true; allowAntisocialOrHateUsage true; allowPoliticalOrReligiousUsage true; 51 humanoid bones; 17 materials; extensionsUsed includes VRMC_springBone, VRMC_materials_mtoon, VRMC_node_constraint. | https://github.com/vrm-c/vrm-specification/blob/master/samples/Seed-san/README.md ; https://raw.githubusercontent.com/vrm-c/vrm-specification/master/samples/Seed-san/vrm/Seed-san.vrm (downloaded and meta parsed) | yes | 2026-10-22 |
| 34 | VRM1_Constraint_Twist_Sample: VRM 1.0; licenseUrl https://vrm.dev/licenses/1.0/; authors pixiv Inc.; avatarPermission everyone; commercialUsage corporation; allowRedistribution true; modification allowModificationRedistribution; creditNotation unnecessary; allowAntisocialOrHateUsage false; 54 humanoid bones; 13 materials; MToon and spring bones. | https://raw.githubusercontent.com/vrm-c/vrm-specification/master/samples/VRM1_Constraint_Twist_Sample/vrm/VRM1_Constraint_Twist_Sample.vrm (downloaded and meta parsed) ; README at samples/VRM1_Constraint_Twist_Sample/README.md | yes | 2026-10-22 |
| 35 | VRM samples index: Seed-san and VRM1_Constraint_Twist_Sample listed as practical avatars; other entries are feature tests. | https://raw.githubusercontent.com/vrm-c/vrm-specification/master/samples/README.md | yes | 2026-10-22 |

### C. Asset licence findings

| Asset | Licence | Permissions (from file meta, which is the licence settings) | Obligations | Style check | Use for OmpOS pet |
|---|---|---|---|---|---|
| Seed-san (VirtualCast, Inc.) | VRM Public License 1.0 (licenseUrl https://vrm.dev/licenses/1.0/) | avatarPermission everyone; commercialUsage corporation; allowRedistribution true; modification and redistribution allowed | creditNotation required: credit must be displayed | Repo describes it as a practical sample avatar with PBR, MToon, spring bones and expressions; anime look not visually confirmed (screenshot could not be opened in this run) | Candidate. Licence fits a shipped desktop or web app if credit is shown. |
| VRM1_Constraint_Twist_Sample (pixiv Inc.) | VRM Public License 1.0 | Same permissions as Seed-san; creditNotation unnecessary | none beyond licence text | Feature-test oriented sample; anime look not visually confirmed | Candidate for testing; less suitable as a product asset. |
| VRM spec text and schemas (vrm-c/vrm-specification) | No LICENSE file at root; no repository licence field | Unknown | Unknown | not applicable | Do not copy spec text or schemas into the product until the owner obtains a licence statement. Implementation based on the public spec is not blocked by this finding but should be confirmed with counsel. |
| VRM Public License 1.0 itself | Governs model files, not the spec repo (per vrm.dev terms) | Model meta.licenseUrl must point to the VRM PL 1.0 URL | If the pet ships a VRM file, its licence settings must be kept intact | not applicable | Applies to any VRM file we ship. |
| Libraries: Avalonia, three.js, @pixiv/three-vrm, pixiv/three-vrm repo, SharpGLTF, Silk.NET, UniVRM | MIT | none | Keep MIT copyright and licence notices in shipped notices | not applicable | No licence cost. |
| Live2D Cubism SDK (2D alternative) | Live2D SDK Release License | Individuals and small-scale enterprises exempt; business entities need a licence depending on plan | Contact or purchase plan per content type | not applicable | Budget item if this fallback is chosen. |

### D. 2D sprite fallback

- Avalonia: Image or custom controls; DrawingContext.DrawImage(bitmap, sourceRect, destRect) is documented (https://docs.avaloniaui.net/docs/graphics-animation/custom-rendering, read 2026-10-22). Works without GPU interop, so it is available on Software-only Linux and Windows sessions and on Android and iOS.
- Blazor: HTML img or canvas with CSS animation or JS; no GPU requirement.
- Licence: none beyond the art itself; art must be commissioned or licensed separately (not researched).
- Verdict: feasible-verified on all heads at API level. This is the recommended fallback for every head where 3D is not-feasible or unverified.

## Owner decisions

1. Rendering approach. Options:
   a. Web-only 3D pet in the Blazor head with three.js and @pixiv/three-vrm (MIT); desktop shows the same pet through an embedded web view or a sprite fallback.
   b. Native 3D pet in Avalonia desktop: custom glTF/VRM/MToon renderer on OpenGlControlBase, with SharpGLTF or Prowl.Clay for parsing. No existing .NET MToon renderer was found; this is the largest effort.
   c. 2D sprite pet on all heads (no 3D dependency).
   d. Hybrid: 3D in Blazor, sprite on Avalonia desktop and mobile.
2. Mobile scope. Options: defer Android and iOS pets; sprite-only on mobile; or pursue a native-view experiment for iOS and Android (requires a macOS build machine for iOS).
3. Asset source. Options: use Seed-san (VRM PL 1.0, credit required); use VRM1_Constraint_Twist_Sample for testing only; commission a custom anime VRM and set its meta.licenseUrl to the VRM PL 1.0 URL with chosen settings; or use other VRoid or VRM sample models (each needs its own licence check; none checked here).
4. Licence budget. Runtime libraries are MIT at zero cost. Options: free path (MIT libraries, Seed-san with credit), commissioned custom avatar (cost not researched), or Live2D Cubism for a 2D rig (business licence depends on plan; contact Live2D).
5. Spec licence. Options: accept risk and treat the public spec as implementation reference only; ask vrm-c/VRM Consortium for a written licence statement for the spec repository; or ask counsel to decide.
6. Feature depth. Options: minimum (load model, idle animation, blink and lookAt via expressions) or full (spring bones, constraints, expressions, click and voice reactions). Feature depth drives the .NET renderer effort in option 1b.
7. Credit display. Options: add a credits panel showing "Seed-san model by VirtualCast, Inc." and the VRM PL 1.0 link; or choose an asset with creditNotation unnecessary.

## Unverified

- Runtime behaviour of any head: no builds, tests or runs were executed (assignment rule). Every "feasible-verified" verdict is API-level only.
- OpenGlControlBase on Android and iOS: the compositor GPU-interop path was not confirmed; GitHub API rate limits prevented a complete scan of the iOS Metal and Android folders.
- macOS OpenGl mode: long-term status not checked.
- SharpGLTF support for VRMC_vrm, MToon and skin data: the package was checked only at NuGet metadata level; the library source was not opened.
- Prowl.Clay VRM and MToon capability: NuGet description only.
- @pixiv/three-vrm 3.5.5 peer dependency on a specific three.js version and browser WebGL2 availability: not checked.
- Seed-san and VRM1_Constraint_Twist_Sample anime visual style: screenshots not opened in this run.
- Creative Commons compatibility of VRM files (vrm-c/vrm-specification issue #358): seen only as a search snippet; not opened.
- Avalonia custom-rendering GPU interop APIs (ImportGpuImage and related): documented in the docs example only; not checked against Avalonia source per platform.
- Live2D plan details (plans A to G): only the top-level licence page was read.
- Silk.NET repository page: GitHub returned 404; only NuGet metadata was read.
- UniVRM Unity version requirement: taken from a search summary, not from an opened source.
- Japanese VRM PL 1.0 text: compared with the English text at summary level only.
- Avalonia Metal zero-copy claims and Avalonia 12 performance figures found in search summaries were NOT used as facts.

## Sources

All opened 2026-10-22.
- https://github.com/AvaloniaUI/Avalonia
- https://www.nuget.org/packages/Avalonia
- https://github.com/AvaloniaUI/Avalonia/releases
- https://docs.avaloniaui.net/docs/avalonia12-breaking-changes
- https://docs.avaloniaui.net/api/avalonia/opengl/controls/openglcontrolbase
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/src/Avalonia.OpenGL/Controls/OpenGlControlBase.cs
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/src/Avalonia.OpenGL/Composition/OpenGlCompositionInterop.cs
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/src/Avalonia.Base/Rendering/Composition/Compositor.cs
- https://docs.avaloniaui.net/docs/graphics-animation/custom-rendering
- https://docs.avaloniaui.net/docs/platform-specific-guides/windows.md
- https://docs.avaloniaui.net/docs/platform-specific-guides/macos.md
- https://docs.avaloniaui.net/docs/platform-specific-guides/linux.md
- https://docs.avaloniaui.net/docs/platform-specific-guides/android/
- https://docs.avaloniaui.net/docs/platform-specific-guides/ios.md
- https://docs.avaloniaui.net/docs/platform-specific-guides/webassembly.md
- https://docs.avaloniaui.net/docs/supported-platforms.md
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/src/iOS/Avalonia.iOS/Eagl/EaglDisplay.cs
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/src/iOS/Avalonia.iOS/Metal/MetalPlatformGraphics.cs
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/src/Avalonia.Metal/IMetalExternalObjectsFeature.cs
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/src/Browser/Avalonia.Browser/Rendering/BrowserWebGlRenderTarget.cs
- https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/src/Browser/Avalonia.Browser/Rendering/RenderTargetBrowserSurface.cs
- https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/call-javascript-from-dotnet?view=aspnetcore-10.0
- https://github.com/mrdoob/three.js
- https://www.npmjs.com/package/three
- https://www.npmjs.com/package/@pixiv/three-vrm
- https://github.com/pixiv/three-vrm
- https://raw.githubusercontent.com/vrm-c/vrm-specification/master/specification/VRMC_vrm-1.0/README.md
- https://raw.githubusercontent.com/vrm-c/vrm-specification/master/specification/VRMC_vrm-1.0/meta.md
- https://github.com/vrm-c/vrm-specification
- https://raw.githubusercontent.com/vrm-c/vrm-specification/master/LICENSE (HTTP 404)
- https://raw.githubusercontent.com/vrm-c/vrm-specification/master/LICENSE.md (HTTP 404)
- https://vrm.dev/en/licenses/1.0/index
- https://vrm.dev/licenses/1.0/
- https://github.com/vrm-c/UniVRM
- https://www.nuget.org/packages/SharpGLTF.Core
- https://www.nuget.org/packages/Prowl.Clay
- https://www.nuget.org/packages/Silk.NET.OpenGL
- https://www.live2d.com/en/sdk/license/
- https://github.com/vrm-c/vrm-specification/blob/master/samples/Seed-san/README.md
- https://raw.githubusercontent.com/vrm-c/vrm-specification/master/samples/Seed-san/vrm/Seed-san.vrm
- https://raw.githubusercontent.com/vrm-c/vrm-specification/master/samples/VRM1_Constraint_Twist_Sample/vrm/VRM1_Constraint_Twist_Sample.vrm
- https://raw.githubusercontent.com/vrm-c/vrm-specification/master/samples/README.md
