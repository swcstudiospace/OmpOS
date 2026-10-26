# ADR-0010: Feasibility of a host-owned MCP gateway for omp

- Status: Proposed
- Date: 2026-10-22
- Owner: ompos-maintainers
- Deciders: ompos-maintainers
- Scope: whether OmpOS can make a host-owned MCP gateway the only MCP source of an omp run, and which omp mechanisms would carry it. Measured against omp 18.12.5 as installed on this workstation. Does not cover any live model turn, the gateway implementation, or auth for the gateway. Live-turn governance is OD-07 in `docs/open-questions.md`.

## Context

OmpOS wants an omp run whose MCP tools come only from a gateway the host owns, with no MCP server that omp discovers from disk. Three questions were asked:

1. Does omp accept an extra config overlay (`--config`) that can declare MCP servers, and can that overlay point omp's MCP client at an HTTP endpoint instead of a local command?
2. Does omp have a setting that disables or routes its own direct MCP connections?
3. What are the exact config keys, and which command or file path shows each?

No `docs/decisions/` entry numbered ADR-0009 exists; this record takes the number 0010 as assigned.

## Method and constraints

- No omp session, prompt, RPC turn or ACP session was started. No model was called. Every omp invocation was `--help` (see the command log below).
- Installed binary: `command -v omp` gives `/root/.local/share/reflex/bun/bin/omp`, which resolves to `.../@oh-my-pi/pi-coding-agent/dist/cli.js`. The package ships its TypeScript source at `.../@oh-my-pi/pi-coding-agent/src` and `pi-utils` at `.../@oh-my-pi/pi-utils/src`. The package root has no `docs/` directory. `config/mcp-schema.json` and comments in `src/mcp/config.ts` cite `docs/mcp-config.md`, which is not shipped in the install.
- Evidence levels used below:
  - **verified (help)**: printed by an omp `--help` run in this session.
  - **verified (source)**: read in the shipped source at the cited path and line. No runtime behaviour was executed.
  - **verified (runtime)**: none. No finding in this record was observed by running omp against a target.
  - **not verified**: the reason is stated.

### Command log (exact commands and output excerpts)

```
$ command -v omp
/root/.local/share/reflex/bun/bin/omp
$ readlink -f "$(command -v omp)"
/root/.local/share/reflex/bun/install/global/node_modules/@oh-my-pi/pi-coding-agent/dist/cli.js
$ grep -n '"version"' /root/.local/share/reflex/bun/install/global/node_modules/@oh-my-pi/pi-coding-agent/package.json
3:	"version": "18.12.5",
```

```
$ timeout 30 omp --help > /tmp/omp-help-top.txt 2>&1; echo "exit=$?"
exit=0
$ sed -n 28,31p /tmp/omp-help-top.txt
      --cwd=<value>                     Directory to start in (overrides the launch cwd)
      --mode=<value>                    Output mode: text (default), json, rpc, or rpc-ui
      --config=<value>                  Load an extra config.yml-style overlay for this run (repeatable)
      --add-dir=<value>                 Add a workspace directory beyond the working directory (repeatable)
$ sed -n 88,91p /tmp/omp-help-top.txt
COMMANDS
  acp            Run omp as an ACP (Agent Client Protocol) server over stdio
  agents         Manage bundled task agents
  anonymize      Write a shareable copy of a session and its subagents: turn contents redacted, metadata kept, paths and literals replaced by consistent mock tokens
$ grep -c -i mcp /tmp/omp-help-top.txt
0
```

The grep count of `0` is the basis for "no MCP flag and no MCP command in top-level help".

```
$ timeout 30 omp mcp --help 2>&1 | head -120; echo "exit=${PIPESTATUS[0]}"
AI coding assistant

USAGE
  $ omp launch [MESSAGES...] [FLAGS]
...
exit=0
```

`omp mcp` is not a command. The `mcp` word fell through to the default `launch` command, whose `--help` printed its usage and exited 0.

```
$ timeout 30 omp config --help > /tmp/omp-help-config.txt 2>&1; echo "exit=$?"
Manage configuration settings

USAGE
  $ omp config [ACTION] [KEY] [VALUE...] [FLAGS]

ARGUMENTS
  ACTION   Config action (list|get|set|reset|path|init-xdg)
  KEY      Setting key
  VALUE    Value (for set/reset)
exit=0
```

```
$ timeout 20 omp acp --help < /dev/null > /tmp/omp-help-acp.txt 2>&1; echo "exit=$?"
Run omp as an ACP (Agent Client Protocol) server over stdio

USAGE
  $ omp acp
exit=0
```

