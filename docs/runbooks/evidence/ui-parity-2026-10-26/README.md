# Phase 50 evidence after the view restyle (step 15): `after-dark/` and `after-light/`

Date: 2026-10-26. Debug build of `apps/OmpOS.Native` after steps 5 to 14 and the fix-up of this integration pass, run under Xvfb (1440x900, no window manager, so no title bar; the main window is 1280x800 at 10,10, the Settings window 960x680) with the real omp 18.12.6 as engine. Per run a sandbox: `HOME`, `PI_CODING_AGENT_DIR` and `OMPOS_STATE_DIR` under `/tmp/f8shot/<dark|light>` (state directory deleted before each run). Dark is the default; Light is set by writing `{"theme":"light"}` to `$OMPOS_STATE_DIR/config/settings.json` (the member `FileSettingsStore` reads). No prompt was sent. Plugins: the sandbox has no `~/.agents` and no Substrate address, so GSD is "unavailable" and Substrate "off" in these shots, as the application reports them (the connected view is in `../phase-48-omp-plugins-2026-10-26/`).

## Method

```sh
# Run A (fresh state): xvfb-run -a -s "-screen 0 1440x900x24" sh runA.sh dark|light
sleep 15; import 01-empty-panel; ctrl+shift+k, 3 s, import 05-kanban-empty; ctrl+shift+a, ctrl+comma (Settings);
Down per page (Runtime, General, Agents, Models, Tab, Rules, Skills, Commands, Plugins), import after 3 s; Models after 60 s ("Loading providers from omp..." takes about 60 s);
click "Provider templates" (720,223), click "Use template" of Ollama (1011,326), scroll the form with 8 wheel clicks.
# Run B (fresh state): ctrl+n (New agent), 8 s, click the model chip (450,771), 50 s (omp starts, the model list opens) -> 02-chat-session;
Escape; ctrl+shift+t (New omp terminal), 55 s -> 03-omp-terminal-session; ctrl+shift+k -> 04-kanban-cards (a chat card and a terminal card).
# Run C (Dark only): xdotool windowsize 960 640 on the main window; 01 empty, ctrl+n, model chip (310,611), 50 s -> 02.
```
The pointer is parked at (1400,880) for each capture (no window manager: keyboard focus follows the pointer, and a pointer over a card shows its tooltip). Scripts: `scripts` are not committed; the commands above are complete.

## Files (same names in both folders)
`01-empty-panel`, `02-chat-session` (model flyout open), `03-omp-terminal-session` (omp setup wizard), `04-kanban-cards`, `05-kanban-empty`, `settings-1-runtime` to `settings-11-keyboard` (`settings-10-network` after one diagnostics run, `settings-10-network-bottom` scrolled to the last rows, `settings-11-keyboard-bottom` scrolled to the middle of the list), `settings-4a/4b/4c` (templates open, form after "Use template", form scrolled); `after-dark` also has `01-empty-panel-960x640` and `02-chat-session-960x640`.

## Defects found in the first captures and fixed in this pass
1. Primary buttons drew their label in the body text colour (white on lavender in Dark, near-black on purple in Light): the application-wide `TextBlock` foreground style beat the button's colour. The foreground setter is gone from `App.axaml` and `OmpOSAndroidApp.axaml`; `ButtonLabelColourTests` pins it.
2. `SelectableTextBlock` (pipeline fix and graph lines, settings selectable text) did not get the text-role classes (`mono`, `muted`, `small`, ...): the selectors are now `:is(TextBlock)`.
3. "No messages yet." drawn twice in an empty thread: the live announcement is now drawn invisible (still a live region) while it repeats the state heading (`SessionViewEmptyStateTests`).
4. The empty/loading/error panel state hugged the top: it is centred in the thread area.
5. The Stop button looked live while nothing runs: it is dimmed while idle (still a tab stop, so the keyboard map is unchanged).
6. Settings pages differed in left edge (the column shrank to its text): the column stretches to its 720 px maximum on every page.
7. Models: "Add API key" and "Test connection" stacked; they now share a row, the key editor opens below (tab order unchanged). The "Provider templates" expander header was a black box with a white outline and centred text; it is a raised row now.
8. Rules and Skills: "0 entries." above the empty sentence is invisible while the page is empty (still a live region).
9. Thread header Close glyph is 14 px.

