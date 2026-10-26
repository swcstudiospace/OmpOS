# REPORT-2: PROBE-2, omp TUI with the four OmpOS plugins, no model turn

Date: 2026-10-26. Binary: /root/.local/share/reflex/bun/bin/omp (dist/cli.js), omp v18.12.5 (its banner says 18.12.6 is available).
No prompt was typed, no Enter was pressed, no credential was used, no omp login. Sandbox: /tmp/omp-probe2/tui (HOME=home, PI_CODING_AGENT_DIR=agent, cwd=work). Harness: Python pty 120x40 plus pyte (installed only into /tmp/omp-probe2/tui/pylib). Raw captures: /tmp/omp-probe2/tui/captures/ (main-pyte.json, filter-run2.json, flags-conc.json, summary.json).

## Launch used (no acp; all 5 plugin paths verified to exist)
argv: omp --no-extensions -e /root/src/repos/ompos/plugins/claude-ultrathink -e /root/src/repos/ompos/plugins/agent-swarm/omp -e /root/src/repos/ompos/plugins/agent-substrate/packages/projector/omp/substrate-status.ts -e /root/src/repos/ompos/plugins/agent-substrate/packages/projector/omp/substrate-skills.ts -e /root/src/repos/ompos/plugins/gsd-core-omp
env names: PATH, TERM=xterm-256color, LANG=C.UTF-8, HOME, PI_CODING_AGENT_DIR, XDG_CONFIG_HOME, XDG_DATA_HOME, XDG_CACHE_HOME, XDG_STATE_HOME (all under HOME), ULTRATHINK_UPLIFT=0, SWARM_DIR, SWARM_GSD_STATE_FILE=(empty), SWARM_CLAUDE_STATE_DIR, SUBSTRATE_OMP_PENDING_DIR, SUBSTRATE_TOKENS_DIR. ULTRATHINK_STATE_DIR was set only in the two-TUI test.

## Run table
| run | extra flags / keys | env delta | observed |
|---|---|---|---|
| R1 main | none | base | Splash logo with "press ⏎ to skip" early on, then the main UI. Two Ctrl+C one second apart did NOT exit within 5 s; SIGKILL was needed. |
| R2 slash | one "/" keystroke | base | Palette shows: security, settings, setup, plan, plan-review, vibe, goal, guided-goal, loop, plus a "skill: 413 skills" row. Window is scrollable; not all entries visible. |
| R3 filters | "/" then typed text (no Enter) | base | /swarm: swarm, ultrathink-swarm. /substrate: substrate. /gsd: gsd-core. /ultrathink: 9 rows (below). /skill: skillful, skills, skill:, reload-plugins, ultrathink-ui, ultrathink-skip, modelpreset, effort, vibe. |
| R4 exit | two Ctrl+C 150 ms apart | base | exit rc=0 in 0.14 s |
| F1 | --session-dir /tmp/omp-probe2/tui/sessions-x | base | starts, quit rc=0 in 0.41 s. sessions-x stayed empty. New file agent/custom-session-files/75cb1a4 (cause not identified). |
| F2 | --continue | base | starts fresh, no error, no session file |
| F3 | --resume nosuch-id | base | prints: Error: Session "nosuch-id" not found. then: Run `omp --resume` without an argument to pick from recent sessions, or `omp` to start a new one. Exits rc=1 at once. |
| F4 | --no-session | base | starts, no session file |
| C1+C2 | two TUIs, same cwd | A: agent, home, ultrathink-a; B: agent-b, home-b, ultrathink-b. SWARM_DIR shared. | Both alive with identical status lines. Both exit rc=0 on a fast double Ctrl+C (0.38 s). New files: agent/terminal-sessions/pts-14 and agent-b/terminal-sessions/pts-15. swarm/ and ultrathink-a|b/ stayed empty. |

## (1) Startup with NO credentials
It starts and does not block on a login screen or model picker. The composer accepts input. Quoted screen text:
- "GSD-Core not found at /tmp/omp-probe2/tui/home/.agents/gsd-core. Install: npx @opengsd/gsd-core --kimi --global (or set GSD_CORE_HOME)" (from gsd-core-omp)
- "Warning: No default model selected. Use /login, set an API key environment variable, or select a local model with /model or --model."
- "Update Available / New version 18.12.6 is available. Run: omp update"
- Status line (substrate-status, ultrathink, linear, notion, greptile): "ultrathink > substrate NOT_OBSERVED_IN_WINDOW · 1/1 observed · 1 planes down > idle > linear ⏳ notion ⏳ greptile ⏳"
- Swarm widget (agent-swarm): "○ swarm idle" and "no subagents running"
- Footer: "π > ⬢ no-model > 🗑 omp-probe2/tui/work > ◫ 10K/? ⟲" and "⇧⇥ to change thinking effort"
- The substrate "1 planes down" is a soft failure: no substrate server at 127.0.0.1:7410, and SUBSTRATE_URL was unset.

