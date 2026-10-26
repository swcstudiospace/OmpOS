# Phase 40 evidence: the Settings window and the composer model chip, running

Date: 2026-10-26. The Release build of `apps/OmpOS.Native` run under Xvfb with the real omp 18.12.6 as engine, in a sandbox: `HOME`, `PI_CODING_AGENT_DIR` and `OMPOS_STATE_DIR` point under `/tmp/f6shot`, so no user configuration is read or written. No model prompt was sent, no key was entered and no login flow was started. The sandbox has no omp credential of its own; the machine environment holds one provider variable, which is why the Models page shows Anthropic as "credential present in omp". The screenshots are the whole 1440x900 virtual screen. Xvfb runs no window manager, so windows have no title bar and the Settings window (1000x720) sits at its centred position over the main window (1280x800) at the top left.

## Method

```sh
mkdir -p /tmp/f6shot/home/.omp/agent /tmp/f6shot/state
cd /root/src/repos/ompos
HOME=/tmp/f6shot/home PI_CODING_AGENT_DIR=/tmp/f6shot/home/.omp/agent OMPOS_STATE_DIR=/tmp/f6shot/state \
  xvfb-run -a -s "-screen 0 1440x900x24" sh -c '
    ./apps/OmpOS.Native/bin/Release/net10.0/OmpOS.Native > /tmp/f6shot/app.log 2>&1 & APP=$!
    sleep 15                                  # main window up
    import -window root 00-main-window-empty.png
    xdotool key ctrl+comma; sleep 4           # Settings window, Runtime page
    import -window root settings-1-runtime.png
    xdotool key Down; sleep 3                 # one Down per page: General, Agents, Models (wait 10 s), Tab, Rules, Skills, Commands, Plugins
    import -window root settings-2-general.png   # and so on
    kill $APP'
```

- Models page with a template: wait 28 s after the page opens (the page runs `omp models --json` and one `omp token <provider>` per provider; "Loading providers from omp..." shows until then), click the "Provider templates" header at (760,175), then "Use template" of the first card (Ollama) at (1030,299); the form scrolled with mouse wheel clicks. "Add provider" at (1065,123) opens the blank form.
- Composer chip: no Settings window; click "New agent" at (150,220), wait 12 s, click the "Model" chip at (342,768).
- Every run ends with `kill $APP`; afterwards no `OmpOS.Native`, `omp` or `sleep` process of the run was left (one unrelated Xvfb of another job, started a day earlier, was already there).

## Files

| File | What it shows |
|---|---|
| `00-main-window-empty.png` | The main window with no session: rail (Sessions, Tools, New agent, New omp terminal, filter, Settings), empty state. |
| `settings-1-runtime.png` to `settings-9-plugins.png` | The nine Settings pages in navigation order: Runtime, General, Agents, Models, Tab, Rules, Skills, Commands, Plugins. Footer buttons differ per page (Reset to defaults, Discard changes and Save only where the page edits something). |
| `models-a-templates-open.png` | Models page with the "Provider templates" expander open: Ollama, LM Studio, llama.cpp, vLLM cards with "Use template". |
| `models-b-template-picked-top.png`, `models-d-template-picked-bottom.png` | The add-provider form after "Use template" on Ollama (local): name, id, protocol, endpoint, authentication prefilled; model ids, supported routes, plain-http warning, Save and Cancel below. |
| `models-c-add-provider-blank.png` | The blank add-provider form from the "Add provider" button. |
| `chat-a-new-agent-session.png` | A chat session just created with New agent: header, pipeline strip, empty transcript, composer with the Model chip. |
| `chat-b-model-chip-flyout.png` | The Model chip flyout. The sandbox omp has no credential, so no model list: the flyout shows the load error. Taken after the width fix described below. |

## What looks wrong (findings)

1. Model chip flyout: its status line was clipped at the flyout edge ("...requires authent"). Cause: the flyout grid had only a `MinWidth`, so the wrapping text never got a width limit. Fixed in `src/OmpOS.Native.Views/Views/ModelPickerView.axaml` (`Width` of the stage flyout width, as the pipeline flyout does); the screenshot shows the fixed result.
2. Model chip flyout, wording: the error reads "omp acp requires authentication but no AuthMethodId is configured on AcpClientOptions." That is an internal message shown to the user. Real omp advertises `authMethods: [agent]` in the sandbox, and the head never sets `AuthMethodId`, so a chat session cannot open without an existing credential (see the report; not changed here).
3. Unthemed buttons: "New omp terminal" and "Settings" in the rail, "Skip to conversation", Close, Reset to defaults, Cancel, "Add API key", "Test connection", "Use as default", "Use template" are plain white boxes with a thin border and, in the rail, text pinned to the top-left without padding, next to the themed purple "New agent" and "Save". The Sessions and Tools toggles use a blue and grey that match neither.
4. "Skip to conversation" is visible at the top left of the main window without focus.
5. Page titles (Runtime, General, ...) render in a narrow light face unlike the body font.
6. The empty transcript says "No messages yet." twice (small line, then the heading).
7. Models page: "Add API key" and "Test connection" stack vertically instead of sitting side by side; a stray one pixel vertical bar shows at the top right of the page (x about 1147).
8. Rules and Skills say "0 entries." above the empty-state sentence, which repeats the information.
9. The Stop button of the composer looks enabled while nothing runs.
10. The plain-http warning of the add-provider form is shown for a localhost endpoint where it does not apply.
11. The first Models load took longer than 10 s in this sandbox (cold omp), with only "Loading providers from omp..." on the page.
12. The app log shows `omp acp pump failed for session (null)` at Error level for the failed handshake of finding 2: a failure the session caused by closing itself is logged as a pump fault.
