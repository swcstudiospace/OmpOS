# Cursor parity research: MCP and agent runs

Checked: 2026-10-22, against primary cursor.com pages (`.md` variants where served). Each Cursor finding cites its URL. Findings are paraphrased; no quotation exceeds fifteen words.

Scope: this file compares Cursor's documented behaviour with the acceptance text of ADE-16 (Part one) and ADE-06, ADE-07 and ADE-14 (Part two) in `docs/plans/ade-harness-plan.md`. It does not change any requirement, roadmap or code.

## How to read the verified column

- **yes**: Cursor's primary documentation states the same behaviour as the ADE text (agree).
- **no**: Cursor's primary documentation states different behaviour (differ).
- **not found**: no primary Cursor page states the behaviour. The ADE text is not a Cursor claim for that item (per R-12).

Scope limit: the verified column records what Cursor's documentation says. It does not record a test of Cursor's product behaviour. Third-party pages (learncursor.dev, the Cursor community forum, teebe.io, crystl.dev, truefoundry.com) were read for leads only and are not cited as evidence below.

## Part one: MCP (ADE-16)

ADE-16 acceptance text (from the plan): a stdio and an HTTP MCP server each register tools in one tool registry; tools can be enabled or disabled per session; a disabled tool is not offered to the model; MCP tool calls go through the ACP approval path (R-10).

| Item (ADE-16 clause) | Cursor finding | URL | Verified against Cursor |
|---|---|---|---|
| MCP client support exists | Cursor connects to external tools and data sources through MCP. Servers are installed from the Customize page or set in an mcp.json file. | https://cursor.com/docs/mcp | yes |
| stdio server (local process) | Cursor launches a stdio server as a local command and talks to it over stdin and stdout. | https://cursor.com/docs/mcp | yes |
| HTTP server | Cursor supports Streamable HTTP servers given by URL, with optional headers. Cursor also supports the older SSE transport, which the ADE text does not mention. | https://cursor.com/docs/mcp ; https://cursor.com/help/customization/mcp | yes |
| Tools from more than one configured server are listed together | MCP tools from configured servers appear under "Available Tools", and Agent uses them when relevant. | https://cursor.com/docs/mcp | yes |
| "One tool registry" (internal structure) | No page describes an internal tool registry. Only the user-visible tool list is documented. | https://cursor.com/docs/mcp | not found |
| Enable or disable a server | A server toggle in Customize enables or disables it without removing it. The CLI has `agent mcp enable` and `agent mcp disable`. | https://cursor.com/docs/mcp ; https://cursor.com/help/customization/mcp ; https://cursor.com/docs/cli/mcp | yes |
| Enable or disable a single tool | Tools can be toggled by clicking the tool name in the tools list at the top of the chat panel. The primary MCP reference documents server-level toggles only. | https://cursor.com/help/customization/mcp | yes |
| Enable or disable per session | No page describes a per-session scope for MCP enablement. The CLI enable command adds the server to a local approved list, which is persistent. | https://cursor.com/docs/cli/mcp | not found |
| Disabled server is not loaded and not offered to the model | A disabled server does not load and does not appear in chat. The CLI says a disabled server does not load and does not prompt for approval. | https://cursor.com/docs/mcp ; https://cursor.com/docs/cli/mcp | yes |
| Disabled single tool is not offered to the model | No page states that a toggled-off tool is withheld from the model. | https://cursor.com/help/customization/mcp | not found |
| Per-run choice of MCP servers (cloud) | Cloud runs can use MCP servers configured for the team, and the iOS app lets the user choose the MCP servers a run uses at launch. This is a per-run selection, not a per-session toggle on a local session. | https://cursor.com/docs/cloud-agent/capabilities ; https://cursor.com/docs/cloud-agent/mobile | yes |
| Local per-session MCP enablement | Not documented for local sessions. See the row above for cloud runs. | https://cursor.com/docs/cloud-agent/mobile | not found |
| MCP tool calls require approval (default) | Cursor asks for approval before an MCP tool runs, by default. The CLI has a flag that skips MCP approval prompts. | https://cursor.com/docs/mcp ; https://cursor.com/docs/cli/mcp | yes |
| Approval runs through the ACP permission path | Cursor's MCP page does not name ACP as its approval mechanism. The CLI MCP page describes ACP only as a protocol for building custom clients. | https://cursor.com/docs/cli/mcp | not found |
| Every MCP call goes through approval (ADE approval path) | Run Modes apply to MCP tools. In the default Auto-review mode, allowlisted MCP tools run immediately and other calls go to a classifier. So a call is not always prompted. | https://cursor.com/docs/agent/security/run-modes ; https://cursor.com/docs/mcp | no |
| stdio needs a local process (ADE platform column, not acceptance text) | Cloud agents accept custom MCP servers over HTTP or stdio. SSE and mcp-remote are not supported there. So stdio is not limited to a local process in Cursor's cloud. | https://cursor.com/docs/cloud-agent/capabilities | no |

