# R1 Rename inventory: OmpOS to Autonogrammer

Status: research input for a one-shot rename script. Facts below were measured on branch `feat/autonogrammer` (HEAD 82129b9) with throwaway Python in `/tmp/ag-recon/` (`tok.py`, `tok2.py`, `comp.py`, `paths.py`, `brand2.py`; machine-readable output `tok2.json`, `comp.json`, `omp_idents_code.json`). Nothing was built, tested or run. Items marked [INFERENCE] are not verified in the repo.

## 0. Headline numbers

| Measure | Value |
| --- | --- |
| Tracked files (`git ls-files`) | 5789 = 5575 text + 211 binary + 3 gitlinks (`plugins/agent-substrate`, `plugins/agent-swarm`, `plugins/claude-ultrathink`; not checked out, `.gitmodules`) |
| Tracked paths containing the brand (boundary regex of 1.1) | 5016 (83 directories carry them); 47 further paths contain only an `omp` token |
| Text files with brand content | 5405, 38,436 brand tokens (second counter: 38,425; differs only by boundary edge cases) |
| Brand tokens by zone | src 10,386; tests 7,684; evidence 8,173; docs 3,556; .planning (phases/research/top) 3,521; .planning/milestones 1,982; apps 1,090; services 933; root 478; scripts 271; deploy 76; plugins 70; THIRD-PARTY-NOTICES 82; assets 50; .github 33; tools 23; CHANGELOG 19; openapi.json 9 |
| Recommended rewrite scope (active code + live docs) | about 24,650 tokens (src, tests, apps, services, root, scripts, plugins, deploy, assets, .github, tools, docs/**) |
| Bare `omp`, `Omp*`, `OMP*` tokens (not brand) | `omp` 8,985; `Omp` 5,321; `OMP` 224 |

Key trap found: the case-sensitive substring `ompos` occurs inside `compose`, `composer`, `composition`, `decompose` (4,756 rejected identifier occurrences, see 1.3). A plain `s/ompos/autonogrammer/` would corrupt them. A boundary regex is mandatory.

## 1. Token histogram

### 1.1 Boundary rules used (the proposed script regexes)

```
BRAND  = (?:(?<![A-Za-z])ompos|(?<![A-Za-z])OMPOS|(?<![A-Z])Omp[Oo][Ss]|(?<=[a-z])Omp[Oo][Ss])(?![a-z])
OMP    = segment of an alnum chunk (split on _ . - / and camel/Pascal humps) equal to omp / Omp / OMP,
         left edge: start, non-letter, or lowercase->Uppercase hump; right edge: end, non-letter, or lowercase->Uppercase hump
```

`OmpOS` may sit after a lowercase letter (`theOmpOS`) but lowercase `ompos` may not (`compose`). The right edge may not be a lowercase letter (rejects `Omps`, `ompost`).

### 1.2 Segment-level histogram (all 9 distinct brand and omp segments, whole tracked tree)

| Token | Count | Files | Example 1; Example 2 |
| --- | --- | --- | --- |
| `OmpOS` | 29,022 | 5,341 | `.editorconfig:392`; `.env.example:1` |
| `omp` | 8,985 | 1,114 | `.env.example:55`; `.env.example:65` |
| `ompos` | 8,366 | 836 | `.env.example:24`; `.env.example:47` |
| `Omp` | 5,321 | 826 | `.env.example:55`; `.env.example:81` |
| `OMPOS` | 808 | 242 | `.env.example:4`; `.env.example:48` |
| `OMP` | 224 | 65 | `README.md:110`; `README.md:156` |
| `OmpOs` | 192 | 90 | `apps/OmpOS.Web/Demo/DemoApiHandler.cs:158`; `:243` |
| `Ompos` | 41 | 22 | `docs/decisions/ADR-0008-contract-versioning.md:80`; `src/OmpOS.Contracts/ContractVersion.cs:12` |
| `ompOs` | 7 | 2 | `services/OmpOS.Api/Cloud/Environment/EnvironmentConfigResolver.cs:13`; `:16` |

Casing consequence: the rename map needs six entries, not four. `OmpOs` (mostly `OmpOsJsonContext`, 133 hits) and `ompOs` (camelCase `ompOsFile`) are real.

### 1.3 Rejected substrings (proof the boundary regex excludes them)

1,367 distinct identifiers contain `omp` but fail the boundary rules, 31,488 occurrences. Top rejects (identifier, count, first example):

| Identifier | Count | Example |
| --- | --- | --- |
| `prompt` | 3,400 | `.planning/PROJECT.md:9` |
| `StringComparison` | 2,899 | `.planning/phases/20-avalonia-foundation/20-RESEARCH.md:339` |
| `composer` / `Composer` | 1,041 / 622 | `.planning/REQUIREMENTS.md:194` |
| `compose` / `Compose` | 714 / 160 | `.env.example:46` |
| `composition` | 350 | `.planning/REQUIREMENTS.md:72` |
| `decompose` | 41 | `.planning/milestones/v0.9.1-phases/02-core/frames/rpc-out.ndjson:3` |
| `compile`, `component`, `complete`, `compare`, `compute`, `compat`, `company` | 72, 319, 656, 98, 103, 64, 2 | `THIRD-PARTY-NOTICES.md:4956` (company) |

Of these, 4,756 occurrences contain the substring `ompos` (`composer`, `compose`, `ComposerViewModel`, `LocalPodmanComposeProvider`, `EngineComposition`). Possessive `Omps…` test names (3) are rejected by the right edge (they mean the omp engine's own).

### 1.4 Identifier-level histogram (739 distinct identifiers; top and notable)

Full list: `/tmp/ag-recon/tok2.json` (key `ident`). Classes by count of distinct identifiers: BRAND-lower 15, BRAND-IDENT 55, BRAND-ENV (OMPOS_*) 139, OMP-UPPER 15, OMP-IDENT (first-party `Omp*`/`omp*`) 514 with 5,716 hits, bare `omp` 8,568.

| Identifier | Count | Files | Examples |
| --- | --- | --- | --- |
| `OmpOS` | 28,773 | 5,331 | `.editorconfig:392`; `.env.example:1` |
| `ompos` | 8,314 | 828 | `.env.example:24`; `.env.example:47` |
| `OmpOsJsonContext` | 133 | 49 | `apps/OmpOS.Web/Demo/DemoApiHandler.cs:158` |
| `OmpOSTokens` | 78 | 35 | `apps/OmpOS.Native/App.axaml:9` |
| `OmpOSError` | 42 | 17 | `src/OmpOS.Contracts/Errors/OmpOSError.cs:7` |
| `OmpOSDemo` | 27 | 13 | `apps/OmpOS.Web/OmpOS.Web.csproj:11` |
| `OMPOS_API_ORIGIN` | 59 | 23 | `docs/decisions/ADR-0017-web-head-demo-mode.md:25`; `scripts/render-web-config.sh:2` |
| `OMPOS_LIVE_OMP` | 42 | 18 | `README.md:156` |
| `OmpConfig` | 172 | 106 | `OmpOS.Linux.slnf:20` (project name segment) |
| `OmpLauncher` | 156 | 87 | `OmpOS.Linux.slnf:25` |
| `OmpPath` | 144 | 71 | `apps/OmpOS.Native/Agents/AgentHead.cs:180` |
| `OmpApiFactory` | 140 | 54 | `tests/OmpOS.Api.IntegrationTests/OmpApiFactory.cs:15` |
| `ompProviderId` | 126 | 11 | `docs/reference/providers.md:417`; `docs/api/openapi.json:6416` |
| `OmpProviderApi` | 89 | 21 | `apps/OmpOS.Native/Settings/Models/CustomProviderMapper.cs:61` |
| `OmpLaunchPlan` | 79 | 41 | `apps/OmpOS.Native/Agents/SessionEngineSet.cs:499` |
| `OmpAcpClient` / `OmpRpcSession` | 76 / 74 | 38 / 28 | `docs/decisions/ADR-0002-surface-technology.md:29` |
| `OmpTerminalState` | 76 | 10 | `apps/OmpOS.Native/Agents/SessionEngineSet.cs:1284` |
| `ScriptedOmp` / `FakeOmp` | 60 / 39 | 17 / 11 | `tests/OmpOS.Native.Tests/AgentLoop/AgentHeadPanelTests.cs:25` |
| `OmpTelemetry` | 15 | 10 | `services/OmpOS.Api/Telemetry/OmpTelemetry.cs:9` |
| `OMP_PROFILE` | 16 | 10 | `src/OmpOS.Harness.Terminal/LinuxLocalPtyBackend.cs:24` |
| `omp_result` | 19 | 5 | `plugins/ompos-hooks-omp/src/bridge.mjs:325` |

Compound forms that matter for external binding (not in the identifier table because `-`, `.`, `/` split words): see section 2.

## 2. Classification and recommendation

Legend: R = rename (script), K = keep, A = alias + migrate (rename but keep a read fallback or a one-time data migration).

### 2.1 BRAND (rename, R)

All 6 casings above in prose, identifiers, namespaces, `OmpOS.*` project names, `ompos-*` kebab (CSS classes and custom properties: 798 distinct `ompos-*`/`--ompos-*` names, e.g. `src/OmpOS.UI/wwwroot/styles/base.css:204` `ompos-gap-8`; JS globals in `src/OmpOS.UI/wwwroot/scripts/ompos-*.js`), `OMPOS_*` env names (see 2.4), `avares://OmpOS.Native.Views/...` and `clr-namespace:OmpOS...` (121 `avares://OmpOS` and 144 `clr-namespace:OmpOS` hits, assembly-name coupled, renamed together with the csproj).

### 2.2 EXTERNAL-ENGINE (keep, K) the external `omp` CLI and protocol

The product is a client of the `omp` engine; these identify the engine, not the brand. All bare lowercase `omp` words (8,985 tokens; about 3,244 in prose between spaces, 346 in quoted C# strings such as `"omp could not take the message."` at `apps/OmpOS.Native/Agents/HeadCardSteering.cs:129`) are K.

| Item | Evidence | Rec |
| --- | --- | --- |
| Executable `omp`, `omp acp`, `omp --mode rpc --no-ui` | `src/OmpOS.Harness.Rpc/OmpRpcProcess.cs:13,39`; `apps/OmpOS.Native/Agents/AgentHeadOptions.cs:27,42`; 257 `omp acp` and 10 `--mode rpc` lines outside .planning/evidence | K |
| Engine home `~/.omp`, `.omp/` convention dir, `$HOME/.omp` | `deploy/worker-entrypoint.sh:79,82`; `docs/plans/agent-loop/dossier-gsd-omp.md:33` (`~/.omp/agent/agents`); 153 lines | K (but `.ompos` is brand, see 2.3) |
| Engine env: `OMP_PROFILE`, `PI_CODING_AGENT_DIR`, `OMP_AUTH_BROKER_URL`, `OMP_AUTH_BROKER_TOKEN` | `src/OmpOS.Harness.Terminal/LinuxLocalPtyBackend.cs:24`; `services/OmpOS.Api/Cloud/Secrets/CloudSecretNames.cs:25` | K |
| `omp://` doc URLs (`omp://providers.md`, `omp://sdk.md`) | `apps/OmpOS.Native/Settings/Models/OmpBuiltInProviders.cs:8`; 61 lines, 14 files | K |
| `@oh-my-pi/pi-coding-agent` npm package, `oh-my-pi` | `deploy/api.Containerfile:36,57`; `deploy/worker.Containerfile:39`; 30 lines, 20 files (51 incl. bare `oh-my-pi`) | K |
| `models.yml`, `agent.db` (engine files) | `.github/ISSUE_TEMPLATE/bug_report.yml:10`; `src/OmpOS.Harness.OmpConfig/OmpCredentialStore.cs:16`; 304 and 18 lines | K |
| Engine wire fields `omp`, `omp_result`, `omp.extensions`, `omp.compact` | `src/OmpOS.Harness.AgentLoop/Connection/PluginConnectionDocument.cs:28`; `src/OmpOS.Harness.Plugins/Hooks/HookDecisionWire.cs:53`; `plugins/ompos-hooks-omp/package.json` (`"omp": {"extensions"...}`); `src/OmpOS.Presentation/Commands/SlashCommandCatalogue.cs:216` | K |
| Build args `OMP_VERSION`, `OMP_TGZ_SHA512` (pinned engine 18.12.5) | `deploy/api.Containerfile:45-46`; `deploy/worker.Containerfile:48-49` | K |
| Substrate plugin env `SUBSTRATE_TOKEN_OMP`, `SUBSTRATE_OMP_PENDING_DIR` (owned by the submodule) | `apps/OmpOS.Native/Agents/AgentLoopEnvironment.cs:28`; `docs/plugins.md:111` | K |
| Plugin dir `plugins/gsd-core-omp` and its `.github/dependabot.yml:42` entry | tracked first-party, `CONTRIBUTING.md:112` | K |
| Engine-path option keys `Harness__Acp__OmpPath`, `Worker__Omp__Path`, `Worker__Omp__ExtraArguments__*` | `.env.example:55,81,82`; `services/OmpOS.AgentWorker/Configuration/WorkerOmpOptions.cs:3` | K (they bind the engine executable); tied to 2.5 decision |

### 2.3 FIRST-PARTY-OMP-IDENT (`Omp*`, `*Omp`, `ompX`, `_omp`): decision needed (default K)

514 distinct identifiers, 5,716 hits (4,704 in code zones), 188 files whose basename carries one (src 115, tests 62, apps 6, services 5), 4 project directories (`OmpOS.Harness.OmpConfig`, `OmpOS.Harness.OmpLauncher` and their `.Tests`). By family (code-zone hits): launch/locate/path/version 1,199; config/models/provider/credential 947; RPC/ACP/event/session 668; terminal/plugin 329; doubles (`FakeOmp*`, `ScriptedOmp*`, `InMemoryOmp*`) 448; camel and sentence-style test names 732+381.

Almost all of them name the omp engine integration (`OmpLauncher` launches `omp`; `OmpAcpClient` speaks ACP to `omp`). Renaming `OmpLauncher` to `AutonogrammerLauncher` would be wrong semantically. Recommendation: K for all engine-describing identifiers, and rename only the product-abbreviation ones. Found so far (an `Omp` that abbreviates OmpOS, not the engine):

| Identifier | Evidence | Suggested |
| --- | --- | --- |
| `OmpApiFactory` (140 hits, 54 files) | `tests/OmpOS.Api.IntegrationTests/OmpApiFactory.cs:15` (`WebApplicationFactory<Program>`) | `ApiFactory` |
| `OmpTelemetry` (file `services/OmpOS.Api/Telemetry/OmpTelemetry.cs`) | `:9`; `SourceName = "OmpOS.Api"` | `ApiTelemetry` |
| `AddOmpOpenApi`, `AddOmpWebServices`, `AddOmpWebDemoServices` | `services/OmpOS.Api/OpenApi/OpenApiConfig.cs:13`; `apps/OmpOS.Web/Services/WebServiceRegistration.cs:22`; `apps/OmpOS.Web/Demo/DemoRegistration.cs:13` | `AddAutonogrammer*` |

There may be a few more (the 176 "other" identifiers in `/tmp/ag-recon/omp_idents_code.json`); they need an owner or reviewer pass. The script should take an explicit `ompIdentRename` map (default empty) rather than a pattern.

### 2.4 EXTERNAL-BOUND (identifiers fixed outside the code)

136 distinct `OMPOS_*` names in non-.planning/non-evidence files (139 including them). Groups:

| Group | Examples (count in code/docs/deploy) | Who reads or sets it | Rec |
| --- | --- | --- | --- |
| Plugin protocol, host to tracked plugins | `OMPOS_HOOKS_URL`, `OMPOS_HOOKS_TOKEN`, `OMPOS_HOOKS_STARTUP_FILE` (`plugins/ompos-hooks-omp/src/bridge.mjs:200-208`), `OMPOS_STEER_DIR`, `OMPOS_CONNECTION_FILE` (`plugins/gsd-core-omp/src/index.ts:80,100`) | producer `src/OmpOS.Harness.AgentLoop/...` and consumer both tracked; the three submodule plugins read `SUBSTRATE_*`/`SWARM_*`/`ULTRATHINK_*` only (`docs/plugins.md:105-114`) [submodule sources not checked out: unverified] | R, atomic with plugins |
| Hook script environment | `OMPOS_HOOK_EVENT`, `OMPOS_SESSION_ID`, `OMPOS_PROJECT_DIR`, `OMPOS_PLUGIN_ROOT`, `OMPOS_VERSION` (`src/OmpOS.Harness.Plugins/Hooks/HookEnvironment.cs:49-102`) | user-authored hook scripts | A: set both names for one release |
| Desktop/user runtime config | `OMPOS_API_BASE_URL` (`apps/OmpOS.Native/Composition/HeadServices.cs:44`), `OMPOS_PLUGINS_ROOT`, `OMPOS_STATE_DIR` (`AgentLoopEnvironment.cs:16,19`), `OMPOS_COMPLETION_ENDPOINT`, `OMPOS_COMPLETION_CONFIG` (`HeadSettingsCopy.cs:13`), `OMPOS_PROVIDER_*` pattern `OMPOS_PROVIDER_<ID>_KEY` (`ProviderKeyEnvironment.cs:9`) | user shells and saved provider keys | A: read `AUTONOGRAMMER_*` first, fall back to `OMPOS_*` |
| Hosted deploy | `OMPOS_API_ORIGIN`, `OMPOS_DEMO` (Vercel build; `vercel.ts:7-9`, `scripts/vercel/render-csp.sh:4-7`), `OMPOS_RUN_UID/GID/VOLUME_PATH`, `OMPOS_ENVIRONMENT`, `OMPOS_GIT_TOKEN` (`deploy/worker-entrypoint.sh:43-45`; `services/OmpOS.Api/Cloud/Workers/CloudWorkerVariableNames.cs:22,28`), `OMPOS_REVISION`, `OMPOS_RELEASE`, `OMPOS_VERSION` | Vercel project settings, Railway service variables | R plus matching dashboard edits (gating) |
| Local stack and CI | `OMPOS_POSTGRES_PASSWORD`, `OMPOS_DRAGONFLY_PASSWORD`, `OMPOS_SKIP_SECRET_SCAN`, `OMPOS_TEST_POSTGRES_CONNECTION` (`.github/workflows/ci.yml:32-33`; `deploy/compose.yaml:16,33`; `scripts/test-services.sh:86`) | repo secrets and developer `.env` | R (CI generates them per run) |
| Test-only | `OMPOS_LIVE_OMP/SSH/PODMAN`, `OMPOS_TEST_*`, `OMPOS_PROVIDER_MINE_KEY`, canary names | tests | R |
| `OmpOS__Debug__LogPrompts` | 2 hits; section `"OmpOS"` in `services/OmpOS.Api/appsettings.json:27`, `services/OmpOS.Api/Options/DebugOptions.cs:9` | ASP.NET config binding | R |

Other EXTERNAL-BOUND items:

| Item | Evidence | Rec |
| --- | --- | --- |
| HTTP header `X-Ompos-Contract-Version` | `src/OmpOS.Contracts/ContractVersion.cs:12`; `tests/OmpOS.Contracts.Tests/ContractVersionTests.cs:32`; `docs/decisions/ADR-0008-contract-versioning.md:80` | A: server accepts both for one release (web head on Vercel and API on Railway deploy separately [INFERENCE]) |
| Webhook headers `X-OmpOS-Signature/-Timestamp/-Delivery` | `src/OmpOS.Harness.Automations/Hosted/WebhookHeaders.cs:12,15,18`; `docs/reference/automations.md:92-94,101` | A: third-party senders sign with them; accept both |
| `.ompos/` convention dir (project and `~/.ompos`): `mcp.json`, `hooks.json`, `rules/`, `skills/`, `commands/`, `agents/`, `run-mode.json`, `environment.json`, `permissions.json`, `sandbox.json`, `setup.json`, `ignore` | `src/OmpOS.Harness.Workspace/WorkspaceConventions.cs:9`; `Conventions/ConventionDirectories.cs:7`; `src/OmpOS.Harness.AgentLoop/Safety/SafetyFileLocations.cs:13`; `src/OmpOS.Harness.Mcp/Config/McpLocations.cs:19,40,48`; `src/OmpOS.Harness.Plugins/Hooks/HookSourceLocator.cs:44,50`; `services/OmpOS.Api/Cloud/Environment/EnvironmentConfigFile.cs:16`; `src/OmpOS.Harness.AgentLoop/Safety/FileRunModeStore.cs:6`; docs `ompos/mcp.json` 80 hits; also published in `docs/api/openapi.json:5152` ("`.ompos` 0, `.omp` 1") | A: user repos already hold `.ompos/`; read `.autonogrammer` first then `.ompos` |
| System hook files | `HookSourceLocator.cs:22-24` (`/Library/Application Support/OmpOS/hooks.json`, `%ProgramData%\OmpOS`, `/etc/ompos/hooks.json`) | A |
| User data dir `OmpOS` under config/state: `mcp-state.json`, `environments.json`, `setup-consent.json`, `workspace-sets.json` | `src/OmpOS.Harness.Mcp/Catalog/FileMcpStateStore.cs:36`; `src/OmpOS.Harness.Remote/EnvironmentProfileStore.cs:46`; `src/OmpOS.Harness.Workspace/FileSetupConsentStore.cs:50`; `.../WorkspaceSet/WorkspaceSetStore.cs:48`; `apps/OmpOS.Native/Agents/AgentLoopEnvironment.cs:40`; `HeadServices.cs:424`; `README.md:107` (`~/.local/share/OmpOS`) | A: one-time move, keep reading the old dir |
| Persisted markers `# ompos:begin <id>` / `# ompos:end <id>` inside the user's omp `models.yml` | `src/OmpOS.Harness.OmpConfig/OmpModelsLayout.cs:11`; `OmpManagedBlockSpan.cs:5-6` | A: recognise both; otherwise old blocks are never removed or updated |
| JWT claim `ompos_hooks_consent` | `services/OmpOS.AgentWorker/Hooks/HooksConsentSnapshotReader.cs:17` | R both sides atomically (A if tokens outlive deploy) |
| Mention URI scheme `ompos://terminal/...` | `src/OmpOS.Presentation/Mentions/MentionUris.cs:10`; `tests/OmpOS.Presentation.Tests/Mentions/MentionSourceTests.cs:148` | A if persisted in transcripts [INFERENCE] |
| Steering directive `"source":"ompos"`; `CursorVersion = "ompos"` | `plugins/gsd-core-omp/src/steer-inbox.mjs:129`; `src/OmpOS.Harness.AgentLoop/Steering/FileSteeringChannel.cs:38`; `src/OmpOS.Harness.Plugins/Hooks/HookSession.cs:55` | R steering (both ends tracked); A for `CursorVersion` (hook scripts may test it) |
| OpenTelemetry: activity source/meter `OmpOS.Api`; metrics `ompos.cloud.*`, `ompos.sessions.*`; tags `ompos.session.id` | `services/OmpOS.Api/Telemetry/OmpTelemetry.cs:12`; `services/OmpOS.Api/Cloud/CloudMetrics.cs:13-46`; `Metrics/ApiMetrics.cs:24`; `Endpoints/SessionEndpoints.cs:142` (14 sites); exporter default `none` (`appsettings.json:24`) | R; dashboards/queries on GreptimeDB must follow |
| PostgreSQL schema `ompos` (+ advisory-lock key text `ompos.cloud-agents.schema`) | `src/OmpOS.Sessions.Postgres/PostgresCloudAgentSchema.cs:24,26`; `docs/runbooks/evidence/railway-ompos-api-2026-10-21.md:25` shows 0 rows in the `ompos` schema on 2026-10-21 | R if still absent in production; else A (schema rename migration) |
| Compose/container names `ompos-postgres/-dragonfly/-greptimedb`; `POSTGRES_USER/DB=ompos` | `deploy/compose.yaml:12,14-15,30,41`; waits in `.github/workflows/ci.yml:148-150` | R (local only) |
| Worker image: OS user and group `ompos` uid/gid 10001; OCI labels `ompos-agent-worker`; data dir `$volume_path/ompos` | `deploy/worker.Containerfile:83-84,109-111`; `deploy/worker-entrypoint.sh:82,90` | R; A for the volume subdirectory if a volume exists |
| Docker label `io.ompos.cloud-agent`; git branch prefix `ompos/cloud/`; podman secret `ompos-{id}-credential` | `services/OmpOS.Api/Cloud/Workers/CloudWorkerLabels.cs:7`; `Cloud/Git/GitBranchNames.cs:9` (also `openapi.json:4918`); `src/OmpOS.Sandbox/PodmanSandboxCredential.cs:19,27` | A for label and branch prefix (running workers, pushed branches), R for secret |
| MSBuild/assembly: no csproj sets `AssemblyName` or `PackageId`, so assembly names come from csproj file names; `RootNamespace` in 4 csproj; `InternalsVisibleTo` 19 sites; MSBuild property `OmpOSDemo` (27 hits) and `AssemblyMetadata Include="OmpOSDemo"`; `/app/OmpOS.AgentWorker.dll` | `apps/OmpOS.Web/OmpOS.Web.csproj:11,14,19,26`; `deploy/worker-entrypoint.sh:65` | R (all in-tree) |
| Android `ApplicationId` `com.swcstudiospace.ompos`, label `OmpOS` | `apps/OmpOS.Android/OmpOS.Android.csproj:7`; `apps/OmpOS.Android/Properties/AndroidManifest.xml:4` | decision (store identity) |
| PWA/brand strings `"OmpOS"` | `apps/OmpOS.Web/wwwroot/site.webmanifest:2-3` (generated by `scripts/brand/build_brand.py:228`; asserted at `scripts/ci/check-brand.sh:150`) | R in generator and check together |
| GitHub: repo `swcstudiospace/OmpOS` (54 files, 140 lines): AGPL source offer `NOTICE:20`; `Directory.Build.props:10`; `.github/ISSUE_TEMPLATE/config.yml:4,7,10`; `deploy/worker.Containerfile:111`; CHANGELOG compare links `CHANGELOG.md:267-269`. Submodule URLs in `.gitmodules` are other repos (`swcstudiospace/{ultrathink,swarm,substrate}`) and do not change | | R only after the GitHub repo is renamed (gating); GitHub redirects old URLs |
| Vercel: `ompos-web.vercel.app` (24 hits, `README.md:15`) and one preview host; the Vercel project name is outside the repo | `vercel.ts` has no project name; reads `OMPOS_DEMO`/`OMPOS_API_ORIGIN` | R after the Vercel project is renamed (gating) |
| Railway: `ompos-api` service and `ompos-api.railway.internal:8080` | `docs/runbooks/hosting.md:31,45,47` marks it "Planned, not created"; `.env.example:38` "Nothing is deployed yet"; create was refused 2026-10-21 (`railway-ompos-api-2026-10-21.md:3`). Project is `Ultrathink`, not brand | R if still not created. The task brief says the API runs on Railway: the owner must confirm [INFERENCE: it may have been created after 2026-10-21] |
| Reserved or fake hosts: `ompos.dev` schema URL (`docs/plans/v0.15.0-research/08-hooks-design.md:121`); `web.ompos.local` (`tests/OmpOS.Web.Tests/ApiBaseUrlResolverTests.cs:14`) | | R |
| SSE/event names, JSON type discriminators, table names carrying the brand | greps for `event: ompos`, `JsonDerivedType`/`JsonPropertyName` with brand: 0 hits; only the schema `ompos` above | none to rename |

### 2.5 Plugin directory and ID

`plugins/ompos-hooks-omp` (7 tracked files; `package.json` `"name": "ompos-hooks-omp"`; referenced 30x in `.planning/STATE.md:41`, `tests/OmpOS.Harness.AgentLoop.Tests/Launch/HookBridgeLaunchTests.cs:36`, `tests/OmpOS.AgentWorker.Tests/CloudHooksFixture.cs:32`): rename the brand segment only, `autonogrammer-hooks-omp`; keep the `-omp` suffix (engine extension). `plugins/gsd-core-omp` unchanged (no brand). Submodule content is out of scope: it is not in the superproject tree.

## 3. Path plan

| Item | Count |
| --- | --- |
| Directories to `git mv` | 83, all at depth 1 under `apps/` (3), `src/` (36), `tests/` (41), `services/` (2) and `plugins/` (1, `ompos-hooks-omp`); no nested brand-named directory exists. They carry 5,000 files. |
| Files with a brand basename inside those directories | 108 (e.g. `OmpOS.Native.Views.csproj`, `OmpOsJsonContext.cs`, `OmposJsonFile.cs`, `OmposFilesTests.cs`, `ompos.css`, `ompos-*.js`, `ompos-*.svg`, `ompos.ico`) |
| Loose brand-named files outside those directories | 16: `OmpOS.slnx`, `OmpOS.Linux.slnf`, `assets/brand/ompos-*` (9), `docs/plans/agent-loop/dossier-ompos-seams.md`, `docs/runbooks/evidence/railway-ompos-api-2026-10-21.md`, `tools/browser-spike/OmpOS.BrowserSpike.csproj`, `tools/gweb/named/OmpOS.Gweb.Named.csproj`, `tools/gweb/stock/OmpOS.Gweb.Stock.csproj` |
| Casing variants in paths | `OmpOS` 5,086; `ompos` 35; `Ompos` 2 (`OmposJsonFile.cs`, `OmposFilesTests.cs`); `OmpOs` 1 (`OmpOsJsonContext.cs`) |
| Case-only renames | 0 (every target differs from its source; no case-insensitive filesystem hazard) |
| Target collisions | 0 (checked case-insensitively over all 5,789 targets; no tracked path already contains `autonogram`) |
| Path length | longest tracked path stays 102 characters (an unchanged evidence PNG); longest basename after rename 69; none over 200 |

Order (all `git mv`, no content change in the same commit so history follows with `-M`):
1. Rename the 108 inner files in place (basename only), 2. `git mv` each of the 83 directories (one level, so deepest-first is trivially satisfied), 3. the 16 loose files, 4. then rewrite contents. Alternatively rewrite contents first and move afterwards; either way run the content pass over the new paths so relative `ProjectReference` paths (253 lines) and `Include=` paths match.

Not renamed by path: 47 paths with `omp` only (26 under `docs/runbooks/evidence/`, 13 in `plugins/gsd-core-omp/`, 4 in `.planning/phases/`, 3 in `docs/plans/`, 1 in `docs/decisions/`), and the 188 files with first-party `Omp*` basenames (see 2.3) unless the owner lists them.

Build files:
- 85 csproj, 1 `OmpOS.slnx` (164 brand tokens), 1 `OmpOS.Linux.slnf` (163), `Directory.Build.props` (`Product`, `RepositoryUrl` at `:4,10`), `Directory.Packages.props`.
- 85 `packages.lock.json`: 375 lines carry lowercase project ids `ompos.*` (`"ompos.contracts": {"type": "Project"}` with no `contentHash`, e.g. `apps/OmpOS.Native/packages.lock.json:247`). Do not text-rewrite blindly: renamed ids move in the sorted key order. Regenerate with `dotnet restore --force-evaluate` (CI uses `--locked-mode`, `.github/workflows/ci.yml:63,173`). Not run here.
- `InternalsVisibleTo`: 19 sites (csproj `<InternalsVisibleTo Include>` and `[assembly: ...]` in `AssemblyInfo.cs`); string literals of assembly names, rewritten by the token rule.
- `RootNamespace` set only in `apps/OmpOS.Android`, `src/OmpOS.Native.Views` (`OmpOS.Native`), `tests/OmpOS.Workflows.Tests`, `tools/browser-spike`.
- Folder or file-name conventions embedded in strings: `avares://OmpOS.Native.Views/...` (121 hits), `_content/OmpOS.UI/...` (`apps/OmpOS.Web/wwwroot/css/app.css:2`), `/app/OmpOS.AgentWorker.dll`.
- Using-order hazard: `.editorconfig:40` `dotnet_sort_system_directives_first = true`, `:39` groups not separated, `:394` IDE0005 warning, CI runs `dotnet format OmpOS.Linux.slnf --verify-no-changes` (`ci.yml:91`) and builds with `TreatWarningsAsErrors`. 2,022 `.cs` files have a using block containing the brand; a simulation (System first, then ordinal) shows 496 files would be out of order after a text rename (`OmpOS` sorts after `Microsoft`, `Autonogrammer` before). The script needs a using-block re-sort pass or a `dotnet format` run on the developer machine (not run here; the comparer details are [INFERENCE]).

Generated or derived files (regenerate, do not hand-rewrite):
- `docs/api/openapi.json`: tests require served equals committed (`tests/OmpOS.Api.IntegrationTests/GuardTests.cs:69`; "the API build regenerates" per `tests/OmpOS.Web.Tests/OpenApiCoverageTests.cs:7`); brand in title `"OmpOS.Api | v1"` (`:4`) and 8 doc strings.
- `THIRD-PARTY-NOTICES.md`: deterministic output of `scripts/legal/generate-third-party-notices.py`, freshness checked at `scripts/ci/check-licences.sh:54`; contains paths such as `src/OmpOS.UI/wwwroot/lib/xterm` (`:7837`); needs the NuGet cache. Sorted tables may reorder, so regenerate.
- Brand assets and manifest: `scripts/brand/build_brand.py` (`:228`) builds them; `scripts/ci/check-brand.sh` hard-codes 13+ file paths and `name == "OmpOS"` (`:150`). The renamed files keep the old pixels; new artwork is a design task, not a rename.

## 4. Exclusions (recommended)

| Zone | Files | Brand tokens | Reason | Rec |
| --- | --- | --- | --- | --- |
| Binary files: 192 PNG, 6 TTF, 6 WOFF2, 4 WEBP, 2 ICO, 1 ICNS | 211 | n/a | not text; 160 of them are under evidence. Only brand-named binaries are renamed by path (Android launcher mipmaps 15, `assets/brand/*.png`, `apps/OmpOS.Native/Assets/Brand/ompos-*.png|.ico|.icns`, `src/OmpOS.UI/wwwroot/brand/*`) | rename paths, never content |
| `docs/runbooks/evidence/**` | 266 (88 text with brand, 160 PNG) | 8,173 | recorded evidence of past runs (screenshots, probe logs, dated reports) | exclude content and paths |
| `.planning/milestones/**` (incl. `frames/*.ndjson`) | 65 (62 with brand) | 1,982 | closed v0.9.1 archive; recorded ACP/RPC frames | exclude |
| `.planning/phases/**` (160 files) | 160 (152 with brand) | 3,193 | historical phase records; describe the product under its old name; GSD tools read them | recommend exclude |
| `.planning/` live files: `PROJECT.md`, `STATE.md`, `ROADMAP.md`, `REQUIREMENTS.md`, `MILESTONES.md`, `config.json`, `state.json`, `research/**` (9 top-level + 13 research) | 22 | 328 | active planning state | rewrite the product name only; leave history sections |
| Recorded protocol fixtures in tests: `tests/OmpOS.Harness.Acp.Tests/TestData/frames/*.ndjson`, `tests/OmpOS.Harness.Rpc.Tests/TestData/frames/*` (5 files) | 5 | 2 real hits (e.g. a `/tmp/ompos-rpc-live` path in `rpc-live-20261021T171351Z.ndjson`) | recorded engine output replayed by tests; every other `ompos` hit in them is `decompose`/`composition` | exclude (replay tests compare bytes) |
| `THIRD-PARTY-NOTICES.md` | 1 | 82 | generated (see 3) | regenerate |
| `docs/api/openapi.json` | 1 | 9 | generated (see 3) | regenerate from the API |
| `LICENSES/*` (AGPL-3.0, MIT, OFL-1.1) | 3 | 0 | licence texts | exclude (no brand present) |
| `LICENSE` | 1 | 0 | licence text | exclude |
| `CHANGELOG.md` | 1 | 19 | history; compare links `:267-269` point at `swcstudiospace/OmpOS` | keep released entries; add a rename entry under `[Unreleased]`; rewrite header (`:3`) |
| `.gitleaksignore` | 1 | 14 fingerprint lines `commit:path:rule:line` (e.g. `:6`, `:12`) | paths are history paths at fixed commits; rewriting them breaks the ignore | protect the file entirely |
| `docs/decisions/ADR-*` (23 files, 394 tokens), `docs/plans/v0.15.0-research/**` and `docs/plans/agent-loop/**`, `docs/quality/*-audit-*.md` | ~65 | ~1,700 | dated decision and audit records ("Deciders: ompos-maintainers" is a historical team label) | owner decision (see 5.2); default exclude ADR bodies, rename titles/product name in `docs/README.md`, `docs/SUMMARY.md`, guides, reference, runbooks |
| `plugins/{agent-substrate,agent-swarm,claude-ultrathink}` | 3 gitlinks | n/a | submodule content is not tracked in this tree; only the pinned SHAs exist | nothing to rewrite |

## 5. Proposed `rebrand.config.json`

```json
{
  "tokenMap": {
    "OmpOS": "Autonogrammer", "OmpOs": "Autonogrammer", "Ompos": "Autonogrammer",
    "ompOs": "autonogrammer", "ompos": "autonogrammer", "OMPOS": "AUTONOGRAMMER"
  },
  "brandRegex": "(?:(?<![A-Za-z])ompos|(?<![A-Za-z])OMPOS|(?<![A-Z])Omp[Oo][Ss]|(?<=[a-z])Omp[Oo][Ss])(?![a-z])",
  "caseRules": {
    "pascal": "Autonogrammer", "camelLeading": "autonogrammer",
    "allCaps": "AUTONOGRAMMER", "kebabSnakeDottedLower": "autonogrammer",
    "article": "an Autonogrammer (starts with a vowel sound like OmpOS; no a/an edits needed)"
  },
  "protectedPatterns": [
    "(?<![A-Za-z0-9])omp(?![A-Za-z0-9])",
    "\\bOMP_[A-Z_]+\\b", "\\bomp://", "@oh-my-pi/", "oh-my-pi", "PI_CODING_AGENT_DIR",
    "~/\\.omp\\b", "(?<![A-Za-z])\\.omp/", "\\$HOME/\\.omp", "models\\.yml", "agent\\.db",
    "SUBSTRATE_[A-Z_]*OMP[A-Z_]*", "gsd-core-omp", "-hooks-omp",
    "\\bomp (acp|--mode)", "\\bFAKE_OMP_[A-Z_]+\\b", "\\bOMP_(VERSION|TGZ_SHA512)\\b"
  ],
  "ompIdentRename": { "OmpApiFactory": "ApiFactory", "OmpTelemetry": "ApiTelemetry",
                      "AddOmpOpenApi": "AddAutonogrammerOpenApi", "AddOmpWebServices": "AddAutonogrammerWebServices",
                      "AddOmpWebDemoServices": "AddAutonogrammerWebDemoServices" },
  "ompIdentDefault": "keep",
  "compatAliases": [
    "env AUTONOGRAMMER_* with fallback OMPOS_* (desktop, hook scripts)",
    "header X-Autonogrammer-Contract-Version (accept X-Ompos-Contract-Version)",
    "webhook headers X-Autonogrammer-* (accept X-OmpOS-*)",
    "convention dir .autonogrammer then .ompos then .omp",
    "user data dir Autonogrammer with one-time move from OmpOS",
    "models.yml markers '# autonogrammer:begin' (recognise '# ompos:begin')",
    "docker label, git branch prefix, mention URI scheme (accept old on read)"
  ],
  "excludeGlobs": [
    "docs/runbooks/evidence/**", ".planning/milestones/**", ".planning/phases/**",
    "tests/OmpOS.Harness.Acp.Tests/TestData/frames/**", "tests/OmpOS.Harness.Rpc.Tests/TestData/frames/**",
    "LICENSE", "LICENSES/**", ".gitleaksignore", "THIRD-PARTY-NOTICES.md", "docs/api/openapi.json",
    "docs/decisions/ADR-*.md", "docs/plans/v0.15.0-research/**", "docs/plans/agent-loop/**", "docs/quality/*-audit-*.md",
    "**/packages.lock.json", "**/*.png", "**/*.ico", "**/*.icns", "**/*.webp", "**/*.ttf", "**/*.woff2"
  ],
  "partialFiles": { "CHANGELOG.md": "header line 3 only", ".planning/*.md": "product name only" },
  "pathRename": {
    "apply": "tokenMap to every path segment", "order": ["inner file basenames", "83 directories (depth 1)", "16 loose files"],
    "expectedCounts": { "directories": 83, "innerFiles": 108, "looseFiles": 16, "collisions": 0, "caseOnly": 0 }
  },
  "postSteps": [
    "re-sort using blocks (2,022 files; 496 break order)",
    "dotnet restore --force-evaluate for 85 packages.lock.json",
    "regenerate docs/api/openapi.json via the API build",
    "python3 scripts/legal/generate-third-party-notices.py",
    "update scripts/ci/check-brand.sh path list and name assertion; rebuild brand assets (design)"
  ]
}
```

### 5.1 Acceptance probes for the script (cheap, grep-able)

- After the run, `git grep -nE '<brandRegex>'` over non-excluded paths returns 0 lines; `git grep -n 'compose'` count unchanged (proves no substring damage).
- `git grep -c 'omp acp'` and `~/.omp` counts unchanged outside excluded zones.
- Path counts match `expectedCounts`; `git ls-files | grep -ci ompos` is 0 except evidence and `.planning/milestones` content paths.

### 5.2 Ambiguous decisions for the owner

1. First-party `Omp*` identifiers (2.3): keep (engine-describing, recommended) or rename all to `Autonogrammer*`? Recommended: keep, rename only the 5 product-abbreviation names plus any the reviewer adds.
2. Do ADRs and dated research or audit documents get the new name (rewrite history) or stay frozen (recommended: frozen bodies, renamed titles in indexes)?
3. `.planning/phases/**`: freeze (recommended) or rewrite.
4. Compat window: alias-and-migrate (2.4, A items) or hard cutover? Hard cutover is safe only if no user, deployment or external webhook sender exists; the repo says pre-1.0, no live model turn, and no Railway deploy (`README.md:15`, `docs/runbooks/hosting.md:15`).
5. Brand style for CSS prefix and env prefix: long `autonogrammer-*` and `AUTONOGRAMMER_*` (mechanical, 798 CSS names, 136 env names) or a short alias such as `ag-`/`AG_`?
6. Android `ApplicationId` `com.swcstudiospace.ompos`: change or keep (store identity)?
7. Domain and hosts: `ompos-web.vercel.app`, `ompos.dev` schema URL, `swcstudiospace/OmpOS` repo name: new names and the order of external renames.
8. ADR "Deciders: ompos-maintainers" is a historical label; `.github/CODEOWNERS` uses `@SomeRandmGuyy`, so no GitHub team depends on it.
9. Wording around the trademark clause in `NOTICE:41` ("The licence grants no right to use the name OmpOS or its logo"): legal text, needs the owner's wording for the new name.

## Decisions needed and gating

| # | Decision | Gates |
| --- | --- | --- |
| G1 | Confirm the final casing map (6 entries) and `Autonogrammer` spelling | script constants |
| G2 | 2.3 policy for first-party `Omp*` identifiers and the product-abbreviation list | 188 files, 4 project directories, test names |
| G3 | Compat aliases vs hard cutover for env vars, header, `.ompos`, data dir, `models.yml` markers, label, branch prefix, schema | runtime behaviour code beyond the script |
| G4 | Whether `ompos-api` exists on Railway and whether the `ompos` Postgres schema was ever created (2026-10-21 evidence: neither) | schema and service rename vs migration |
| G5 | GitHub repo, Vercel project, Railway service renamed first (names, order) | URL rewrites in `NOTICE`, `Directory.Build.props`, README, CHANGELOG links, Vercel/Railway variable names |
| G6 | Frozen vs rewritten history docs (ADR, `.planning/phases`, research, audits) | exclusion list |
| G7 | New brand artwork and Android `ApplicationId` | `scripts/brand`, `check-brand.sh`, Android project |
| G8 | Who runs the generated-file steps (lock files, OpenAPI, notices, using sort) and on which machine (dotnet SDK, NuGet cache) | CI green after the rename commit |
| G9 | Submodule content (`SUBSTRATE_*`, `SWARM_*`, `ULTRATHINK_*` plugins) may read `OMPOS_*` or `.ompos`: not verifiable here (not checked out) | confirm in the three submodule repos before cutover |
