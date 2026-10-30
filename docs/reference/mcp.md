---
description: "Reference for MCP servers in Autonogrammer: where configuration is read from, validation, the catalog, the MCP settings page and how servers reach omp sessions."
---

# MCP servers

Status: Partial (loader, catalog, page, 13 presets and the session wiring are built, wired into the desktop head and unit tested; no live omp session or model turn ran; no connection checker; no MCP servers for ssh, container or WSL sessions)

Last verified: 2026-10-28

Applies to: v0.15.0

MCP (Model Context Protocol) servers give the agent extra tools: a documentation search, a database, a browser. Autonogrammer reads the MCP configuration files you already have for Autonogrammer, Cursor, Claude Code and VS Code, checks them, and offers the servers to every session it starts. The MCP settings page shows each server with its state and the problems it found.

> **Status of this release** (recorded 2026-10-28, tree 7535859).
>
> - **Built, wired into the desktop head, proven by tests.** `HeadServices` builds one `McpServerCatalog` per head (`mcp-state.json` in the configuration folder); `SessionEngineSet` refreshes it for each local session and passes `OfferedServers()` to `EngineComposition` as `ExtraMcpServers`, after the plan's own servers (the plan wins a name clash). Settings > MCP servers is the `McpPageView` over `McpCatalogSettingsSource`, added by `SettingsWindowFactory`. Tests: `McpServerCatalogTests`, `McpTemplateCatalogTests`, `McpPresetInstallerTests`, `McpPresetSourceTests`, `McpPresetPageViewTests`, `EngineCompositionOptionsTests`, `HeadServicesWiringTests`. The MCP page rendered in the Xvfb desktop smoke (`docs/runbooks/evidence/desktop-app-2026-10-28/settings-11-mcp.png`).
> - **Built, not wired.** No connection checker is registered (`McpServerCatalog` is built without an `IMcpConnectionChecker`), so *Check connection* is hidden and *Connected*, *Failed* after a check and *Sign-in needed* never appear. Sessions in a remote environment (ssh, container, WSL) get no MCP servers from the local files (`plan.Extras.IsRemote` skips `OfferedServers()`). The web Settings > MCP page only lists the configuration files, read-only (`McpFilesPage`, `WebSettingsPagesTests`); presets, consent and switches exist on the desktop only. Sign-in (OAuth) is modelled as a state only.
> - **Not live-verified.** No live omp session and no model turn ran (open question OD-07), so "omp connects to the server" is **UNVERIFIED**: `omp acp` accepts the `mcpServers` array by its source (see [ADR-0010](../decisions/ADR-0010-omp-mcp-gateway-feasibility.md)). No preset address was contacted; the 13 presets were read from the vendors' documentation on 2026-10-28. Nothing ran on Windows, macOS or Android.

## Why Autonogrammer reads the files itself

`omp acp` starts every session with its own MCP discovery switched off. The only MCP servers an ACP session connects are the ones the client sends in the `mcpServers` array of `session/new`, `session/load`, `session/resume` and `session/fork`. So a `.cursor/mcp.json` that omp would read in a terminal is ignored in Autonogrammer unless Autonogrammer reads it and sends the servers itself. That is what this page does ([ADR-0010](../decisions/ADR-0010-omp-mcp-gateway-feasibility.md), [ADR-0014](../decisions/ADR-0014-mcp-client.md)).

## Where servers are read from

Every file that exists is read. A server with a name that an earlier file already declared is ignored, and the page lists it as a note. Lower number wins.

| Order | Scope | File | Top-level key | Variables |
| --- | --- | --- | --- | --- |
| 1 | Project | `<root>/.autonogrammer/mcp.json` (the Autonogrammer-owned file) | `mcpServers` | Cursor syntax |
| 2 | Project | `<root>/.cursor/mcp.json` | `mcpServers` | Cursor syntax |
| 3 | Project | `<root>/.mcp.json` (Claude Code) | `mcpServers` | Claude syntax |
| 4 | Project | `<root>/.vscode/mcp.json` | `servers` | Cursor syntax |
| 5 | User | `~/.autonogrammer/mcp.json` | `mcpServers` | Cursor syntax |
| 6 | User | `~/.cursor/mcp.json` | `mcpServers` | Cursor syntax |
| 7 | User | `~/.claude.json` | top-level `mcpServers` | Claude syntax |

