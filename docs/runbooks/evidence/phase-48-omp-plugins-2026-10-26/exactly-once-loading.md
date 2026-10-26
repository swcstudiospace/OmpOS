# REPORT-1 (PROBE-1): does `--no-extensions -e ...` load each plugin once under a polluted global config?

Date 2026-10-26. omp 18.12.5. Sandboxes under /tmp/omp-probe2 only. No session/prompt sent, no credentials used, /root/.omp and /root/src/repos/ompos untouched. Probe: ACP handshake (initialize, session/new, settle 6-7 s); raw available_commands_update counted per name; tool list via an extension (ctxprobe5) that logs getAllTools() after session_start.

## Sandboxes
- polluted: HOME=/tmp/omp-probe2/polluted/home, PI_CODING_AGENT_DIR=/tmp/omp-probe2/polluted/agent. config.yml: extensions [/root/src/repos/agent-swarm/omp] (verified exists). extensions/: herdr-omp-agent-state.ts and orca-agent-status.ts (dummies registering herdr-state and orca-status), substrate-status.ts and substrate-skills.ts as symlinks to /root/src/repos/agent-substrate/packages/projector/omp/ (verified). agent.db is an empty placeholder.
- clean: empty HOME and agent dir. f-agent: agent dir containing only agent.db -> polluted/agent/agent.db symlink.
- E (OmpOS set) = -e /root/src/repos/ompos/plugins/{claude-ultrathink, agent-swarm/omp, agent-substrate/packages/projector/omp/substrate-status.ts, agent-substrate/packages/projector/omp/substrate-skills.ts, gsd-core-omp}. All exist.
- Identity facts: ompos agent-swarm/omp differs from the sibling only in test/commands.test.ts. The ompos substrate-status.ts is a SEPARATE inode with identical content (diff rc 0), not a symlink. So config and -e name different files.

## Matrix (commands = raw count in last available_commands_update; no run produced a duplicate command name)
| run | flags / env | sandbox | cmds | herdr-state, orca-status loaded | tools total / swarm_ tools (ctx) | stderr |
|---|---|---|---|---|---|---|
| A | --no-extensions E | polluted | 472 | no | 27 / 8 | none |
| B | E (no flag) | polluted | 477 | yes | 27 / 8 | none |
| C | --no-extensions --config overlay E | polluted | 472 | no | not run | none |
| D | --no-extensions E, PI_CONFIG_FILES=overlay | polluted | 472 | no | not run | none |
| E | --no-extensions E | clean | 472 | no (no dummies) | not run | none |
| F | --no-extensions E | f-agent (symlink agent.db only) | 472 | no | not run | none; starts OK |
| G | --config overlay E (no --no-extensions) | polluted | 477 | yes | 27 / 8 | none |
| H | --config overlay, no E | polluted | 54 | yes (swarm absent) | not run | none |
| I | no flags | polluted | 462 | yes | 27 / 8 | none |
| K | -e /root/src/repos/agent-swarm/omp (same string as config) | polluted | n/r | n/r | 27 / 8 | none |
| L | -e sibling substrate-status.ts | polluted | n/r | n/r | 27 / 8 | none |

Controlled marker matrix (marker.ts logs import.meta.url on every factory call; out of the same log file):
| run | setup | factory calls |
|---|---|---|
| M1 | ambient extensions/mk-link.ts -> marker.ts, plus -e marker.ts (different strings, same realpath) | 2 |
| M2 | ambient symlink plus -e of the SAME string as the symlink | 1 |
| M3 | ambient symlink plus --no-extensions -e marker.ts | 1 (ambient dropped) |
| M4 | -e marker.ts given twice | 2 |
| M5 | config list marker.ts plus -e marker-copy.ts (distinct files) | 2 |
| M6 | config list marker.ts plus -e marker.ts (same string) | 1 |
| M7 | config list plus --config overlay extensions: [] | 0 (config list replaced) |
| M8 | config list, overlay [] plus -e marker.ts | 1 |
| M9 | config list plus --no-extensions -e marker-copy.ts | 1 (config list dropped) |

First pass note: the ctx log was empty in the first pass (ctx file had an escaping bug). Fixed before the runs marked above. Command-level and tool-level counts in the first pass are unaffected by that bug.

