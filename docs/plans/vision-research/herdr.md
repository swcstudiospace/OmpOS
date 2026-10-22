# herdr: verification of herdr as a runtime for AI coding agents

Research date: 2026-10-22 (all sources below were opened on this date unless stated).

## Verdict

**partially-verified.** The project is real, primary-sourced and identifiable: `herdr` by the `herdrdev` GitHub organisation, Apache-2.0, Rust, latest release v0.9.5 (published 2026-10-14). Verified: it is a background server that owns PTY panes, so detached clients leave agent processes running; it runs on Linux, macOS and Windows x86_64; it has a CLI and a JSON-over-local-socket API for workspaces, panes and agents; `omp` is a first-class agent kind (`herdr agent start --kind omp`) with an official omp integration that reports state and session identity. The owner's premise is only partly supported. Verified: processes do **not** survive a Herdr server restart or a machine restart; only the layout is restored, and the agent conversation is resumed only through a native session reference (which needs the omp integration installed). Not verified in any primary source: multi-day durability, behaviour across laptop lid-close or OS sleep, and a complete security model (only owner-only socket file mode was confirmed). The name matches only one project; two GitHub forks and a former repository name point to the same project or to copies.

## Table

Columns: fact | finding | source URL | verified (opened) | date read.

| # | Fact | Finding | Source URL | Verified | Date read |
|---|------|---------|-----------|----------|-----------|
| 1 | Exact name, canonical repo | `herdr`; repo `herdrdev/herdr`; description "the runtime your coding agents live on" | https://api.github.com/repos/herdrdev/herdr ; https://github.com/herdrdev/herdr | yes | 2026-10-22 |
| 2 | Project website | https://herdr.dev (docs at /docs/) | https://herdr.dev/ | yes | 2026-10-22 |
| 3 | Former repo name | `github.com/ogulcancelik/herdr` resolves to the same repo: API lookup by repository id 1193909050 returns `herdrdev/herdr` | https://api.github.com/repositories/1193909050 | yes | 2026-10-22 |
| 4 | Maintainer org / people | Owner org `herdrdev` (type Organization). `.github/MAINTAINERS` lists GitHub users `ogulcancelik`, `kangal-bot`, `JJLiebig` | https://github.com/herdrdev ; https://raw.githubusercontent.com/herdrdev/herdr/master/.github/MAINTAINERS | yes | 2026-10-22 |
| 5 | Forks that look like the project | `mxhm/herdr` and `Mihailorama/herdr-terminal` are forks (`fork: true`) of `herdrdev/herdr`, same description. They are copies, not the project | https://api.github.com/repos/mxhm/herdr ; https://api.github.com/repos/Mihailorama/herdr-terminal | yes | 2026-10-22 |
| 6 | Which name matches the owner's description | Only `herdrdev/herdr` (herdr.dev) describes itself as a runtime for coding agents. No other `herdr` project found by search | https://herdr.dev/ | yes | 2026-10-22 |
| 7 | What it is | Single Rust binary, "terminal workspace manager for AI coding agents"; no Electron; runs inside the user's terminal | https://raw.githubusercontent.com/herdrdev/herdr/master/Cargo.toml ; https://herdr.dev/ | yes | 2026-10-22 |
| 8 | What it runs | Background server holds PTY panes (shells, agent CLIs, servers, tests). Herdr "owns their terminals" and does not wrap agents | https://raw.githubusercontent.com/herdrdev/herdr/master/docs/versions/0.9.5/website/src/content/docs/session-state.mdx | yes | 2026-10-22 |
| 9 | Detach keeps processes | Detach with `ctrl+b q`; server and panes keep running; reattach by running `herdr` | https://raw.githubusercontent.com/herdrdev/herdr/master/docs/versions/0.9.5/website/src/content/docs/persistence-remote.mdx ; https://herdr.dev/docs/quick-start/ | yes | 2026-10-22 |
| 10 | Hangup handling | On Linux and macOS the server ignores SIGHUP; stop deliberately with `herdr server stop` or SIGTERM | https://herdr.dev/docs/persistence-remote/ | yes | 2026-10-22 |
| 11 | Server restart | Original pane processes are gone. Herdr restores saved shape (workspaces, tabs, panes, cwd, layout, focus). Shells, servers, tests are not preserved | https://raw.githubusercontent.com/herdrdev/herdr/master/docs/versions/0.9.5/website/src/content/docs/session-state.mdx | yes | 2026-10-22 |
| 12 | Agent conversation after restart | Resumes only with native agent session restore (official integration or agent self-reported resume command). Otherwise panes come back as new shells in the saved directory | session-state.mdx (row 11 source) ; https://github.com/herdrdev/herdr/releases (v0.9.2 entry) | yes | 2026-10-22 |
| 13 | Shutdown on Linux | With systemd-logind, Herdr requests a short delay to save state before host shutdown | session-state.mdx (row 11 source) | yes | 2026-10-22 |
| 14 | Live handoff across server replacement | Experimental and opt-in (`herdr update --handoff`); Unix only; not for Homebrew, mise or Nix installs. Keeps pane processes across a server replacement | session-state.mdx ; https://raw.githubusercontent.com/herdrdev/herdr/master/docs/versions/0.9.5/website/src/content/docs/windows-beta.mdx (Not-supported table) | yes | 2026-10-22 |
| 15 | Default update behaviour | `herdr update` installs the client and keeps compatible servers and panes running; a restart is needed only for server-side changes | https://herdr.dev/docs/install/ | yes | 2026-10-22 |
| 16 | Multi-day runs | No primary source states a duration, uptime or memory guarantee for multi-day sessions. The only primary-source statement is that the server runs until stopped | — | no (absence) | 2026-10-22 |
| 17 | Laptop lid close / network drop | Homepage says agents keep working when the lid is closed or the network drops. No primary doc tests OS sleep or suspend behaviour | https://herdr.dev/ | yes (claim only) | 2026-10-22 |
| 18 | Remote persistence | Run herdr on an SSH host; panes keep running after detach. `herdr --remote <machine>` attaches through the local OpenSSH client; Herdr stores no passwords, keys or control sockets | https://herdr.dev/docs/persistence-remote/ ; https://raw.githubusercontent.com/herdrdev/herdr/master/docs/versions/0.9.5/website/src/content/docs/connecting-machines.mdx | yes | 2026-10-22 |
| 19 | Operating systems | Stable binaries for Linux, macOS and Windows x86_64. Windows ARM64 runs the x86_64 build under emulation. Homebrew, mise, Nix, and direct download | https://herdr.dev/docs/install/ | yes | 2026-10-22 |
| 20 | Windows limits | Unsupported on Windows: `herdr terminal attach` direct attach, live handoff, Unix foreground process groups, signed binary | https://herdr.dev/docs/windows-beta/ | yes | 2026-10-22 |
| 21 | Session CLI (list / attach / stop / delete) | `herdr session list [--json]`, `herdr session attach <name>`, `herdr session stop <name>`, `herdr session delete <name>`. Each named session has its own panes and socket | https://raw.githubusercontent.com/herdrdev/herdr/master/docs/versions/0.9.5/website/src/content/docs/cli-reference.mdx ; https://herdr.dev/docs/persistence-remote/ | yes | 2026-10-22 |
| 22 | Session create semantics | Primary docs show `session attach <name>` but do not state whether it creates a missing session | — | no (unclear) | 2026-10-22 |
| 23 | Creating and running in panes | `herdr workspace create --cwd PATH --label TEXT`; `herdr pane split`; `herdr pane run <pane_id> <command>` (submits command with Enter); `herdr pane close <pane_id>` | cli-reference.mdx (https://raw.githubusercontent.com/herdrdev/herdr/master/docs/versions/0.9.5/website/src/content/docs/cli-reference.mdx) ; https://herdr.dev/docs/socket-api/ | yes | 2026-10-22 |
| 24 | Starting an agent kind | `herdr agent start <name> --kind KIND --pane ID [-- <agent-args>]` needs an existing idle shell pane. Kinds include `omp`, `pi`, `claude`, `codex`, `opencode` and others | https://raw.githubusercontent.com/herdrdev/herdr/master/docs/versions/0.9.5/website/src/content/docs/cli-reference.mdx ; agent-automation.mdx | yes | 2026-10-22 |
| 25 | Attaching, listing agents | `herdr agent list`; `herdr agent attach <target> [--takeover]`; `herdr terminal attach <id>`; `herdr agent prompt`, `agent wait` | cli-reference.mdx ; https://herdr.dev/docs/agents/ | yes | 2026-10-22 |
| 26 | Stop | `herdr server stop` stops the default server and its panes; `herdr session stop <name>` stops a named session | https://herdr.dev/docs/persistence-remote/ | yes | 2026-10-22 |
| 27 | Socket API | JSON requests over a local socket: default `~/.config/herdr/herdr.sock`; named sessions `~/.config/herdr/sessions/<name>/herdr.sock`. Schema via `herdr api schema --json` | https://herdr.dev/docs/socket-api/ | yes | 2026-10-22 |
| 28 | Agents control herdr | Agents can open panes, read output and wait on each other through the socket API / agent skill | https://herdr.dev/docs/socket-api/ ; https://herdr.dev/docs/agent-automation/ | yes | 2026-10-22 |
| 29 | Hosting omp (oh-my-pi) as child | `omp` is a supported agent (table "Supported by Herdr"). `herdr integration install omp` writes `~/.omp/agent/extensions/herdr-omp-agent-state.ts`. OMP reports state and session identity; resume via `omp --resume=<session>` | https://herdr.dev/docs/agents/ ; https://herdr.dev/docs/integrations/ ; https://raw.githubusercontent.com/herdrdev/herdr/master/src/integration/assets/omp/herdr-agent-state.ts | yes | 2026-10-22 |
| 30 | omp identity | oh-my-pi: `can1357/oh-my-pi`, description "Coding agent with the IDE wired in", fork of Pi (badlogic/pi-mono) by Mario Zechner; config under `~/.omp/agent` | https://api.github.com/repos/can1357/oh-my-pi ; https://raw.githubusercontent.com/can1357/oh-my-pi/main/README.md | yes | 2026-10-22 |
| 31 | Pi vs OMP are separate | Herdr keeps `pi` and `omp` integrations separate; refuses an install that would load the OMP extension into Pi | integrations.mdx (raw) | yes | 2026-10-22 |
| 32 | Licence (herdr) | Apache License 2.0 | https://api.github.com/repos/herdrdev/herdr (license field) ; https://raw.githubusercontent.com/herdrdev/herdr/master/Cargo.toml | yes | 2026-10-22 |
| 33 | Licence (omp) | MIT License | https://api.github.com/repos/can1357/oh-my-pi | yes | 2026-10-22 |
| 34 | Current version (herdr) | v0.9.5 (Cargo.toml `version = "0.9.5"`; GitHub latest release tag v0.9.5) | https://api.github.com/repos/herdrdev/herdr/releases/latest ; https://raw.githubusercontent.com/herdrdev/herdr/master/Cargo.toml | yes | 2026-10-22 |
| 35 | Last release date (herdr) | v0.9.5 published 2026-10-14T16:07:28Z (hotfix for v0.9.4, published 2026-10-13) | https://api.github.com/repos/herdrdev/herdr/releases/latest ; https://github.com/herdrdev/herdr/releases | yes | 2026-10-22 |
| 36 | Repo dates (herdr) | Created 2026-03-27T17:54:33Z; last push 2026-10-22T18:20:09Z | https://api.github.com/repos/herdrdev/herdr | yes | 2026-10-22 |
| 37 | Stars (herdr) | 45,368 (GitHub API); homepage shows 44,627 at its own render time | https://api.github.com/repos/herdrdev/herdr ; https://herdr.dev/ | yes | 2026-10-22 |
| 38 | Latest omp release | v18.12.6 published 2026-10-21T21:02:53Z | https://api.github.com/repos/can1357/oh-my-pi/releases/latest | yes | 2026-10-22 |
| 39 | Socket file permission | Client socket set to owner-only mode 0o600 on Unix (`SOCKET_PERMISSION_MODE = 0o600`) | https://raw.githubusercontent.com/herdrdev/herdr/master/src/server/socket_paths.rs ; https://raw.githubusercontent.com/herdrdev/herdr/master/src/ipc.rs | yes | 2026-10-22 |
| 40 | Peer credential check | No SO_PEERCRED / getpeereid / uid check found in `src/server/client_accept.rs`, the file I searched. Not confirmed across the whole codebase | https://raw.githubusercontent.com/herdrdev/herdr/master/src/server/client_accept.rs | partial | 2026-10-22 |
| 41 | Windows elevated servers | Elevated Windows servers accept only administrator clients by default; ordinary clients of the same account are allowed only by opt-in config or flag | https://herdr.dev/docs/windows-beta/ | yes | 2026-10-22 |
| 42 | Security policy file | No SECURITY.md in the repository tree (tree listing checked) | https://api.github.com/repos/herdrdev/herdr/git/trees/master?recursive=1 | yes | 2026-10-22 |
| 43 | Install methods | `curl -fsSL https://herdr.dev/install.sh | sh`; `brew install herdr`; `mise use -g herdr`; Nix flake; Windows PowerShell installer | https://herdr.dev/docs/install/ | yes | 2026-10-22 |

## Owner decisions

1. **Restart durability.** Processes do not survive a Herdr server restart or a machine reboot. Choose one:
   - (a) Run Herdr on an always-on host (VPS or desktop) and reach it over SSH. Restarts become rare.
   - (b) Keep the laptop as host and accept that a reboot loses live processes. Rely on native omp session resume (`omp --resume`) for conversations.
   - (c) Add an external supervisor or a per-session restart policy that re-runs `agent start --kind omp` after restart.
2. **Scope of "all sessions on herdr".** Choose one:
   - (a) Every omp session starts with `herdr agent start --kind omp` (gets state reporting and native resume).
   - (b) Only long-lived sessions go through herdr; short one-off runs use plain processes.
   - (c) Start omp with `pane run` (no kind), which gives no integration-based state or resume.
3. **Where the runtime lives.** Choose one: local laptop, or a remote host over `herdr --remote <machine>`. If local, decide how to prevent OS sleep. The primary docs do not cover sleep.
4. **Security posture.** The socket is owner-only (0o600), so any process running as the same account can drive every pane, and agents can use the socket API by design. Choose one: accept same-account trust, or isolate the runtime account / container.
5. **Version and channel.** Choose one: pin v0.9.5 (latest verified release) and upgrade by hand, or track the stable channel via `herdr update`. Preview builds are not recommended for production by the docs.
6. **Live handoff.** Live handoff is experimental and Unix-only. Choose one: do not rely on it; or use `herdr update --handoff` only on a tested host.

## Unverified

- **Multi-day durability:** no primary doc gives a duration, memory or log-retention guarantee. The only verified fact is that the server runs until stopped or until the host restarts.
- **Laptop lid close / OS sleep:** homepage claim only (row 17). No doc tests suspend or sleep; OS suspend may freeze the server's processes. Not checked on a real machine (out of scope; no builds or runs allowed).
- **Session create semantics:** unclear whether `herdr session attach <name>` creates a missing session (row 22).
- **Peer authentication:** only the socket file mode was confirmed (row 39). I did not read the full codebase, so a missing peer-credential check is stated as "not found in the file I searched" (row 40).
- **Omp integration version numbers:** the bundled omp extension header says `HERDR_INTEGRATION_VERSION=10`, while the integrations doc says "OMP version 3" for native session restore. I did not establish whether these are the same numbering scheme.
- **Secondary-only claims, not used in the verdict:** full name "Ogulcan Celik"; a claimed YC F26 batch; a claimed star count of about 41,000 on 2026-09-28. These appeared only in search summaries, not in primary sources.
- **Pi vs omp equivalence:** this report treats herdr's `omp` kind as oh-my-pi. The link is strong (herdr's OMP extension and docs both use `~/.omp/agent`, and oh-my-pi's README uses the same path). The pages I read in herdr's docs do not name the oh-my-pi repository; this is an inference.
- **Full session-state middle section:** I read the mdx source; a minor part of the snapshot/shutdown section was truncated in the rendered view. The verified facts above are from the mdx source.

## Sources

Opened 2026-10-22 unless noted.

- https://herdr.dev/
- https://herdr.dev/docs/quick-start/
- https://herdr.dev/docs/persistence-remote/
- https://herdr.dev/docs/session-state/
- https://herdr.dev/docs/socket-api/
- https://herdr.dev/docs/agents/
- https://herdr.dev/docs/integrations/
- https://herdr.dev/docs/install/
- https://herdr.dev/docs/windows-beta/
- https://github.com/herdrdev/herdr (GitHub API content)
- https://api.github.com/repos/herdrdev/herdr
- https://api.github.com/repos/herdrdev/herdr/releases/latest
- https://api.github.com/repositories/1193909050
- https://api.github.com/repos/ogulcancelik/herdr
- https://api.github.com/repos/mxhm/herdr
- https://api.github.com/repos/Mihailorama/herdr-terminal
- https://api.github.com/repos/herdrdev/herdr/git/trees/master?recursive=1
- https://api.github.com/repos/herdrdev/herdr/contents/distribution/agent-detection
- https://github.com/herdrdev/herdr/releases (Atom feed read)
- https://raw.githubusercontent.com/herdrdev/herdr/master/Cargo.toml
- https://raw.githubusercontent.com/herdrdev/herdr/master/CHANGELOG.md
- https://raw.githubusercontent.com/herdrdev/herdr/master/.github/MAINTAINERS
- https://raw.githubusercontent.com/herdrdev/herdr/master/src/ipc.rs
- https://raw.githubusercontent.com/herdrdev/herdr/master/src/server/socket_paths.rs
- https://raw.githubusercontent.com/herdrdev/herdr/master/src/server/headless.rs
- https://raw.githubusercontent.com/herdrdev/herdr/master/src/server/client_accept.rs
- https://raw.githubusercontent.com/herdrdev/herdr/master/src/integration/assets/omp/herdr-agent-state.ts
- https://raw.githubusercontent.com/herdrdev/herdr/master/docs/versions/0.9.5/website/src/content/docs/session-state.mdx
- https://raw.githubusercontent.com/herdrdev/herdr/master/docs/versions/0.9.5/website/src/content/docs/integrations.mdx
- https://raw.githubusercontent.com/herdrdev/herdr/master/docs/versions/0.9.5/website/src/content/docs/cli-reference.mdx
- https://raw.githubusercontent.com/herdrdev/herdr/master/docs/versions/0.9.5/website/src/content/docs/socket-api.mdx
- https://raw.githubusercontent.com/herdrdev/herdr/master/docs/versions/0.9.5/website/src/content/docs/agent-automation.mdx
- https://raw.githubusercontent.com/herdrdev/herdr/master/docs/versions/0.9.5/website/src/content/docs/connecting-machines.mdx
- https://raw.githubusercontent.com/herdrdev/herdr/master/docs/versions/0.9.5/website/src/content/docs/windows-beta.mdx
- https://raw.githubusercontent.com/can1357/oh-my-pi/main/README.md
- https://api.github.com/repos/can1357/oh-my-pi
- https://api.github.com/repos/can1357/oh-my-pi/releases/latest
- https://omp.sh/ (HTTP 200 checked; content not used for any fact)
- Search-only (not opened, not used for facts): web search results citing medium.com, flaviocopes.com, innfactory.ai, addrom.com, bitdoze.com, tecmint.com, aiweekly.co, intelligentliving.co, coddykit.com.
