---
description: "Glossary of Autonogrammer terms: ACP, agent, approval, checkpoint, cloud agent, Demo, head, hook, omp, plugin, run mode, session, steering and more."
---

# Glossary

Status: Built

Last verified: 2026-10-28

Applies to: v0.15.0

| Term | Meaning |
|---|---|
| ACP | Agent Client Protocol, version 1. The JSON protocol Autonogrammer speaks to `omp acp` over standard input and output. |
| Agent | A coding session you can watch and steer; technically a session. |
| Agents window | The default desktop surface: session rail, thread, composer and the Kanban tab. |
| Approval (tool approval) | A request from omp to run a command, edit a file or call a tool, which you allow or deny unless the run mode auto-approves it. |
| Auto-review | The default run mode: allow rules and a short list of known-safe read-only commands run; everything else asks. Cursor's hosted classifier step is not available. |
| Automation | A saved prompt with a schedule that starts a session locally or a cloud agent. Cron only on the desktop. |
| AGPL-3.0-or-later | The licence of Autonogrammer. Network use is distribution: see [Licensing](../licensing.md). |
| BYOK | Bring your own key: you supply the provider key. |
| Built-in skill | One of ten skills Autonogrammer ships (`review`, `create-hook` and others), found with scope Built-in; a project skill of the same name takes over. |
| Changes bar | The Keep, Undo and Review controls over the files an agent edited. |
| Checkpoint | A saved state of the files before an agent turn, used to rewind. |
| Cloud agent | An agent that runs in a cloud environment (a worker on Railway) instead of on your machine. |
| Coding plan | A subscription from a model vendor that is bring-your-own-key for coding tools, with its own host and usage rules. |
| Container environment | A running podman container that omp runs in through `podman exec`; WSL distributions are the Windows equivalent. |
| Control plane | The part of the API that owns cloud agent records, secrets and limits. |
| Demo | The web head with a simulated engine answered inside the browser; labelled "Demo" on screen. |
| Engine | omp. |
| Graph ID | The identifier `ut-<ts36>-<8hex>` that Ultrathink and Substrate use for one plan of work. |
| GSD-Core | The workflow and planning system whose phases appear as task context; `plugins/gsd-core-omp` binds it to omp. |
| Harness | The libraries that talk to omp and plan its launch (`Autonogrammer.Harness.*`). |
| Head | An application project: desktop (`apps/Autonogrammer.Native`), web (`apps/Autonogrammer.Web`) or Android (`apps/Autonogrammer.Android`). |
| Hook | A script run on an agent event, configured in `hooks.json` in Cursor's format. |
| Kanban | The board with one card per session. |
| MCP | Model Context Protocol: servers that give the agent extra tools. |
| omp | The engine: `omp`, package `@oh-my-pi/pi-coding-agent` (MIT). |
| omp terminal | omp's terminal UI embedded in a terminal view. |
| Plugin | One of the four omp extensions Autonogrammer launches with: Ultrathink, Substrate, Swarm, GSD-Core. |
| Presentation layer | `Autonogrammer.Presentation`: view models with no UI framework, shared by every head. |
| Profile (remote) | A saved remote environment: an `ssh` host and how to start omp there. |
| Railway | The hosting platform intended for the API, the cloud agent worker and PostgreSQL. Only a staged private worker runs there (2026-10-28); the API is not deployed. |
| Run | One turn of work, from your prompt to the agent stopping. |
| Run mode | Auto-review, Allowlist or Run Everything. |
| Session | The append-only log, status and catalogue entry behind an agent. |
| Slash command | A `/name` typed in the composer: an engine command, an Autonogrammer catalogue token, or a command, skill or subagent file. |
| Stage | A step of the agent loop reported by a plugin. |
| Steering | Sending a message to a running turn. |
| Substrate | The plugin for cross-session state; its server is optional. |
| Worker | `Autonogrammer.AgentWorker`, the process inside a cloud agent that bridges the API to omp. |
| Workspace | The folder (or set of folders) an agent works in. |
| Worktree | A separate git working tree used for one run, so the run's edits do not touch yours. |