## Answers
**(1) Does --no-extensions suppress the global config.yml extensions list and the extensions/ directory?** Yes, both. It sets disableExtensionDiscovery, which makes discovery explicit-only (sdk.ts 1740, 1083-1086: configured paths = explicit only; loader discoverExtensionPaths runs the ambient branch only when ambient is true). Evidence: run A lacks herdr-state and orca-status while I has them; M9 and M3 drop the config and ambient marker while keeping -e. Project-level .omp/config.yml is also dropped in this mode (same branch; not run directly).

**(2) Duplicates and dedupe rule.** Dedupe is by string, not realpath. loader.ts 578-591: seen Set keyed by path.resolve(extPath); the first string wins. M1 proves symlink and real path both load (factory 2 calls, both import.meta.url = real marker.ts). M2 and M6 prove the same string is deduped. M4 shows explicit -e duplicates of the same string are NOT deduped (2 calls), so the -e path is not the same code route as the discovery seen-set. Consequence for polluted B/G: swarm loads from the config path and from the ompos -e path (different strings, different files) = 2 factory calls [INFERENCE from M5; swarm itself not instrumented]. substrate-status and substrate-skills load from the extensions/ symlink and from the ompos -e copy = 2 each [INFERENCE from M1/M5]. Duplicates are SILENT: no stderr, no log warning (grep of sandbox logs for duplicate/conflict/already/shadow found nothing); ACP command names are keyed by name (raw counts never exceed 1); getAllTools is name-keyed (27 tools, 8 swarm_ in A, B, G, I alike). So duplicate registration is only visible through side effects: handlers, timers and bridges registered twice [INFERENCE].

**(3) Overlay extensions: [] replaces or merges?** Replaces. settings.ts 1620-1625 (extensionsSourceLevel) picks the highest layer that defines extensions (overrides, then --config overlay, then project, then global); the whole array comes from that single layer. Evidence: M7 (global list gone, 0 calls), H (overlay with no -e: swarm absent). The overlay does NOT touch the ambient extensions/ directory: G and H still load herdr-state and orca-status.

PI_CONFIG_FILES is split with path.delimiter (settings.ts 676; colon on Linux), and is an overlay exactly like --config (run D equals run A).

**(4) Recipes that load each plugin exactly once and keep credentials usable**
| candidate | once each? | credentials | verdict |
|---|---|---|---|
| R1: real PI_CODING_AGENT_DIR and HOME, `omp acp --no-extensions -e E` (= A with the real dirs) | yes (A: 472 cmds, no herdr/orca, M3/M9 pattern) | agent.db stays in the real agent dir; no code path in --no-extensions touches auth [INFERENCE from source, not run with real creds] | RECOMMENDED |
| R2: real dirs, E without --no-extensions (= B) | NO: swarm and substrate double | yes | reject |
| R3: real dirs, --config overlay extensions [] plus E (= G) | swarm once, but extensions/ still ambient, so substrate double | yes | reject unless extensions/ is emptied, which edits the user's config |
| R4: real dirs, PI_CONFIG_FILES overlay plus --no-extensions plus E (= D) | yes (same as A) | yes | equivalent to R1; flag is simpler |
| R5: clean empty agent dir plus E (= E) | yes | NO (no agent.db, no settings) | reject |
| R6: new agent dir with only an agent.db symlink plus E (= F) | yes (no ambient config or extensions) | symlink reachable [untested with real creds] | loses config.yml (model roles, theme, skills dirs); usable only if config.yml is also symlinked [untested] |

Verdict: R1 (or R4 as an equivalent). In R1, herdr-omp-agent-state, orca-agent-status and rtk.ts in the user's extensions/ dir stop loading in OmpOS sessions; the sibling substrate symlinks stop loading too, and the ompos copies of swarm and substrate load instead. This is the intended effect, but it is a behaviour change for the user's global herdr/orca/rtk hooks.

## Unknown / not tested
- Real credentials and session/prompt behaviour (not run, by instruction). The credential-preservation claim for R1 is from source, not observed.
- Whether the duplicate swarm and substrate factories have observable side effects (not instrumented; only the marker analog was measured).
- Project-level .omp/config.yml suppression under --no-extensions (not run).
- `/.omp/plugins (linked npm plugins) suppression under --no-extensions (source says ambient-only; not run).
- Real agent dir skills and commands (customDirectories /root/agent-skills) are NOT extensions and were not checked for double-load.
- F's symlink-only agent dir with real config.yml (not run).