## (2) Commands visible in the slash menu (names are unique unless noted)
- agent-swarm: swarm ("Plan a brief with the AgentSwarm and dispatch a01-orchestrator in this session"), ultrathink-swarm
- substrate-status/skills: substrate ("Show Agent Substrate usage and server state")
- gsd-core-omp: gsd-core ("GSD-Core binding status (version, agents linked/missing)")
- claude-ultrathink: ultrathink-quick, ultrathink-skip, ultrathink-off, ultrathink-on, ultrathink-track, ultrathink-status, ultrathink-ui, ultrathink-swarm
- DUPLICATE: ultrathink-off appears twice. One entry has the 🧩 icon ("Turn planning off for Omp on this machine until turned on"); the other has ✎ ("Turn Ultrathink planning off in this app until turned back on"). Duplicate count: 1 name. The source of the second entry was not diagnosed.
- Built-in or other: skill:, skills, skillful, reload-plugins, modelpreset, effort, vibe, goal, plan, loop, fast, slow, computer, advisor, browser, todo, restart, settings, setup, security.
- The "skill: 413 skills" count was not traced to a source.

## (3) Flags and env for session directory and resume (from omp --help)
- --session-dir=<value>: Directory for session storage and lookup.
- PI_CODING_AGENT_DIR: Session storage directory (default ~/.omp/agent).
- -c/--continue: Continue previous session. -r/--resume=<id|path|picker>: Resume a session. --restore=<value>: resume and continue unfinished turns.
- --no-session: don't save session (ephemeral). --cwd=<value>: start dir override. --profile / OMP_PROFILE: isolated profile.
- Positional MESSAGES: an initial message starts a model turn. Not run (no model).
- Observed: a TUI start creates no session .jsonl until a message is sent. The ACP probe did create an empty jsonl per session/new.
- For launch: use --session-dir only if the session store must move; a bad --resume id is fatal (rc=1); --continue with no history is safe.

## (4) HERDR_* env
None set. Not varied, so no evidence either way. No herdr widget appeared (herdr-omp-agent-state.ts was not loaded).

## (5) Recipe and side effects
Recipe:
- argv = [OMP, --no-extensions, -e, claude-ultrathink, -e, agent-swarm/omp, -e, substrate-status.ts, -e, substrate-skills.ts, -e, gsd-core-omp]; no acp; no positional prompt.
- cwd = workspace. PTY 120x40 (omp sends DA queries ESC[c; answering ESC[?62;22c worked; DSR ESC[6n was never seen).
- env as listed above. Set ULTRATHINK_STATE_DIR per omp-managed dir (recommended by dossier, exercised only in C1+C2). SWARM_DIR must be per session (the shared dir here is a test artifact).
- Shutdown: send ^C twice within about 150 ms. Two presses 1 s apart do not exit; SIGKILL was the fallback. Ctrl+D and SIGTERM were not tested.
- Set GSD_CORE_HOME if gsd-core should bind; otherwise it prints the not-found warning.
Side effects:
- Probe processes wrote nothing under /root/.omp. Their HOME and PI_CODING_AGENT_DIR were sandboxed.
- Check: 65 files under /root/.omp have mtime after the marker. They are parent or sibling session logs, agent.db(-wal/-shm), token-opt, ultrathink session JSON and logs. Three mention omp-probe2, but they are the parent agent transcripts of these commands (w6-probes-1/2.jsonl and a run_code log), not probe output.
- No leftover sandbox omp processes after the runs.
- Writes that did happen inside the sandbox: home/.cache/bun piles (transpile cache), agent/terminal-sessions/pts-N, agent/custom-session-files/*. No stdout or stderr was written outside the pty.

## (6) Two TUIs, same cwd, different agent dir and ULTRATHINK_STATE_DIR
No interference observed at startup over about 12 s. Both shared the same SWARM_DIR and produced no writes there. Not tested: concurrent prompts, swarm dispatch, or substrate writes.

## Unknown
- Behavior after the first message (no credentials, no model).
- Whether --session-dir redirects the TUI session store. Not observed, because no message was sent.
- The source of the duplicate ultrathink-off entry and of "skill: 413 skills".
- Whether HERDR_* changes anything (not varied).
- Whether the 1 s double-Ctrl+C failure is a window-timing rule or a bug.
- Whether /annotate /green /review are absent under --no-extensions (not rechecked in the TUI).
- Whether the "Update Available" check makes a network call (the dossier saw an unexplained 443 connection).
