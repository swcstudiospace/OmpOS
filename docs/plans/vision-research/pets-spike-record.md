# Pets companion: spike record and asset licence record

Scope: Phase 41 (VX-09), criteria 3 and 4. Status as of 2026-10-26.

This record states status only. Nothing in it is built, run or deployed by this document. The research it builds on is `docs/plans/vision-research/pets-feasibility.md` (read 2026-10-22; no builds, tests or runs were executed for it).

## Status key

- **NOT BUILT**: no code exists for it.
- **NOT RUN**: the check could not be run or was not run. It is never recorded as passed.
- **BLOCKED**: a named owner decision or external prerequisite is missing.

## 3D renderer: NOT BUILT

ROADMAP Phase 41 criterion 3 allows a 3D renderer only after a per-target spike passes. No 3D renderer is built, no 3D asset is loaded, and no 3D library is referenced by any project. The default recorded in `.planning/STATE.md` (q3) is a 2D anime companion first, with 3D gated by a per-target spike.

Feasibility verdicts from `pets-feasibility.md`, API level only, runtime not executed:

| Target | Verdict in the research |
| --- | --- |
| Blazor WebAssembly (web head) | feasible-verified at API level: three.js and @pixiv/three-vrm (both MIT) through JavaScript interop |
| Avalonia desktop: Windows, macOS, Linux X11, Linux Wayland | feasible-verified at API level for the GPU path (OpenGlControlBase); no .NET library was found that renders VRM 1.0 with MToon and spring bones, so a desktop 3D pet needs a custom renderer |
| Avalonia Android | feasible-unverified |
| Avalonia iOS | feasible-unverified; needs macOS and Xcode |
| Avalonia WebAssembly (not the Blazor head) | not-feasible for OpenGlControlBase, source-inferred |

## Per-target spike: NOT RUN

| Target | Spike | Blocker | Owner to unblock |
| --- | --- | --- | --- |
| Web (Blazor WebAssembly) | NOT RUN | needs a browser run | owner decision on the rendering approach, then a browser host |
| Windows desktop | NOT RUN | needs a Windows 11 24H2 runner with a GPU | owner, runner host |
| macOS desktop | NOT RUN | needs the Mac channel (XP-11) | owner, Mac channel |
| Linux desktop | NOT RUN | needs a GPU desktop session; the research notes a Software fallback on headless or VM sessions | owner decision on the rendering approach |
| Android | NOT RUN | needs an Android 16 device or emulator; this host has no /dev/kvm and no SDK API 36 | owner, device or emulator |
| iOS | NOT RUN | needs the Mac channel (XP-11) | owner, Mac channel |

The spike is not required while 3D stays unbuilt. It becomes required only if the owner chooses a 3D rendering approach (owner decision 1 in `pets-feasibility.md`). Tracked in OQ-72.

## Owner decisions open

All seven owner decisions in `pets-feasibility.md` are open: rendering approach, mobile scope, asset source, licence budget, spec licence, feature depth and credit display. The appearance sign-off (`.planning/STATE.md`, open pauses) is also open. Tracked in OQ-72.

## Asset licence record

**No character assets are shipped.** As of 2026-10-26 the repository contains no VRM, glTF, sprite, image or other character asset for the companion, and no code loads one. A search of the tracked file names for `.vrm` and `.glb` files and for names containing sprite, avatar or mascot found none.

ROADMAP Phase 41 criterion 4 requires the owner to sign off the appearance before art ships, and requires a verified licence for every asset. Until both exist, the record below is the only asset record.

| Asset | Source | Licence | Licence verified | Credit required | Status |
| --- | --- | --- | --- | --- | --- |
| None | none | not applicable | not applicable | not applicable | no character assets shipped |

Candidates the research found (not adopted, not downloaded into the repository, not approved by the owner):

| Candidate | Licence facts from the research (read 2026-10-22) | Credit |
| --- | --- | --- |
| Seed-san sample (VirtualCast, Inc.) | VRM 1.0, licenseUrl https://vrm.dev/licenses/1.0/, avatarPermission everyone, commercialUsage corporation, allowRedistribution true, modification allowModificationRedistribution | creditNotation required |
| VRM1_Constraint_Twist_Sample (pixiv Inc.) | VRM 1.0, same licenseUrl, avatarPermission everyone, commercialUsage corporation, allowRedistribution true | creditNotation unnecessary; the research notes it for testing only |

Unverified in the research: the anime visual style of both candidates (screenshots not opened), and the licence of the vrm-c/vrm-specification repository itself (no licence field on GitHub; LICENSE and LICENSE.md return HTTP 404 on master). Other VRoid or VRM samples were not checked.

Rule for any future asset: add a row above with its source URL, licence text or URL, read date, credit requirement and the owner's sign-off date, before the asset enters the repository.

## Open items

1. Appearance sign-off and asset source: owner. Route: OQ-72.
2. Rendering approach and the per-target spike: owner, then runner hosts, the Mac channel and an Android 16 device. Route: OQ-72.
3. Gate result for the current HEAD: to be recorded by the orchestrator gate run.
