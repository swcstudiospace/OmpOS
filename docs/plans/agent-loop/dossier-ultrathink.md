# Dossier: ~/src/repos/claude-ultrathink (read-only discovery, 2026-10-26)

Paths below are relative to `~/src/repos/claude-ultrathink` ("UT") unless absolute. omp = `~/.local/share/reflex/bun/install/global/node_modules/@oh-my-pi/pi-coding-agent` (omp 18.12.5, `omp --version` -> `omp/18.12.5`; bun 1.4.0). Nothing was built, installed, run or modified. Secrets masked.

---------------------------------------------------------------------------------------------------
## 1. Identity

| fact | value |
|---|---|
| remote | `origin https://github.com/swcstudiospace/claude-ultrathink.git` (fetch+push). `gh repo view` reports the canonical repo is now **`swcstudiospace/ultrathink`** (renamed; old URL redirects; PR check URLs use `/ultrathink/`). |
| branch | `feat/v1.16-code-quality-sweep` (tracks `origin/feat/v1.16-code-quality-sweep`) |
| HEAD | `00ce3e6be70d1277934e39515904b55403b9b56d` "test: address the Greptile findings on PR 59" (`git describe` = `v1.15-35-g00ce3e6`) |
| `git ls-remote origin HEAD` | `7cfd05423e9d87c7b6afd918d2feb58a8b2b645c HEAD` (= `refs/heads/main`) |
| `git ls-remote origin refs/heads/feat/v1.16-code-quality-sweep` | `00ce3e6be70d1277934e39515904b55403b9b56d` -> HEAD **is on the remote**, but ONLY on that feature branch |
| `git branch -r --contains HEAD` | `origin/feat/v1.16-code-quality-sweep` only. `origin/main` = `7cfd054` (PR #58 merge, 2026-10-25); HEAD is **11 commits ahead of main, 0 behind**; merge-base = `7cfd054`. |
| PR | #59 OPEN, non-draft, MERGEABLE, base `main`. All 11 checks pass (`gh pr checks 59`: CodeQL x2, "Check and test (Bun 1.2.x)", "(Bun latest)", Dependency audit, License policy, Policy checks, Semgrep x2, Greptile Review). |
| diff main..HEAD | 449 files, +47913/-13286 (Biome lint/format sweep, knip dead-code, test clock removal, `noExplicitAny`). `git diff --stat origin/main..HEAD -- src/host/omp.ts package.json` prints nothing: **`src/host/omp.ts` and `package.json` are byte-identical on `main` and HEAD**. `src/host/{progress,types,view}.ts` do differ. |
| tags | `v0.3.0 v0.4.0 v1.0 v1.1 v1.2 v1.3 v1.5 v1.6 v1.7 v1.8 v1.9 v1.14 v1.15` + `archive/*`. `git tag --contains HEAD` is empty: no tag at/after HEAD (35 commits past `v1.15` = `f8612af`). |
| package version | `package.json` `"version": "1.4.0"` (private); marketplace/plugin manifests also 1.4.0 (version string does NOT track git tags). |

**Pin guidance:** `00ce3e6` exists on the remote *now* but lives on an open PR branch; a squash-merge + branch delete would orphan it (submodule fetch of an unreachable sha can fail). `7cfd054` (main) is stable and has the identical omp extension entry (`omp.ts`, `package.json` unchanged). Recommendation: pin `7cfd054` now; re-pin after PR 59 merges if the lint/dead-code sweep is wanted.

### Dirty state (`git status --porcelain` = 4 top-level entries, ALL untracked, 0 modified tracked files; `-uall` = 36 files — not 8)
```
?? .cursor/            -> .cursor/mcp.json (1 file)
?? .grok/              -> .grok/agents/a01..a30*.md (30), .grok/config.toml, .grok/hooks/agent-swarm.json
?? .mcp.json
?? .omp/               -> .omp/config.yml
```
Characterisation: these are **machine-local projector outputs** of agent-substrate/agent-swarm, not ultrathink source:
- `.mcp.json` and `.cursor/mcp.json`: `{ "mcpServers": { "substrate": { "type":"http", "url":"${SUBSTRATE_URL}/mcp", "headers":{"Authorization":"Bearer ${SUBSTRATE_TOKEN}"} } } }` (placeholders only).
- `.grok/config.toml`: comment "substrate-mcp, projected by agent-substrate packages/projector" + `[mcp_servers.substrate] url = "${SUBSTRATE_URL}/mcp"`.
- `.grok/hooks/agent-swarm.json`: `UserPromptSubmit` command `python3 /var/tmp/ut-verify/wt-swarm/hooks/user_prompt_submit.py` (hard-coded local path).
- `.omp/config.yml`: `extensions:\n  - ~/src/repos/agent-swarm/omp\n  - /var/tmp/ut-verify/wt-swarm/omp` (project-level omp config that loads the agent-swarm omp extension when omp runs with cwd = this checkout).
A submodule checkout of the pinned commit will NOT contain any of these. Ignored (not dirty): `.claude/ .gsd/ .hermes/ .planning/ .pytest_cache/ .ruff_cache/ .swarm/ node_modules/ hosts/*/__pycache__ hosts/prime-agent/.venv`. `.swarm/` here contains `events.jsonl` (6.9K) and `tasks.db` (28K) (agent-swarm runtime state; unrelated to ultrathink).

---------------------------------------------------------------------------------------------------
## 2. omp plugin shape

### package.json (UT/package.json)
```json
"name":"ultrathink","version":"1.4.0","private":true,"type":"module","license":"AGPL-3.0-or-later",
"engines":{"bun":">=1.2"},
"omp":{"extensions":["./src/host/omp.ts"]},
"pi":{"extensions":["./src/host/omp.ts"]},
"devDependencies":{"@biomejs/biome":"2.6.3","@oh-my-pi/pi-ai":"^18.12.1","@oh-my-pi/pi-catalog":"^18.12.1","@types/bun":"^1.4.3","knip":"6.43.0","yaml":"2.9.1"}
```
No `dependencies`. Scripts: `test`=`bun test`; `check`=`bunx --package typescript@5.9.3 tsc --noEmit`; `check:services`; `lint`=`biome check --error-on-warnings .`; `deadcode`=`knip --include exports,types`; `verify`=check+lint+deadcode+test+2 python bridge tests; `hook:uplift`/`uplift`=`bun hooks/uplift.ts [--ctl]`; `mcp`=`bun src/mcp/cli.ts`; `ship`=`bun src/ship/cli.ts`; `setup`=`bun scripts/setup.ts`; `policy:*`, `compliance`, `chaos`.

### Entry: `src/host/omp.ts` (2390 lines, `export default createOmpExtension()` at 2390; factory `createOmpExtension(options)(pi)` at 1069-2388)
Registered on `pi` (the omp `ExtensionAPI`):

**Events (`pi.on`)** — line / what / when
| event | line | behaviour |
|---|---|---|
| `before_agent_start` | 2289 | THE planning hook. Fires for every submitted user prompt (before the model turn). Skips subagents; handles `/ultrathink-quick` bypass; optional swarm auto-run; captures config+control+live Model; launches or reuses a per-session "flight" (planner promise); **races** the plan against `raceMs=25000` ms (omp caps handlers at 30 s: `runner.ts EXTENSION_HANDLER_TIMEOUT_MS=30_000`). Fast plan -> returns `{message: planMessage(...)}` inline. Slow plan -> returns `{message: pendingMessage()}` now and delivers the plan later with `pi.sendMessage(planMessage, {deliverAs:"aside"})`. No plan/skip -> returns undefined (prompt untouched). Whole handler wrapped in try/catch -> `return;` (fail-open). |
| `session_start` | 1749 | attach TUI bar widget; start 2 s activity-receipt poll (TUI only) |
| `session_switch` | 1757 | cancel all flights, close dashboard, re-attach |
| `session_shutdown` | 1768 | `shutDown=true`, cancel flights |
| `agent_start, agent_end, turn_end, tool_execution_start, tool_execution_end` | 1776-1787 | `observeFlights(ctx)` (cancel flights whose session/cwd/live-model changed) + remount bar |
| `turn_start` | 1788 | increments per-session `turns` counter (submission identity) |
| `tool_result` (x2) | 1799, 1816 | (a) edit/write attribution: `recordEditAttribution` -> adds `workRepos`/`repoMismatch` to the session record, `ctx.ui.notify(warning)`; (b) PR-sync nudge: when `github{op:"pr_create"}` or a PR-creating bash tool succeeds for a planned session -> `pi.sendMessage({customType:"ultrathink-sync", ...}, {deliverAs:"aside"})` telling the agent to run skill `ultrathink-sync` with `stateFile=` and `prUrl=` |
| `agent_end` | 1898 | (a) Teachable Moments capture: detached `teach observe` spawn (`spawnObserveDetached`) if `teach.enabled` and capture != explicit; (b) ship nudge: for a planned `gsd-*` skill run with committed work on a feature branch -> `pi.sendMessage({customType:"ultrathink-ship"}, {deliverAs:"aside"})` once per graph (only if `ship.enabled`) |
| `pi.events` bus (in-process) | 1164, 1182 | listens `substrate:status` and `token-opt:savings`; emits `substrate:status-request` |

**Slash commands (`pi.registerCommand`)** — 1937-2072
- `/ultrathink-quick|skip|off|on|track|status` (verbs from `src/uplift/commands.ts:45`); registered as `ultrathink-<verb>`. Subagent sessions forward the text as a user message. `quick <msg>`: arms `quick={sessionId,text}` then `pi.sendUserMessage(text)` -> the next `before_agent_start` for exactly that text returns early (no plan, no rows). Other verbs call `runControl([...])` and show the reply with **`ctx.ui.notify(reply,"info")`**.
- `/ultrathink-ui [overview|jev|moments|skills|card]` — native TUI dashboard via `ctx.ui.custom`; `card` publishes a summary card (`pi.sendMessage`, `triggerTurn:false`).
- `/ultrathink-swarm <brief>|<brief>|… | status` — fans out detached AgentSwarm lanes (see §6).
(`getArgumentCompletions` for `track` and `ui`.)

**Custom message types** (`src/host/omp-render.ts:18-33`): `ultrathink-plan`, `ultrathink-pending`, `ultrathink-sync`, `ultrathink-ship`, `ultrathink-insight`, `ultrathink-swarm`, `ultrathink-activity` (delivery `{deliverAs:"nextTurn", triggerTurn:false}`). `registerMessageRenderer` is used only when `pi.registerMessageRenderer` exists and `uiEnabled`.
**Widgets:** `ui.setWidget("ultrathink", factory, {placement:"aboveEditor"})` status bar (store: `BarStore`, `src/host/omp-ui.ts:32-92`). **No tools are registered** (no `pi.registerTool`).

### How the plan reaches the model (answer)
NOT a prompt rewrite and NOT a system-prompt addendum. `before_agent_start` returns `{ message: CustomMessage }` where
```
{ customType:"ultrathink-plan", content:<plan context markdown>, display:true, details:<PlanView>, attribution:"agent" }
```
omp (`session/agent-session.ts` ~8180-8230, `runner.ts emitBeforeAgentStart` 2165-2215) turns each returned `message` into a `role:"custom"` AgentMessage appended to the turn's context after the user's message. The user's typed text is sent to the model **unchanged** (extension cannot rewrite it — so a `raw:`/`uplift:` prefix typed by the user stays visible to the model; the prefix is stripped only inside the planner). `systemPrompt` is never returned. The plan text is `formatPromptContext()` (`src/claude/output.ts:446`): header `## Prompt Uplift` + "Specification file: <abs path to sessions/<id>.xml>" + planning-model line + substrate brief + recalled lessons/docs/skills + (THINK addendum, "Workflow waves: 1: n1 · 2: n2, n3 (parallel) …") + Linked issues + clarification (HITL) addendum + `## Ultrathink tracking` instructing the agent to **invoke skill `ultrathink-kickoff` with `stateFile=<path>` before coding** + optional `## Ship`. Real sample from a session JSONL: 84,265 chars. If planning is slow: `PENDING` text = "Ultrathink is still planning this prompt… read and investigate only: no edits and no mutating commands…" (omp.ts:344). On failure/no plan: `NO_PLAN` = "Ultrathink planning produced no plan. Continue with the user's request as written." (only when the outer limit timed out).

### CRITICAL: does the hook run under `omp acp` (stdio JSON-RPC) with a non-TUI client? -> YES (verified in omp source), with caveats
Evidence (omp src, read-only):
- `main.ts:2334-2362`: `omp acp` -> `createAcpSessionFactory` -> per `session/new`: `SessionManager.create(cwd)` (persisted session file, `agentId=acp:<sessionId>`), full extension discovery (unless `--no-extensions`), `enableMCP:false`.
- `modes/acp/acp-agent.ts:2558-2650` `#configureExtensions`: `createAcpExtensionUiContext(...)`, `extensionRunner.initialize(..., uiContext, "rpc")`, then `emit({type:"session_start"})`. `ExtensionRunner.hasUI()` = `uiContext !== noOpUIContext` (`runner.ts:1085`) -> **`ctx.hasUI === true`, `ctx.mode === "rpc"`** under ACP.
- `isSubagentSession(ctx)` (omp.ts:833-849, doc 818-832) returns true ONLY when: session file nested `<parent>/<Agent>.jsonl`; or file inside temp lease dir `omp-task-<n>`/`omp-eval-agent-<n>`; or `hasUI===false` AND (no session file OR header has `parentSession`). ACP top-level sessions have `hasUI` true and a real session file -> **false -> planning runs**. (A `omp -p --no-session` run has hasUI false + no file -> treated as subagent -> NOT planned. `omp rpc --no-ui` likewise.)
- `planPrompt` extra subagent guard (`src/host/plan.ts:163-171`): for host `omp`, scans `${PI_CODING_AGENT_DIR||~/.omp/agent}/sessions/*/*/*.jsonl` (mtime < 12 h) for a `"type":"session"` header whose `id` equals the session id (`src/host/omp-session.ts:43`) = id exists one directory deeper -> `skip("subagent")`. A normal ACP session file lives at `sessions/<cwd-slug>/<ts>_<id>.jsonl` (depth 2 -> glob `*/*/*.jsonl` would need depth 3) so it is not matched.
- In ACP, session id seen by the client (`NewSessionResponse.sessionId`, acp-agent.ts:697 = `record.session.sessionId`) **equals** the id the extension reads via `ctx.sessionManager.getSessionId()` -> state file `sessions/<that id>.json` (id kept verbatim if it matches `^[a-z0-9_.-]{1,120}$`, which omp UUIDv7 ids do; else slug+`~`+sha256[:16], `src/claude/state.ts:139-153`).
Caveats under ACP (all `UNKNOWN` until a live `omp acp` run; commands to resolve in each line):
1. **Slash-command replies are invisible**: `acp-agent.ts:572` `notify` only `logger.debug`s; `setStatus/setWidget/custom/setFooter` are no-ops. `/ultrathink-status|off|on|skip|track` replies (sent via `ctx.ui.notify`) never reach an ACP client. Their side effects (control.json / `.skip` file) do happen. `/ultrathink-ui` returns the "unsupported host" read-only snapshot text through `pi.sendMessage(... triggerTurn:false)`.
2. **Custom messages are not live-streamed over ACP**: `acp-event-mapper.ts` maps only assistant `message_update/message_end`, tool events, todos etc.; `role:"custom"` messages appear only in `session/load` replay (`#messageToReplayNotifications`, `acp-agent.ts:2320`, as `user_message_chunk`, text only, `details` dropped). So an ACP client will NOT see the plan/pending/sync/ship messages in the stream. Observe them via the state files (§3) or the session JSONL (`type:"custom_message"`, see §3).
3. Deferred ("aside") plan delivery after `agent_end`: comments in omp-render.ts/omp.ts say `deliverAs:"aside"` can start a turn when idle; effect on an open `session/prompt` turn under ACP is unverified -> `UNKNOWN: drive 'omp acp' with a prompt that takes >25 s to plan and watch session/update + stopReason`.
4. `startActivityPoll` runs only when `ctx.mode==="tui"` (omp.ts:1707) -> no activity receipt cards under ACP. The TUI bar widget is a no-op under ACP.
5. Planning blocks the first model token by up to 25 s (race) — fully async beyond that. Typical full plan takes minutes (Graph + one CoT call per node + HITL clarify + tracker rows).

### How it decides to SKIP (all paths)
Order inside `before_agent_start` (omp.ts:2289-2386) then `planPrompt` (`src/host/plan.ts:107-326`) then `runPromptSubmit` (`src/claude/hook.ts:543`):
1. `isSubagentSession(ctx)` -> return (omp.ts:2291).
2. `/ultrathink-quick <msg>` armed and same session+text+submission -> return (no plan) (2298-2309).
3. `maybeAutoRun` (swarm.autoRun + SDLC verb prompt + `ULTRATHINK_SWARM_ROOT` valid + not already laned) -> spawns a detached AgentSwarm lane, arms `quick`, **returns without a plan** (2242-2318).
4. `skipReason()` (plan.ts:94): env `ULTRATHINK_UPLIFT=0` OR `ULTRATHINK_CHILD=1` OR `isChildInvocation()` -> `"child-or-disabled"`; `request.parent_session_id` -> `"parent-session"`; `platform==="cron"` -> `"cron"`; blank prompt -> `"empty"`. (`env` = `{...process.env, ULTRATHINK_HOST:"omp"}` -> so it is the omp process env.)
5. host omp: session id found as nested subagent file (above) -> `"subagent"`.
6. `parseUltrathinkCommand(text)` (`/ultrathink-<verb>`, `/ultrathink:<verb>`, `/ultrathink <verb>`) -> `"ultrathink-command"`.
7. `planningTarget` (`src/uplift/skill.ts:371`): skips `nested-child` (prompt starts with `<!-- ultrathink-child-prompt -->`), ultrathink's own skills (`ultrathink-kickoff|sync|plan|ship|teach`), `/ultrathink*` commands, and any other slash command with no skill file (`"slash-command"`). **`/gsd-*` and `/skill:gsd-*` are planned even with no skill file installed** (`gsdPlanningTarget`, skill.ts:331,395-401); omp skill scaffold `[IMPORTANT: User invoked the "<name>" skill; follow its instructions. Full skill below.]` (skill.ts:55) is unwrapped to the user instruction.
8. `decideUplift` (`src/uplift/detect.ts:53`) with `UpliftState{enabled: control.enabled ?? config.uplift.enabled, skipOnce: <session .skip flag exists>, skipTrivial: config.uplift.skipTrivial(true)}`: `raw:` prefix -> passthrough (-> `"precheck-passthrough"`); `/cmd` or `<command-name>` -> skip; already-uplifted XML root (`BUILD_PROMPT|FIX_PROMPT|RESEARCH_PROMPT|CHANGE_PROMPT|UPLIFTED_PROMPT|uplifted|ultrathink`) -> skip; text containing `graph ut-<base36>-<8hex>` (unless `uplift:`) -> skip; armed skip-once -> skip (flag consumed/cleared); disabled and no `uplift:` prefix -> skip; trivial ack (`yes|ok|continue|go|lgtm|…`) -> skip. `uplift:` prefix forces planning (even when off / trivial). Results surface as `skipped: "precheck-skip" | "precheck-passthrough"`.
9. Jev plan gate `planGate` (hook.ts:645): if an OpenRouter/Vercel key resolves and `decisions.points` contains `plan`, P(plan_worthy) < 0.2 -> `skipped:"jev-plan-skip"` with an echo notice; no key / error -> fail-open (plan proceeds).
10. Engine selection: `native-unavailable`/unresolved model -> `skipped: <reason>` + notice "Prompt Uplift skipped · <label>".
11. Uplift LLM throw -> `"uplift-failed"`; cancellation -> `"aborted"`; other -> `"engine-error"`.
Programmatic per-run controls for an external host: see §3.

### Directories
- `skills/` (6 SKILL.md): `ultrathink-plan` (read plan/last-plan.json), `ultrathink-kickoff` (finish tracker rows via `ultrathink-mcp track complete`, HITL questions, optional `graph_register` to an MCP server named `substrate`), `ultrathink-sync` (update rows with PR), `ultrathink-ship` (assess -> PR -> Greptile 5/5 -> merge), `ultrathink-teach` (Teachable Moments), `ultrathink-cursor` (hand off to a01-orchestrator, Cursor only).
- `commands/` (6 Claude-style md): `ultrathink-{off,on,quick,skip,status,track}.md` (use `${CLAUDE_PLUGIN_ROOT}`).
- `hooks/`: `hooks.json` (Claude/Grok/Muse hooks: `UserPromptSubmit`->`hooks/uplift.ts` (timeout 86400), PostToolUse `AskUserQuestion`->`answers.ts`, bash->`pr-sync.ts`,`flow-eval.ts`, Skill->`flow-eval.ts`, Stop->`stop.ts`), `engine.ts` (child engine for Hermes/Muse; the only user of `ULTRATHINK_PROGRESS_FD`), `muse-{prompt,post-tool,stop}`, `discovery-manifest.helpers.ts`. **Not used by omp** (omp plans in-process).
- `hosts/`: `cursor/` (`ultrathink-cursor-plan.js` beforeSubmitPrompt hook + `commands/`, `gsd-commands/`), `grok/ultrathink.md`, `grok-bot/` (README, commands, remote, skills, ultrathink-protocol), `hermes/` (python plugin `bridge.py`, `plugin.yaml`), `prime-agent/` (python, uv.lock, SKILL.md).
- `plugins/omp-token-opt/` — a SECOND omp plugin (own `package.json`: `omp.extensions:["./src/index.ts"]`, dep `gpt-tokenizer 4.1.0`, bun>=1.2.9): measured token savings; writes `~/.omp/agent/token-opt/live.json`; emits `token-opt:savings`. `plugins/ultrathink-connector/` — Claude desktop MCP bundle (`.claude-plugin`, `.mcp.json`, README, `package.sh`).
- `services/ultrathink/` — a hosted MCP/OAuth/HTTP service (`server.ts`, `routes`, `store`, `worker`, `obs`, `auth`, `oauth`, `chaos`…); `bin/ultrathink-connector` runs the stdio variant. Not needed by omp integration.
- `bin/`: `ultrathink`, `ultrathink-mcp`, `ultrathink-ship`, `ultrathink-grokbot`, `ultrathink-connector`, `run-bun` (POSIX sh launchers).
- Plugin manifests: `.claude-plugin/{plugin.json,marketplace.json}`, `.cursor-plugin/`, `.muse-plugin/`, `.omp-plugin/marketplace.json` (`name:"ultrathink"`, plugin `source:"./"`, v1.4.0 — Claude-marketplace-schema file for omp marketplace install). `.omp/config.yml` is the untracked dev file above (NOT part of the repo).

---------------------------------------------------------------------------------------------------
## 3. What an EXTERNAL host (C#) can observe or drive

### 3.1 State directory (host `omp`)
`stateDirForHost("omp")` (`src/host/paths.ts:38`) = `${PI_CODING_AGENT_DIR || ~/.omp/agent}/ultrathink`; override `ULTRATHINK_STATE_DIR` (absolute; ignored if inside `.planning`). The omp extension resolves it as `resolve(cwd, options.stateDir ?? resolveStateDir({...process.env, ULTRATHINK_HOST:"omp"}))` (omp.ts:1107). Files (observed live on this machine at `~/.omp/agent/ultrathink/`, 130 sessions):
```
control.json            {"skipOnce":false}  (legacy key; schema below). Written atomically with a lock; 0o700 dir.
last.json               mirror of the most recent SessionRecord (readLast)
last-plan.json          carrier: {host, sessionId, specPath, statePath, graphId, instruction, context}  (src/host/carrier.ts)
sessions/<id>.json      SessionRecord (full schema below)  — written ONCE at end of planning (stage "state"), then updated in place
sessions/<id>.xml       the full uplifted spec XML (result.xml; sample 120 KB) — path is the plan's "Specification file:"
sessions/<id>.skip      empty flag file = armed /ultrathink-skip for that session
swarm/<sha8>-<i>-<nonce>/ {run.log, events.jsonl, kickoffs/, plans/, autonomous.log, latest_correlation, tasks.db ...}  swarm lanes (§6)
teach/{inbox,moments,outbox,sessions}/   Teachable Moments store; teach/activity receipts
todos.json, memory.md   (present on this machine; not written by ultrathink source I read -> UNKNOWN: grep -rn 'todos.json' src)
```
**Not live:** the SessionRecord is written after the whole pipeline (uplift+graph+clarify+track) finishes. There is no file/pipe a C# host can tail for stage progress during planning (progress events are an in-process callback; `ULTRATHINK_PROGRESS_FD` is documented as "Omp no longer uses it", `docs/configuration.md:701`, `src/host/progress.ts:98`). To get live stage events OmpOS must either (a) ship its own tiny omp extension that subscribes to `pi.events`/forwards, or (b) poll for `sessions/<acpSessionId>.json` and the omp session JSONL, or (c) accept coarse states: no record / pending custom_message / plan custom_message.

### 3.2 `ControlState` (`src/claude/state.ts:36`, control.json)
```ts
{ enabled?: boolean; thinkEnabled?: boolean; hitlEnabled?: boolean; trackEnabled?: boolean; engine?: "auto"|"claude"|"grok"|"muse" }
```
`readControl` ignores unknown keys. `enabled:false` = planning off for this host+stateDir (all sessions) until `on`. Config fallback: `uplift.enabled` (default true).

### 3.3 `SessionRecord` (`src/claude/state.ts:45-95`) — sessions/<id>.json
```ts
{ sessionId: string; at: number /*epoch ms*/;
  engine?: string;                       // label e.g. "omp-native:anthropic/claude-haiku-5-5 [detected]"
  modelResolution?: ModelResolution;     // {version:"1.0.0", state:"detected"|"unresolved"|…, host:"omp", transport:"omp-native", source:"ctx.model", reason:"live-model", engineSelection:{engine:"auto", source:"config"|"control", nativeOptOut:boolean}, api, provider, modelId, modelKnown, label}
  engineError?: string;                  // first redacted engine error (explains fallback)
  degraded?: string[];                   // "uplift" | "graph" | "fill:<nodeId>"  (stages that used boilerplate)
  host?: string;                         // "omp"
  result: UpliftResult;                  // {xml, original, root:"BUILD_PROMPT|FIX_PROMPT|RESEARCH_PROMPT|CHANGE_PROMPT|UPLIFTED_PROMPT", source:"llm"|"fallback", fallbackReason?, rawReplyChars?, attempts?}  (src/types.ts) (record also embeds graph/degraded copies)
  graph?: ThoughtGraph;                  // {goal, nodes: ThoughtNode[]}
  clarifications?: Clarification[];
  plan?: TrackPlan;                      // exists only when result.source !== "fallback"
  kickedOff?: boolean; synced?: boolean; // plan-scoped flags, false at write
  tracking?: TrackingRefs;
  skill?: { name: string; summary?: string; source: "omp"|"hermes"|"hermes-bundle"|"slash" };
  ship?: ShipState; knowledge?: KnowledgeLookup; lessons?: LessonsLookup; skills?: SkillsLookup; docs?: DocsLookup;
  decisions?: DecisionRecord[];          // Jev decisions (§4)
  workRepos?: string[]; repoMismatch?: { labelled: string|null; edited: string[] } | null }   // omp tool_result attribution
```
Real sample key order: `sessionId, at, engine, modelResolution, degraded, host, result, graph, clarifications, plan, tracking, skill, kickedOff, synced, knowledge, lessons, skills, decisions, workRepos, ship`.

### 3.4 omp session JSONL (also observable)
`~/.omp/agent/sessions/<cwd-slug>/<ISO-ts>_<sessionId>.jsonl`; plan delivery lines (verified in a real file):
```json
{"type":"custom_message","customType":"ultrathink-plan","content":"## Prompt Uplift\n…","display":true,"details":{<PlanView>},"attribution":"agent","id":"…","parentId":"…","timestamp":"…"}
{"type":"custom_message","customType":"ultrathink-pending","content":"Ultrathink is still planning this prompt…","display":true,"attribution":"agent",…}
```
`details` (PlanView, `src/host/view.ts:19-55`) keys seen: `root, source, engine, skill, graphId, goal, elapsedMs, nodes[], waves, clarifications, tracking` (+ `modelResolution`). **PlanView is the best UI-ready projection** (see §5).

### 3.5 CLI with machine-readable output
- `bin/ultrathink <verb>` -> `bun src/uplift/commands.ts` (via `bin/run-bun`, `--no-env-file`; exit 127 if bun missing). Host state: **must set `ULTRATHINK_HOST=omp`** (otherwise detected host falls back to `claude-code` -> `~/.claude/ultrathink`) and optionally `PI_CODING_AGENT_DIR` / `ULTRATHINK_STATE_DIR`. Verbs (USAGE, commands.ts:61-80): `status | off | on | skip --session <id> | track on|off|status | last | think on|off|last|status | hitl on|off|last|status | grok [status|engine auto|claude|grok|muse] | decisions check|probe <point> <cases.json> | hindsight check | ragflow check|datasets|search "<q>" | teach status|list|show|capture|recall|confirm|forget|sync|observe|promote|export | doctor [--json] | prune [--older-than <days>] [--dry-run]`. All output is human text on stdout EXCEPT: **`ultrathink doctor --json`** (Findings `{id, section: runtime|config|credentials|state|pstack, level: ok|info|warn|error, title, detail?…}`, exit 0/1/2, offline, `src/doctor/`) and `ultrathink-ship` (always prints ONE JSON object, exit 0 except usage=2; `assess|pr|review|merge|run|status --state <sessions/<id>.json> [--cwd <dir>]`, `src/ship/cli.ts`).
- `bin/ultrathink-mcp` -> `src/mcp/cli.ts`: `serve <notion|linear|greptile>` (stdio relay), `auth status|set-key <provider> (--stdin|--env-file <p> --var <N>)|login <provider> [--port n] [--redirect url] [--tailscale] [--no-listen]|logout <provider>`, `check [provider…]`, `track complete --state <sessions/<id>.json>`, `session mark --state <p> <kicked-off|synced>`, `notion init --parent <url|id> [--title] [--write-config]`. (Providers: notion, linear, greptile = OAuth/MCP; openrouter, vercel, hindsight, ragflow = API-key only.) `auth status` output format -> `UNKNOWN: bin/ultrathink-mcp auth status` (code at src/mcp/cli.ts:535).
- No `--json` for `status`. A C# host that wants state should read the JSON files directly (stable shapes above) rather than parse `status` text.

### 3.6 Logs / telemetry
- `ULTRATHINK_DEBUG=1` -> `[ultrathink]` lines on stderr (prompt hook; with Decisions on also one `[ultrathink] decisions …` line per decision: point, P/error kind, model, latency, attempts, cost — never message/state/key). Under `omp acp`, process.stdout is redirected to stderr (acp-mode.ts `isolateProtocolStdout`) so nothing corrupts the JSON-RPC stream.
- Planner emits `ProgressEvent`s (`src/host/progress.ts`) in-process only: `begin{sessionId,engine,track?,skill?,modelResolution?}`, `stage{stage: brief|uplift|think|knowledge|clarify|plan|track|state, phase: start|end, ok?, detail?}`, `graph{total,nodes[{id,title,kind,dependsOn?}]}`, `node{phase:start|done,id,title,kind,index,total,fallback?,steps?}`, `track{linear?{nodes:[n,N],steps:[n,N]},notion?{task,nodes,steps},error?}`, `issue{provider:linear|notion,nodeId,step?,identifier?,url}`, `activity{notice:ActivityNotice{v:1,kind,title,source?,outcome?,count?,names?,detail?}}`, `end{outcome: planned|skipped|failed, detail?, modelResolution?}`.
- Receipts: `teach/activity` rows (read by TUI poller only). Swarm lane logs `<state>/swarm/<lane>/run.log`.
- Substrate: a successful plan POSTs `/events` (§6).
- `~/.omp/agent/token-opt/live.json` (omp-token-opt plugin) read by the bar.

### 3.7 Config files & env (hermeticity levers)
Config merge order (low -> high; `src/config.ts:970-981`, `claudeConfigPaths`): `${XDG_CONFIG_HOME||~/.config}/ultrathink/config.json` -> `${CLAUDE_CONFIG_DIR||~/.claude}/ultrathink.json` -> `<cwd>/.claude/ultrathink.json` (project layer: cannot set executables/URLs/backends/swarm.autoRun/pstack/state). Defaults (`defaultConfig`, config.ts:238): `uplift{enabled:true,skipTrivial:true,maxChars:0,echo:true}`, `think{enabled:true,minNodes:5,maxNodes:8,engine:"auto"}`, `hitl{enabled:true,maxQuestions:4,knowledgeBase:false}`, `track{enabled:true,budgetMs:0,concurrency:6}`, `substrate{url:""}`, `decisions{enabled:true,provider:"auto",model:"~typesafe/jev-latest",points:[plan,ship,knowledge,blocking,teachable,skillworthy],timeoutMs:3000,zdr:true,planSkipBelow:0.2,…}`, `ship{enabled:false,autoMerge:false,skills:["gsd-"],minScore:5,…}`, `teach{enabled:true,capture:"auto",recall:true,autoPromote:true,…}`, `swarm{autoRun:false}`, `state{retentionDays:0}`, `models{hosts:{},providerDefaults:{}}`, `claude/grok/muse` route configs.
Env (`docs/configuration.md:626-723`): `ULTRATHINK_UPLIFT=0` (hard off for process, even `uplift:` prefix), `ULTRATHINK_TRACK=0`, `ULTRATHINK_SHIP=0`, `ULTRATHINK_DECISIONS=0`, `ULTRATHINK_HINDSIGHT=0`, `ULTRATHINK_RAGFLOW=0`, `ULTRATHINK_TEACH=0`, `ULTRATHINK_GATEWAY=0`, `ULTRATHINK_HOST`, `ULTRATHINK_STATE_DIR`, `ULTRATHINK_MCP_STORE` (credential store, default `~/.config/ultrathink/mcp-credentials.json`), `ULTRATHINK_SWARM_ROOT`, `ULTRATHINK_SWARM_MAX_LANES`, `ULTRATHINK_PSTACK*`, `ULTRATHINK_CONFIG_DIR`, `ULTRATHINK_DEBUG`, `ULTRATHINK_CHILD=1` (never planned), `ULTRATHINK_DECISIONS_URL`, `SUBSTRATE_URL|TOKEN|TIMEOUT_MS(1500)|DISABLED=1`, `ULTRATHINK_SUBSTRATE_BACKEND`, `HINDSIGHT_API_URL|KEY|TOKEN`, `RAGFLOW_URL|API_KEY`, `OPENROUTER_API_KEY`, `AI_GATEWAY_API_KEY`, `DESK_GATEWAY_*`, `GSD_TOOLS`, `BUN`, `HOME`, `XDG_CONFIG_HOME`, `PI_CODING_AGENT_DIR`, `CLAUDE_CONFIG_DIR`.

### 3.8 Turning planning on/off / force-skip for ONE run (options)
| goal | mechanism | notes |
|---|---|---|
| process-wide hard off | launch omp with env `ULTRATHINK_UPLIFT=0` | simplest, hermetic, wins over everything |
| per-state-dir off | write `{"enabled":false}` to `<stateDir>/control.json` (or `ULTRATHINK_HOST=omp bin/ultrathink off`) | persists for all sessions using that state dir -> use a per-run `ULTRATHINK_STATE_DIR` |
| config off | `uplift.enabled:false` in a user config file (`XDG_CONFIG_HOME/ultrathink/config.json`) | `uplift:` prefix can still force |
| next prompt of ONE session | `ULTRATHINK_HOST=omp ultrathink skip --session <acpSessionId>` -> creates `<stateDir>/sessions/<safeId>.skip`; consumed+deleted by that session's next prompt | must exist BEFORE `before_agent_start` fires; needs the id (known after `session/new`) |
| one message | send ACP prompt `/ultrathink-quick <text>` (extension command; calls `pi.sendUserMessage(text)`; plan skipped for exactly that text) | ACP supports extension commands incl. `sendUserMessage` follow-ups (acp-agent.ts:1047) |
| one message, prefix | text starting `raw: ` is skipped, BUT the model receives the literal `raw:` prefix | extension cannot rewrite prompt |
| force plan | text starting `uplift: ` | prefix remains in model-visible text |
| rows only off | `ULTRATHINK_TRACK=0`, `control.trackEnabled:false`, or leave `linear.team` and `notion.dataSourceUrl` empty (`trackingOff`, commands.ts:119 -> agent is told "TRACKING_OFF_NOTE": no kickoff row creation) | |

---------------------------------------------------------------------------------------------------
## 4. Decisions feature (Jev / OpenRouter Decisions API)

- Code: `src/decisions/{types,client,gate,questions,flow,vercel,cli}.ts`. Doc: `docs/how-to/use-jev-decisions.md`. Config key `decisions` (user layer; "Always on"; `enabled` in files is ignored, `types.ts:60`). Branch `feat/decisions-jev` also exists on remote (older).
- Endpoints: OpenRouter `https://openrouter.ai/api/alpha/decisions` (override only via env `ULTRATHINK_DECISIONS_URL`, only https openrouter.ai or loopback); Vercel `https://ai-gateway.vercel.sh/v4/ai/evaluation-model` (model header `ai-model-id`, `typesafe-ai/jev`). Model default `~typesafe/jev-latest`. Rail pick: `provider:"auto"` -> Vercel if its key exists else OpenRouter (`gate.ts resolveDecisionsKeys`). Keys: credential store `providers.openrouter|vercel` `{kind:"api_key",apiKey}` (wins) else env `OPENROUTER_API_KEY` / `AI_GATEWAY_API_KEY`. **No key => `active()` false => "inactive", every point silently skipped (no HTTP, no record).** `ULTRATHINK_DECISIONS=0` kills it.
- Decision points: `plan | ship | knowledge | blocking | teachable | skillworthy`. Thresholds: plan skip <0.2; ship veto <=0.2, approve >=0.7; knowledge grounded >=0.8; blocking promote >=0.5; teachable drop <0.3, auto-confirm >=0.8; skillworthy >=0.5. Timeout 3000 ms total incl. one retry (>=500 ms left), `zdr:true`.
- **Where recorded:** `SessionRecord.decisions: DecisionRecord[]` (in `sessions/<id>.json`; call order; never holds state content or key) — `src/decisions/types.ts:214`:
```ts
{ point: DecisionPoint; outcome:"ok"|"error"; model:string /*resolved when ok else requested*/; id?:string /*response.id*/; p?:number /*present iff ok*/;
  probabilities: Record<string,number> /*e.g. {plan_worthy:0.97}; {} on error*/; threshold:number;
  action: "plan"|"skip-plan"|"veto"|"advise-veto"|"approve"|"reject"|"none"|"keep"|"reject-claim"|"promote"|"drop"|"hold"|"auto-confirm"|"skip"|"fail-open";
  latencyMs:number; attempts:number; cost?:number; error?: "auth"|"credits"|"bad-request"|"too-large"|"rate-limit"|"upstream"|"timeout"|"network"|"invalid-response"; at:number /*epoch ms*/ }
```
Real sample (this machine): `[{"point":"blocking","threshold":0.5,"latencyMs":560,"attempts":1,"outcome":"error","model":"~typesafe/jev-latest","probabilities":{},"action":"fail-open","error":"credits","at":1793001041141}, …x3]`. A plan-gate skip returns `skipped:"jev-plan-skip"` and is NOT persisted to a record (no state write on skip); it is only echoed as a notice (`formatPlanSkipNotice`) in the hook result (`hook.ts:651-657`).
- Also: Jev "flow score" (`hooks/flow-eval.ts`, `src/decisions/flow.ts`) appends JSONL `FlowRecord` rows (`appendFlowScore(path, record)`); path/format of FlowRecord -> `UNKNOWN: sed -n 1,145p src/decisions/flow.ts` (Claude-hooks only; not used by omp). Host UI for omp: `/ultrathink-ui jev` dashboard reads saved rows (`src/host/omp-insights.ts` — allowlisted rows; out-of-range values "never clamped").
- CLI: `ultrathink decisions check` (one live decision: model, latency, cost), `decisions probe <point> <cases.json>`.

---------------------------------------------------------------------------------------------------
## 5. Graph-of-Thought / Chain-of-Thought data model

### Types (`src/think/types.ts`)
```ts
NodeKind = "understand"|"decompose"|"generate"|"compare"|"critique"|"aggregate"|"refine"|"synthesize"
ThoughtNode { id: string /*n1,n2,…*/; title: string; kind: NodeKind; question: string; dependsOn: string[]; thinking?: string /*CoT rationale: 5-8 numbered steps*/; conclusion?: string }
ThoughtGraph { goal: string; nodes: ThoughtNode[] }   // MIN_NODES=5, MAX_NODES=8, MIN_STEPS=5, MAX_STEPS=8
FALLBACK_GRAPH: n1 understand, n2 decompose, n3 generate(Approaches), n4 critique(Risks), n5 synthesize(Plan) (chain)
```
Planner rules (`src/think/prompts.ts`): first node `understand`, last `synthesize`, ids n1.., depends_on only earlier ids, >=1 `critique` node (its conclusion ends with "Open questions:" list -> feeds HITL), `synthesize` conclusion MUST contain plain-line WORKFLOW:
```
WORKFLOW
Wave 1 (parallel): <unit> — files: <paths> — done when: <observable check>
Wave 2: <unit> — files: … — done when: …
Verify: <commands>
```
(exactly one `Verify:` line). Pipeline `src/think/pipeline.ts` (graph JSON call, then one CoT completion per node, parallel by dependency level; `claude.concurrency` default from config).
### Waves
`workflowWaves(graph)` (`src/think/graph.ts:209`): topological dependency levels -> `[{wave: 1-based, parallel: ids.length>1, ids: string[]}]`. XML (graph.ts:232-266):
```xml
<GRAPH_OF_THOUGHT>
  <GOAL>…</GOAL>
  <NODE id="n1" kind="understand" title="…" [issue="LIN-1" issueUrl="…" notionUrl="…"]>
    <QUESTION>…</QUESTION><DEPENDS_ON>n1,n2</DEPENDS_ON><RATIONALE>…</RATIONALE><CONCLUSION>…</CONCLUSION>
  </NODE> …
  <WORKFLOW><WAVE n="1" parallel="false">n1</WAVE><WAVE n="2" parallel="true">n2, n3</WAVE>…</WORKFLOW>
</GRAPH_OF_THOUGHT>
```
plus `<CLARIFICATIONS><CLARIFICATION id header blocking><QUESTION/><WHY/><OPTIONS><OPTION recommended="true">…</OPTION></OPTIONS><ANSWER source="user|assumed|knowledge" [evidence]>…</ANSWER></CLARIFICATION></CLARIFICATIONS>` and `<ISSUES graphId status [notionTaskUrl]><ISSUE node identifier url notionUrl>…<SUBISSUE step …>…</SUBISSUE></ISSUE></ISSUES>` (`src/track/render.ts`). Root of the spec: one of `BUILD_PROMPT|FIX_PROMPT|RESEARCH_PROMPT|CHANGE_PROMPT|UPLIFTED_PROMPT` with children ORIGINAL, SYSTEM_ROLE, CONTEXT|APP_CONTEXT, SCOPE, CONSTRAINTS, ACCEPTANCE_CRITERIA, OUT_OF_SCOPE (+ optional) (`src/uplift/prompt.ts`). Clarification type: `{id, question, header (<=12 chars), why, options[{label,description?}], default?, blocking, answer?, answeredAt?, source?:"user"|"assumed"|"knowledge", evidence?}` (max 4 questions).
### TrackPlan / TrackingRefs (`src/track/types.ts`)
`TrackPlan{graphId, task:TaskRow{graphId,item,description,upliftedPrompt(<=1900 chars),agent,status,linearState,repo?,branch?}, issues:IssueRow[]{graphId,nodeId,item,thought}, subIssues:SubIssueRow[]{graphId,nodeId,item,step,thought}, linearIssues[], linearSubIssues[], hitl:{blocking,nonBlocking}}` (one Issue per node, one Sub-Issue per CoT step; 8 nodes/57 sub-issues in a real sample). `TrackingRefs{graphId,status:"complete"|"partial"|"failed",linearTeam?,linear:{nodes:Record<nodeId,IssueRef>,steps:Record<"nodeId.step",IssueRef{id,identifier,url,title}>},notion:{taskUrl?,nodes:Record<id,url>,steps},errors:string[],updatedAt}`.
### Graph ID
`generateGraphId()` (`src/track/plan.ts:33`): `ut-${Date.now().toString(36)}-${randomUUID().slice(0,8)}` e.g. `ut-mvoyixof-a2d90b1b`. Validators: `/^ut-[a-z0-9]+-[0-9a-f]{8}$/` (`src/gateway/types.ts:43`); planned-graph reference regex `/\bgraph\s+ut-[a-z0-9]+-[0-9a-f]{8}\b/i` (detect.ts:14). Minted in `buildTrackPlan` only when `result.source !== "fallback"`, i.e. **a fallback spec has no Graph ID, no TrackPlan, no rows, and no substrate event**.
### What a UI needs to render stages (all present in PlanView, `src/host/view.ts:19`)
`{root, source:"llm"|"fallback", engine?, modelResolution?, skill?, graphId?, goal?, elapsedMs, nodes:[{id,title,kind,wave(0-based level),dependsOn[],conclusion?,issue?{identifier,url},notionUrl?,steps:[{step,title,identifier?,url?}]}], waves: string[][] (ids per level), clarifications:[{id,question,blocking,answer?,recommended?}], tracking?:{status,notionTaskUrl?,errors[],issues,subIssues}}`. Stage names for a progress strip: `brief, uplift, think, knowledge, clarify, plan, track, state` + node-level `node start/done`, phases `idle|planning|delivered|skipped|failed`, delivery `inline|aside|pending` (`BarState`, omp-ui.ts:32). Live stages are NOT externally observable (see 3.1); final state is.

---------------------------------------------------------------------------------------------------
## 6. Integration with agent-swarm, agent-substrate, GSD

### Agent Substrate (opt-in, fail-open; `src/substrate/brief.ts`)
- Config: `substrate.url` (config) or env `SUBSTRATE_URL` (env wins); bearer env `SUBSTRATE_TOKEN`; `SUBSTRATE_TIMEOUT_MS` (default 1500); `SUBSTRATE_DISABLED=1` kills it; `substrate.backend:"direct"|"gateway"` (+`ULTRATHINK_SUBSTRATE_BACKEND`; gateway = desk gateway, user-file only).
- Pre-plan: `POST <url>/brief` body `{repo, branch, graph_id, surface:"omp"}` -> Markdown text injected as `## Agent Substrate brief` into the plan context and into the planner prompts (Graph planned knowing other agents' work). Failure/slow/unset -> `""`.
- Post-plan: ONE `POST <url>/events` `{kind:"note", summary:"ultrathink planned graph <id> (<n> nodes)", surface:"omp", session_id:"<ompSessionId>:<graphId>", graph_id, repo, branch, payload:{ultrathink:"plan", nodes, host:"omp", skill?}}` — only for real (non-fallback) plans with a written record and non-turn session id (`announcePlan`, hook.ts:490-541). The substrate adopts the **Graph ID `ut-<ts36>-<8hex>` unchanged** as its correlation key.
- `graph_register`/`graph_claim`/leases are NOT called by ultrathink code. `graph_register` is performed by the **agent** inside the `ultrathink-kickoff` skill (`skills/ultrathink-kickoff/SKILL.md:83`: only if an MCP server named `substrate` is connected in the session with a `graph_register` tool). Under `omp acp` the factory passes `enableMCP:false` (main.ts) so MCP servers from omp's mcp.json aren't connected unless the ACP client supplies them in `session/new` `mcpServers` (acp-agent `#configureMcpServers`).
- Omp bus: extension listens for `substrate:status` (shape `{v:1,state,usage,coverage,planesDown,updatedAt,reason}`, parsed by `parseSubstrateStatus`, omp-ui.ts:341) emitted by agent-substrate's projector `substrate-status.ts` extension; `pi.events.emit("substrate:status-request")` at startup.
- Untracked `.mcp.json`/`.cursor/mcp.json`/`.grok/config.toml` are agent-substrate projector outputs (above).
### Agent Swarm
- `/ultrathink-swarm <briefA>|<briefB>|…` (max `ULTRATHINK_SWARM_MAX_LANES`, default 3, hard cap 6) and `/ultrathink-swarm status`; auto-run when user config `swarm.autoRun:true` (**user files only**) AND env `ULTRATHINK_SWARM_ROOT` = absolute agent-swarm checkout containing `hooks/autonomous_run.py` AND `isSdlcPrompt` (first word, after `sudo|please|hey `, in the SDLC verb list `…deploy,release,ship,scaffold,upgrade,integrate,write,set…`, not `/`) in a top-level session (`src/swarm/teams.ts:142-228`, omp.ts:2242-2287). A lane = detached `python3 <SWARM_ROOT>/hooks/autonomous_run.py --runtime omp --cwd <cwd> --brief <brief[+pstack orchestrate block]>` with env `{...process.env, SWARM_DIR:<stateDir>/swarm/<sha8(brief)>-<i>-<nonce>, SWARM_AUTONOMOUS_RUN_CAP_S?}`, stdout/stderr -> `<lane>/run.log`. Status: `python3 <root>/scripts/orch_status.py --json --repo <cwd>` with that `SWARM_DIR` (10 s timeout). **When a lane starts, the user's prompt is NOT planned by ultrathink** (owned by the lane; `quick` armed).
- `## Cursor swarm` note + `.swarm`: exist ONLY in the Cursor host hook (`hosts/cursor/ultrathink-cursor-plan.js:46`): text "If this plan is new implementation work, run the ultrathink-cursor skill. It spawns the a01-orchestrator subagent once with this plan's Graph ID. Do not spawn a02-requirements through a15-docs yourself, and do not run swarm_run.py or autonomous_run.py." **Omp does not emit it.** For the OmpOS composition order (Ultrathink -> Swarm) the hand-off today is: plan injected -> agent (or OmpOS) takes the Graph ID/state file -> swarm. `.swarm/` in UT is just agent-swarm runtime state (events.jsonl, tasks.db), ignored by git, unrelated to ultrathink.
- Project `.omp/config.yml` (untracked) loads agent-swarm's omp extension dir when cwd = this repo.
### GSD
- `/gsd-*`, `/skill:gsd-*` and `<plugin>:gsd-*` prompts are planning input without any skill file (`src/uplift/skill.ts:70-76,331,395-401`, CHANGELOG Unreleased "GSD slash commands are planned"): the task is the text after the command (or `Run the "<name>" skill. <summary>`); stacked `/ultrathink-*` after a gsd name is skipped. SessionRecord.skill = `{name:"gsd-…", source:"slash"|"omp"}`.
- Ship: for skills matching `ship.skills` (default prefix `gsd-`), after `agent_end` with committed work on a feature branch, nudge to run `ultrathink-ship` (opt-in `ship.enabled`, default false; `ULTRATHINK_SHIP=0` off). `GSD_TOOLS` env points at `gsd-tools.cjs` (roadmap read for ship assessment; else lookup under `~/.agents/gsd-core`? -> `UNKNOWN: grep -n "GSD tools lookup" docs/configuration.md`).
- `hosts/cursor/gsd-commands/` + pstack bridge (`pstack.enabled` user-only) map `/gsd-*` to skills; Cursor only.

---------------------------------------------------------------------------------------------------
## 7. Fresh-clone requirements

- Runtime: **bun >= 1.2** (package.json `engines`; `bin/run-bun` message "Install Bun 1.2 or later"); CI runs "Bun 1.2.x" and "Bun latest"; this machine bun 1.4.0, omp 18.12.5 (omp itself is bun-based and loads extension `.ts` directly).
- `bun install` is **not required to load the omp extension**. `package.json` has zero runtime dependencies; every static import in `src/host/omp.ts` is relative or `node:*`; `@oh-my-pi/pi-ai` is imported with `import type` (erased) plus ONE dynamic `import("@oh-my-pi/pi-ai")` (omp.ts:609) and `import("@oh-my-pi/pi-catalog/provider-models")` (omp.ts:487) whose literal specifiers omp's loader remaps to its bundled copies (code comment omp.ts:480-484 "after `bun install`" is the dev fallback; catalog failure only drops the provider-default tier). `yaml`, biome, knip, `@types/bun` are devDeps (lint/test/types only). `bun install --frozen-lockfile` (`bun.lock`) is needed only for `bun test`, `bun run check/lint/deadcode`. (This checkout has `node_modules/` with `@oh-my-pi/pi-ai pi-catalog pi-natives pi-utils pi-wire omptype` as dev types.)
- Python only for swarm lanes (`python3`) and the hermes/prime-agent bridge tests — not for omp.
- Loading from a bare checkout/submodule: the checkout root is a valid omp "extension package dir" (`package.json#omp.extensions` -> `./src/host/omp.ts`; omp docs `extension-loading.md`). Load mechanisms (omp docs/source, not run):
  - **`omp plugin link <dir>`** (symlink into `~/.omp/plugins/node_modules/`) — what `docs/install.md:268` documents; verify with `omp plugin list` ("`ultrathink@1.4.0` enabled"). This machine ALREADY has `~/.omp/plugins/node_modules/ultrathink -> ~/src/repos/claude-ultrathink` and `omp-token-opt -> …/plugins/omp-token-opt` (HOME-level state: every omp on this machine loads the dev checkout; the plugins dir is `~/.omp/plugins` = config root, independent of `PI_CODING_AGENT_DIR`; only `PI_CONFIG_DIR`/HOME change it, `pi-utils/dirs.ts:700`).
  - **`omp -e|--extension <dir-or-file>`** (repeatable; a package dir with `package.json#omp.extensions` is expanded; with `--no-extensions` ambient discovery is off but explicit `-e` still loads and sibling skills/commands/rules of that package stay eligible).
  - **`omp --trusted-extension <absolute .ts file>`** (repeatable; loads exactly that module; bypasses discovery; ACP-aware `loadTrustedSessionExtensions`; **does not** authorise sibling skills/commands, so the plan's "invoke skill ultrathink-kickoff" would have no skill; kickoff/plan skill text is then only reachable by file path).
  - `extensions:` list in `~/.omp/agent/config.yml` or `<cwd>/.omp/config.yml`.
  - `--plugin-dir <path>`: creates a synthetic Claude-plugin-format root (`discovery/plugin-dir-roots.ts`: `id=<name>@__local__`, `origin:"plugin-dir"`) -> skills/commands/MCP/hooks of a Claude-format plugin. **UNKNOWN whether it also loads `package.json#omp.extensions`**: resolve with `grep -n "plugin-dir" ~/.local/share/reflex/bun/install/global/node_modules/@oh-my-pi/pi-coding-agent/src/discovery/helpers.ts` (injectPluginDirRoots at line 1508) or a throwaway `omp --plugin-dir <ut> --list-extensions`-style run. RISK: UT root also has `.claude-plugin/plugin.json` + `hooks/hooks.json` (Claude UserPromptSubmit -> `hooks/uplift.ts`, would plan with the `claude` CLI engine under host fallback `claude-code`): if `--plugin-dir`/`-e` surfaces `hooks/hooks.json` through omp's Claude-hook providers, planning could double-fire. `UNKNOWN: omp --no-extensions -e <ut> -p … with ULTRATHINK_DEBUG=1 and count [ultrathink] lines`.
- Command inventory (documented; **not run by me**): `bun install --frozen-lockfile`; `bun run check` (tsc 5.9.3 via bunx -> network on first run); `bun run check:services`; `bun run lint`; `bun run deadcode`; `bun test`; `python3 hosts/hermes/bridge_test.py`; `python3 hosts/prime-agent/bridge_test.py`. Documented status: PR 59 CI green on both Bun legs (all 11 checks pass, incl. CodeQL, Semgrep, Greptile). `.github/workflows/ci.yml` lines 52-106. Local runs here: none.
- Skills/commands the agent is told to invoke (`ultrathink-kickoff` etc.) are found only when UT is loaded as an extension *package* (or skill dirs are added). `bin/ultrathink` and `bin/ultrathink-mcp` are the CLIs those skills call, resolved by absolute path embedded in the plan text (`SHIP_CLI`, `trackCommand`) — requires `bun` on the agent's PATH or `$BUN`.

---------------------------------------------------------------------------------------------------
## 8. Degradation / failure behaviour; HOME-level state a hermetic omp would lack

Everything is fail-open: a planning problem never blocks the prompt.
- Extension handler crash/throw -> `return;` (no plan). Planner promise reject -> empty context -> prompt proceeds (`finishFlight({context:""})`). Optional bus/widget failures swallowed by `guard`.
- Native engine unavailable (no live model facade, `ctx.models/modelRegistry` missing) -> selection reports `native-unavailable` -> `skipped` with notice "Prompt Uplift skipped · <label>" (no subprocess, no copied credentials). Planning always uses the **session's own live model** (`completeSimple` via `ctx.modelRegistry.resolver`); override `models.hosts.omp:{provider,model}`; provider defaults map `models.providerDefaults`.
- Transient LLM failures retried 2x (backoff 2 s/6 s + 25 % jitter) only for 408/429/5xx or network errors; 401/403/404 and `subscription|permission|forbidden|unauthorized|insufficient quota` never retried (omp.ts:525-542).
- Engine failure -> **fallback spec**: `UpliftResult.source:"fallback"` (+`fallbackReason`: `provider_error:<status>|provider_error|empty_reply|unparseable_reply|graph_minimum_not_met|input_over_limit`), `degraded:["uplift"]`, `engineError` recorded; the injected context says "The <engine> planning call failed… The specification below is generic fallback text, not a real plan; treat the attached graph as unverified. No tracker rows were created" (output.ts:413); **no Graph ID/TrackPlan/rows/substrate event**. Graph or per-node CoT failure -> `FALLBACK_GRAPH`/boilerplate nodes, `degraded:["graph"|"fill:<id>"]`. Uplift call throw -> `skipped:"uplift-failed"`. HITL/knowledge/clarify failures -> stage `ok:false`, plan continues.
- Tracker down / OAuth missing: `createGatewayTracker` returns undefined when neither `linear.team` nor `notion.dataSourceUrl` configured (provider never contacted); with credentials missing -> `TrackingRefs.status:"partial"|"failed"`, `errors:["notion: login required"…]`, plan continues (real sample: Notion ok, Linear `nodes:{}` -> `partial`). Credential store `~/.config/ultrathink/mcp-credentials.json` (0600; providers linear/greptile/notion/hindsight/vercel/openrouter/ragflow). OAuth login is interactive (`ultrathink-mcp auth login`, localhost callback).
- Substrate down/slow -> brief `""`, event not sent; 1.5 s default timeout.
- Jev: no key -> points inactive; key but credits/auth/network error -> `DecisionRecord{outcome:"error",action:"fail-open"}` and the plan proceeds (real sample: `error:"credits"`).
- Hindsight/RAGFlow/Greptile KB lookups are bounded by `teach.timeoutMs` (2500) / their own timeouts and degrade to `outcome:"off|none|error"` in the record (real sample: Greptile KB lookup `ms:2630704` = 44 min wall time recorded with `outcome:"none"` — a lookup that long implies planning latency can be very large; `hitl.knowledgeBase:true` makes `knowledge` stage await it before clarify: **set `hitl.knowledgeBase:false` for deterministic latency**).
- State write failure -> plan still delivered inline without file pointers ("No spec file… fall back to the inline spec").
- Sessions accumulate forever (`state.retentionDays` default 0; 130 records/261 files here); `ultrathink prune --older-than N`.
- Pending plan caveat: while pending, agent is instructed no edits; if the session switches/ends/model changes the flight is cancelled and nothing is delivered (SESSION_ENDED / MODEL_CHANGED notes only in the bar).
**HOME-level state/credentials this machine's ultrathink currently depends on (a hermetic OmpOS-launched omp would NOT have unless HOME/XDG are replaced):**
- `~/.config/ultrathink/config.json` — here sets `notion.dataSourceUrl`, `linear.team:"Spectrum Web Co"`, `substrate.url:http://127.0.0.1:7410`, `ship{enabled,autoMerge:true,…}`, `hitl.knowledgeBase:true`, `hindsight{enabled,url:https://hindsight-api-production-…}`, `ragflow{enabled,url:https://ragflow-production-…}`, `teach{…autoPromote}`, `decisions.provider:"openrouter"`, **`swarm.autoRun:true`**, `grok.shunt*`, `claude.model`.
- `~/.config/ultrathink/mcp-credentials.json` — linear/greptile/hindsight/vercel/openrouter api_key, notion oauth.
- `~/.omp/plugins/node_modules/ultrathink` symlink + `~/.omp/agent/config.yml` (`extensions: [~/src/repos/agent-swarm/omp]`), `~/.omp/agent/ultrathink/` state, `~/.omp/agent/mcp.json`.
- Process env on this machine: `SUBSTRATE_URL`, `SUBSTRATE_TOKEN`(***), `ULTRATHINK_SWARM_ROOT=~/src/repos/agent-swarm`, `SWARM_ED25519_KEY`(***).
=> Without a hermetic override, launching omp for OmpOS here would (a) load the dev checkout instead of the pinned submodule, (b) create Notion/Linear rows and call Greptile/Hindsight/RAGFlow/OpenRouter on every non-trivial prompt, (c) auto-spawn detached swarm lanes for SDLC-sounding prompts and then **not plan them**, (d) contact the substrate. Recommended hermetic launch: `HOME`/`XDG_CONFIG_HOME`/`PI_CODING_AGENT_DIR`(+`PI_CONFIG_DIR` for the plugins dir) pointed at an OmpOS-owned dir, `ULTRATHINK_STATE_DIR` per run, explicit `-e <submodule>` with `--no-extensions`, env `ULTRATHINK_SWARM_ROOT` unset, `SUBSTRATE_DISABLED=1` (or set deliberately), `ULTRATHINK_TRACK=0` or empty `linear.team`/`notion.dataSourceUrl`, `ULTRATHINK_DECISIONS=0`, `ULTRATHINK_HINDSIGHT=0`, `ULTRATHINK_RAGFLOW=0`, `ULTRATHINK_TEACH=0`, a config with `hitl.knowledgeBase:false`.

---------------------------------------------------------------------------------------------------
## 9. Licence
`package.json`: `"license": "AGPL-3.0-or-later"`. Every source file starts `// SPDX-License-Identifier: AGPL-3.0-or-later` / `// Copyright (C) 2026 SWC Studio`. `LICENSE` header (lines 1-4):
```
                    GNU AFFERO GENERAL PUBLIC LICENSE
                       Version 3, 19 November 2007

 Copyright (C) 2007 Free Software Foundation, Inc. <https://fsf.org/>
```
README.md:229 "…under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version." GitHub licenseInfo `agpl-3.0`. Implication (not legal advice): OmpOS using it as a git submodule + running it as a separate omp plugin process is aggregation; do not copy/fork its source into OmpOS; a C# adapter that only reads its JSON state/CLI stays separate. AGPL section 13 (network-use source offer) applies to modified versions that users interact with over a network.

---------------------------------------------------------------------------------------------------
## Key findings for the C# OmpOS host (top-level summary)
1. The `before_agent_start` plan hook DOES run under `omp acp` (ctx.hasUI=true, mode "rpc", persisted session file); it skips only subagent/child sessions.
2. The plan is a custom message appended after the unchanged user prompt (not a rewrite, not a system prompt); up to ~84 KB; instructs the agent to invoke skill `ultrathink-kickoff` before coding.
3. ACP clients do NOT receive the plan/pending/status text in the update stream and `/ultrathink-status` replies vanish (`notify` is debug-log only); observe via `sessions/<acpSessionId>.json` + `.xml` or the session JSONL `custom_message` entries.
4. No live stage events leave the omp process (ULTRATHINK_PROGRESS_FD unused by omp); the SessionRecord is written once at the end -> OmpOS needs its own small bridge extension for live "Ultrathink uplift" stage events, or show coarse states (pending/planned/skipped).
5. Session-state location `${PI_CODING_AGENT_DIR:-~/.omp/agent}/ultrathink`, keyed by the ACP session id; `ULTRATHINK_STATE_DIR` per run isolates it.
6. Per-run control: env `ULTRATHINK_UPLIFT=0` (hard off); `ULTRATHINK_HOST=omp ultrathink skip --session <id>`; `/ultrathink-quick <text>` ACP command; `uplift:`/`raw:` prefixes leak into the model-visible text.
7. Hermeticity is the big risk: this machine's HOME config has `swarm.autoRun:true`, Notion/Linear rows, Hindsight/RAGFlow/Jev/substrate calls, a global plugin symlink to the dev checkout, and env `ULTRATHINK_SWARM_ROOT`/`SUBSTRATE_*`.
8. Pinning: HEAD `00ce3e6` is only on open-PR branch `feat/v1.16-code-quality-sweep` (11 ahead of `main` `7cfd054`); repo renamed to `swcstudiospace/ultrathink` (redirect works). Dirty files are untracked machine-local projector output (36 files).
9. Fresh clone loads without `bun install` (zero runtime deps; pi-ai imports erased/remapped by omp); tests/typecheck need `bun install --frozen-lockfile`; bun >=1.2.
10. AGPL-3.0-or-later: keep as submodule/plugin, never copy source.
