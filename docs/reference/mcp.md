---
description: "Reference for MCP servers in OmpOS: where configuration is read from, validation, the catalog, the MCP settings page and how servers reach omp sessions."
---

# MCP servers

Status: Partial (loader, catalog and page are built and unit tested; not run against a live omp session; head wiring is tracked in the parity audit)

Last verified: 2026-10-28

Applies to: v0.15.0

MCP (Model Context Protocol) servers give the agent extra tools: a documentation search, a database, a browser. OmpOS reads the MCP configuration files you already have for OmpOS, Cursor, Claude Code and VS Code, checks them, and offers the servers to every session it starts. The MCP settings page shows each server with its state and the problems it found.

> **Status of this release.** The loader, the catalog, the settings page and its tests are built. The application heads do not yet pass the offered servers to `session/new` and the page is not yet in the settings navigation: both are wired in a later change (see [What is not wired yet](#what-is-not-wired-yet)). Nothing here has run against a live omp session, so "omp connects to the server" is **UNVERIFIED**: `omp acp` accepts the `mcpServers` array by its source (see [ADR-0010](../decisions/ADR-0010-omp-mcp-gateway-feasibility.md)), and a live turn needs the owner's approval to confirm it. Sign-in (OAuth) is modelled as a state only.

## Why OmpOS reads the files itself

`omp acp` starts every session with its own MCP discovery switched off. The only MCP servers an ACP session connects are the ones the client sends in the `mcpServers` array of `session/new`, `session/load`, `session/resume` and `session/fork`. So a `.cursor/mcp.json` that omp would read in a terminal is ignored in OmpOS unless OmpOS reads it and sends the servers itself. That is what this page does ([ADR-0010](../decisions/ADR-0010-omp-mcp-gateway-feasibility.md), [ADR-0014](../decisions/ADR-0014-mcp-client.md)).

## Where servers are read from

Every file that exists is read. A server with a name that an earlier file already declared is ignored, and the page lists it as a note. Lower number wins.

| Order | Scope | File | Top-level key | Variables |
| --- | --- | --- | --- | --- |
| 1 | Project | `<root>/.ompos/mcp.json` (the OmpOS-owned file) | `mcpServers` | Cursor syntax |
| 2 | Project | `<root>/.cursor/mcp.json` | `mcpServers` | Cursor syntax |
| 3 | Project | `<root>/.mcp.json` (Claude Code) | `mcpServers` | Claude syntax |
| 4 | Project | `<root>/.vscode/mcp.json` | `servers` | Cursor syntax |
| 5 | User | `~/.ompos/mcp.json` | `mcpServers` | Cursor syntax |
| 6 | User | `~/.cursor/mcp.json` | `mcpServers` | Cursor syntax |
| 7 | User | `~/.claude.json` | top-level `mcpServers` | Claude syntax |

- A project file beats a user file. In a multi-root workspace an earlier root beats a later one, and the page shows the root's folder name in front of the path.
- `.vscode/mcp.json` may hold comments and trailing commas. `inputs` and `${input:...}` are not supported (see below).
- `~/.claude.json` is a large file that holds more than servers. Only its top-level `mcpServers` is read; the per-project `projects.<path>.mcpServers` entries are **not** read.
- Files OmpOS does not read, although omp reads them in a terminal: `~/.omp/agent/mcp.json`, `.omp/mcp.json`, `.claude/mcp.json`, Codex, Gemini, OpenCode and Windsurf files. Say so in an issue if you need one.
- OmpOS writes one file only: `.ompos/mcp.json` of the first workspace root, when you use *Add from template*. Cursor, Claude and VS Code files are never changed.
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
| `${env:NAME}` | all | The environment variable `NAME` of the OmpOS process. A variable that is set to an empty string is set. |
| `${workspaceFolder}` | all | The workspace root that holds the file. In a user file: the first workspace root. |
| `${workspaceFolderBasename}` | all | The last segment of that folder. |
| `${userHome}` | all | The user's home directory. |
| `${pathSeparator}`, `${/}` | all | `/` or `\`. |
| `${NAME}`, `${NAME:-default}` | `.mcp.json`, `~/.claude.json` | The environment variable, or the default after `:-`. |
| `${input:id}` | VS Code | **Not supported.** The server is not offered. |

**A variable that cannot be expanded is never replaced by an empty string.** The text stays as written, the page shows an error that names the variable (never a value) and the field, and the server is marked *Failed* and is not offered to sessions until the variable is set and you reload. A `${...}` that is not a variable of the file's format (for example `${HOME}` in a Cursor file) is passed on unchanged and reported as a warning, because it may be text for a shell.

## What is passed to omp

OmpOS sends only servers that are all of: switched on, complete (every variable expanded, a command or a URL present) and, where required, approved. The entries are the ACP `McpServer` objects omp's own client sends: the field names are read from the ACP client in `OmpOS.Harness.Acp` and the omp source recorded in [ADR-0010](../decisions/ADR-0010-omp-mcp-gateway-feasibility.md).

```json
[
  { "name": "files", "command": "npx", "args": ["-y", "@modelcontextprotocol/server-filesystem", "/work/app"], "env": [{ "name": "LOG_LEVEL", "value": "warn" }] },
  { "type": "http", "name": "search", "url": "https://mcp.example.com/mcp", "headers": [{ "name": "Authorization", "value": "Bearer …" }] },
  { "type": "sse", "name": "legacy", "url": "https://legacy.example.com/sse", "headers": [] }
]
```

`env` and `headers` are lists of `{name, value}` pairs in name order. The array is sent when a session is created, loaded, resumed or forked; a change to your files applies to the next session, not to one that is running.

## Approval

A project file arrives with a repository, so it is not trusted the way your own `~/.cursor/mcp.json` is. A project server needs your approval before OmpOS offers it to a session when it

- starts a process (every stdio server), or
- reads the environment (`${env:NAME}`, `${NAME}`) in a remote server, because its URL or headers could otherwise collect a secret from your environment.

User files and project remote servers that read no variable need no approval.

The page shows the exact command as written, the names of the environment variables and headers, and nothing else. *Approve* records consent for that **exact declaration**: a hash of the command, arguments, environment, URL and headers as written, before variables are expanded. The same file gives the same hash under any checkout directory and on any machine. Change one argument and the server asks again. The hash does not cover the content of a script the command names (`node ./server.js`): review scripts as you review code. Approving starts nothing; omp starts the server when a session needs it.

Approvals and your on/off choices live in `mcp-state.json` under the per-user application data folder (`~/.config/OmpOS` on Linux). A damaged or unknown-version file counts as "nothing approved" and is moved aside as `mcp-state.json.damaged`.

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

A failed check does not remove a server from the sessions: the check runs on the machine that runs OmpOS, and omp may run elsewhere (a remote host, a sandbox).

## Sign-in (OAuth)

OmpOS does **not** implement MCP OAuth. What exists is the state and the instruction:

- A remote server that answers a check with an authentication challenge shows *Sign-in needed* and the sentence "Run `/mcp reauth NAME` in the session". omp's `/mcp` command (`add`, `list`, `remove`, `test`, `reauth`, `unauth`, `enable`, `disable`, `reconnect`) runs the sign-in and **opens the browser on the machine omp runs on**. For a remote or cloud session that is not your screen.
- Cursor signs in with authorization code and PKCE, a static client (`auth: {CLIENT_ID, CLIENT_SECRET, scopes}`) and the fixed redirect `http://localhost:8787/callback`. omp has its own `oauth` block (`clientId`, `clientSecret`, `scope`, `redirectUri`, `callbackPort`, `callbackPath`). OmpOS passes neither block to omp: an ACP `mcpServers` entry has no OAuth field. A server that needs a token you already have can take it as a header: `"Authorization": "Bearer ${env:NAME}"`.
- The built-in check of this release does not exist yet (see below), so *Sign-in needed* appears only once a head provides a connection checker.

## Not supported, and why

| Not supported | Why |
| --- | --- |
| Elicitation (a server asks you a question in the chat) and sampling (a server asks the model for a completion) | omp's ACP mode does not support server-initiated requests of either kind. |
| OAuth flows inside OmpOS | See above: omp owns the flow and the ACP entry has no field for it. |
| `envFile`, `cwd` | The ACP stdio entry has only `command`, `args` and `env`. |
| `${input:id}` and VS Code `inputs` | OmpOS has no input prompts at session start. |
| Per-tool allow lists, `mcpAllowlist`, enterprise network modes | Tool approval is omp's, and OmpOS adds no second policy. |
| MCP resources, roots and the Apps extension in the chat | They live inside omp's MCP client. OmpOS neither blocks nor renders them: UNVERIFIED what omp does with them in an ACP session. MCP prompts reach you as custom slash commands, which omp provides. |
| Hot reload of a running session | The array is fixed when the session is created. |

## What is not wired yet

The code is complete and tested on its own, and these three connections are the next change. They are listed so nothing looks finished that is not.

1. `session/new`, `load`, `resume` and `fork` are not yet given `McpServerCatalog.OfferedServers()`. Until they are, no configured server reaches a model turn.
2. The settings window has no *MCP servers* entry yet; the page exists as `McpPageView` over `McpSettingsViewModel`.
3. No connection checker is registered: *Check connection* is hidden and *Connected*, *Failed* and *Sign-in needed* cannot appear from a check. The checker needs a Streamable HTTP transport that carries headers; the current `McpClientConnector.ConnectHttpAsync` takes only a URL.

## For developers

| Piece | Where |
| --- | --- |
| Loader, parser, variable expansion, consent identity, ACP mapper | `src/OmpOS.Harness.Mcp/Config/` |
| Catalog (states, consent, switches, checks, template), state store | `src/OmpOS.Harness.Mcp/Catalog/` |
| Page model and copy (no UI framework) | `src/OmpOS.Presentation/Mcp/` |
| Desktop page and the catalog adapter | `src/OmpOS.Native.Views/Views/Mcp/McpPageView.axaml`, `McpCatalogSettingsSource.cs` |
| Tests | `tests/OmpOS.Harness.Mcp.Tests/Config/`, `tests/OmpOS.Presentation.Tests/Mcp/`, `tests/OmpOS.Native.Tests/Mcp/` |

Secrets: a diagnostic, a status reason, a preview and `ToString()` of a server name files, servers, fields and variable names and the host of a URL. They never carry an environment value, a header value, an argument or a URL path. A JSON syntax error is reported as a line and a column, not as the text it stopped at. The tests plant canary strings in every secret position and assert that none reaches any displayed or logged text.
