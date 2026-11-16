# VX-07: default model reaches a new omp ACP session (2026-11-10)

Scope: Phase 36 criterion 6 (omp adapter) and Phase 40 (model providers). Question: does a fresh `omp acp` session start on the Settings default model without the app sending anything?

Method (no model turn, no credential used):

1. `omp --version` → `omp/18.18.6`.
2. `omp config get modelRoles` → `default: anthropic/claude-haiku-5-5:medium`.
3. Bounded stdio handshake with `omp acp`: `initialize` (protocolVersion 1), then `session/new` (cwd `/tmp`, no MCP servers).

Result:

- `initialize` answered.
- `session/new` returned `sessionId`, `configOptions` (`mode`, `model`) and `modes`.
- `model.currentValue = anthropic/claude-haiku-5-5`, which is the base of the `default` role. The `:medium` suffix is a thinking level, not part of the model id.

Conclusion: omp applies its own `modelRoles.default` at session creation. The app's default path needs no initial `set_config_option`.

Scope limits: this checks only the default. A per-session pick through the composer's model picker (`ModelPickerBinding` → `SetModelAsync`) is a separate path and is not exercised here. Whether a picked model survives a restart is not checked. No provider credential was used, so the default model's own call is not verified (OQ-71).

Repeated with `omp acp --no-extensions`, the flag the agent-loop launch planner adds (`OmpLaunchPlanner.cs:75`): `model.currentValue` is still `anthropic/claude-haiku-5-5`. The launch-shape caveat is therefore covered for the default path.
