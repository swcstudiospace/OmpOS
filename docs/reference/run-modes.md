# Run modes, permissions.json and sandbox.json

Run modes decide how much an agent may do without asking you. OmpOS uses Cursor's names and Cursor's file formats, so the files you wrote for Cursor keep working, and it says plainly where it does less than Cursor.

> **Status of this release.** The decision engine, the file readers and writers, the Run mode page and the launch mapping are built and unit tested. They have not been run against a live omp session, and the application heads do not yet register the page, build the resolver or pass `--approval-mode` when they start omp. Until that wiring lands, omp runs with its own default (`yolo`: nothing asks). The "Where it is enforced" table says what each part does once it is wired.

## The three modes

| Mode | What it does in OmpOS |
| --- | --- |
| **Auto-review** (default) | A call that matches an allow rule runs. A read-only command from a short built-in list (below) runs when every file it names is inside the read boundary. A call that matches a deny rule is refused. Every other call asks you. |
| **Allowlist** | A call that matches an allow rule runs. A call that matches a deny rule is refused. Every other call asks you. |
| **Run Everything** | Every call runs. omp asks OmpOS nothing in this mode. |

**Auto-review's classifier is not available in OmpOS.** In Cursor, Auto-review sends each call it cannot settle to a small hosted model on Cursor's servers. OmpOS has no hosted model and sends nothing anywhere itself, so a call the classifier would have reviewed asks you. This was decided, not left out by accident: adding a classifier would mean OmpOS choosing a model provider and sending your commands to it. The `autoRun.allow_instructions` and `autoRun.block_instructions` sentences are read, kept and shown on the Run mode page. They are not enforced, because only a model can act on a sentence.

The known-safe list (Auto-review only): `pwd`, `ls`, `cat`, `head`, `tail`, `wc`, `echo`, `which`, `uname`, `whoami`, `grep`, and `git status`, `git diff`, `git log`, `git show`, `git rev-parse`, `git ls-files`, `git blame`. A command line with a redirect, a substitution, a subshell, a background job, a `git` option that writes or runs a program (`--output`, `--ext-diff`, `--textconv`, `-c`) or an argument outside the read boundary is not on the list and asks.

The mode you choose is kept in `~/.ompos/run-mode.json` as `{ "mode": "auto-review" }` (`allowlist`, `unrestricted`). A new mode applies to agents you start afterwards: omp's approval policy belongs to the process.

## What a call is judged on

| Call | Runs without asking when | Asks when |
| --- | --- | --- |
| Shell command (ACP kind `execute`) | every simple command in it matches a `Shell` allow rule or terminal allowlist entry; in Auto-review also when it is known-safe | any other case, including a line OmpOS cannot read with confidence |
| MCP tool | it matches an `Mcp` allow rule or an `mcpAllowlist` entry | otherwise |
| Web fetch (kind `fetch`) | its host matches a `WebFetch` allow rule | otherwise |
| Read or search of files (kinds `read`, `search`) | every file is inside the read boundary, or matches a `Read` allow rule | a file is outside the boundary |
| Edit, delete or move (kinds `edit`, `delete`, `move`) | every file is inside the workspace and none is protected, or the file matches a `Write` allow rule | a file is outside the workspace or protected |
| Anything else | Run Everything only | always |

A call whose command, files or address OmpOS could not read from omp's request asks. A call that matches a deny rule is refused in Auto-review and Allowlist, before any allow rule is looked at.

Protected paths always ask, whatever the allow rules say: `.ompos/*.json` and `.ompos/**/*.json`, `.cursor/**/*.json`, `.claude/**/*.json`, `.vscode/**`, `*.code-workspace`, `.git/hooks/**`, `.git/config`, `.git/info/attributes`, `.cursorignore`, and `~/.ompos/**/*.json` and `~/.cursor/**/*.json`. An agent therefore cannot widen its own permissions without you.

The order of everything that can say no: a hook (see [Hooks](hooks.md)) may refuse a call first; then the approval policy of the session (which tool kinds may reach a decision at all); then the deny rules; then the mode and the allow rules; then you.

## The files

OmpOS reads up to eight files. Lowest precedence first:

| Order | Scope | `permissions.json` | `sandbox.json` |
| --- | --- | --- | --- |
| 1 | User, Cursor | `~/.cursor/permissions.json` | `~/.cursor/sandbox.json` |
| 2 | User, OmpOS | `~/.ompos/permissions.json` | `~/.ompos/sandbox.json` |
| 3 | Project, Cursor | `<workspace>/.cursor/permissions.json` | `<workspace>/.cursor/sandbox.json` |
| 4 | Project, OmpOS | `<workspace>/.ompos/permissions.json` | `<workspace>/.ompos/sandbox.json` |

Both formats allow comments and trailing commas. Unknown keys are ignored. A file that cannot be used (invalid JSON, not an object, larger than 256 KiB, a folder) is ignored as a whole and reported on the Run mode page as a problem; reading never fails and never stops the other files. OmpOS writes only the OmpOS files (`.ompos`), only when you change a value on the Run mode page, and refuses a file that holds comments (it would lose them): edit that one by hand. A Cursor file is never written.

### permissions.json

| Key | Meaning |
| --- | --- |
| `terminalAllowlist` | Commands that run without asking. `git` matches any `git` command (not `gitk`); `git status` matches commands that start with those two words; `npm:install*` matches `npm` with arguments that match `install*`. Case-sensitive. |
| `mcpAllowlist` | MCP tools that run without asking, written `server:tool`. `*` matches any run of characters in either part; both parts ignore case. An entry without `:` is ignored. |
| `autoRun.allow_instructions`, `autoRun.block_instructions` | Sentences for Auto-review's classifier. Kept and shown; not enforced (see above). |
| `permissions.allow`, `permissions.deny` | **OmpOS addition.** Permission tokens, the shape Cursor's CLI uses in its own configuration. Cursor's `permissions.json` has no deny list. |