Other help was saved to `/tmp/omp-help-launch.txt` and `/tmp/omp-help-config.txt` and read; those files are outside the repository.

Source reads used the search tool over the install `src/` tree and `sed -n` / `grep -n` on the files cited below. The exact final check commands and their output:

```
$ grep -rl "mcpServers" src        (install src/, path prefix trimmed)
main.ts  slash-commands/builtin-completions.ts  slash-commands/helpers/mcp.ts
capability/extension.ts  modes/acp/acp-agent.ts  modes/controllers/command-controller.ts
modes/controllers/mcp-command-controller.ts  mcp/config.ts  mcp/types.ts
mcp/config-writer.ts  config/mcp-schema.json  discovery/mcp-json.ts
discovery/omp-plugins.ts  discovery/agent-plugin-format.ts  discovery/windsurf.ts
discovery/cursor.ts  discovery/claude-plugins.ts  discovery/claude.ts
discovery/gemini.ts  discovery/builtin.ts  extensibility/plugins/marketplace/types.ts

$ grep -n "mcpServers" src/config/settings.ts
(no matches)

$ sed -n 2489,2493p src/config/settings.ts
		let settings: RawSettings = {};
		for (const filePath of this.#configFiles) {
			const overlay = await this.#loadOverlayYaml(filePath, captureLegacyChangelogVersion);
			settings = this.#deepMerge(settings, overlay);
			if (Object.hasOwn(overlay, "shellPath")) shellPathSource = filePath;

$ sed -n 9,16p src/mcp/settings.ts
export const cfgMcpEnableProjectConfig = register({
	id: "mcp.enableProjectConfig",
	type: "boolean",
	default: true,

$ sed -n 128,135p src/mcp/config.ts
	const disabledServers = new Set(Array.isArray(userConfig.disabledServers) ? userConfig.disabledServers : []);
	const forcedEnabled = new Set(Array.isArray(userConfig.enabledServers) ? userConfig.enabledServers : []);
	// Scope exclusions drop entries entirely BEFORE deduplication: with project
	// config disabled, a project entry must not shadow anything.
	const includeServer = (server: MCPServer & { _source: SourceMeta }): boolean =>
		enableProjectConfig || server._source.level !== "project";

$ sed -n 575,580p src/main.ts
			deferUsageReserveConfirmation: true,
			enableMCP: false,
			titleSystemPrompt,

$ sed -n 2733,2740p src/modes/acp/acp-agent.ts
	#toMcpConfig(server: McpServer): MCPServerConfig {
		if ("command" in server) {
			return {
				type: "stdio",
				command: server.command,
				args: server.args,
				env: this.#toNameValueMap(server.env),

$ sed -n 1190,1195p <pi-utils>/src/dirs.ts
export function getMCPConfigPath(scope: "user" | "project", cwd: string = getProjectDir()): string {
	if (scope === "user") {
		return path.join(getAgentDir(), "mcp.json");
	}
	return path.join(getProjectAgentDir(cwd), "mcp.json");
}

$ grep -rn -i mcp src/modes/rpc | wc -l
4          (rpc-frame.ts "memcpy"; wire "mcp_prompt" slash-command source x3; no MCP config command)

$ grep -n "restrictToolNames = " src/sdk.ts
2252:	const restrictToolNames = options.restrictToolNames === true;
```

A filesystem-wide `find /` for `mcp-config.md` was started by mistake in the background and cancelled before completion. No result from it is used here.

## Findings

### Q1. Does `--config` accept an overlay, and can it declare MCP servers?

| Claim | Level | Evidence |
|---|---|---|
| `--config=<value>` is accepted and repeatable: "Load an extra config.yml-style overlay for this run". | verified (help) | `omp --help` line 30; `src/cli/flag-tables.ts:122-124` (`result.config = [...]`). |
| The overlay is a YAML mapping merged into the **settings** layer, not into the MCP layer. | verified (source) | `src/config/settings.ts:2489-2493` deep-merges each overlay into `settings`; `#loadOverlayYaml` at `settings.ts:2509` parses YAML and rejects non-mapping files. |
| `--config` and `PI_CONFIG_FILES` feed the same overlay list. | verified (source) | `src/config/settings.ts:676-678`. |
| An overlay value overrides the matching registry setting for that process. | verified (source) | `src/cli/config-cli.ts:352`: "A --config / PI_CONFIG_FILES overlay overrides this value for this process." |
| **The overlay cannot declare MCP servers.** The key `mcpServers` is merged into settings and no code reads it from there. | verified (source) | `grep -n "mcpServers" src/config/settings.ts` has no matches. The `mcpServers` readers are the file-based discovery providers, `mcp/config.ts`, `mcp/config-writer.ts`, ACP, and the capability types (list above). None reads `Settings`. |
| **The overlay cannot point omp's MCP client at an HTTP endpoint**, because it cannot declare a server at all. | verified (source) by consequence of the row above | Same evidence. |
| An `mcpServers:` key inside an overlay is parsed, merged, and then inert. | verified (source) | Follows from the two rows above. |
| Runtime confirmation of the overlay behaviour above. | **not verified** | No runtime run. Confirming needs `omp --config <file> config get …` or a session. `config get` is not `--help`, so it was outside the allowed command set for this ADR. |

