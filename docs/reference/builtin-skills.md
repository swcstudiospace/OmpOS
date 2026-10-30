---
description: "Reference for the 10 skills Autonogrammer ships (create-hook, review, agent-review and more), how built-in skills are discovered, and the 13 MCP presets."
---

# Built-in skills and MCP presets

Status: Partial (10 skills discovered by the desktop slash palette and Skills page; 13 MCP presets on the desktop MCP page; ssh, container, WSL, API-hosted and web sessions carry no built-in skills; no live omp session or model turn ran)

Last verified: 2026-10-28

Applies to: v0.15.0

Autonogrammer ships a small set of skills inside the application. They are plain `SKILL.md` files with Autonogrammer's own wording, found automatically, and run like any skill you add yourself: type the name after a slash. This page lists what ships, what does not and why, how they are discovered, and the MCP presets that come with the MCP page (the same presets are listed in [MCP servers](mcp.md#presets)).

> **Status of this release** (recorded 2026-10-28, tree 7535859).
>
> - **Built, wired into the desktop head, proven by tests.** `ConventionSlashDiscoverySource` and `ConventionSettingsLoader` build their sources with `ConventionSources.Local(...).WithBuiltin()`, so the desktop slash palette and Settings > Skills list the 10 skills with the scope `builtin`; the `skills` folder is copied next to the application by `Autonogrammer.Harness.Workspace.csproj`. Tests: `BuiltinSkillsDesktopTests` (the real `skills` folder: the 10 tokens resolve to built-in skills, `/review` resolves to the Skill badge with the scope `BuiltIn`, a project skill takes over, palette rows, the Skills page) and `BuiltinSkillSourceTests`, `BuiltinSkillDiscoveryTests`, `BuiltinSkillManifestTests` in `Autonogrammer.Harness.Workspace.Tests`. In the slash catalogue the 10 tokens are Built with the Skill handler.
> - **Built, not wired.** API-hosted sessions carry no built-in skills (`ConventionsReader` does not add the source, and `ConventionsMapper` skips built-in entries), so the web head shows none. Sessions in a remote environment (ssh, container, WSL) use `RemoteConventionSources`, which has no built-in source, and the remote slash discovery lists what the remote workspace has. The custom-mode picker (`ConventionModeSkillSource`) and the @ mention provider (`ConventionMentionProvider`) do not include built-in skills.
> - **Not live-verified.** No live omp session ran one of the skills and no model turn ran (open question OD-07); the published layout of the `skills` folder was not verified by `dotnet publish` (the `bin` layout was). Nothing ran on Windows, macOS or Android.

## What ships

Each skill lives in `assets/skills/<name>/SKILL.md` in the repository and is copied to a `skills` folder next to the application. All of them are offered after a slash and may be picked by the model (`user-invocable: true`, `disable-model-invocation: false`). None of them changes a file, a branch or a remote without telling you what it will do first.

| Slash command | What it does | Writes files? |
| --- | --- | --- |
| `/create-hook` | Writes a hook script and its entry in `.autonogrammer/hooks.json` (or `~/.autonogrammer/hooks.json`), using the schema in [Hooks](hooks.md). Adds to the file; never rewrites existing entries. | Yes: the script and the entry, after asking what the hook should do |
| `/create-rule` | Chooses between an `AGENTS.md` section and a `.mdc` rule (always, intelligent, file scoped, manual) and writes it. | Yes: one rule file |
| `/create-skill` | Creates `.autonogrammer/skills/<name>/SKILL.md` (or the folder the repository already uses) with a precise description and steps. | Yes: one skill folder |
| `/create-subagent` | Writes `.autonogrammer/agents/<name>.md` with a focused role, read-only by default for reviewers. | Yes: one definition file |
| `/migrate-to-skills` | Lists custom commands (and, on request, agent-decided rules), asks which to convert, writes skills. Keeps the originals until you name them for deletion. | Yes, after confirmation |
| `/review` | General review of the working diff, a named change or the last commit: correctness, regressions, tests, maintainability. | No |
| `/review-security` | Security review with a checklist and evidenced, ranked findings. | No |
| `/agent-review` | Review of everything that differs from the base branch. **Quick** is one pass over the diff; **Deep** is one pass per file in risk order, then a cross-file pass. Severity-ranked findings with `path:line` and a suggested fix. Reads `BUGBOT.md`, `AGENTS.md` and matching rules as extra criteria. Edits nothing unless you ask for a fix afterwards. | No |
| `/split-to-prs` | Plans a stack of small branches from one large change, shows the plan, and only after you approve it makes a backup branch and the stack. Never force-pushes; never pushes or opens pull requests unless asked. | Git branches and commits, after approval |
| `/update-settings` | Says where an Autonogrammer setting lives and how to change it. Prefers the Settings window; edits `config/settings.json` only with a backup and a yes. Never writes keys. Replaces `/update-cursor-settings` and `/update-cli-config`. | Only on request, with a backup |

`/agent-review` is the local half of Cursor's Agent Review. The automatic run after a commit and the Source Control tab entry are part of the Changes panel, not of this skill.

## What does not ship, and why

These Cursor built-in skills have no Autonogrammer counterpart. Their slash tokens stay in the [command catalogue](slash-commands.md) as unavailable, with the reason.

