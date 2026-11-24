# UX specification — v0.16.0 phases 66–78 (T77d9-ux)

Task: T77d9-ux, correlation 77d97ef8-56a4-4b53-ae66-5b8c5e5d659e, capability ux.spec.
Fidelity: lofi (Figma unavailable, ASCII wireframes). Provisional: true (no brand.guide input;
colours, type, spacing and motion are taken verbatim from `src/Autonogrammer.UI/wwwroot/styles/tokens.css`
— White and Purple, mirrored in `AutonogrammerTokens.axaml` — which is the single source A06 implements
against, not an invented palette). Tokens version: 1.4.0 (`design/tokens.json`).
A11y target for every screen: WCAG 2.2 AA. Lowest normal-text ratio measured this run: 6.25
(light `color.status.error` #b3261e on `color.surface.default` #fafaf9; see ratio table in `design/a11y-requirements.md`).
Focus ring requirement: 3:1 non-text (SC 1.4.11). Named states are families; dotted children
(`error.network`, `empty.session`) satisfy their family.

Reuse first (decision logic 1): no new design-system component is introduced. Every region reuses an
existing component from `src/Autonogrammer.UI/Components/**` or a `.autonogrammer-*` class in
`wwwroot/styles/`. Component `ref` values use `ds/Name@1.4` where the version names the token set
version; the repo class/component in column 3 is what A06 implements.

Breakpoints (token `breakpoint.*`, `ux_breakpoint_order_check` valid, starts at 0):
`base` 0, `sm` 560, `md` 840, `lg` 1200, `xl` 1440. CSS expresses them as
`@media (max-width: 559.98px)`, `839.98px`, `@media (min-width: 1200px)`. Below `md` the rail is an
off-canvas drawer with focus trap; below `sm` the shell is single column. NFR-A1 viewports
1440/1024/390 plus reflow 320 map onto `xl`/`lg`/`sm`/`base`. Motion: `motion.duration-fast` 120 ms,
`motion.duration` 200 ms, both 0 under `prefers-reduced-motion: reduce` and `data-motion="reduce"`
(`base.css`). No carousel, no auto-advance, no looping effect.

Dark-pattern lint: no fake urgency, no hidden cost, no confirm-shaming. Approval copy states the
exact risk and the scope of the grant; blocked states state the owner action needed in plain words
(AC-111/112). No screen asks for a seed phrase or private key (wallet sign-in offers provider
sign-in as the equal alternative, `SignInPanel`).

I18n (NFR-I1): every user-facing string resolves from the presentation copy layer
(`AccountCopy`, `KanbanCopy`, `AboutViewCopy`, etc.); no hardcoded literal is introduced here.
Synthetic data only in wireframes.

---

## SCR-WebWorkbench — hosted web shell (primary screen)

AC refs: AC-103 (hosted bundle serves the real app), AC-104 (WCAG 2.2 AA, 0 axe errors).
Flows: `guest` (redirects to SCR-SignIn) → `authenticated` (workbench, session transcript, composer).

| ref | repo implementation | props |
|---|---|---|
| `ds/SkipLink@1.4` | `AppShell` (`href="#main"`) | `label: Skip to main content` |
| `ds/TopBar@1.4` | `Shell/TopBar` | `height: size.titlebar.height` |
| `ds/Button@1.4` | `.autonogrammer-btn` | `variant: primary` on send/apply actions |
| `ds/IconButton@1.4` | `.autonogrammer-icon-btn` | `size: 32px (28px small)` |
| `ds/SurfaceTabs@1.4` | `TopBarSurface` (see SCR-SurfaceSwitch) | `selected: code` |
| `ds/Rail@1.4` | `Rail/AgentRail` (nav landmark `Agents`) | `width: size.rail.width` |
| `ds/ThreadView@1.4` | `Thread/ThreadView` | `max-width: size.thread.max (760px)` |
| `ds/Composer@1.4` | `Composer.razor` (textarea + send) | `min: size.composer.min (72px)` |
| `ds/LiveRegion@1.4` | `TranscriptAnnouncements` (`role=status`) | `region: persistent container` |

Interactive ids and focus order (identical list, `ux_spec_completeness_check`):
`skip-link`, `drawer-menu`, `surface-switch-code`, `surface-switch-cowork`, `view-tab-agents`,
`view-tab-kanban`, `theme-switch`, `rail-new-agent`, `rail-agent-row-first`, `rail-settings`,
`thread-header-action`, `composer-input`, `composer-send`, `footer-source-link`.
Focus lands on the route heading (`h1[tabindex=-1]`) after navigation (`RouteFocus`);
it returns to `drawer-menu` when the drawer closes (`AppShell.CloseDrawerAsync`).

States (drawn, ASCII): `default`, `loading`, `error.network`, `error.validation`,
`empty.session`, `success`.

```
-- default (desktop >= lg, 1440) -------------------------------------------------
+------------------------------------------------------------------+
| [skip-link] TopBar: workspace | Code|Cowork tabs | theme [sign-out]|
+----------+-------------------------------+----------------------+
| Rail nav | Thread: h1 + blocks          |  (main, max 760)     |
| Agents   |  - transcript blocks        |                      |
| [new]    |  - approvals / changes      |                      |
| rows...  | +-------------------------+  |                      |
| Settings | | composer-input (72px)  |  |                      |
|          | | [send] model-picker      |  |                      |
+----------+-------------------------------+----------------------+
| Footer: version | source link                                        |
+------------------------------------------------------------------+
-- loading ---------------------------------------------------------
| TopBar (skeleton) | Rail (skeleton rows) | Thread: status "Loading" |
| (role=status, no spinner-only). Composer disabled with reason.    |
-- error.network ---------------------------------------------------
| Thread: ErrorBlockView + Retry button (focus moves to heading).  |
| Copy names what failed and the next step; no technical stack text.|
-- error.validation ------------------------------------------------
| Composer inline error under input (aria-describedby), Send stays   |
| enabled only after correction; error text uses error token.      |
-- empty.session ---------------------------------------------------
| New-agent guidance block (what to paste, starter example) +      |
| Composer focused. No illustration-only meaning.                  |
-- success ---------------------------------------------------------
| Thread blocks rendered; LiveRegion announces completion once.    |
| Status is word + icon, never colour alone.                       |
-- < md (drawer) ---------------------------------------------------
| TopBar only; Rail hidden behind scrim, focus trapped in drawer,  |
| Escape closes and returns focus to drawer-menu.                  |
```

A08 baseline: `scripts/web-smoke/a11y` states `agents`, `session-running`, `completed-changes`,
`failed-run`, `closed-session`, `terminal`, `new-conversation`, `kanban` must each reach 0 axe
errors at 1440x900 and 320x568 in light and dark (NFR-A1, AC-104). Route announcements via
`RouteAnnouncerRegion`.

---

## SCR-SignIn — passwordless sign-in (code + link)

AC refs: AC-106 (both paths end in signed-in state, 2 of 2). Flow: `guest` only.
Reuses: `Account/SignInPanel`, `Account/AccountState`, `Components.Auth.AuthGate`.

Interactive ids / focus order: `heading`, `email-input`, `code-input`, `send-code`,
`verify-code`, `resend-code`, `provider-button-first`, `wallet-button`, `cancel`.
`heading` is `h1[tabindex=-1]`; on the awaiting-link phase focus moves to `code-input`
(`SignInPanel.OnAfterRenderAsync`) and is never stolen on re-render.

States: `default` (email + send), `loading` (sending, disabled controls with reason),
`error.validation` (bad email/code, inline), `error.network` (provider/wallet unavailable,
`role=status` notice + try-another-way), `empty.session` — not applicable, replaced by
`default` with the other-ways group hidden until state is signed-out; `success`
(signed-in → redirect to workbench, callback sentence announced).

```
+----------------------------------------------+
| h1 Sign in (tabindex=-1)                     |
| [email-input________]                        |
| [send-code]  (primary)                       |
| --- or continue with ---                     |
| [provider-button]  [wallet-button]           |
| status text (role=status)                    |
+----------------------------------------------+
```

Copy never asks for password, seed phrase or private key. Escape cancels the pending request
(`OnKey`). Full coverage: `ux_state_coverage_check` with declared
`[default, loading, error.validation, error.network, success]` covers
`[default, loading, error, success]`; `empty` is documented N/A above (nothing to list yet).

## SCR-SurfaceSwitch — Code / Cowork switch

AC refs: AC-108 (switching + Kanban query shows 0 sessions of the other surface).
Reuses: `TopBarSurface`, `Cowork/CoworkWorkbench`, `Kanban/KanbanView`.
Renders inside the TopBar slot; it is a `tablist` with two tabs.

Interactive ids / focus order: `surface-switch-code`, `surface-switch-cowork`,
`view-tab-agents`, `view-tab-kanban`. Switching is a tab activation: `aria-selected`,
roving tabindex (arrow keys), focus stays on the active tab, and the rail label flips
`Agents` ↔ `Tasks`. A session opened under one surface is never listed under the other.

States: `default` (code selected), `success` (switch confirmed, board queried),
`error.network` (board query failed — stay on current surface, announce, offer retry),
`empty.session` (no sessions on the target surface — guidance + new-conversation action),
`loading` (board skeleton, tabs disabled with reason). No `error.validation` content exists
here; the family is covered by the shared `error` family via `error.network`.

## SCR-CoworkChat — Cowork chat core and parity (projects, memory, schedules, plugins)

AC refs: AC-109 (6 of 6 behaviours, 0 tool calls without approval), AC-115 (3 of 3 scheduled
runs, 0 unregistered tools), AC-116 (14 of 14 bundle rows with evidence, 0 proofs without).
Reuses: `Cowork/CoworkConversation`, `Cowork/CoworkComposer`, `Cowork/CoworkRail`,
`Cowork/ProjectsView`, `Cowork/MemoryView`, `Approvals/ToolApprovalRow`, `Thread/ConfirmBar`.

Interactive ids / focus order: `rail-tasks`, `composer-input`, `composer-send`,
`approval-allow`, `approval-deny`, `dock-tab-projects`, `dock-tab-memory`,
`dock-tab-schedules`, `dock-tab-plugins`, `artifact-open`.
Every tool call renders an approval card first; allow/deny are two separate 32 px buttons,
allow is never the focused default. Declined calls announce and stop — they are never
retried silently.

States: `default`, `loading` (streaming, `aria-busy`, stop button), `error.network`
(stream dropped — resume from cursor), `error.validation` (empty/unreadable attachment
refused with reason), `empty.session` (no conversation yet), `success` (turn complete,
announced once). Memory parity: a retried write shows "already saved" rather than a duplicate
row (AC-114 cross-check).

```
+----------------------------+----------------------------+
| Cowork rail (Tasks)        | Conversation (streaming)   |
| projects/memory/schedules  |  blocks + approval card    |
| dock tabs                  |  [allow] [deny]          |
|                            | composer [send] [stop]   |
+----------------------------+----------------------------+
```

## SCR-ArtifactReview — versioned artifacts, safe rendering

AC refs: AC-110 (script executions 0, owner-scope violations 0, 21 render rules).
Reuses: `Cowork/Artifacts/*`, `Blocks/DiffView`, `Blocks/MarkdownBlockView`, `ChangesBar`.
The frame is a sandboxed document; scripts never run. Export is a primary button with a
visible format label; sanitiser failure blocks export and explains which rule failed.

Interactive ids / focus order: `artifact-version`, `artifact-content`, `artifact-export`,
`artifact-render-rule`, `artifact-close`. Focus is trapped in the artifact dialog while open
and returns to `artifact-open` on close.

States: `default`, `loading` (render in progress), `error.network` (fetch failed),
`error.validation` (unsupported mime — refused, no partial render),
`empty.session` (no versions yet), `success` (rendered + export offered).

## SCR-DispatchApproval — browser, computer use, dispatch, memory

AC refs: AC-111 (phase 72 stays Blocked, 0 production grants), AC-112 (blocked actions > 0,
0 unapproved screen reads), AC-113 (100% relayed in window, 0 duplicates), AC-114 (0 duplicated
memory rows over 50 replays).
Reuses: `Cowork/BrowserPane`, `Dispatch/DispatchApprovalPanel`, `Dispatch/DispatchPageView`,
`MemoryView`. Phase 72 is fail-closed: the browser pane shows the blocked statement and the
owner action needed; there is no grant button anywhere on this screen.

Interactive ids / focus order: `browser-pane-content` (readonly region, not a tab stop),
`computer-approval-allow`, `computer-approval-deny`, `dispatch-queue-status`,
`dispatch-retry`, `memory-saved-notice`. Computer use requires explicit per-action approval;
screen reads are themselves approvals. Dispatch queue shows send time, relay receipt and
idempotency key; a queued item is never dispatched twice.

States: `default` (blocked browser + pending approval), `loading` (relaying),
`error.network` (relay unreachable — queued, visible, not lost),
`error.validation` (unreadable image refused — AC-109/112), `empty.session`
(no paired devices — pairing guidance), `success` (relayed + receipt, memory saved once).

## SCR-DesktopRelease — installers and checksums

AC refs: AC-118 (SHA256SUMS-desktop 6 of 6, rehearsal 1 of 1 Windows smoke).
Reuses: `About/AboutView`, `ShellFooter` (version), settings export.
Non-interactive mostly: a table of 6 files with name, size, checksum (mono, copyable) and a
verify action. Text is `12px` mono (`typography.size.small`); checksum truncation is visual
only — the full value and title attribute are exposed.

Interactive ids / focus order: `release-table`, `checksum-copy-first`, `rehearsal-status`,
`download-link`. Download is a real link (never a button that pretends to download).

States: `default`, `loading` (rehearsal running, progress text),
`error.network` (rehearsal unreachable), `empty.session` (no candidates yet),
`error.validation` (checksum mismatch — blocked, do not install), `success`
(verified, install offered).

---

## Explicit note — ACs with no screen (not orphans)

The other nine ACs constrain these screens but are verified by gates, not by a screen spec.
They are listed here so the AC trace is honest instead of forcing a fake screen:

| AC | Why no screen | Verified by |
|---|---|---|
| AC-101, AC-102 | Repo hygiene / scaffold record — no user sees it. | `repo-hygiene` gate |
| AC-105, AC-107, AC-117 | Supply-chain / data plane / full gate — backend outcomes. | `ci` / `data-contract` / `full-gate` |
| AC-119, AC-120, AC-121, AC-122 | Branch preservation, SOTA hardening, PR gate, STATE/ROADMAP update — process outcomes. | `git-gate` / `quality-gate` / `ci` / `docs-gate` |

No screen exists for them and none is claimed. `ux_ac_trace_check` for this set is run over the
13 UI ACs above (AC-103, 104, 106, 108, 109, 110, 111, 112, 113, 114, 115, 116, 118).

## Requirements handed to A06 / A08

- Implement focus order exactly as listed; add nothing to the tab order (decorative icons are
  `aria-hidden`, status dots never receive focus).
- Keep 32 px controls / 28 px small; never ship a 20 px chip as a control (chips are not
  targets; `ux_target_size_check` covers the 11 targets in `a04.targets`).
- Announce every state change through the persistent live region; never insert a new node with text.
- The primary-screen JSON for the trace is `SCR-WebWorkbench` below; the other six screens are
  specified in this file with the same shape and are covered by the same checks.

```json
{ "screen_id": "SCR-WebWorkbench", "flows": ["guest", "authenticated"],
  "states": ["default", "loading", "error.network", "error.validation", "empty.session", "success"],
  "components": [ { "ref": "ds/Button@1.4", "props": { "variant": "primary" } } ],
  "a11y": { "target": "WCAG-2.2-AA", "focus_order": ["skip-link", "drawer-menu", "surface-switch-code", "surface-switch-cowork", "view-tab-agents", "view-tab-kanban", "theme-switch", "rail-new-agent", "rail-agent-row-first", "rail-settings", "thread-header-action", "composer-input", "composer-send", "footer-source-link"], "min_contrast": 6.25 },
  "tokens_version": "1.4.0", "fidelity": "lofi", "provisional": true,
  "acceptance_refs": ["AC-103", "AC-104"] }
```

---

## T855b addendum — verification refresh, phases 66–77 (T855b-ux)

Task: T855b-ux, correlation 855b317b-954c-4303-b010-d0a18406ab35,
capability ux.spec. This addendum specifies the UX for reading and
closing the verification refresh; it reuses the components above and
the same tokens version 1.4.0 (`design/tokens.json`, unchanged —
no token edit, no brand change, L2). Fidelity lofi, provisional true.
A11y target WCAG 2.2 AA; lowest normal-text ratio measured this run
6.25 (`color.status.error` #b3261e on #fafaf9). AC refs are AC-201
to AC-209 from acceptance-T855b.md and are traced below.

Web smoke / headless title criteria (66-SC3/SC4 gaps, 77 AG-77-13,
NFR-A2): evidence is a recorded `run-smoke.sh --a11y-report <dir>`
bundle (axe 0 errors, 0 incomplete, light+dark at 1440/1024/390,
plus the headless title check `MainWindowTitleTests`). Until a fresh
bundle exists on HEAD, the screens render `gaps_found` with the exact
closing steps; screen-reader-only criteria stay in
`human_verification` and are never shown as automated passes.

## SCR-VerificationRefresh — phase verification board

AC refs: AC-201, AC-202, AC-209. Flows: `authenticated`.
Reuses: `ds/TopBar@1.4`, `ds/Rail@1.4`, `ds/ThreadView@1.4`,
`ds/LiveRegion@1.4`, `ds/Button@1.4`.

Interactive ids / focus order: `phase-filter`, `phase-row-first`,
`evidence-link`, `digest-toggle`, `refresh-run`. Focus lands on the
board heading (`h1[tabindex=-1]`) on load and returns to the filtered
row on close. Status is word + icon, never colour alone (`passed` /
`gaps_found` / `human_needed` / `stale`).

States (drawn, ASCII): `default`, `loading`, `error.network`,
`error.validation`, `empty.session`, `success`.

```
-- default (desktop >= lg, 1440) ----------------------------------
| TopBar | Filter [phase-filter]                                   |
| board  | 66 gaps_found  [phase-row-first]  digest v1 current     |
|        | 77 gaps_found  digest stale → refresh-run (button)      |
|        | [evidence-link] opens the cited evidence bundle         |
-- loading ---------------------------------------------------------
| board skeleton rows; role=status 'Refreshing evidence';         |
| filter + refresh-run disabled with reason.                        |
-- error.network ---------------------------------------------------
| inline notice 'Evidence bundle unreachable — retry' (focus to   |
| heading); board keeps last known state, never blank.             |
-- error.validation -------------------------------------------------
| filter with no match shows inline 'No phase matches x'          |
| (aria-describedby); board unchanged.                             |
-- empty.session ---------------------------------------------------
| 'No phases match the filter' + clear-filter action (primary).    |
-- success ---------------------------------------------------------
| board re-rendered with fresh digests; live region announces      |
| '9 of 9 files refreshed' once.                                   |
```

## SCR-WebWorkbenchEvidence — smoke and title evidence screen

AC refs: AC-208. Reuses the workbench components above.
Interactive ids / focus order: `skip-link`, `drawer-menu`,
`theme-switch`, `rail-new-agent`, `composer-input`,
`composer-send`, `footer-source-link`.

States: `default`, `loading`, `error.network`, `error.validation`,
`empty.session`, `success` (drawn in the SCR-WebWorkbench frame
above; this screen adds only the evidence banner: commit hash, SDK
10.0.402, axe counts per width/theme, headless title result). An
absent fresh bundle renders `gaps_found` — the exact run steps —
rather than a green pass (66-SC3/SC4 stay open until run).

## SCR-DocsHub — SUMMARY + version + PR gate screen

AC refs: AC-203, AC-205, AC-206, AC-207.
Interactive ids / focus order: `version-banner`, `changelog-link`,
`state-link`, `pr-gate-status`, `summary-count`.

States: `default` (0.20.13, SUMMARY == PLAN count), `loading`
(gate polling, status text), `error.network` (CI unreachable),
`error.validation` (SUMMARY count mismatch listed as a gap),
`empty.session` (no SUMMARY yet — writing guidance), `success`
(PR merged on all-green CI, version banner announced).

## SCR-HumanVerificationGate — human-only criteria screen

AC refs: AC-204. Interactive ids / focus order:
`human-verify-list`, `step-detail`, `confirm-button`, `defer-button`.
Every human criterion renders with its evidence and exact closing
steps (screen-reader pass OQ-483, owner actions 1–8); nothing on
this screen can read as an automated pass. States mirror the board:
`default`, `loading`, `error.network`, `error.validation`,
`empty.session`, `success` (all human criteria confirmed → next
review unlocked, announced once).

## T855b AC trace

`ux_ac_trace_check`: acceptance 201–209 over the four screens above —
`coverage_pct` 100, `uncovered_acs` [], `unknown_refs` [],
`screens_without_refs` []. State coverage `complete` true; spec
completeness `complete` true (focus ids match interactive ids,
min_contrast 6.25 above the 4.5 floor).

```json
{ "screen_id": "SCR-VerificationRefresh", "flows": ["authenticated"],
  "states": ["default", "loading", "error.network", "error.validation", "empty.session", "success"],
  "components": [ { "ref": "ds/Button@1.4", "props": { "variant": "primary" } } ],
  "a11y": { "target": "WCAG-2.2-AA", "focus_order": ["phase-filter", "phase-row-first", "evidence-link", "digest-toggle", "refresh-run"], "min_contrast": 6.25 },
  "tokens_version": "1.4.0", "fidelity": "lofi", "provisional": true,
  "acceptance_refs": ["AC-201", "AC-202", "AC-209"] }
```

---

## T9013 addendum — terminal-retirement / session UI impact (T9013-ux)

Task: T9013-ux, correlation 901308c5-825d-45fe-8832-36b59c0771ae,
capability ux.spec. Upstream: PROVISIONAL T9013-req (US-301..310,
AC-301..310). Tokens unchanged at 1.4.0 (`design/tokens.json`; no
token edit, no brand change, no new component — L2). Fidelity lofi
(no Figma), provisional true (no brand.guide; colours verbatim from
tokens). A11y target WCAG 2.2 AA; lowest normal-text ratio measured
this run 6.25 (`color.status.error` #b3261e on #fafaf9, `ux_contrast`).
Scope per A01: this lane specifies only the session/terminal UI
impact; it edits no `.planning/` file (A03 owns ROADMAP/phases 79+).

### Wave 1 verdict: no visible change (AC-304)

AC-304 requires 0 changed session-owner assignments in Wave 1 and
spec §3 lists terminal-owner retirement as Won't-this-wave. So in
Wave 1 nothing the user sees changes: `SessionKind.Chat|Terminal`
(`src/Autonogrammer.Contracts/Agents/SessionKind.cs`), the rail
New Terminal button + Terminal tag (`AgentRail.razor`), terminal
routing (`WorkbenchView`: `IsTerminal` → `TerminalView`), the Kanban
kind text "Terminal", and the `TerminalAutoRun` settings toggle all
render exactly as today. Any Wave-1 diff that hides, renames or
reorders these is a defect against this spec, not an improvement.
Chat (ACP) remains the default kind; Wave 1 adds no badge claiming
a transport the user cannot act on (AC-302 is an inventory result,
not a screen — see no-screen table).

### Later retirement phase: plan only (AC-309)

The states below are the binding plan for the future retirement
phase A03 sequences (execution is AC-309 plan-only in this wave).
Reuse only: `TerminalView` (its existing `TerminalViewState`
Connecting/Live/Reconnecting/Ended/Unavailable + `TerminalCopy`),
`AgentRail`, `ds/Button@1.4`. No new component (a new component
would be L3 and stops for human approval).

Retirement rules A06 must implement when that phase lands:

1. Creating stops, history does not vanish. Remove `rail-new-terminal`
   from the rail (and from focus order; nothing else moves). Persisted
   Terminal rows/cards stay listed with the word tag "Terminal" plus
   a word status (Ended/Retired — never colour alone); they are never
   silently deleted and never silently converted to Chat.
2. Opening a retired terminal renders the existing Unavailable frame
   (`h2` + `role=status` reason, `error.unavailable` below) with copy
   resolved from `TerminalCopy` stating terminal sessions are retired
   and offering New Agent (Chat/ACP) as the next action. It must not
   start omp, must not offer Reconnect (Reconnect stays only for
   Ended+connection-lost, as `TerminalView` already gates it), and
   must not promise PTY transcript replay unless A03 confirms the
   backend retains it — open question below, do not invent it.
3. Steering shows the existing `SteerUnavailable` help sentence; the
   steer textarea/buttons are absent (as when no steering service is
   supplied), never disabled without a stated reason.
4. `TerminalAutoRun` leaves the tab order with its row: remove the
   toggle in the same change, with the settings page announcing the
   removal once via its live region; a stored value is ignored, not
   acted on. Do not leave a switch that does nothing.
5. Announce state changes once through the existing persistent
   `role=status` regions; focus lands on the unavailable heading on
   open and returns to the originating rail row on back/close.

## SCR-SessionTerminal — session list + terminal view (impact screen)

AC refs: AC-304 (Wave 1: 0 owner changes ⇒ UI unchanged), AC-309
(this retirement design is plan-only in Wave 1). Flows:
`authenticated`. Reuses `ds/Rail@1.4`, `ds/TerminalView@1.4`
(`Components/Terminal/TerminalView.razor`), `ds/Button@1.4`.

Interactive ids / focus order (tab stops only; the status text
`role=status` and screen `role=group` are reading-order regions,
not tab stops): `rail-new-agent`, `rail-new-terminal` (Wave 1 and
until retirement; deleted by rule 1 after), `rail-session-row-first`,
`terminal-reconnect` (present only in Ended+connection-lost),
`terminal-steer-input`, `terminal-steer-followup`,
`terminal-steer-sendnow`. Send-now is `variant: primary` but never
the autofocused default; both steer buttons stay disabled until the
box has nonblank text (existing `CanSend`).

States (drawn; `ux_state_coverage_check` complete, missing []):
`default`, `loading`, `error.network`, `error.validation`,
`error.unavailable`, `empty.session`, `success`.

```
-- default (Wave 1, unchanged; live terminal) ---------------------
| Rail: [rail-new-agent] [rail-new-terminal]                      |
|  row: title <tag Terminal> (word, not colour alone)             |
| TerminalView: status role=status "Connected..." (TerminalCopy)  |
|  screen role=group aria-label "omp terminal output" (read-only) |
|  steer group: [terminal-steer-input] [followup] [sendnow prim.] |
|  help: Steering messages go to the omp session, not the shell.  |
-- loading (Connecting / Reconnecting) ----------------------------
| status role=status "Connecting..." / "Connection lost.         |
| Reconnecting; output resumes where it stopped." No spinner-only.|
-- error.network (Ended + connection lost) ------------------------
| status "connection was lost and could not be restored." +       |
| [terminal-reconnect] (small). After 5 failed retries it stops.  |
-- error.validation -----------------------------------------------
| blank steer box: both steer buttons disabled, help text states  |
| why; steer failure writes role=status result, data-failed=true.|
-- error.unavailable (no transport today; retired in later phase)-|
| h2 "terminal is not available..." + p role=status reason from  |
| transport/retirement copy. No screen, no Reconnect, no steer.   |
| Focus -> heading; next action offered: New Agent (Chat).        |
-- empty.session --------------------------------------------------
| rail has no sessions: new-agent guidance (existing empty state);|
| after retirement there is simply no New Terminal button.        |
-- success (Live, or Ended normally) ------------------------------
| Live: chunks in order, announced once. Ended: status "The       |
| terminal has ended." — no Reconnect (that is not a failure).    |
```

Dark-pattern lint: no fake urgency or migration nagging; retirement
copy states the fact and the alternative once, in plain words.
Synthetic data only in the wireframe. All copy from `TerminalCopy` /
`AgentPanelCopy` (NFR-I3 pattern); no literals introduced.

### Explicit note — T9013 ACs with no screen (not orphans)

AC-301, AC-302, AC-303, AC-305, AC-306, AC-307, AC-308, AC-310 are
inventory/process/tracker outcomes with no user-facing surface in
Wave 1; no screen is claimed for them (verified by their named
source-grep/diff/doc checks). `ux_ac_trace_check` is therefore run
over the UI ACs AC-304, AC-309: coverage_pct 100, uncovered [],
unknown [], screens_without_refs []. Spec completeness complete
true (min_contrast 6.25 ≥ 4.5 floor, focus ids = interactive ids).

Open questions for A01/A03 (do not guess in implementation):
(a) does the backend retain any readable record of a retired
terminal session (its PTY output is streamed, not a chat
transcript); (b) OQ-1/OQ-2 from T9013-req still keep this
addendum provisional.

```json
{ "screen_id": "SCR-SessionTerminal", "flows": ["authenticated"],
  "states": ["default", "loading", "error.network", "error.validation", "error.unavailable", "empty.session", "success"],
  "components": [ { "ref": "ds/TerminalView@1.4", "props": { "variant": "existing" } }, { "ref": "ds/Button@1.4", "props": { "variant": "primary" } }, { "ref": "ds/Rail@1.4", "props": {} } ],
  "a11y": { "target": "WCAG-2.2-AA", "focus_order": ["rail-new-agent", "rail-new-terminal", "rail-session-row-first", "terminal-reconnect", "terminal-steer-input", "terminal-steer-followup", "terminal-steer-sendnow"], "min_contrast": 6.25 },
  "tokens_version": "1.4.0", "fidelity": "lofi", "provisional": true,
  "acceptance_refs": ["AC-304", "AC-309"] }
```
