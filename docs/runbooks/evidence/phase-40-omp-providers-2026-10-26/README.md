# Phase 40 evidence: how omp holds providers, credentials and the default model

Date: 2026-10-26. Real omp 18.12.6 in a sandbox (`HOME` and `PI_CODING_AGENT_DIR` under `/tmp`), fake keys only, no model prompt, no login flow, no call that needs a credential.

| File | What it shows |
|---|---|
| `omp-provider-surface.md` | The offline read paths (`omp models --json`, `omp token <provider>`, `omp config get modelRoles`), the `models.yml` schema and its behaviour when broken, `omp auth import -` and where a key is stored, and the ACP `configOptions` model selector with `session/set_config_option`. It is the basis of OQ-92 and of `src/OmpOS.Harness.OmpConfig`. |

Findings that shape the Settings window:

- omp lists only providers it has a credential for, so an empty list means "no credential", not "no providers".
- A broken `models.yml` does not stop omp: it exits 0 and warns on stderr, so the editor validates every write with omp itself and rolls back.
- omp has no command to remove a credential or a provider; OmpOS edits only blocks it owns and never deletes a credential.
- omp stores a stored API key unencrypted in `agent.db` (owner-only file mode). OmpOS keeps no copy and says so next to the key field.
