# Lens 2: Boundaries and Security

Repository: `/root/src/repos/ompos` at commit 59e9dd1 (branch feat/v0.9.2-hardening).
Scope: tracked files under `src/`, `apps/`, `tests/`, `scripts/`, `.github/`, `bootstrap.sh`. `apps/OmpOS.Web/wwwroot/lib` and `bin/`/`obj/` excluded. Read-only audit; no builds, tests, or package commands were run.

Totals: 31 rows (High 1, Medium 6, Low 24). Row cap of 40 not reached.

## 1. Findings

| file:line | QUALITY_BAR item | severity | disposition | evidence |
|---|---|---|---|---|
| apps/OmpOS.Web/Services/ApiBaseUrlResolver.cs:50-52 | Security | High | fix | `?apiBaseUrl=` accepts any https origin; Program.cs:17,24 send prompts and SSE there. Doc at :38-41 says otherwise. |
| src/OmpOS.Sandbox/AgentOsProvider.cs:65,256 | Security | Medium | fix | Bearer token sent to any absolute URI; ctor (:59-62) checks only IsAbsoluteUri, so cleartext http:// is allowed. |
| src/OmpOS.Harness.Acp/AcpStdioTransport.cs:247 | Boundaries | Medium | fix | Child stdout read by ReadLineAsync with no line cap (called at :108); ValidateJsonObject (:120) runs after buffering. |
| src/OmpOS.EngineHost/RemoteEngineHost.cs:343 | Boundaries | Medium | fix | Remote SSE ReadLineAsync has no line cap; frame data grows in a StringBuilder; JSON parse at :417 has no size check. |
| apps/OmpOS.Web/Services/SseParser.cs:43 | Boundaries | Medium | fix | `_lineBuffer` appends each chunk until a newline arrives; the parser has no max line or frame size. |
| src/OmpOS.Sandbox/LocalPodmanComposeProvider.cs:139,503 | Security | Medium | fix | Compose file (incl. Spec.Environment, :733) in shared temp dir, default mode; siblings use owner-only. |
| src/OmpOS.Harness.Workspace/SetupRunner.cs:36-51 | Security | Medium | fix | Repo .ompos/setup.json steps run with no consent; env names only syntax-checked (SetupFileReader.cs:92). |
| src/OmpOS.Harness.Acp/AcpStdioTransport.cs:201 | Boundaries | Low | defer | Child stderr read by ReadLineAsync with no line cap; stderr is diagnostics only. |
| src/OmpOS.Harness.Rpc/OmpRpcProcess.cs:237 | Boundaries | Low | defer | stderr ReadLineAsync has no line cap; only the tail count is bounded (:240). |
| apps/OmpOS.Web/Services/ApiSessionClient.cs:37 | Boundaries | Low | defer | ReadFromJsonAsync on API bodies (also :50, :72; SseEventStream.cs:327) has no response size cap. |
| src/OmpOS.Harness.Registry/RegistryReader.cs:28 | Boundaries | Low | defer | Whole local file read before TryParse, no size cap (RegistryWriter.cs:57 is the same). |
| src/OmpOS.Harness.Herdr/HerdrSessionStore.cs:41 | Boundaries | Low | defer | Local store read with no size cap before deserialize (:54). |
| src/OmpOS.Harness.Plugins/PluginStateStore.cs:42 | Boundaries | Low | defer | Local state read with no size cap before deserialize (:43). |
| src/OmpOS.Native.Views/MainView.axaml.cs:633 | Boundaries | Low | defer | Path is contained by WorkspaceRoot.Resolve, but File.ReadAllText loads the whole file with no size cap. |
| src/OmpOS.Native.Views/Views/Export/TranscriptExportView.axaml.cs:66 | Security | Low | accept | Writes to a user-typed path and overwrites silently; user-initiated desktop action. |
| src/OmpOS.Harness.Acp/AcpStdioTransport.cs:189 | Security | Low | accept | Process.Start with ArgumentList, no shell, executable from config. Same at OmpRpcProcess.cs:67, OmpVersionProbe.cs:57. |
| src/OmpOS.Harness.Terminal/TerminalGate.cs:106 | Security | Low | accept | Agent proposals wait for approval unless AutoRunEnabled, which defaults to false (TerminalSettings.cs:11). |
| src/OmpOS.Harness.Terminal/TerminalGate.cs:256 | Security | Low | accept | Working directory resolved through WorkspacePath (symlink-aware); must be an existing directory inside the root. |
| src/OmpOS.Harness.Acp/AcpAgentSession.cs:951 | Security | Low | accept | Agent fs/read and fs/write disabled unless FsRoot is set; containment check is separator-aware (:1014-1025). |
| src/OmpOS.Harness.Worktrees/WorktreeManager.cs:274 | Security | Low | accept | Recursive delete of Path.Combine(_root, runId), reached only after WorktreeRunId.Validate (:121). |
| src/OmpOS.Sessions.Postgres/PostgresSessionRepository.cs:45 | Security | Low | accept | 8 SQL literals joined with +; all constant text with @parameters. No interpolated SQL in src/. |
| src/OmpOS.Sandbox/LocalPodmanComposeProvider.cs:133 | Security | Low | accept | Generated sandbox id checked by IsSafeId before it reaches a file path or argv. |
| src/OmpOS.Sandbox/RailwaySwarmProvider.cs:38 | Security | Low | accept | Hard-coded https://backboard.railway.com endpoint; the only non-XAML URL literal in src/ .cs files. |
| src/OmpOS.Harness.Plugins/PluginManifestSource.cs:22 | Boundaries | Low | accept | Manifest size checked (64 KiB) before ReadAllBytes; parser re-checks at PluginManifestParser.cs:40. |
| src/OmpOS.Harness.Plugins/HookRunner.cs:94 | Security | Low | accept | Hook runs only when its command hash has recorded consent; otherwise reports AwaitingConsent. |
| src/OmpOS.Harness.Workspace/WorkspaceFiles.cs:38 | Boundaries | Low | accept | Convention files (setup, modes) capped at 256 KiB before read; both loaders use this reader. |
| src/OmpOS.Harness.Rpc/OmpRpcProcess.cs:114 | Boundaries | Low | accept | RPC stdout capped at 1 MiB per line; rpc_chunk reassembly capped at 64 MiB (RpcChunkAssembler.cs:112). |
| src/OmpOS.Sandbox/PodmanSandboxCredential.cs:63 | Security | Low | accept | Credential staging file uses CreateNew with user-only mode (:70); Herdr store likewise (HerdrSessionStore.cs:254). |
| tests/OmpOS.Sandbox.Tests/NonPodmanCredentialFailClosedTests.cs:20 | Security | Low | accept | Fixture secrets (sk_test_secret, railway-test-token). All 8 test secret-like hits are fixtures or assertions. |
| .github/workflows/ci.yml:10 | Security | Low | accept | Workflow token scoped with `permissions: contents: read`; no pull_request_target or secrets. usage found. |
| scripts/test-services.sh:58 | Security | Low | accept | Passwords from openssl rand; env file chmod 600 (:84-85); passed to podman via env, not argv (:61). |

