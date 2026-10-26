# REPORT-3: PROBE-3, does real omp run the OmpOS plugin extension, write the connection report and serve the heartbeat?

Date: 2026-10-26. Binary: /root/.local/share/reflex/bun/bin/omp (ACP initialize agentInfo.version 18.12.5).
No model prompt was sent, no message was typed, no Enter on a message, no credentials, no omp login. Probe sandboxes: /tmp/omp-probe3/sb-acp (ACP) and /tmp/omp-probe3/sb-tui (TUI), HOME and PI_CODING_AGENT_DIR inside each. Session dirs: /tmp/omp-probe3/acp/session/{connection,steer} and /tmp/omp-probe3/tui/session/{connection,steer}; the parents were created 0700 before launch.

Launch: omp [acp] --no-extensions -e claude-ultrathink -e agent-swarm/omp -e substrate-status.ts -e substrate-skills.ts -e gsd-core-omp. Base env as in PROBE-2 plus OMPOS_CONNECTION_FILE=<session>/connection/report.json and OMPOS_STEER_DIR=<session>/steer. strace was used for the ACP run only.

## (A) ACP mode
- initialize ok (agentInfo.version 18.12.5); session/new ok (modes default, plan). stderr empty; no agent-to-client requests.
- report.json written at 2026-10-26T13:17:07.627Z (session_start). Mode 0600 inside a 0700 dir, 17536 bytes. Unchanged at 8 s and 14 s after session/new.
- alive.json: mode 0600, pid 2713974 (same pid as the report), startedAt 13:17:07.608Z, mode rpc. lastSeen 13:17:12.628Z (age 3.33 s at snapshot 8 s) and 13:17:17.643Z (age 4.32 s at snapshot 14 s, 6 s apart). The 5 s refresh cadence holds.
- inbox, acks, done exist as 0700 dirs and are empty.

## (B) TUI mode (Python pty 120x40, no acp, no prompt)
- Run 1: two Ctrl+C 50 ms apart did NOT exit within 5 s; omp was killed with SIGKILL. Kept as run1-tui-result.json.
- Run 2 (same, 150 ms apart): exit rc 0, 0.164 s after the second press. Data below is run 2 (tui-result.json).
- Silence: 45453 raw pty bytes, zero occurrences of OMPOS, OmpOS, ompos, steer, inbox, alive.json, heartbeat, connection report, report.json. The 8 s screen (tui-screen-8s.txt) has 24 non-empty lines and none match. Visible lines are omp itself (Update Available 18.12.6, no default model) and other plugins status lines (ultrathink/substrate status, swarm idle, no subagents running).
- Heartbeat: alive.json mode 0600, pid 2726825 (equal to the omp pid), startedAt 13:19:04.507Z, mode tui. lastSeen 13:19:09.508Z (age 0.15 s at 8 s) and 13:19:14.508Z (age 1.15 s at 14 s). Run 2 reused the run 1 steer dir, so it overwrote alive.json; directory mtimes are from run 1 and the content is from run 2.
- Report: mode tui, pid 2726825, 28 tools, 425 commands, commandsAvailable true, omp.version null.

## (C) Names and fields
Commands in the report (identical in ACP and TUI): 425 entries = 413 skill:* entries + 12 extension names: autoresearch, gsd-core, substrate, swarm, ultrathink-off, ultrathink-on, ultrathink-quick, ultrathink-skip, ultrathink-status, ultrathink-swarm, ultrathink-track, ultrathink-ui.
Tools, ACP (27): ast_edit, bash, debug, edit, eval, glob, goal, grep, init_experiment, log_experiment, lsp, read, run_experiment, swarm_assignment, swarm_catalog, swarm_gate, swarm_ingest, swarm_plan, swarm_status, swarm_tool_call, swarm_transition, task, todo, update_notes, wait, web_search, write.
Tools, TUI (28): the same 27 plus ask.
Full lists: /tmp/omp-probe3/lists/{acp,tui}-{tools,commands}.txt. The ACP session/update available_commands (add-dir, advisor, compact, model, and so on) is omp built-in and is NOT part of the plugin report.
- Ultrathink commands ultrathink-quick, ultrathink-skip, ultrathink-off, ultrathink-on, ultrathink-track, ultrathink-status, ultrathink-ui, ultrathink-swarm: all 8 present in both modes.
- Swarm command swarm: present. Swarm tools swarm_status, swarm_plan, swarm_assignment, swarm_ingest, swarm_transition, swarm_gate, swarm_catalog, swarm_tool_call: all 8 present in both modes.
- Substrate command substrate: present. GSD-Core command gsd-core: present.
- omp.version is null in both reports (the plugin passes version null: omp exposes no version to extensions), although ACP initialize reports 18.12.5.
- mode strings: ACP reports rpc; TUI reports tui. The same strings appear in alive.json.

## (D) Subagents
No subagent was spawned (no prompt was sent), so the skip path was not exercised. Each run wrote exactly one pid: report and alive.json shared the same pid across the 8 s and 14 s snapshots, and in the TUI run that pid equals the omp process pid. The files alone cannot confirm the subagent skip.

## (E) Surprises and side effects
- Source reading (not runtime-tested): writeConnectionReport in src/connection-report.mjs never creates its parent directory; only the steer start() does mkdir. If the host does not create the connection parent, the report is silently not written. Both runs here had the parent pre-created.
- The gap between the two Ctrl+C presses matters: 50 ms did not exit in run 1; 150 ms exited with rc 0 in run 2.
- The sandbox TUI showed no GSD-Core not-found warning (PROBE-2 did). Not investigated.
- Files under /tmp/omp-probe3: 332 files newer than the marker, all inside /tmp/omp-probe3: the two sandboxes (Bun transpile cache piles under home/.cache/bun/@t@, agent/agent.db, models.db, skill-descriptions.db, a session .jsonl of 583 bytes), the two session dirs, the lists, and the result files.
- ACP strace (acp-strace.txt): 208 write-like lines, none with an absolute path outside sb-acp or acp/session. Most are fd-relative Bun cache piles (their names match the find listing). One write goes to /sys/kernel/debug/tracing/trace_marker, a kernel trace marker from omp.
- /root/.omp: 40 files newer than the marker; 17 remain after excluding *.jsonl under sessions and any path under logs: agent/agent.db (-shm, -wal), token-opt/*.json (13 incl. live.json), ultrathink/sessions/01a16365-90fc-77f5-b689-b4491206f2a7.json, and sessions/.../3264.bash.log and 2231.run_code.log. None of the 17 contains omp-probe3 or OMPOS. Their ompos hits are the repo path in the parent agent DB and logs. These are the parent agent activity, not probe output. ULTRATHINK_STATE_DIR was unset; by content, the probe runs left no ultrathink state under /root/.omp.
- The TUI run was not straced; its side effects were checked only by find on /tmp/omp-probe3 and the /root/.omp scan.

## Files
/tmp/omp-probe3/: MARKER, acp-result.json, acp-strace.txt, acp/session/{connection,steer}, tui-result.json, run1-tui-result.json, tui-screen-8s.txt, run1-tui-screen-8s.txt, lists/, evidence-e.json.