Lists from every file are joined, as in Cursor. Duplicates are shown once.

**Precedence of deny.** A deny rule from any file stands. No file can remove another file's deny, and an allow rule never beats a deny rule. A workspace file therefore cannot weaken a deny rule of your user file.

**Untrusted workspaces.** A workspace's allow entries make commands run without asking, so a repository you cloned could ask to run `curl`. The loader takes a `trustProject` flag: when it is false the workspace files can still add deny rules and block instructions, and their allow entries are ignored and reported. Cursor has no such switch. Which workspaces the heads treat as trusted is an open question.

### Permission tokens

| Token | Matches |
| --- | --- |
| `Shell(git)` | the command's first word; same forms as `terminalAllowlist` (`git status`, `curl:*`). For deny rules OmpOS also sees through `sudo`, `env`, `nohup`, `time`, `xargs` and similar wrappers, `sh -c '...'` text, substitutions and subshells, and matches `/bin/rm` as `rm`. An allow rule needs every simple command of a line (`a && b`, `a | b`) to match, and never matches a line with a redirect, substitution, subshell, background job or leading `NAME=value`. |
| `Read(src/**)`, `Write(docs/**)` | a file. `*` matches inside one folder level, `**` any number of levels (also none), `?` one character. A relative pattern is anchored at the workspace root: `.env*` is the workspace's `.env` files, `**/.env*` those at any depth. A pattern with no wildcard also covers everything below it. `~` is your home. |
| `WebFetch(docs.example.com)` | the host of the address. `*` any host; `*.example.com` any subdomain (not `example.com` itself). Paths and ports are ignored. |
| `Mcp(github:*)` | `server:tool` as in `mcpAllowlist`. |

Paths are compared after every symlink is resolved. An allow rule never matches a link that leads out of the folder it names, and a link inside the workspace that points outside is outside. A deny rule matches either the path as written or the real path. A path that cannot be resolved (a link loop, an unreadable folder) is never readable and never inside the workspace.

An invalid token is ignored with a problem; the rest of the file applies. Tokens are one line of at most 512 characters; each list keeps its first 500 entries.

### sandbox.json

| Key | Meaning |
| --- | --- |
| `readBoundary` | `system` (default): the agent may read anywhere without asking. `workspace`: it reads freely inside the workspace and the read allowlist; anything else asks. |
| `additionalReadPaths` | The read allowlist: folders (everything below), globs such as `~/notes/*.md`, and `~` paths. Applies only with `readBoundary: workspace`. Under `workspace` the folders `skills`, `rules` and `plugins` of `~/.ompos` and `~/.cursor` are always readable. |

A value in a file replaces the one from Settings. The workspace file wins over the user file, and an OmpOS file wins over a Cursor file in the same place. `additionalReadPaths` is replaced as a whole, not joined. An untrusted workspace can only make the boundary stricter: its `workspace` boundary applies; its `system` boundary and its read paths are ignored. Cursor's other `sandbox.json` keys (network policy, writable paths) describe an operating-system sandbox and are ignored.

The read boundary does not apply in Run Everything.

## Where it is enforced, and where it is not

omp has no operating-system sandbox: no Landlock, no Seatbelt, no seccomp. Its approval policy belongs to the omp process (`omp acp --approval-mode`; ACP has no per-session field), and its `bash.patterns` setting is "an approval policy, not containment".

| Mode | `--approval-mode` OmpOS passes to omp | What omp then asks OmpOS |
| --- | --- | --- |
| Auto-review, Allowlist | `always-ask-cached` when the read boundary is `workspace` or a `Read` deny rule exists; otherwise `always-ask` | every file read (cached form only), every write, every shell command, MCP tool and fetch; OmpOS settles each from the files, or asks you |
| Run Everything | `yolo` | nothing |

`always-ask` lets omp approve file reads itself, so OmpOS cannot see them; that is why a `workspace` boundary or a `Read` deny rule needs the cached form. omp's default is `yolo`: a launch that passes no flag asks nothing.

| Rule | Auto-review and Allowlist | Run Everything |
| --- | --- | --- |
| `Shell` deny | enforced by OmpOS on every call omp asks about, and by omp's own `bash.patterns` (`deny` is absolute) | enforced by omp's `bash.patterns` only; they match a command line that starts with the denied command, not one buried in a pipeline |
| `Read`, `Write`, `WebFetch`, `Mcp` deny | enforced by OmpOS on every call omp asks about | **not enforced**: omp does not ask |
| Allow rules, allowlists | skip the question | not needed |
| Read boundary | enforced for the agent's file tools | not applied |

What this does not do:

- It does not contain a command. A command you let run has your files, your network and your processes. The Run mode page says so.
- It does not constrain a shell started through omp's `eval` tool beyond what omp asks. `bash.patterns` do not cover `eval`.
- Read and write rules apply to the agent's file tools. A shell command is matched by `Shell` rules only (and, in Auto-review, by the known-safe list's argument check).
- Matching is a conservative reading of a command line, not a shell parser. As in Cursor, allowlists and instructions are best-effort convenience, not a security boundary.
- Request field names (`rawInput`, `locations`) and the MCP tool naming are read from omp's permission requests in several spellings and are not yet verified against a live omp run. A call whose command or file cannot be read asks.

## The terminal auto-run switch

The Agents page switch "Run terminal commands without asking" governs the commands OmpOS itself proposes in its terminal, not the tool calls the agent makes through omp. On the Run mode page's terms it is Run Everything for that one path, and off is Allowlist: every proposed command waits for you. A later change can let the same allowlist and deny rules decide those proposals, using the same engine with a shell request.