### Q2. Is there a setting that disables or routes omp's own direct MCP connections?

| Mechanism | Scope | Level | Evidence |
|---|---|---|---|
| **No global "disable all MCP" switch** for the user or config layer. No `enableMCP` config key, no `--no-mcp` flag, no MCP environment variable. | whole run | verified (source, absence) | `grep -c -i mcp` on top-level help gives 0. `grep -rn -i mcp src/modes/rpc` gives 4 lines, none a config switch. The `enableMCP` option appears only in SDK/internal options (`src/sdk.ts:764`), not as a user setting. |
| `mcp.enableProjectConfig` (boolean, default `true`): when `false`, entries whose source level is `"project"` are dropped before deduplication. | project-level servers only | verified (source) | `src/mcp/settings.ts:9-16`; `src/mcp/config.ts:131-135`. Which sources carry level `"project"` beyond `<cwd>/.omp/mcp.json` was not fully enumerated; `src/discovery/builtin.ts:237-238` shows the project file as level `"project"`. |
| `disabledServers` (array of names) in the user `mcp.json`: highest-precedence denylist, applied by name to any source. | per named server | verified (source) | `src/config/mcp-schema.json` `disabledServers` (line 23); `src/mcp/config.ts:128` and `:142` (`suppressServer` checks name for every source). |
| `enabledServers` (array of names) in the user `mcp.json`: overrides a server's `enabled: false`; does not override `disabledServers`. | per named server | verified (source) | `src/config/mcp-schema.json` `enabledServers` (line 32); `src/mcp/config.ts:129`, `:143`. |
| Per-server `enabled: false`. | per server | verified (source) | `src/config/mcp-schema.json` `serverBase.enabled`; `src/mcp/config.ts:143`. |
| `--tools` is a tool-name allowlist. It does **not** set `restrictToolNames`, so MCP discovery still runs. | | verified (source) for the flag wiring; not verified at runtime | `src/cli/flag-tables.ts:200-208`; `src/main.ts:1700-1702` (`options.toolNames`); `src/sdk.ts:2252` (`restrictToolNames` only from an explicit option). Whether the allowlist then filters MCP tools out is **not verified**. |
| `--no-extensions` (disables extension discovery). Whether it removes plugin-supplied MCP sources is **not verified**. | plugin sources | not verified | Help line "Disable extension discovery". `src/sdk.ts:1740`, `:1083` show `explicit-only` mode; the MCP loader's use of `extensionRoots` was not traced. |
| **ACP mode routes MCP to the host.** `omp acp` forces `enableMCP: false` on every session it creates (so on-disk MCP discovery is skipped), then connects only the servers the ACP client supplies in `session/new.mcpServers`. | ACP sessions only | verified (source) | `src/main.ts:533-540` (comment: "MCP servers in ACP sessions are owned exclusively by the ACP client") and `:579` (`enableMCP: false`); `src/modes/acp/acp-agent.ts:695` (`session/new` takes `mcpServers`); `:2668-2730` (`#configureMcpServers` disconnects prior servers, connects only the given set, and throws if any server fails to connect); `:2733-2760` (`#toMcpConfig`: `command` → stdio, `type: "http"` → http, `type: "sse"` → sse, otherwise rejected). |
| ACP advertises HTTP and SSE MCP transports. | | verified (source) | `src/modes/acp/acp-agent.ts:663-666`: `mcpCapabilities: { http: true, sse: true }`. |
| **SDK embedders route MCP to the host.** `createAgentSession` takes `enableMCP` (false skips discovery and ignores `mcpManager`) and `mcpManager`; `MCPManager.connectServers(configs, sources)` connects a given set. | library use | verified (source) | `src/sdk.ts:764-766`; `src/mcp/manager.ts:777-781`; `src/mcp/index.ts` exports the manager; `src/sdk.ts:986` exports `MCPManager` as a type. |
| RPC mode has no MCP configuration route. | RPC | verified (source, absence) | `src/modes/rpc/`: the only `mcp` hits are `rpc-frame.ts` ("memcpy"), and `mcp_prompt` as a slash-command source label in the wire schema and generated types. |
| Env redirection of the user `mcp.json` directory. `PI_CODING_AGENT_DIR` overrides the agent directory, which is where user `mcp.json` lives. | whole agent dir | verified (source) for path; not verified at runtime | `<pi-utils>/src/dirs.ts:1-6` (header comment), `:496-503`, `:641-643` (`getAgentDir`), `:1190-1192` (`getMCPConfigPath("user")`). The agent dir also holds other state; what else it contains was not traced. |

