---
description: "OmpOS documentation: an agent workspace on the omp engine with a desktop app, a web app, cloud agents and remote environments. Install, configure, use every feature and see what is built, partial or blocked."
---

# OmpOS documentation

Status: Built (this page is the entry point; every feature page states its own status)

Last verified: 2026-10-28

Applies to: v0.15.0

OmpOS is a workspace for coding agents, aimed at the Cursor 3.27 feature set under an open licence (AGPL-3.0-or-later). Its engine is [omp](https://github.com/can1357/oh-my-pi): you open a **desktop app** or a **web app**, start several agents, watch them work, steer them while they run, review their file changes, and optionally run them in a git worktree, on another machine over `ssh`, or in the cloud. Every omp process OmpOS starts runs with four plugins: Ultrathink, Substrate, Swarm and GSD-Core.

These pages say only what is implemented. Where something is built but has not been exercised live (no model turn, no Windows or macOS run, no Railway deploy) the page says so, and the [parity audit](quality/cursor-parity-audit-2026-10-28.md) lists every Cursor feature with its status.

![The OmpOS web app in the Demo build, Light theme: the agent rail, a streaming thread and the composer.](assets/screens/web-agents-light.png)

*Screenshot of the Demo build: a simulated engine produces the thread; no model is called.*

![The OmpOS desktop app on Linux, Light theme: the Agents window.](assets/screens/desktop-agents-light.png)

*Screenshot of the desktop app on Linux.*

## Start here

| I want to | Read |
|---|---|
| Install and run OmpOS | [Getting started](guides/getting-started.md) |
| Use the desktop app | [Desktop app guide](guides/desktop-app-guide.md) |
| Use the web app, or see the Demo | [Web app guide](guides/web-app-guide.md) |
| Understand the ideas | [Concepts](guides/concepts.md), [Architecture](architecture.md), [Glossary](guides/glossary.md) |
| Pick a model provider and add a key | [Choose a model provider](guides/providers-guide.md) |
| Find a setting | [Settings guide](guides/settings-guide.md) |
| Fix a problem | [Troubleshooting and FAQ](guides/troubleshooting-and-faq.md) |
| See what is built and what is not | [Status and roadmap](guides/status-and-roadmap.md) |

## Features at a glance

| Feature | Status | Page |
|---|---|---|
| Agents window, thread, composer, Kanban, Changes bar, steering (desktop, Linux) | Built | [Desktop app guide](guides/desktop-app-guide.md) |
| Web app and Demo | Partial | [Web app guide](guides/web-app-guide.md) |
| Model providers and coding plans (103 rows) | Partial | [Choose a model provider](guides/providers-guide.md) |
| Slash commands (92 Cursor tokens) | Partial | [Slash commands](reference/slash-commands.md) |
| Hooks | Partial | [Hooks](reference/hooks.md) |
| Remote environments over ssh | Partial | [Remote environments](runbooks/remote-ssh.md) |
| Multi-repo workspaces | Partial | [Multi-repo](runbooks/multi-repo.md) |
| Cloud agents on Railway | Partial | [Cloud agents](runbooks/cloud-agents.md) |
| Run modes and permissions | Partial | [Run modes](reference/run-modes.md) |
| MCP servers | Partial | [MCP servers](reference/mcp.md) |
| Checkpoints and worktrees | Partial | [Checkpoints and worktrees](reference/checkpoints-and-worktrees.md) |
| HTTP API | Built | [HTTP API](reference/api.md) |

## The documentation site

This documentation publishes through GitBook's GitHub Git Sync, with the repository as the source of truth. It is published after the owner connects GitBook, see [Publish the docs with GitBook](runbooks/gitbook-publish.md); until a live URL is confirmed, read these pages on GitHub.

## Project

[Licensing](licensing.md) · [Contributing, security and support](guides/community.md) · [Architecture decision records](decisions/README.md) · [Open questions](open-questions.md) · [Changelog](https://github.com/swcstudiospace/OmpOS/blob/main/CHANGELOG.md)