- A project file beats a user file. In a multi-root workspace an earlier root beats a later one, and the page shows the root's folder name in front of the path.
- `.vscode/mcp.json` may hold comments and trailing commas. `inputs` and `${input:...}` are not supported (see below).
- `~/.claude.json` is a large file that holds more than servers. Only its top-level `mcpServers` is read; the per-project `projects.<path>.mcpServers` entries are **not** read.
- Files Autonogrammer does not read, although omp reads them in a terminal: `~/.omp/agent/mcp.json`, `.omp/mcp.json`, `.claude/mcp.json`, Codex, Gemini, OpenCode and Windsurf files. Say so in an issue if you need one.
- Autonogrammer writes one file only: `.autonogrammer/mcp.json` of the first workspace root, when you use *Add from template* or *Add from preset*. Cursor, Claude and VS Code files are never changed.
- A file over 2 MiB, a file that is not valid JSON and a file you cannot read each become one problem on the page. The other files are still read.

## The entry

```json
{
  "mcpServers": {
    "files": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "${workspaceFolder}"],
      "env": { "LOG_LEVEL": "warn" }
    },
    "search": {
      "url": "https://mcp.example.com/mcp",
      "headers": { "Authorization": "Bearer ${env:SEARCH_TOKEN}" }
    },
    "legacy": { "type": "sse", "url": "https://legacy.example.com/sse" }
  }
}
```