### Q3. Exact config keys, and the file or command that shows each

Keys below are as written in the bundled schema or registry. "Shown by" is the file and line in the install that defines the key. Runtime display via `omp config get` was not run (see Q1).

| Key | Where it goes | Type and default | Effect | Shown by |
|---|---|---|---|---|
| `mcpServers` | `<agentDir>/mcp.json` (user; `<agentDir>` from `getAgentDir()`, normally `~/.omp/agent`) and `<cwd>/.omp/mcp.json` (project) | object: name → server | declares MCP servers | `src/config/mcp-schema.json:13`; `pi-utils/src/dirs.ts:1190-1195`; `src/discovery/builtin.ts:237-238` |
| `mcpServers.<name>.type` | same | `"http"`, `"sse"`, or `"stdio"` (default stdio) | transport | `mcp-schema.json` `httpServer`, `sseServer`, `stdioServer` |
| `mcpServers.<name>.url` | same | string; required for http and sse | endpoint | `mcp-schema.json` `httpServer`: `required: ["type","url"]` |
| `mcpServers.<name>.headers` | same | string map | headers sent with requests | `mcp-schema.json` `httpServer.headers` |
| `mcpServers.<name>.command`, `.args`, `.env`, `.cwd` | same | stdio only | local process | `mcp-schema.json` `stdioServer` |
| `mcpServers.<name>.enabled` | same | boolean | per-server enable | `mcp-schema.json` `serverBase.enabled`; `src/mcp/config.ts:143` |
| `mcpServers.<name>.timeout` | same | number, ms; 0 disables client timeout | request timeout | `mcp-schema.json` `serverBase.timeout` |
| `disabledServers` | user `mcp.json` | string array | denylist by name, any source | `mcp-schema.json:23`; `src/mcp/config.ts:128, 142` |
| `enabledServers` | user `mcp.json` | string array | overrides `enabled: false` only | `mcp-schema.json:32`; `src/mcp/config.ts:129, 143` |
| `mcp.enableProjectConfig` | `config.yml` or `--config` overlay | boolean, default `true` | drops project-level MCP sources | `src/mcp/settings.ts:9-16`; `src/mcp/config.ts:131-135`; `src/cli/config-cli.ts:352` |
| `--config=<path>` | CLI, repeatable | path to YAML overlay (settings only) | overlays settings | `omp --help` line 30; `src/cli/flag-tables.ts:122-124` |
| `PI_CONFIG_FILES` | env, path-delimited | overlay list | same as `--config` | `src/config/settings.ts:676` |
| `PI_CODING_AGENT_DIR` | env | path | relocates the agent dir (user `mcp.json` with it) | `pi-utils/src/dirs.ts:4-5, 496-503, 641-643` |
| `PI_CONFIG_DIR` | env | name, default `.omp` | relocates the config root name | `pi-utils/src/dirs.ts:328-330` |
| `--tools=<names>` | CLI | tool allowlist | see Q2 table | `src/cli/flag-tables.ts:200-208` |
| `session/new.mcpServers` | ACP client | list of `{ name, type, url, headers }`, `command`/`args`/`env` for stdio | host-owned MCP servers per ACP session | `src/modes/acp/acp-agent.ts:695, 2668, 2733-2760` |

Note: `omp mcp …` is not a command. The `/mcp` slash command (`src/modes/controllers/mcp-command-controller.ts`) operates inside a running session and was not used.

## Verdict

**Feasible-unverified** for a host-owned MCP gateway, with two sub-verdicts:

- **Via the `--config` overlay: not feasible.** The overlay is settings-only. It cannot declare MCP servers, so it cannot point MCP at an HTTP endpoint. (Verified by source. Runtime not verified.)
- **Via ACP `session/new.mcpServers` (or the SDK `mcpManager` route): feasible by source.** Omp's ACP mode turns off on-disk MCP discovery for its sessions and connects only the servers the host supplies, over `http` or `sse`. That is the mechanism a host-owned gateway needs. The path is verified by source only; no ACP session was started.