## 2. Scans

Every pattern below was run with the grep tool over the stated paths. Counts are matching lines unless stated.

1. `Process\.Start`, over src/, apps/, tests/, scripts/: **17 hits** (src 4, tests 13, apps 0, scripts 0).
   - src (4), all fine: AcpStdioTransport.cs:189, OmpRpcProcess.cs:67, OmpVersionProbe.cs:57, and OmpRpcClient.cs:55 (`OmpRpcProcess.Start` wrapper). Each uses ArgumentList with UseShellExecute=false; the executable is configuration, not request data.
   - tests (13), all fine: local-binary fixtures and probes (RunWorktreeIsolationTests.cs:249, RepoHygieneTests.cs:54, FixtureHttpProcess.cs:46, McpHttpTests.cs:24,43,69 matched through the `FixtureHttpProcess.StartAsync` substring, RpcLiveCaptureTests.cs:78, RpcLiveSmokeTests.cs:33, RpcProcessStartTests.cs:16, PodmanCommand.cs:48, PostgresTwoProcessAppendTests.cs:120, PublishSmokeTests.cs:31, RenderCspScriptTests.cs:153). They start controlled binaries from test code.
2. `ServerCertificate|DangerousAccept|ServicePointManager|RemoteCertificateValidationCallback`, over src/, apps/, tests/, scripts/, bootstrap.sh: **0 hits**. Positive control `HttpClient\b` over src/ returned hits (RemoteEngineHost.cs:27, CompletionClient.cs:21, AgentOsProvider.cs:39, RailwaySwarmProvider.cs:40), so the search is live.
3. `BinaryFormatter|TypeNameHandling|NetDataContractSerializer|JavaScriptSerializer`, over src/, apps/, tests/, scripts/: **0 hits**.
4. `JsonSerializer\.Deserialize`:
   - src/: **11 hits**. Each classified:
     - RemoteEngineHost.cs:417: remote SSE payload, no size cap (see RemoteEngineHost.cs:343). Not validated beyond the typed contract.
     - AcpAgentSession.cs:1368: ACP envelope; upstream line unbounded (AcpStdioTransport.cs:247); malformed input becomes a typed error.
     - HerdrSessionStore.cs:54: local store file, no cap (Low finding HerdrSessionStore.cs:41).
     - PluginManifestParser.cs:121: bounded by the 64 KiB check at :40. Fine.
     - PluginStateStore.cs:43: local state file, no cap (Low finding PluginStateStore.cs:42).
     - RegistryExport.cs:42: caller bytes, no cap at parse (Low finding RegistryReader.cs:28). Malformed input is refused, not thrown.
     - SetupFileReader.cs:48: text from WorkspaceFiles.ReadText, 256 KiB cap. Fine.
     - WorkspaceLoader.cs:109: text from WorkspaceFiles.ReadText (WorkspaceLoader.cs:71), 256 KiB cap. Fine.
     - PostgresSessionRepository.cs:332, :428, :483: database content written by the store itself. Fine, not external input.
   - apps/: **1 hit**: SseEventStream.cs:327 (API payload, no cap; Low finding ApiSessionClient.cs:37).
   - tests/: many hits, all round-trip fixtures in test code. Not enumerated one by one because the tool output was truncated.
   - scripts/: 0 hits.