| Field | Applies to | Meaning |
| --- | --- | --- |
| `command`, `args`, `env` | stdio | omp starts the command as a child process and speaks MCP over its standard input and output. `args` and `env` values may be strings, numbers or booleans; they are passed as strings. |
| `url`, `headers` | http, sse | An absolute `http` or `https` URL and the request headers. A URL without `type` is Streamable HTTP. |
| `type` | all | `stdio`, `http` (also `streamable-http`) or `sse`. Needed only when the entry has both `command` and `url`, or for SSE. |
| `disabled: true`, `enabled: false` | all | The server is off. A choice you make on the page wins over the file. |
| `auth`, `oauth` | remote | Remembered and reported. **Not passed to omp.** See [Sign-in](#sign-in-oauth). |
| `envFile`, `cwd` | stdio | **Not passed**, with a warning. The ACP entry of a stdio server has only `command`, `args` and `env`. Move the variables into `env`. |

A server name is 1 to 64 letters, digits, `_`, `-` or `.`. omp registers the server under its name and puts it in front of the server's tool names, so spaces and other characters are refused (UNVERIFIED: how omp prefixes tool names; the narrow alphabet is chosen to be safe whatever it does).

Limits: 64 servers per file, 256 arguments, 128 environment variables or headers per server, 8,192 characters per value, JSON nested at most 32 deep. An entry over a limit is dropped with a problem; it never stops the rest of the file.

## Variables

Variables are expanded in `command`, `args`, `env` values, `url` and `headers` values.

| Syntax | Files | Expands to |
| --- | --- | --- |
| `${env:NAME}` | all | The environment variable `NAME` of the Autonogrammer process. A variable that is set to an empty string is set. |
| `${workspaceFolder}` | all | The workspace root that holds the file. In a user file: the first workspace root. |
| `${workspaceFolderBasename}` | all | The last segment of that folder. |
| `${userHome}` | all | The user's home directory. |
| `${pathSeparator}`, `${/}` | all | `/` or `\`. |
| `${NAME}`, `${NAME:-default}` | `.mcp.json`, `~/.claude.json` | The environment variable, or the default after `:-`. |
| `${input:id}` | VS Code | **Not supported.** The server is not offered. |

**A variable that cannot be expanded is never replaced by an empty string.** The text stays as written, the page shows an error that names the variable (never a value) and the field, and the server is marked *Failed* and is not offered to sessions until the variable is set and you reload. A `${...}` that is not a variable of the file's format (for example `${HOME}` in a Cursor file) is passed on unchanged and reported as a warning, because it may be text for a shell.

## What is passed to omp

Autonogrammer sends only servers that are all of: switched on, complete (every variable expanded, a command or a URL present) and, where required, approved. The entries are the ACP `McpServer` objects omp's own client sends: the field names are read from the ACP client in `Autonogrammer.Harness.Acp` and the omp source recorded in [ADR-0010](../decisions/ADR-0010-omp-mcp-gateway-feasibility.md).

```json
[
  { "name": "files", "command": "npx", "args": ["-y", "@modelcontextprotocol/server-filesystem", "/work/app"], "env": [{ "name": "LOG_LEVEL", "value": "warn" }] },
  { "type": "http", "name": "search", "url": "https://mcp.example.com/mcp", "headers": [{ "name": "Authorization", "value": "Bearer …" }] },
  { "type": "sse", "name": "legacy", "url": "https://legacy.example.com/sse", "headers": [] }
]
```

`env` and `headers` are lists of `{name, value}` pairs in name order. The array is sent when a session is created, loaded, resumed or forked; a change to your files applies to the next session, not to one that is running.

## Approval

A project file arrives with a repository, so it is not trusted the way your own `~/.cursor/mcp.json` is. A project server needs your approval before Autonogrammer offers it to a session when it

- starts a process (every stdio server), or
- reads the environment (`${env:NAME}`, `${NAME}`) in a remote server, because its URL or headers could otherwise collect a secret from your environment.

User files and project remote servers that read no variable need no approval.

The page shows the exact command as written, the names of the environment variables and headers, and nothing else. *Approve* records consent for that **exact declaration**: a hash of the command, arguments, environment, URL and headers as written, before variables are expanded. The same file gives the same hash under any checkout directory and on any machine. Change one argument and the server asks again. The hash does not cover the content of a script the command names (`node ./server.js`): review scripts as you review code. Approving starts nothing; omp starts the server when a session needs it.

Approvals and your on/off choices live in `mcp-state.json` under the per-user application data folder (`~/.config/Autonogrammer` on Linux). A damaged or unknown-version file counts as "nothing approved" and is moved aside as `mcp-state.json.damaged`.

## States

| State | Meaning | Offered to sessions |
| --- | --- | --- |
| Ready | Read and valid; no connection check has run. | yes |
| Needs approval | A project server you have not approved in this exact form. | no |
| Off | Switched off, in the file or on the page. | no |
| Connecting | A connection check is running. | yes |
| Connected | A connection check succeeded; the page shows the tool count. | yes |
| Failed | A variable is not set, or a connection check failed. The page shows the reason. | not when a variable is missing; yes after a failed check |
| Sign-in needed | A connection check got an authentication challenge. | yes |

*Check connection* starts the server (stdio) or opens a connection (remote) once, lists its tools and closes it. It runs only when you press the button, and only for a server that would be offered. Every environment value, header value and URL is removed from a failure reason before the page shows it.

A failed check does not remove a server from the sessions: the check runs on the machine that runs Autonogrammer, and omp may run elsewhere (a remote host, a sandbox).

## Sign-in (OAuth)

Autonogrammer does **not** implement MCP OAuth. What exists is the state and the instruction:

- A remote server that answers a check with an authentication challenge shows *Sign-in needed* and the sentence "Run `/mcp reauth NAME` in the session". omp's `/mcp` command (`add`, `list`, `remove`, `test`, `reauth`, `unauth`, `enable`, `disable`, `reconnect`) runs the sign-in and **opens the browser on the machine omp runs on**. For a remote or cloud session that is not your screen.
- Cursor signs in with authorization code and PKCE, a static client (`auth: {CLIENT_ID, CLIENT_SECRET, scopes}`) and the fixed redirect `http://localhost:8787/callback`. omp has its own `oauth` block (`clientId`, `clientSecret`, `scope`, `redirectUri`, `callbackPort`, `callbackPath`). Autonogrammer passes neither block to omp: an ACP `mcpServers` entry has no OAuth field. A server that needs a token you already have can take it as a header: `"Authorization": "Bearer ${env:NAME}"`.
- There is no built-in check yet (see [Status by head](#status-by-head)), so *Sign-in needed* appears only once a head provides a connection checker.

## Not supported, and why

| Not supported | Why |
| --- | --- |
| Elicitation (a server asks you a question in the chat) and sampling (a server asks the model for a completion) | omp's ACP mode does not support server-initiated requests of either kind. |
| OAuth flows inside Autonogrammer | See above: omp owns the flow and the ACP entry has no field for it. |
| `envFile`, `cwd` | The ACP stdio entry has only `command`, `args` and `env`. |
| `${input:id}` and VS Code `inputs` | Autonogrammer has no input prompts at session start. |
| Per-tool allow lists, `mcpAllowlist`, enterprise network modes | Tool approval is omp's, and Autonogrammer adds no second policy. |
| MCP resources, roots and the Apps extension in the chat | They live inside omp's MCP client. Autonogrammer neither blocks nor renders them: UNVERIFIED what omp does with them in an ACP session. MCP prompts reach you as custom slash commands, which omp provides. |
| Hot reload of a running session | The array is fixed when the session is created. |

## Presets

Settings > MCP servers > Add from preset lists 13 ready-made entries (`McpTemplateCatalog.Default`, `src/Autonogrammer.Harness.Mcp/Templates/`). *Add* (`McpServerCatalog.AddPresetAsync`) writes the entry to `.autonogrammer/mcp.json` of the first workspace root and switches it off. A preset holds no credential: read its note, sign in or export the named variable, then switch it on; the [approval rules](#approval) apply as for any project server. A preset that is already configured shows *Added*. The *Add from template* section of the same page adds a blank stdio or HTTP skeleton.

| Preset | Kind | Sign-in |
| --- | --- | --- |
| Linear (`https://mcp.linear.app/mcp`) | remote | OAuth |
| Notion (`https://mcp.notion.com/mcp`) | remote | OAuth |
| Sentry (`https://mcp.sentry.dev/mcp`) | remote | OAuth |
| GitHub (`https://api.githubcopilot.com/mcp/`) | remote | token in `GITHUB_PERSONAL_ACCESS_TOKEN` |
| Railway (`https://mcp.railway.com`) | remote | OAuth |
| Atlassian (`https://mcp.atlassian.com/v2/mcp`) | remote | OAuth |
| Stripe (`https://mcp.stripe.com`) | remote | OAuth |
| Supabase (`https://mcp.supabase.com/mcp`) | remote | OAuth |
| Vercel (`https://mcp.vercel.com`) | remote | OAuth |
| Context7 (`https://mcp.context7.com/mcp`) | remote | token in `CONTEXT7_API_KEY` |
| Filesystem (`npx -y @modelcontextprotocol/server-filesystem ${workspaceFolder}`) | stdio | none; needs Node.js 24 or later |
| Fetch (`uvx mcp-server-fetch`) | stdio | none; needs uv |
| Git (`uvx mcp-server-git --repository ${workspaceFolder}`) | stdio | none; needs uv; the server can change the repository |

Each preset cites the vendor page it was read from. A preset that cannot be confirmed is marked `Unverified` and left out of the list (`McpPresetStatus`); none is unverified in this release.

## Status by head

| Head | State |
| --- | --- |
| Desktop, local session | Wired: files read, servers offered to `session/new` (and `load`, `resume`, `fork` through the same array), page, presets. |
| Desktop, ssh, container or WSL session | Not wired: the local files are skipped; nothing is offered. |
| Web | Read-only list of the MCP files and their problems. |
| API-hosted and cloud sessions | Not wired: the API only lists the MCP files for the web page (`WorkspaceSettingsEndpoints`); the cloud worker sends no servers. |
| Connection checker | Not built: it needs a Streamable HTTP transport that carries headers; `McpClientConnector.ConnectHttpAsync` takes only a URL. |

## For developers

| Piece | Where |
| --- | --- |
| Loader, parser, variable expansion, consent identity, ACP mapper | `src/Autonogrammer.Harness.Mcp/Config/` |
| Catalog (states, consent, switches, checks, template), state store | `src/Autonogrammer.Harness.Mcp/Catalog/` |
| Page model and copy (no UI framework) | `src/Autonogrammer.Presentation/Mcp/` |
| Desktop page and the catalog adapter | `src/Autonogrammer.Native.Views/Views/Mcp/McpPageView.axaml`, `McpCatalogSettingsSource.cs` |
| Tests | `tests/Autonogrammer.Harness.Mcp.Tests/Config/`, `tests/Autonogrammer.Presentation.Tests/Mcp/`, `tests/Autonogrammer.Native.Tests/Mcp/` |

Secrets: a diagnostic, a status reason, a preview and `ToString()` of a server name files, servers, fields and variable names and the host of a URL. They never carry an environment value, a header value, an argument or a URL path. A JSON syntax error is reported as a line and a column, not as the text it stopped at. The tests plant canary strings in every secret position and assert that none reaches any displayed or logged text.