## Part two: runs (ADE-06, ADE-07, ADE-14)

### ADE-06: git worktree isolation per agent run

ADE-06 acceptance text: each agent run works in its own git worktree under an operator-controlled path; the main checkout is unchanged by a run.

| Item (ADE-06 clause) | Cursor finding | URL | Verified against Cursor |
|---|---|---|---|
| Git worktree isolation for agent work | Worktrees let an agent work in an isolated Git checkout, so each task has its own files and changes. | https://cursor.com/docs/configuration/worktrees | yes |
| Each agent run has its own worktree | In the Agents Window, starting or moving an agent into a worktree creates a separate checkout for that agent. `/worktree` and `/best-of-n` in the IDE also give each run its own worktree. | https://cursor.com/docs/configuration/worktrees | yes |
| Main checkout unchanged by a run | The main checkout stays untouched. Changes return to it only when the user applies them (for example with `/apply-worktree`). | https://cursor.com/docs/configuration/worktrees | yes |
| Operator-controlled worktree path | No page documents a setting for the worktree location. | https://cursor.com/docs/configuration/worktrees | not found |
| Worktree retention (not in ADE-06 text) | Cleanup runs on a configurable interval (the documented example is 6 hours) and keeps up to 25 worktrees per machine by default, across all workspaces. Cleanup can delete worktrees created outside Cursor, such as by `git worktree add`. | https://cursor.com/docs/configuration/worktrees | yes |
| Worktree UI surface (not in ADE-06 text) | The UI-native worktree feature is available only in the Agents Window. In the IDE, the `/worktree` and `/best-of-n` skills are used instead. | https://cursor.com/docs/configuration/worktrees ; https://cursor.com/docs/agent/agents-window | yes |
| Setup commands run in the new worktree before the agent starts (ADE-19 cross-reference) | A `.cursor/worktrees.json` file runs its setup commands in order in the new worktree, or runs a script. It is read from the worktree path first, then the project root. | https://cursor.com/docs/configuration/worktrees | yes |
| A failing setup step marks the run failed before the agent starts (ADE-19 cross-reference) | No page states what happens to a run when a setup step fails. | https://cursor.com/docs/configuration/worktrees | not found |

### ADE-07: cloud agent runs on the hosted API, with status and reviewable output

ADE-07 acceptance text: a cloud run executes through the hosted API; status moves through queued, running and completed; the diff and transcript are retrievable for review.

