# Phase 48 evidence: the four plugins in omp processes (2026-10-26)

Both reports come from investigation probes that ran real `omp` 18.12.5 inside throwaway sandboxes under `/tmp/omp-probe2`. No `session/prompt` was sent, no credential was used, and nothing under `/root/.omp` or this repository was written. The sandbox "polluted" reproduces the shape of the owner's global omp configuration (a `config.yml` `extensions:` entry plus extension symlinks).

| File | Phase 48 criterion | Result |
|------|--------------------|--------|
| `exactly-once-loading.md` | 3 (exactly-once loading) | `--no-extensions` drops the global `extensions:` list and the `extensions/` directory; each `-e` plugin loads once; the launch recipe of `OmpLaunchPlanner` matches the recommended recipe R1. Side effect: the owner's other global extensions do not load in app-started omp (OQ-83). |
| `omp-terminal-tui.md` | 1, 2, 8 (terminal launch, observability) | The interactive omp starts in a PTY with the same arguments, shows the Ultrathink and Substrate status line and the Swarm widget, lists `swarm`, `ultrathink-*`, `substrate` and `gsd-core` in its slash menu, and exits cleanly on two Ctrl+C within about 150 ms. |

These runs are evidence for the omp side only. The application side (planner for terminal launches, terminal panel action, per-plugin and end-to-end checks) is Phase 48 work that is not done yet.
