# Web app screenshots and smoke, 2026-10-28

**What these show:** the OmpOS web head in its **Demo build** (ADR-0017). Every screen is produced by scripted fixtures answered inside the browser (`apps/OmpOS.Web/Demo`): there is no backend, no model call, no file and no secret. The thread, the approval, the diff and the Kanban cards are simulated. They show the interface, not an agent doing work.

## Method

1. The Demo bundle was built with `scripts/vercel/build-demo.sh` (`dotnet publish apps/OmpOS.Web -c Release -p:OmpOSDemo=true`, static `vercel.json` with the Content-Security-Policy `connect-src 'self'`).
2. `scripts/web-demo/capture.mjs` served the bundle on `127.0.0.1` (`scripts/web-demo/serve-demo.sh`), started headless Chrome and drove it over the DevTools protocol (no browser library). One PNG per screen, with `Emulation.setDeviceMetricsOverride` for the viewport and the theme set through `localStorage` before first paint. Env: `THEME` (light or dark), `W` and `H`, `PREFIX`, `OUT`, `BUNDLE`, `URL`.
3. Viewports and themes captured (28 screens each; the report of each run is in this folder): `L1440` (Light, 1440x900), `D1440` (Dark, 1440x900), `L1024` (Light, 1024x768), `L390` (Light, phone, 390x844, with the agents drawer). `M-*` are the Models page with the provider directory filtered to China coding plans and to Europe.
4. The same script ran against the deployed Demo with `URL=https://ompos-web.vercel.app` (recorded 2026-10-28): `live-vercel-report.json` shows `ready: true`, `errors: []`, `failedRequests: 0`, 18 Settings pages visited; `live-vercel-L1440-01-agents.png` is its first screen. The Vercel production alias of project `ompos-web` (team `swcstudiospace`) serves the same static bundle.
5. Each run's `report.json` lists the shots, the six Demo rows, the Settings pages visited (General, Runtime, Agents, Run mode, Models, Tab, Rules, Skills, Commands, Hooks, MCP, Plugins, Workspaces, Remote environments, Cloud agents, Privacy, Network, Keyboard), the two Models filters, console errors and failed requests (`errors: []` and `failedRequests: 0` in all five).

## Images in this folder

| File | Screen |
|---|---|
| `L1440-01-agents.png`, `D1440-01-agents.png` | Agents: rail, running thread, composer; Light and Dark |
| `L1024-01-agents.png` | The same at 1024 px |
| `L390-01-agents.png`, `L390-02-drawer.png` | Phone: the thread, and the agents drawer |
| `L1440-03-completed-changes.png`, `D1440-03-completed-changes.png` | A completed agent with the Changes bar open |
| `L1440-04-approval.png` | A tool approval card |
| `L1440-05-failed.png` | A failed run with a retryable error |
| `L1440-06-terminal.png` | The omp terminal (scripted output) |
| `L1440-08-slash-palette.png` | The slash palette |
| `L1440-09-kanban.png`, `D1440-09-kanban.png` | The Kanban board |
| `L1440-10-settings-00-general.png` | Settings > General |
| `M-11-models-cn-coding-plans.png`, `M-12-models-eu.png` | Settings > Models, provider directory filtered |
| `live-vercel-L1440-01-agents.png`, `live-vercel-report.json` | The deployed Demo at https://ompos-web.vercel.app |
| `*-report.json` | Per-run capture reports |

The README and the guides use copies in `docs/assets/screens/`: `web-agents-light`, `web-agents-dark`, `web-kanban-light`, `web-settings-models-light`, `web-changes-review-light` and `web-mobile-light` are the files `L1440-01-agents`, `D1440-01-agents`, `L1440-09-kanban`, `M-11-models-cn-coding-plans`, `L1440-03-completed-changes` and `L390-01-agents` of this folder (byte-identical; the pinned names are checked by `OMPOS_RELEASE=1 bash scripts/ci/check-docs.sh`).

## Accessibility smoke

`scripts/web-smoke/smoke-demo.mjs` (run through `scripts/web-smoke/run-smoke.sh`, `SMOKE_TARGETS="api demo"`, `DEMO_DIR`, `DEMO_PORT`, `SHOT_DIR`) runs axe-core 4.14.0 (WCAG 2.0 and 2.1, A and AA; hash in `scripts/web-smoke/axe-core-4.14.0.sha256`) on the Demo in Light and Dark at 1440, 1024 and 390 px across every screen: agents, drawer (phone), running, slash palette, composer menu, approval, completed with Changes, failed, closed, terminal, Kanban and Settings.

- **Result recorded 2026-10-28 for the Wave 5 tree: 68 axe runs, 0 violations.**
- It also asserts: no horizontal scroll, no console error, exception, failed request or HTTP status 400 or above, the Demo banner present and control-free, the skip link as first Tab stop, the drawer's focus trap and Escape, and that the theme switch persists across a reload.
- Known incomplete results, recorded by the author of the smoke and not resolved: `aria-valid-attr-value` on the model chip of the composer menu step; `color-contrast` could not be measured on overlapped or pseudo-element backgrounds (the token pairs are covered by `AccessibilityTests` in `tests/OmpOS.UI.Tests`, 5.1 to 15.4:1 for text and 3.6 to 8.5:1 for non-text); xterm's virtual terminal rows (third party).
- The API half of the smoke (`scripts/web-smoke/smoke.mjs`, the web head against the real API) was not re-run for these screenshots.

## Known visual debt (from the WebPolish report)

- Slash palette at 390 px: the description column is squeezed to about 60 px.
- The slash list and each Kanban lane are keyboard-focusable scroll regions (`tabindex=0`): one extra Tab stop each.
- The boot screen and loading ring, the rail error and empty states and the Retry state are restyled only by token inheritance and are not screenshotted (the Demo cannot reach them).
- The pipeline strip's "Latest:" line and "Steering history (0)" are still plain small text.
- Demo fixture: the workspace name and folder label are both `acme-shop`; the web runtime puts "Can also grant" into the approval's arguments summary, which the card renders as a note.
- Not in the Demo: Display, Worktrees and Ignore settings pages (desktop only); Workspaces, Remote environments, Cloud agents, Agents and Run mode are information pages with a "desktop app" notice; the Hooks and MCP pages are read-only lists; web image attach is not built.

## What this does not prove

A model turn, any real file change, a signed-in or hosted API, a browser other than Chrome, or a screen reader run. The web head against the real API was exercised by the API half of the smoke on earlier trees (see `docs/runbooks/evidence/web-smoke-924ca78-2026-10-21.log`), not for this set.