| Item (ADE-07 clause) | Cursor finding | URL | Verified against Cursor |
|---|---|---|---|
| Cloud runs execute through a hosted API | The Cloud Agents API (v1, public beta) creates agents and runs on Cursor's API host. | https://cursor.com/docs/cloud-agent/api/endpoints | yes |
| Status moves through queued, running and completed | A run's states are CREATING, RUNNING, FINISHED, ERROR, CANCELLED and EXPIRED. The names differ from the ADE text. CREATING is the closest match to queued [INFERENCE]. FINISHED is the closest match to completed. | https://cursor.com/docs/cloud-agent/api/endpoints | no |
| Agent-level status | An agent is ACTIVE, IDLE or ARCHIVED. Execution state lives on runs, not on the agent. | https://cursor.com/docs/cloud-agent/api/endpoints | yes |
| Only one active run per agent | A second run while one is CREATING or RUNNING is refused with a busy error. | https://cursor.com/docs/cloud-agent/api/endpoints | yes |
| Cancel a run (R-4, not in ADE-07 text) | A cancel-run endpoint exists. | https://cursor.com/docs/cloud-agent/api/endpoints | yes |
| Live run events | A stream endpoint emits status, assistant text, tool-call, error and done events. | https://cursor.com/docs/cloud-agent/api/endpoints | yes |
| Environments for cloud runs | Environments are configured per repository or repo group. They support multiple repositories, and the Environments API is in beta. | https://cursor.com/changelog/cloud-agent-canvases-and-environments ; https://cursor.com/docs/cloud-agent/setup | yes |
| Diff review in the UI | Cloud run changes are reviewed in the diff view of the Agents Window. The iOS app reads full diffs and commits. | https://cursor.com/docs/agent/agents-window ; https://cursor.com/docs/cloud-agent/mobile | yes |
| Diff retrievable through the REST API | The API reference has no diff field and no diff endpoint. Artifact endpoints list and download files. The run result lists pushed branches and PR URLs. | https://cursor.com/docs/cloud-agent/api/endpoints | not found |
| Diff metadata for review | Cursor's built-in Cloud MCP returns diff metadata (whether code changed, how much, whether a PR was opened). It does not return the full diff. | https://cursor.com/docs/cloud-agent/capabilities | yes |
| Transcript retrievable for review | The Cursor Cloud MCP fetches transcripts for runs the caller may see. A team admin can read team runs; other users see only their own runs. | https://cursor.com/docs/cloud-agent/capabilities | yes |
| Transcript through the REST API | The stream endpoint carries live events. The run endpoint returns only the final reply, not a stored transcript. | https://cursor.com/docs/cloud-agent/api/endpoints | not found |
| Android not shipped | The mobile page says Android is planned. | https://cursor.com/docs/cloud-agent/mobile | yes |

### ADE-14: integrated terminal; commands need approval; auto-run is opt-in

ADE-14 acceptance text: the terminal starts in the workspace directory; each command needs approval; auto-run is off by default and enabled only by explicit configuration; a declined command never spawns a process.

| Item (ADE-14 clause) | Cursor finding | URL | Verified against Cursor |
|---|---|---|---|
| Integrated terminal that starts in the workspace directory | Cursor runs shell commands in your terminal, under the Run Mode. No page describes an integrated terminal panel or its starting directory. | https://cursor.com/docs/agent/tools/terminal | not found |
| Each command needs approval | Only the Allowlist and Auto-review modes can ask for approval. In Run Everything, every tool call runs without asking. Allowlisted actions run without approval in both Allowlist and Auto-review. | https://cursor.com/docs/agent/security/run-modes | no |
| Auto-run is off by default | Auto-review has been the recommended default since Cursor 3.6. It runs allowlisted calls immediately and sends other shell commands to the sandbox or the classifier. | https://cursor.com/docs/agent/security/run-modes | no |
| Auto-run enabled only by explicit configuration | The user selects Run Everything or Allowlist in Settings > Agents > Approvals & Execution. | https://cursor.com/docs/agent/security/run-modes | yes |
| Declined command never spawns a process | No page states what happens to a process after a command is declined. | https://cursor.com/docs/agent/security/run-modes | not found |
| Sandbox for shell commands (not in ADE-14 text) | Shell commands can run in a sandbox that limits file and network access. Network is blocked by default, then opened by the network mode, with a default mode that adds Cursor's built-in domain list. | https://cursor.com/docs/agent/security/run-modes ; https://cursor.com/docs/agent/tools/terminal | yes |
| Cloud runs ask for approval (cloud counterpart of the approval row) | Cloud agents do not use Run Modes. They run on their own machine and never ask the user to approve an action. | https://cursor.com/docs/agent/security/run-modes | yes |

## Open items for the orchestrator (no edits made)

- ADE-16: the Cursor MCP page documents server-level toggles. Per-tool toggles appear only on the help page. Per-session scope and the "disabled tool withheld from model" behaviour are not documented.
- ADE-07: the plan's status names (queued, running, completed) do not match Cursor's run states. The plan's diff and transcript claims are supported by the UI and Cloud MCP, not by a REST diff endpoint.
- ADE-14: the default Cursor mode (Auto-review) differs from the ADE default of approval for each command. The ADE-14 row's "Cursor reference" column still reads "None in the verified source list"; this research supports adding the run-modes and terminal URLs there, which is outside this file's scope.
- ADE-06: no documented way to set the worktree location. The plan's "operator-controlled path" needs a design decision before Phase 26.
