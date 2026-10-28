---
description: "The ideas behind OmpOS: sessions and agents, the omp engine, the four plugins loop, stages, steering, approvals, changes, and where an agent runs."
---

# Concepts

Status: Built (the concepts below describe implemented behaviour; where a part is Partial the text says so)

Last verified: 2026-10-28

Applies to: v0.15.0

## Agents and sessions

An **agent** in OmpOS is a coding session you can watch and steer. Technically it is a **session**: an append-only, sequence-numbered log of events with an owner, a status and a catalogue entry that survives a restart. You start a session from the rail (desktop: Ctrl+N; web: New). Several sessions run at the same time, each with its own transcript, run state and change set. A closed session moves to an archive and can be reopened; a session whose run was lost can be resumed so it accepts a new prompt.

A **run** is one turn of work: your prompt and everything the agent does until it stops. A **prompt** is your message; if a run is active, a new prompt cannot interrupt it, which is why steering exists.

## The omp engine

OmpOS has no model and no agent logic of its own. The engine is **omp** (the `omp` command, package `@oh-my-pi/pi-coding-agent`, MIT). OmpOS talks to it in two ways:

- **`omp acp`**: the Agent Client Protocol v1 over standard input and output. This is a chat session. OmpOS sends prompts, receives updates (text, thinking, tool calls, plans, permission requests, file edits), answers permission requests and sets options such as the model and the mode.
- **An omp terminal**: omp's own terminal UI embedded in a terminal view, for people who want the omp interface.

Everything model-related is therefore omp's: which providers exist, how a key is stored, what a mode means. OmpOS adds the workspace around it: the rail, Kanban, review, steering, Settings, hooks, remote and cloud. See [Choose a model provider](providers-guide.md).

## The four plugins loop

Every omp process OmpOS starts is launched with four plugins loaded once:

```
prompt -> Ultrathink (plan) -> Swarm (subagents) -> Substrate (session state, steering record)
                      GSD-Core (phase and workflow context across all three)
```

| Plugin | Role |
|---|---|
| Ultrathink | Rewrites the prompt into a spec and a plan of work in waves before the agent starts. |
| Swarm | A subagent orchestrator with 30 agent roles. |
| Substrate | Cross-session state: memory, leases, handoffs and an event ledger. Optional: without a server it reports Unavailable. |
| GSD-Core | Phase and workflow context from `.planning/`, exposed to omp's task tool. |

The plugins are always on; there is no switch. When one is missing or failing the UI says so instead of hiding it. Details and pins: [Plugins](../plugins.md); how to run and check them: [Agent loop](../runbooks/agent-loop.md).

## Stages

The plugins write stage events (planning, subagent work, state updates). OmpOS reads them into a **pipeline strip** under the thread header and a task context, and into the session catalogue, so a stage survives a restart. A stage that has not started or whose plugin is unavailable is shown that way.

## Steering

Steering is sending a message to a **running** turn. In a chat session OmpOS uses omp's native `_session/steering`: the turn takes the message at its next tool-batch or model-call boundary. If the session is idle the message is sent as a normal prompt. In an omp terminal there is no ACP, so OmpOS writes a directive to an inbox that the GSD-Core plugin hands to omp, and shows "delivered" only on acknowledgement. The fallbacks are queue-after-turn. The delivery of an injected steer into a real running turn has not been exercised without a model turn.

## Approvals and run modes

When the agent wants to run a command, edit a file or call a tool, omp asks OmpOS for permission. The **run mode** decides what runs without asking: Auto-review (default), Allowlist or Run Everything. A request that is not auto-approved appears as a tool approval you allow or deny. See [Run modes](../reference/run-modes.md).

## Changes and checkpoints

File edits the agent makes appear in the **Changes** bar with Keep and Undo per file (and Keep all and Undo all), and a per-hunk review. File checkpoints (restore the files to an earlier turn) are built as a library and are Partial: [Checkpoints and worktrees](../reference/checkpoints-and-worktrees.md).

## Where an agent runs

| Where | How | Status |
|---|---|---|
| On this machine | `omp acp` started by the desktop app, in the workspace folder | Built, Linux |
| In a git worktree | A run can work in its own worktree | Partial |
| On another machine | Your own `ssh`, omp on the remote host: [Remote environments](../runbooks/remote-ssh.md) | Partial, never run against a real host |
| In the cloud | A cloud agent on Railway through the API and a worker: [Cloud agents](../runbooks/cloud-agents.md) | Partial, fakes only |
| On the API host | The hosted API runs sessions in process for the web head | Built; in-memory harness echo, no live model turn |

## Hooks, MCP and commands

Hooks let your scripts watch and block agent actions ([Hooks](../reference/hooks.md)). MCP servers give the agent extra tools ([MCP servers](../reference/mcp.md)). Slash commands, skills, rules and subagents found in your workspace files are picked up automatically ([Slash commands](../reference/slash-commands.md)). All three are Partial: built and tested, not run against a live omp session.

## Layers

OmpOS is layered heads, services, libraries, contracts; the presentation layer has no UI framework. See [Architecture](../architecture.md). Words used on these pages: [Glossary](glossary.md).
