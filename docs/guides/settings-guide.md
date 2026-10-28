---
description: "Every page of the OmpOS Settings window with what it does and its status, in the desktop and web heads."
---

# Settings guide

Status: Partial (eleven pages are built and verified on Linux without a model turn; the pages added for v0.15.0 are built as models and views and are being wired into the heads, see the table and the parity audit)

Last verified: 2026-10-28

Applies to: v0.15.0

Open Settings with Ctrl+, in the desktop app, or the Settings button in the web top bar (it opens a dialog). The desktop window is separate and non-modal. The row-by-row decisions against Cursor's settings are in [Settings parity](../settings-parity.md); this page is the user's tour.

![The Settings window on the Models page: provider rows grouped by region, with filters.](../assets/screens/web-settings-models-light.png)

*Screenshot of the Demo build (simulated engine) in the web head. The desktop window has the same pages.*

![The desktop Settings window in the Light theme.](../assets/screens/desktop-settings-light.png)

*Screenshot of the desktop app on Linux.*

## Pages that exist today

| Page | What you do there | Status |
|---|---|---|
| Runtime | Idle stop (default 24 hours, 1 to 720) and how long completed sessions are kept (default 7 days, 1 to 90). | Built |
| General | Theme (Light is the default; System and Dark), motion (follow system, reduce, full), a read-only privacy statement. | Built |
| Agents | The terminal auto-run switch (off by default) and a note that every tool call follows omp's approval policy. | Built; the Run mode choice has its own page below |
| Models | Providers, keys, default model, custom provider form, test connection, and the 103-row catalogue by region and kind. | Built; no real credential was used ([guide](providers-guide.md)) |
| Tab | Read-only status of inline completion: on or off, the provider's host and port. | Built (read-only) |
| Rules | The rule files of the workspace, read-only. | Built (read-only) |
| Skills | The skill files of the workspace, read-only. | Built (read-only) |
| Commands | The slash command catalogue, read-only. | Built (read-only) |
| Plugins | The four plugins connected to every omp process, read-only. There is deliberately no switch: the plugins are always on. | Built (read-only) |
| Network | A **Run diagnostics** button: one unauthenticated GET per known provider endpoint, only when pressed. | Built |
| Keyboard | The shortcuts the shell handles, grouped by scope. Remapping is not built. | Built (read-only) |

## Pages added for v0.15.0

These pages exist as page models and views from the v0.15.0 work. Whether each is reachable from the app you are running is recorded in the [parity audit](../quality/cursor-parity-audit-2026-10-28.md) and in [Settings parity](../settings-parity.md). Until a page appears in the left navigation of your build, treat it as Partial. None has run against a live omp session.

| Page | What it is for | Reference |
|---|---|---|
| Hooks | See the effective hooks and where they came from, approve or revoke a hook version, read the run log. | [Hooks](../reference/hooks.md) |
| MCP | Servers read from OmpOS, Cursor, Claude Code and VS Code files, their state and problems. | [MCP servers](../reference/mcp.md) |
| Remote environments | Profiles for machines reached with your own `ssh`, preflight and diagnosis. | [Remote environments](../runbooks/remote-ssh.md) |
| Cloud agents | Cloud agent list and the new-agent form; needs a configured API with the control plane enabled. | [Cloud agents](../runbooks/cloud-agents.md) |
| Run mode | Auto-review, Allowlist or Run Everything, the read boundary and the allow and deny rules. | [Run modes](../reference/run-modes.md) |
| Workspaces | Several folders in one session. | [Multi-repo](../runbooks/multi-repo.md) |
| Display | Vim keys in the composer, line numbers in code blocks, thinking blocks, status indicators and whether rewind works; the slash commands `/vim`, `/line-numbers`, `/show-thinking` and `/status-indicators` change the same switches. | [Slash commands](../reference/slash-commands.md) |
| Privacy | A statement of what OmpOS does and does not send. | [Licensing](../licensing.md) for the source offer |
| Worktrees | Run in a git worktree and its setup. | [Checkpoints and worktrees](../reference/checkpoints-and-worktrees.md) |
| Ignore | Cursor-style ignore files (gitignore syntax) that hide paths from the agent, from Tab and from `@` mentions. | [Parity audit](../quality/cursor-parity-audit-2026-10-28.md) |

## Settings Cursor has and OmpOS leaves out

OmpOS has no accounts, teams, billing, usage dashboard or hosted Cursor services, so those pages do not exist. Each omission has a reason recorded in [Settings parity](../settings-parity.md) (rows marked Omitted) and counted in the parity audit under OMITTED-BY-DECISION.

## Where settings are stored

| Setting | Stored in |
|---|---|
| Runtime, General, Agents, Display | A settings file under the state directory (`OMPOS_STATE_DIR`) |
| Run mode | `~/.ompos/run-mode.json` |
| Provider keys, custom providers | omp's credential database or `secrets/secrets.json`, and a marked block in omp's `models.yml` ([Choose a model provider](providers-guide.md)) |
| Web head settings | The browser's local storage for theme and motion; nothing for keys |
