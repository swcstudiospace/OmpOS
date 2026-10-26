# PROBE-4 report — omp model/provider management without TUI or model calls

Sandbox: HOME=/tmp/omp-probe4/home, PI_CODING_AGENT_DIR=/tmp/omp-probe4/home/.omp/agent, fake keys only. Real omp version: 18.12.6.

## (A) READ
- `omp models --json` is the offline read path (exit 0), BUT cold cache hits network: first run logged model refresh online-if-uncached (catalog.stencil.so + discovery). Warm cache + clean env returns {"models":[]} with no network. First-ever call needs owner approval.
- List is credential-gated: OPENAI_API_KEY only -> 60 openai models; clean env -> 0; host keys -> anthropic 28 + openrouter 589. --kind all adds keyless local/web rows. Rows carry NO credential field.
- Credential presence: `omp token <provider>` exit 0 + key = resolves (offline); exit 1 No active credential found + Configured providers list = absent. `omp auth export` lists stored creds only.
- Default model = `modelRoles.default` selector (no `model` key). Get: `omp config get modelRoles`. Set: `omp config set modelRoles '{"default":"openai/gpt-4o-mini"}'` (single JSON arg; `set modelRoles.default X` fails). cycleOrder default [smol,default,slow].

## (B) CUSTOM PROVIDERS
- File: <agent-dir>/models.yml (.yaml alt). Verified schema: providers.<id> with baseUrl, apiKey (env NAME | literal | "!cmd"), api (openai-completions|openai-responses|anthropic-messages|...), headers, authHeader, auth: apiKey|none|oauth, disableStrictTools, discovery{type: ollama|llama.cpp|litellm|proxy, timeoutMs}, modelOverrides, models[{id,name,api,reasoning,input,contextWindow,maxTokens,cost,headers,compat}]. Custom models need baseUrl + auth + api.
- Verified: ompos-test provider (env-name apiKey, 1 static model) appears in `models --json`/`find`/`<provider>` fully offline. Broken YAML -> exit 0, stdout = surviving providers, stderr warns models.yml validation failed, custom providers disabled + YAML Parse error; rest of config still loads.
- Reload is per-process (rename visible next invocation, no watcher). No add/remove CLI (actions only ls|find|refresh|<provider>). Safe edit: timestamped backup, comment-preserving block edit of owned providers.<id> only (naive PyYAML round-trip DROPS comments, verified), atomic rename, validate via `omp models find <id>`, roll back on warning.

## (C) CREDENTIALS
- Import: `omp auth import -` with {version:1, credentials:[{provider, credential:{type:"api_key",key} | {type:"oauth",access,refresh,expires}}]}; merges (same provider+account replaced). Wrong shape -> AuthTransferError. No headless remove (only TUI /logout, RPC logout).
- Stored in agent.db table auth_credentials(provider, credential_type, data JSON), mode 0600, PLAINTEXT unencrypted (verified via read-only sqlite3 + strings). `omp auth export` prints secrets in plaintext, unredacted.
- `omp token <provider>` is offline; env overrides stored key. Precedence: --api-key > models.yml apiKey > stored OAuth > stored login api_key > env var > stored static api_key. Built-in env names: OPENAI_API_KEY, ANTHROPIC_API_KEY/_OAUTH_TOKEN, GEMINI_/GOOGLE_API_KEY, GROQ_API_KEY, etc; custom apiKey may name ANY var. .env chain: process > project > agent > config-root > ~/.env.
- `omp usage` and `omp models refresh` are NETWORK calls (quotas; catalog+discovery rewrite of models.db). Gate behind owner approval. `omp login` is interactive.

## (D) ACP SESSION
- 18.12.6 session/new returns {sessionId, configOptions, modes}; NO models/availableModels/currentModelId. Model selector = configOptions {id:"model", currentValue, options[{value:"provider/id",name}]}. Custom provider present: 61 options incl. ompos-test/ompos-model-small.
- `session/set_model` and `session/set_config` do NOT exist (Unknown ACP ext method). Switch = standard `session/set_config_option` {sessionId, configId:"model", value} -> VERIFIED, returns rebuilt configOptions with new currentValue. In-session alt: model/switch/fast/slow slash commands.

## (E) RECOMMENDATION
1. Read: models --json (warm cache) + token per provider (offline presence) + config get modelRoles. 2. Set key: auth import via stdin JSON (never argv/logs); confirm via token + models <provider>; warn export/storage is plaintext. 3. Custom provider: backup, comment-preserving owned-block edit (env-NAME apiKey or auth:none), atomic rename, find-validation. 4. Default: config set modelRoles JSON; pass to ACP via configOptions. 5. Local test offline = list + schema; LIVE validation (discovery/inference) needs approval. 6. Impossible headless: live refresh/usage without approval, interactive login, CLI credential deletion.
Files written outside OmpOS dirs (active agent dir): ~/.omp/agent/models.yml, ~/.omp/agent/config.yml, ~/.omp/agent/agent.db (0600 plaintext creds), ~/.omp/agent/models.db (cache, only via refresh).