| Token | Why |
| --- | --- |
| `/automate` | Creates Cursor Automations, a Cursor cloud service. Autonogrammer has no automation service to target. |
| `/autopilot` | Drives a pull request through a Cursor cloud agent; it needs the cloud-agent service Autonogrammer does not reproduce. |
| `/canvas` | Renders interactive artifacts in a Cursor pane. Autonogrammer has no canvas surface. |
| `/cursor-blame` | Reads Cursor's server-side record of AI-authored changes and their prompts. Autonogrammer keeps no such record. |
| `/loop` | Needs a scheduler that re-runs a prompt. omp's own `/loop` works only in its terminal interface and is not exposed over ACP. |
| `/review-bugbot` | Bugbot is a Cursor-hosted reviewer. `/review` and `/agent-review` cover the local review. |
| `/sdk` | Helps build with the Cursor SDK. Autonogrammer has no SDK of its own. |
| `/shell` | Shell mode is a built-in terminal action of Autonogrammer, not a skill. |
| `/statusline` | Configures the Cursor command-line status line. Autonogrammer has no scriptable status line. |
| `/update-cli-config`, `/update-cursor-settings` | Edit Cursor's own configuration files. Autonogrammer has none; `/update-settings` and `/config` cover Autonogrammer settings. |

The machine-readable lists are `BuiltinSkillManifest.Shipped` and `BuiltinSkillManifest.NotApplicable` in `Autonogrammer.Workspace.BuiltinSkills`; a test compares the shipped list with the bundled folders.

## How they are found

- The scope is `builtin` (`ConventionScope.Builtin`). Its origin is shown as `builtin:skills/<name>/SKILL.md`.
- It has the **lowest precedence**. A project or user skill of the same name wins, in any convention folder (`.autonogrammer`, `.omp`, `.cursor`, `.claude`, `.codex`, `.agents`). The built-in skill that lost is listed as a `Duplicate` diagnostic ("'review' is already provided by ..."), so you can see which of your skills hides a built-in one. To change a built-in skill, copy its folder into `.autonogrammer/skills` and edit it.
- A missing `skills` folder (a head that does not bundle them, or a trimmed install) is not an error: nothing is found and nothing is reported.
- The source reads only inside the `skills` folder.
- A custom command with the same name as a skill still runs first (commands rank before skills in the palette).

### Where it is switched on

Discovery adds the built-in skills when the sources carry a `BuiltinSkillSource`:

```csharp
var sources = ConventionSources.Local(roots, userHome).WithBuiltin();
```

`WithBuiltin()` uses the `skills` folder in `AppContext.BaseDirectory`; pass a `BuiltinSkillSource` to use another folder (tests do). Two call sites make the call: `ConventionSlashDiscoverySource` (the desktop slash palette) and `ConventionSettingsLoader` (the desktop Skills page). The API's conventions reader, the remote setup, the custom-mode source and the mention provider build `ConventionSources.Local(...)` without it.

## MCP presets

Settings > MCP servers > Add from preset offers the presets below (the same list as in [MCP servers](mcp.md#presets); *Add from template* on the same page adds two blank skeletons). A preset is written to the Autonogrammer-owned `.autonogrammer/mcp.json` and stays **switched off** until you enable it. No preset holds a credential: a remote server signs in through OAuth in a browser, or reads its token from an environment variable named in the entry. The presets are tested by `McpTemplateCatalogTests`, `McpPresetInstallerTests`, `McpPresetSourceTests` and `McpPresetPageViewTests`; none was contacted live.

Every address below was read from the vendor's own documentation on 2026-10-28. An address that cannot be confirmed is marked `Unverified` in `McpTemplateCatalog.All` and is left out of the default list; none is unverified today.

| Preset | Address or command | Sign-in | Source |
| --- | --- | --- | --- |
| Linear | `https://mcp.linear.app/mcp` | OAuth | linear.app/docs/mcp |
| Notion | `https://mcp.notion.com/mcp` | OAuth | developers.notion.com/guides/mcp/get-started-with-mcp |
| Sentry | `https://mcp.sentry.dev/mcp` | OAuth | docs.sentry.io/product/sentry-mcp |
| GitHub | `https://api.githubcopilot.com/mcp/` | Bearer token from `GITHUB_PERSONAL_ACCESS_TOKEN` | the `docs/remote-server.md` page of github/github-mcp-server |
| Railway | `https://mcp.railway.com` | OAuth | docs.railway.com/ai/mcp-server |
| Atlassian | `https://mcp.atlassian.com/v2/mcp` | OAuth | Atlassian Rovo MCP server getting-started page |
| Stripe | `https://mcp.stripe.com` | OAuth | docs.stripe.com/mcp |
| Supabase | `https://mcp.supabase.com/mcp` | OAuth | supabase.com/docs/guides/getting-started/mcp |
| Vercel | `https://mcp.vercel.com` | OAuth | vercel.com/docs/agent-resources/vercel-mcp |
| Context7 | `https://mcp.context7.com/mcp` | Bearer token from `CONTEXT7_API_KEY` | context7.com/docs/resources/all-clients |
| Filesystem | `npx -y @modelcontextprotocol/server-filesystem ${workspaceFolder}` | none | modelcontextprotocol/servers, `src/filesystem` |
| Fetch | `uvx mcp-server-fetch` | none | modelcontextprotocol/servers, `src/fetch` |
| Git | `uvx mcp-server-git --repository ${workspaceFolder}` | none | modelcontextprotocol/servers, `src/git` |

The vendors' own pages say to run the fetch and git reference servers with `uvx` (Python); only the filesystem server is an `npx` package, and Autonogrammer uses the commands as documented. Each preset's note (prerequisites, read-only variants, scoping) is in `McpPreset.Note`.

`McpPresetInstaller.AddPresetAsync` writes a preset the same way the blank skeletons are written: other servers and keys in the file are kept, a taken name is refused, an unreadable or invalid file is never overwritten, and only `.autonogrammer/mcp.json` is touched.
