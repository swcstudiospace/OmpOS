# Agent loop evidence archive

Evidence and design for milestone v0.14.0 (the agent loop and the Agent Panel), recorded 2026-10-26. Decision: [ADR-0012](../../decisions/ADR-0012-agent-loop-plugins.md). Plugin guide: [docs/plugins.md](../../plugins.md). Runbook: [docs/runbooks/agent-loop.md](../../runbooks/agent-loop.md).

| File | Contents |
| ---- | -------- |
| [agent-loop-design.md](agent-loop-design.md) | The design: decisions D1 to D9, contracts, library layout, ACP plumbing, panel, composition, test rules, cross-task signatures |
| [dossier-ultrathink.md](dossier-ultrathink.md) | `claude-ultrathink`: state directory, session file, hooks, environment, isolation |
| [dossier-swarm.md](dossier-swarm.md) | `agent-swarm`: omp extension, agents, Python runtime, `.swarm/live.json` and `events.jsonl` |
| [dossier-substrate.md](dossier-substrate.md) | `agent-substrate`: MCP server, tools, stores, tokens, projector extensions, ledger |
| [dossier-gsd-omp.md](dossier-gsd-omp.md) | GSD-Core as an omp plugin: install layout, tools CLI, why `gsd-core-omp` is thin |
| [dossier-ompos-seams.md](dossier-ompos-seams.md) | OmpOS seams: ACP client, engine host, presentation, native head, test patterns |
| [dossier-git-ci.md](dossier-git-ci.md) | Submodules, CI checkout, bootstrap and hygiene-gate effects |
| [dossier-omp-loading.md](dossier-omp-loading.md) | Live probe of `omp acp` loading, hermetic launch, argument order, environment |

Notes:

- Where the design and a dossier disagree, the dossier's cited source wins.
- The design file names an earlier Ultrathink pin. The pin recorded in the OmpOS tree, listed in [docs/plugins.md](../../plugins.md), is authoritative.
- The files are verbatim copies of working notes with three redactions: `/root/` became `~/`, a process id and a git SSH user host were removed or generalised, and no credential, tailnet host name or email address was present. References such as `/tmp/omp-probe/probe.mjs` point at scratch files that are not part of this repository.
- Some dossiers quote the repository hygiene scanner's marker words; the scanner ignores Markdown files.