It is "unverified" rather than "verified" because nothing was executed. These remain:

1. An ACP `session/new` with an HTTP server really connects to the gateway, and the gateway's tools appear in the session.
2. With the ACP mode forced `enableMCP: false`, a project `.omp/mcp.json` server is absent from that session at runtime.
3. The `--config` overlay with an `mcpServers` key has no runtime effect (inert), and `mcp.enableProjectConfig` set through an overlay takes effect.
4. Whether `session/new` makes any model request. Source was not traced to rule it in or out, and the OD-07 rule and the no-live-turn constraint apply until it is.
5. Whether `--no-extensions` removes plugin MCP sources, and whether the `--tools` allowlist filters MCP tools (both not verified).
6. Gateway auth. Only static `headers` were read in the schema. OAuth flow for a gateway (`oauth`, `auth` in schema) was not evaluated.

## Consequences

- The gateway integration must use ACP `session/new.mcpServers` (or the SDK) as its MCP channel. Do not build on `--config` for MCP.
- Failure mode to design for: `#configureMcpServers` throws when any supplied server fails to connect (`acp-agent.ts:2668-2730`). A gateway outage fails `session/new` rather than degrading. The host should health-check the gateway before creating a session.
- Relying on `PI_CODING_AGENT_DIR` to route MCP would also relocate the rest of the agent directory. That is a heavier change than MCP routing and is not recommended without tracing what else lives there.
- Project-level discovery can still add servers in non-ACP runs. Setting `mcp.enableProjectConfig: false` (through a `config.yml` or a `--config` overlay) removes only project-level sources. User-level servers and any other non-project source remain. For a gateway-only run, ACP or the SDK is the route that removes them.
- `docs/mcp-config.md` is cited by `src/mcp/config.ts:111` but is not in the installed package. Treat the schema and source as the authority for keys.

## Next verification step (needs owner approval)

This step starts an ACP session and so falls under OD-07 and the no-model-call rule. No source check establishes that `session/new` is model-free. Run it only after the owner records a new approval in `docs/open-questions.md` (OD-07 says "any further live turn needs a new recorded approval").

Proposed run, for the owner to approve or amend:

1. Start a loopback stub HTTP MCP server on `127.0.0.1:<port>/mcp` that logs every JSON-RPC method it receives and serves a single `tools/list` entry.
2. Start `omp acp` in a scratch working directory that contains a project `.omp/mcp.json` with one stdio server, so the on-disk path has something to ignore.
3. Send `initialize`, then `session/new` with `mcpServers: [{ name: "gateway", type: "http", url: "http://127.0.0.1:<port>/mcp", headers: [] }]`. Send no `session/prompt`. Close the session.
4. Pass criteria: the stub logged the MCP requests; the stub's tool appears in the session; the project stdio server is absent; the stub recorded no traffic other than MCP. Record what the session did with any provider call. Run with no provider credentials so that any model call fails visibly.

Separately and optionally, confirm the overlay behaviour with `omp --config <overlay.yml> config get mcp.enableProjectConfig`. This is not a session, but it is outside the allowed command set in this ADR, so it needs the owner's explicit nod.

Owner decision needed: approve steps 1-4 (and the optional `config get` check), with the approval recorded as a new OD entry before any run.

## Related

- `docs/open-questions.md` OD-07 (live RPC turn approval; one run approved 2026-10-21).
- `docs/decisions/ADR-0008-contract-versioning.md` (format reference).

## Update 2026-10-26: candidate, licence and links

The Status line above is unchanged. This note ties the record to the later ones and adds the facts the decision-record rule asks for.

- The number ADR-0009 now exists ([browser pane](ADR-0009-browser-pane.md)). The sentence "No `docs/decisions/` entry numbered ADR-0009 exists" in Context described the tree on 2026-10-22.
- Candidate: the mechanism this record found feasible by source is ACP `session/new.mcpServers` on omp 18.12.5. The host-side MCP client library is a separate choice, recorded in [ADR-0014](ADR-0014-mcp-client.md). Who runs plugin code and hooks is recorded in [ADR-0013](ADR-0013-plugin-isolation.md).
- Licence: omp (`@oh-my-pi/pi-coding-agent`) is MIT, from the `license` field of the installed package and the LICENSE file at https://github.com/can1357/oh-my-pi/blob/main/LICENSE, read 2026-10-26. SPDX: MIT.
- Verification route: unchanged. Every finding is verified by help output or source reading only, and the next step above still needs a recorded owner approval before an ACP session is started. Open risks are the six items under "These remain" in the Verdict and the failure mode in Consequences.