5. `File\.(ReadAll|WriteAll|Delete|Move|Copy|Open)|Directory\.(Delete|Create|Move)`:
   - src/: **31 matching lines in 16 files**. The list was truncated in the first pass, so I re-ran it on the omitted directories and counted from the two narrowed runs (19 + 12).
     - Path derives from a validated root: WorkspaceFiles.cs:44 and WorkspaceSearch.cs:184 (both behind a size check), WorktreeManager.cs:86,274 (run id validated), MainView.axaml.cs:633 (WorkspaceRoot.Resolve), LocalPodmanComposeProvider.cs:503,589 (temp path with a generated id), AcpAgentSession.cs:851,915,918 (behind FsRoot containment).
     - State or config files, no root: HerdrSessionStore.cs:41,226,267,285; PluginStateStore.cs:42,206,213,214,220; PluginManifestSource.cs:30 (size-checked); RegistryReader.cs:28; RegistryWriter.cs:57,183; SystemRegistryFileSystem.cs:30,33; ProcessTree.cs:93,140 and ProcessRunner.cs:665,756 (read /proc only).
     - User-typed path: TranscriptExportView.axaml.cs:66 (Low finding, accepted).
     - Other: OpenShellProvider.cs:208 reads /proc/self/cgroup. Fine.
   - apps/OmpOS.Web/Services/, apps/OmpOS.Native/, scripts/: 0 hits.
   - tests/: not enumerated. Observed hits are fixture writes or deletes under temp or test repo dirs (RunWorktreeIsolationTests.cs:35,107,219; HarnessBarePathReadinessCheckTests.cs:22,25,35).