## What still looks off
- Popup flyouts (model list, pipeline) show small light corner wedges at two corners: Xvfb has no compositor, so the popup window has no alpha; not seen as a defect of the control styles. Not verified on a real desktop.
- Kanban at 1280 px shows 4.3 columns; the fifth column header is cut at the scroll edge (horizontal scroll, as specified).
- Kanban cards read "unavailable" for Task, Graph, Lessons and Lease without a Substrate; fields mix DM Sans and Space Mono as the spec's id rule says.
- Light: omp's own truecolour output in the terminal uses pale greys (selected row, hint line) that are low contrast on the light terminal body; the emulator palette cannot change them.
- "2 pipeline stages are degraded" notice appears on an idle new session (Substrate off, GSD unavailable in the sandbox): accurate, but loud.
- Substrate brief wording still carries the internal reason code, "(HttpStatus, HTTP 403)" (`SubstrateContextReader`, pinned by `SubstrateStageObserverTests`).

## F9 pass: Network and Keyboard pages, review of every capture
Same recipe as above with sandbox `/tmp/f9shot`, against the build after the F9 fixes below; every file of both folders was re-captured. New pages: `End` selects Keyboard, `Up` selects Network, a click on Run diagnostics runs it once (about 20 s to finish).
The sandbox has network access, so the Network page showed **7 of 14 endpoints reachable**: the seven local endpoints (ollama-local, lm-studio-local, llama-cpp-local, vllm-local and the other localhost ones) read Unreachable ("The name did not resolve, or the connection was refused or lost", 99 to 290 ms) because no local server runs; the public endpoints read Reachable with the status they answered (HTTP 401, 404 or 301, 1.0 to 1.9 s). Rows are listed in catalogue order, no key or header is sent.

### Defects found by viewing every capture, and fixed
1. Check boxes (Agents page, Models custom provider form): the 20 px box sat 4 px above its label centre. The Compact density draws the box inside a 24 px cell (`CheckBoxMinHeight`), while the Settings styles give the control a 32 px target height. `CheckBoxMinHeight` is 32 in both theme dictionaries; `SettingsCheckBoxAlignmentTests` pins box and label to one centre line.
2. 960 px: the rail label "New omp terminal" was cut to "New omp ..." by its key chip. The chip is not drawn in the narrow rail (the tooltip and the Keyboard page keep the shortcut); `AgentPanelLayoutTests.TheTerminalButtonKeepsItsLabelWholeInTheNarrowRail`.
3. Network page: the explanation said "each endpoint listed below the result", but nothing is listed before the first run; it now says "each model provider endpoint OmpOS knows about".

### What still looks off
- Kanban at 1280 px: the fifth column header ("Needs attention") is cut at the scroll edge (horizontal scroll, columns 280 wide, as specified).
- Popup flyouts (model list) show small light corner wedges: no compositor under Xvfb. Not verified on a real desktop.
- "Use as default" (Models) and "Reset to defaults" are ghost buttons in the secondary text colour: they have no border until hovered, so they read as plain text next to bordered buttons. This is the ghost style as specified; not changed.
- Reachable and Unreachable chips on the Network page and "Found" chips on Plugins carry the word only, no status colour (consistent with the other chips).
- Models: the Anthropic row lists "Claude Haiku 4.5" twice in the model flyout; that is the omp catalogue's content.
- Without a window manager the Settings window overlaps the main window at an offset (the "Tools" tab shows behind it); not an application defect.
