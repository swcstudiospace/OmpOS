# Phase 82: Plugin code-connectivity - Context
Source: task T9013-arch, ADR-0038 D5, upstream US-306/AC-306
**Gathered:** 2026-11-23
**Status:** Planned (Waves 2+: plan-and-return). Depends on Phase 80 (RPC-launched omp exists in production composition).

<domain>
## Phase Boundary

Prove plugin code-connectivity is transport-independent: every `ILaunchContributor` point (PluginBundles/`BundleLaunchContributor` skills overlays, `ConfigOverlayWriter` and its `--config` overlay, MCP `Acp*` types plus `Harness.McpGateway`) produces the same effect in an RPC-launched omp as in an ACP-launched one, or the difference is recorded with observed evidence (OQ-526). The inventory in `docs/plugins.md` maps each point to exactly 1 source tree (AC-306): `claude-ultrathink` (submodule, incl. `pstack`), `agent-swarm`, `agent-substrate` (pinned), `gsd-core-omp` and `autonogrammer-hooks-omp` (in-tree), `agent-realm` (NOT initialized).

Not delivered: any pin move on the already-pinned trees, any new plugin, any live model turn to "prove" loading (proof is launch-plan and process-argument evidence plus scripted fixtures).
</domain>

<decisions>
## Implementation Decisions

- **D-01:** `agent-realm`: init-or-remove is decided and executed in this phase (OQ-528 owner call recorded here first). Until then it is listed exactly once as `not-initialized`, never as active; if the decision is remove, the phase removes the submodule entry in the same change.
- **D-02:** Parity evidence per contribution point: the composed launch plan (argv, overlays, skills directories, MCP server set) for an identical session request is captured for both adapters in tests and diffed; parity means the plans are equal modulo the transport flag itself. A point that only reaches omp through an ACP-only channel (for example `session/new.mcpServers`) gets its RPC channel named or is recorded as a transport difference in OQ-526 with a disposition.
- **D-03:** The inventory table lives in `docs/plugins.md`: point, source tree, pin (verbatim `git submodule status` for submodule trees), omp delivery path per transport, proving test.
</decisions>

<verification_posture>
## What "done" means here

The launch-plan diff tests pass for both adapters, the inventory covers 100% of contribution points found in source, the realm decision is executed, and docs checks pass.
</verification_posture>

<canonical_refs>
## Canonical References

- `docs/decisions/ADR-0038-omp-rpc-primary-transport.md`
- `docs/plugins.md`
- `docs/open-questions.md` (OQ-526, OQ-528)
- `.planning/REQUIREMENTS.md` (RPC-82-1 to RPC-82-3)
</canonical_refs>

## Task 8 gate state (evidence pack, 2026-11-26 — owner decision pending, q4 default: unavailable)

No init and no removal performed by the executor. Observed state at execution time:

- `git submodule status` reports ` f28a3acc319d9805c2d2487c05d12a075a2c8ad7 plugins/agent-realm (v0.3.38)`
  (leading space = initialized); `git ls-files -s plugins/agent-realm` = `160000 f28a3acc…` (gitlink, mode 160000).
- The recorded-vs-observed discrepancy stands: OQ-528/this context say NOT initialized, while the working tree is
  populated at the gitlink — yet `plugins/agent-realm/dist/` is absent, so the declared extension
  (`./dist/src/omp/extension.js`) cannot load either way, and "initialized" alone would still require
  `npm ci && npm run build` on Node ≥ 22.13.
- Licence note unchanged: submodule `AGPL-3.0-only` vs repo `AGPL-3.0-or-later`; no recorded owner decision.
- Realm stays `unavailable` everywhere it is listed; no other pin moved. The owner's init-or-remove call (OQ-528)
  remains the blocker for executing Task 8.