6. `(?i)(password|secret|api[_-]?key|token|bearer)\s*[:=]\s*"`, over src/, apps/, tests/, scripts/, .github/, bootstrap.sh: **13 hits** (src 1, tests 8, scripts 4, .github 0).
   - src (1): RailwaySwarmProvider.cs:36 `RedactedToken = "[REDACTED]"`, a sentinel, not a secret.
   - tests (8), all fixtures or assertions: CoordinationConfigurationTests.cs:62 (marker list); NativeSessionReferenceTests.cs:8; AgentOsContractTests.cs:16; NonPodmanCredentialFailClosedTests.cs:20,22; PodmanExecSettingsRegressionTests.cs:89 (assertion string); RailwaySwarmProviderTests.cs:18; VendorStubTests.cs:334.
   - scripts (4): test-services.sh:27 (function parameter), :58 and :59 (`openssl rand` values), :62 (env assignment). Generated at run time. No literal credential anywhere.
7. `"https?://[^"]*"` in src/: **32 files**.
   - 1 .cs file: RailwaySwarmProvider.cs:38 (Railway GraphQL endpoint). Fixed and https. Accepted as Low.
   - 31 .axaml files: XAML `xmlns` namespace URIs, not endpoints.
   - No http:// literal in any .cs file under src/.
8. SQL verbs in string literals built with `+` or `$"`, over src/, apps/, tests/, scripts/: **9 hits** (PostgresSessionRepository.cs:45,49,51,58,72,77,81,85 and PostgresSessionStoreTests.cs:120). All are constant text joined with `+`, with values bound through @parameters. Interpolated SQL (`$"` with a verb): **0 hits**. PostgresSessionSchema.cs:33,35 are constants.
9. `AllowInsecure|Insecure|verify\s*=\s*false`, over src/, apps/, tests/, scripts/, bootstrap.sh, .github/: **0 hits**.

Added scans (boundary and entry-point checks):
- HTTP routes (`\.Map(Get|Post|...)|MapHub|MapControllers|MapGroup|app\.Use`) over src/ and apps/OmpOS.Web/: **0 server routes**. The only hits are A2UI mapping helpers (`A2uiBlockMapping.Map`) and a test-only MCP fixture server. No Kestrel host exists under src/ or apps/.
- Auth, CORS, body-size, and rate-limit names over src/ and apps/OmpOS.Web/: **0 hits**.
- Stdin or console readers (`OpenStandardInput|Console\.In|Console\.ReadLine`): **0 hits**. Line readers exist for ACP, RPC, SSE, and remote engine output only.
- Shell or argv risk (`UseShellExecute = true`, `/bin/sh`, `bash -c`, `cmd`): **0 hits** in src/ and apps/. Every process start found builds argv with ArgumentList.
- Scheme checks: CompletionSettings.cs:38-40 (http/https only); McpClientConnector.cs:58 (http/https only); ApiBaseUrlResolver.cs:50-62 (http only for loopback, any https); AgentOsProvider.cs:59 (IsAbsoluteUri only, so cleartext http is allowed).
- CI and shell risk (`curl|wget ... | sh`, `eval`, `chmod 777`, `--insecure`, `pull_request_target`, `secrets.`) over .github/, bootstrap.sh, scripts/: **0 hits**. `permissions:` appears once (ci.yml:10). `env_file` appears in ci.yml:48 and in test-services.sh write sites at :57, :84-90.

Entry-point confirmation:

Confirmed bounded or validated:
- RPC stdout frames: 1 MiB per physical line (OmpRpcProcess.cs:114; RpcChunkAssembler.cs:34-35), 64 MiB per reassembled frame (:112, :225).
- Plugin manifest: 64 KiB before read and parse (PluginManifestSource.cs:21-22; PluginManifestParser.cs:40).
- Workspace convention files (setup.json, modes, rules, skills): 256 KiB before read (WorkspaceFiles.cs:38).
- Terminal output: bounded characters per stream (SystemProcessRunner.cs:58-79). Hook output: 16 KiB (HookOutput.cs:13).
- Terminal agent proposals: approval gate unless AutoRunEnabled; working directory through WorkspacePath (TerminalGate.cs:106-108, :256).
- ACP agent fs/read and fs/write: disabled without FsRoot; containment checks include symlinks and separators (AcpAgentSession.cs:951, :1014-1025).
- Worktree run id and Herdr session id: validated before use (WorktreeRunId; HerdrSessionId.cs:35).
- Workspace search: files over 1 MiB skipped (WorkspaceSearch.cs:179).
- Database access: parameterized (PostgresSessionRepository.cs).

Not confirmed (no cap or validation found in code I read):
- ACP child stdout line (AcpStdioTransport.cs:247): Medium finding.
- ACP child stderr line (AcpStdioTransport.cs:201) and RPC stderr line (OmpRpcProcess.cs:237): Low findings.
- Remote engine SSE and JSON payload (RemoteEngineHost.cs:343, :417): Medium finding.
- Web SSE parser buffer (SseParser.cs:43): Medium finding. SSE payload (SseEventStream.cs:327): Low finding.
- Web API JSON bodies (ApiSessionClient.cs:37, :50, :72): Low finding.
- Registry export read (RegistryReader.cs:28): Low finding.
- Local state reads (HerdrSessionStore.cs:41, PluginStateStore.cs:42): Low findings.
- Editor file open (MainView.axaml.cs:633): path contained, size uncapped. Low finding.
- Repo setup file execution (SetupRunner.cs:36-51): Medium finding. Unwired today: only tests call it.
- Stdin: no reader exists under src/ or apps/.
- services/OmpOS.Api (the HTTP API that the web client calls): outside the stated audit scope. Its routes, auth, and body limits were not audited.

## 3. Gaps

- Boundaries: the HTTP API host is outside this lens's scope. `services/OmpOS.Api` is excluded by the stated scope (only src/, apps/, tests/, scripts/, .github/, bootstrap.sh). Its endpoints, authentication, and request limits are unverified here. Recommend a lens covering services/.
- Boundaries: MCP client configuration sourcing (src/OmpOS.Harness.Mcp), automation definitions (src/OmpOS.Harness.Automations), and Dragonfly coordination inputs (src/OmpOS.Coordination.Dragonfly) were not traced to their external sources. Only their scheme and size checks were grepped.
- Security: CompletionClient request and response handling (src/OmpOS.Harness.Completion) was checked only for scheme enforcement, not for response bounds or credential handling.
- Security: the approval policy in the native composition root (apps/OmpOS.Native/Program.cs, ComposeProductionEngine) and the hook consent UI were not traced end to end.
- Security: dependency vulnerabilities and secret storage at rest (keychain, user config) are outside what greps can check. The dotnet analyzers cannot run under this lens's rules.
- Tests: `JsonSerializer.Deserialize` and file-operation hits under tests/ were not enumerated one by one. Observed hits were fixtures.
- Scans: the grep tool truncated some result pages. Missing hits were recovered with narrower path reruns; any remaining gap would be in tests/ only.

## 4. Method

I started with the nine required greps, ran a positive control for the search tool, and re-ran truncated scans on narrower paths. For each external-input entry point I read the producer and the consumer, and I checked size caps, scheme checks, and path containment by reading the code at the cited lines rather than by name. Outbound and file sinks that derived paths from a root or an identifier were traced back to their validators (WorkspaceRoot.Resolve, WorkspacePath, WorktreeRunId.Validate, IsSafeId, AcpAgentSession containment). Findings are limited to lines I read. Test-only hits were classified by pattern rather than read individually.
