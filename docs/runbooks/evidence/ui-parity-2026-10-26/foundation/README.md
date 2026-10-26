# Phase 50 foundation evidence (steps 1 to 4 and 6), Dark

Date: 2026-10-26. Debug build of `apps/OmpOS.Native` after the foundation steps (static fonts, tokens, control library, Dark default and theme setting, TargetMin 32), under Xvfb with the real omp as engine in a sandbox: `HOME`, `PI_CODING_AGENT_DIR` and `OMPOS_STATE_DIR` under `/tmp/found-shot`. No prompt was sent. The views are not yet restyled (steps 5 and 7 to 14), so this is the foundation only.

## Method

```sh
HOME=/tmp/found-shot/home PI_CODING_AGENT_DIR=/tmp/found-shot/home/.omp/agent OMPOS_STATE_DIR=/tmp/found-shot/state \
  xvfb-run -a -s "-screen 0 1440x900x24" sh -c '
    ./apps/OmpOS.Native/bin/Debug/net10.0/OmpOS.Native & APP=$!
    sleep 15;  import -window root 01-empty-panel.png
    xdotool key ctrl+shift+k; sleep 3; import -window root 05-kanban-empty.png
    xdotool key ctrl+shift+a; xdotool key ctrl+comma; sleep 4; import -window root 06-settings-runtime.png
    kill $APP'
# second run, new state dir: xdotool mousemove 150 180 click 1 (New agent), sleep 12, 02; mousemove 150 220 click 1 (New omp terminal),
# 03 needs 50 s on this loaded machine (12 s showed a blank terminal because omp had not started); ctrl+shift+k, 04; ctrl+comma, Down, 07 (General)
```

Windows have no title bar (no window manager); the Settings window opens at the top left over the main window. Wait times matter: omp takes up to 50 s to draw in this sandbox.

## Files
01 empty panel, 02 chat session (new, empty), 03 omp terminal session (omp setup wizard), 04 Kanban with two cards, 05 Kanban empty, 06 Settings Runtime, 07 Settings General (new Theme list).

## What still looks off (for the view agents)
1. Skip link visible at rest in every shot (step 5).
2. No top bar, workspace label or window chrome; tabs sit in a plain row (step 5).
3. Rail: toggles, New agent, Filter use the new controls but rows are not the 48 px two-line anatomy; Settings button is a full-width secondary button, not the footer icon (step 7).
4. Thread header, notice, pipeline strip and "Latest" line are still four stacked bands; header buttons (Close, Show pipeline, Dismiss notice) are tall secondary buttons (step 8).
5. Composer: send is a bare outlined glyph, stop a white square, model chip and hint line still under the box; not a rounded card (step 10).
6. "No messages yet." still shown twice (step 8/14).
7. Terminal: framed block and status bar not yet styled; steer composer unchanged (step 11). Terminal ANSI palette is applied through the emulator theme; omp's own truecolour output is unaffected, so the effect is not visible in 03.
8. Kanban: column headers are Anybody 13 px (breaks the 20 px floor) and the fifth column is cut at the right edge; cards show every "unavailable" row; Steer is a 40 px tall button; chips outlined (step 12).
9. Settings: footer buttons are about 50 px high because SettingsStyles gives every Button padding 16 on all sides and MinWidth TargetMin; the nav is raised-grey; page headings use Anybody at 20 (ok); Theme and Motion lists are plain ListBox rows (step 13).
10. Prose size is 13 in several views that name no size; Body 14 is only used where the token is bound.
