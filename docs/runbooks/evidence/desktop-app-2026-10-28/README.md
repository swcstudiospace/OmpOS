# Desktop app screenshots, Linux, 2026-10-28

**What these show:** the real OmpOS desktop head (`apps/OmpOS.Native`, Avalonia 12) running on Linux in the Light theme, with the real omp 18.12.6 installed. **No model turn was run** (OD-07): no screen shows an agent answering. Nothing here ran on Windows, macOS or Android.

## Method

- Tree: the Wave 3 wiring tree (commit `5275a23`, "Wave 3 wiring compiled and tested"), Release build. These screens predate the later wiring of automations, container and WSL environments, built-in skills, MCP presets and image attachments (commits `942d813` and `e7c451e`): the Settings window in the shots has 21 pages and no Automations page, and the Remote environments page has no container section.
- Display: Xvfb at 1440x900, no window manager (so the Settings and About windows overlap the main window instead of centring).
- Isolation: a sandbox `HOME`, `PI_CODING_AGENT_DIR` and `OMPOS_STATE_DIR`, fake provider keys only, no real omp state, no network call by OmpOS itself. The only stderr output was two lines "The hook bridge listens on loopback port N" (`stderr.txt` was not kept).
- One PNG per screen, taken by the F3a agent after driving the window; originals are copied here unchanged (1440x900).

## Images

| File | Screen |
|---|---|
| `01-main-light.png` | Main window, Light theme, rail with Agents and Kanban tabs; the empty state |
| `05-new-agent.png` | A new session: composer, model chip and send button |
| `06-slash-palette.png` | Typing `/` lists 134 commands with OmpOS, Engine and Cursor badges (the catalogue rows plus the commands omp advertises) |
| `04-about.png` | About: brand icon, version, revision, AGPL licence and source link |
| `07-omp-terminal.png` | An omp terminal; omp starts its own setup wizard, no model turn |
| `08-main-after-header-fix.png`, `09-runmode-recheck.png` | Re-shots after the two fixes below |
| `settings-00-runtime.png` to `settings-20-keyboard.png` | The 21 Settings pages in window order: Runtime, General, Display, Agents, Run mode, Models, Tab, Rules, Skills, Commands, Hooks, MCP servers, Plugins, Workspaces, Worktrees, Ignore, Remote environments, Cloud agents, Privacy, Network, Keyboard |

`docs/assets/screens/desktop-agents-light.png` is `06-slash-palette.png` and `docs/assets/screens/desktop-settings-light.png` is `settings-05-models.png`, both resized to 1280x800.

## Findings

- The Light theme and the purple brand render; no crash; all 21 Settings pages render with no clipped text, wrong colour or missing icon. Cloud shows its "not set up" state (no `OMPOS_API_BASE_URL`); Models shows the provider directory with the 24 providers built into omp, and OpenAI with "credential present in omp" (a fake key in the sandbox).
- The agent window shows "6 pipeline stages are degraded" and Ultrathink, Swarm and GSD "unavailable", Substrate "off": the plugin submodules were not checked out in the worktree that was run, so the plugins page reports Ultrathink, Swarm and Substrate as Missing. This is the sandbox, not a defect.
- **Two defects were found and fixed** (AXAML), then re-shot:
  1. "Where should the agent run?: This computer" was clipped in the rail, so the current choice was invisible. Fixed with a wrapping `HeaderTemplate` on `EnvironmentExpander` (`MainView.axaml`); `08-main-after-header-fix.png`. `06-slash-palette.png` and `05-new-agent.png` were taken before this fix and still show the clipped header.
  2. The Run mode page opened scrolled down, because the selected read-access item scrolled the page. Fixed with `AutoScrollToSelectedItem="False"` on `ModeList` and `BoundaryList` (`RunModePageView.axaml`); `09-runmode-recheck.png`.
- The final tree compiled and the Native tests passed (Native.Tests 2012 total, 2 skipped, recorded 2026-10-28); the two AXAML fixes are part of it.

## What this does not prove

A turn with a model, the live behaviour of hooks, mentions, checkpoints or automations in a session, Windows or macOS rendering, high-DPI, or screen-reader behaviour. The headless view tests (`tests/OmpOS.Native.Tests`) are the proof for control names and target sizes.
