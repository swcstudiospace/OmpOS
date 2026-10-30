---
phase: 76-cowork-parity-plugins-auto-isolation
plan: 01
subsystem: plugins
tags: [plugin-bundles, skills, zip-install, conventions, avalonia]

requires:
  - phase: 66.1
    provides: Harness.PluginBundles scaffold and ILaunchContributor
provides:
  - Atomic folder and zip bundle install with named BundleInvalid codes
  - Launch overlay entries and --skills filter for bundle skills
  - ConventionScope.Plugin discovery and create-plugin skill
affects: [76-06]

actuals:
  tokens: 18000
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns:
    - "Bundle skills reach omp only as skills.customDirectories overlay entries plus a --skills include list"
    - "Hooks are listed as BundleComponentNotLoaded(hooks) and never executed"
    - "MCP servers from a bundle stay unconfirmed until a later composition step"

key-files:
  created:
    - src/Autonogrammer.Harness.PluginBundles/BundleInstaller.cs
    - src/Autonogrammer.Harness.PluginBundles/BundleLaunchContributor.cs
    - assets/skills/create-plugin/SKILL.md
  modified:
    - src/Autonogrammer.Harness.Workspace/Conventions/ConventionCatalog.cs
    - src/Autonogrammer.Harness.Workspace/BuiltinSkills/BuiltinSkillManifest.cs

key-decisions:
  - "Overlay entry text is skills.customDirectories=<absolute skills root>"
  - "A disabled skill whose name is a glob cannot be named in --skills, so its bundle skill root is omitted and reported"
  - "Extra types live in their own files because MA0048 rejects more than one top-level type per file"

patterns-established:
  - "BundleInvalid(code) is the only user-facing refusal string"
  - "Project-scope bundles stay off until enabled (BundleProjectScopeOff)"

requirements-completed: [AG-76-1, AG-76-2, AG-76-3, AG-76-4]

coverage:
  - id: D1
    description: Hostile zip and folder installs fail with named codes and leave no bundle directory
    requirement: AG-76-2
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.PluginBundles.Tests/BundleValidatorTests.cs
        status: pass
      - kind: unit
        ref: tests/Autonogrammer.Harness.PluginBundles.Tests/BundleInstallerTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: Enabled bundle skills contribute one overlay entry and disabled skills are filtered or reported
    requirement: AG-76-3
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.PluginBundles.Tests/BundleLaunchContributorTests.cs
        status: pass
      - kind: unit
        ref: tests/Autonogrammer.Harness.PluginBundles.Tests/BundleMcpSourceTests.cs
        status: pass
      - kind: unit
        ref: tests/Autonogrammer.Harness.PluginBundles.Tests/BundlePrecedenceTests.cs
        status: pass
    human_judgment: false
  - id: D3
    description: Plugin roots discover skills, p:a commands and p:x agents; create-plugin ships
    requirement: AG-76-4
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.Workspace.Tests/Conventions/ConventionCatalogPluginTests.cs
        status: pass
      - kind: unit
        ref: tests/Autonogrammer.Harness.Workspace.Tests/BuiltinSkills/BuiltinSkillManifestTests.cs
        status: pass
    human_judgment: false
  - id: D4
    description: Bundle section models and the bundles page view
    requirement: AG-76-1
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Presentation.Tests/Settings/Plugins/BundleSectionModelTests.cs
        status: pass
      - kind: automated_ui
        ref: tests/Autonogrammer.Native.Tests/Settings/BundlesPageViewTests.cs
        status: unknown
    human_judgment: true
    rationale: Native.Views did not compile because sibling Cowork files were mid-edit; the view test was not executed

duration: 90min
completed: 2026-10-30
status: complete
---

# Phase 76 Plan 01 Summary

**Plugin bundles install atomically, refuse hostile zips by named code, and contribute skills only as overlay entries.**

## Performance

- **Duration:** 90 min
- **Started:** 2026-10-30
- **Completed:** 2026-10-30
- **Tasks:** 3
- **Files modified:** bundle installer, launch contributor, convention plugin pass, create-plugin skill, settings models and view

## Accomplishments

- Folder and zip install into a temp sibling then rename, with rollback and `BundleInvalid(code)` for traversal, symlink, size, count and a bad manifest.
- `BundleLaunchContributor` emits `skills.customDirectories=<skills root>` and `--skills` for disabled skills that can be named exactly.
- Commands are `/plugin:command`, MCP servers are `plugin.server` and unconfirmed, hooks are listed as `BundleComponentNotLoaded(hooks)` and never run.
- `ConventionScope.Plugin` and `create-plugin` ship.

## Task Commits

1. **Tasks 1-3** - one commit `feat(76-01):` (feat)

## Files Created/Modified

- `src/Autonogrammer.Harness.PluginBundles/BundleInstaller.cs` - atomic install
- `src/Autonogrammer.Harness.PluginBundles/BundleLaunchContributor.cs` - overlay and skills filter
- `assets/skills/create-plugin/SKILL.md` - built-in skill
- `src/Autonogrammer.Presentation/Settings/Plugins/BundleSectionModel.cs` - settings section model
- `src/Autonogrammer.Native.Views/Views/Settings/Plugins/BundlesPageView.axaml` - section view

## Decisions Made

- Overlay text is `skills.customDirectories=` plus the absolute skills directory (OQ-454 is still open; a skill that cannot be named in `--skills` is omitted and reported).
- No logging package was added. The project has no logger reference and the plan forbids a new package.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Split types so MA0048 accepts the build**

- **Found during:** Task 1
- **Issue:** The analyzer rejects more than one top-level type per file.
- **Fix:** Supporting records and JSON contexts are separate files under the owned PluginBundles tree.
- **Files modified:** `src/Autonogrammer.Harness.PluginBundles/*`
- **Verification:** PluginBundles Release build with `-warnaserror` passed
- **Committed in:** the plan commit

---

**Total deviations:** 1 auto-fixed
**Impact on plan:** Required for the existing analyzer gate. No new package.

## Issues Encountered

`BundlesPageViewTests` was not run. `Autonogrammer.Native.Views` failed to compile on sibling Cowork and memory files that this plan does not own.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

76-06 can adapt `IBundleManagementSource` over `BundleInstaller` and the state stores. It should wire `BundlesPageView` into `SettingsPageViews`.

---
*Phase: 76-cowork-parity-plugins-auto-isolation*
*Completed: 2026-10-30*
