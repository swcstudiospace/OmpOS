# ADR-0014: MCP client for ADE-16

- Status: Proposed 2026-10-26. The package is chosen and in use in `src/Autonogrammer.Harness.Mcp`. The record stays Proposed because OQ-52 closes only when the integration tests are recorded as passing, and none was run when this record was written.
- Date: 2026-10-26
- Owner: autonogrammer-maintainers
- Deciders: autonogrammer-maintainers
- Scope: which client library the host uses to talk to MCP servers (ADE-16 in `docs/plans/ade-harness-plan.md`, open question OQ-52 in `docs/open-questions.md`), how it is wrapped, and where it stops. Does not cover the host-owned gateway that omp connects to ([ADR-0010](ADR-0010-omp-mcp-gateway-feasibility.md); `src/Autonogrammer.Harness.McpGateway`), the MCP server Autonogrammer runs, MCP servers that omp itself talks to, or plugin isolation ([ADR-0013](ADR-0013-plugin-isolation.md)).

## Context

ADE-16 needs the host to connect to stdio and Streamable HTTP MCP servers, register their tools in one registry, enable or disable each tool per session, and route every tool call through an approval decision (plan row ADE-16 and risk R-10). ADR-0004 decision 5 makes MCP the out-of-process route for tools.

Two other MCP paths exist and are separate: omp's own MCP client (ADR-0010 documents that in ACP sessions the host supplies the MCP servers in `session/new.mcpServers`), and the gateway in `src/Autonogrammer.Harness.McpGateway`. This record is only about the client the Autonogrammer host uses.

## Candidate chosen

`ModelContextProtocol` 2.3.0, the C# SDK of the MCP project (NuGet page read 2026-10-26: published 2026-10-20, authors "ModelContextProtocol"). It depends on `ModelContextProtocol.Core` 2.3.0 exactly, which holds the client types the code uses (`McpClient`, `StdioClientTransport`, `HttpClientTransport`), plus `Microsoft.Extensions.Caching.Abstractions` and `Microsoft.Extensions.Hosting.Abstractions` 10.0.13.

Use in the repository:

- `Directory.Packages.props` pins `ModelContextProtocol` 2.3.0 in the group "MCP harness (ADE-16)". `src/Autonogrammer.Harness.Mcp/Autonogrammer.Harness.Mcp.csproj` references it; `tests/Autonogrammer.Harness.Mcp.Fixtures` references it for the fixture server side.
- `McpClientConnector` opens a stdio connection (`ConnectStdioAsync`: explicit command, argument list, working directory, no shell) or a Streamable HTTP connection (`ConnectHttpAsync`: absolute http or https endpoint). Both pin `ProtocolVersion` to `2025-11-25`.
- `McpToolRegistry` owns the connected clients and enables or disables each tool per session. `McpToolInvoker` is the only route to a server: a tool must be enabled for the session, and the injected `McpToolApproval` must approve before the server is contacted.

## Licence

| Package | Version | SPDX | Source | Verified |
| --- | --- | --- | --- | --- |
| ModelContextProtocol | 2.3.0 | Apache-2.0 | https://www.nuget.org/packages/ModelContextProtocol/2.3.0 | NuGet page `License` field, 2026-10-26 |
| ModelContextProtocol.Core | 2.3.0 | Apache-2.0 | https://www.nuget.org/packages/ModelContextProtocol.Core/2.3.0 | NuGet page `License` field, 2026-10-26 |
| Repository of the SDK | main | Apache-2.0, with MIT for older contributions | https://github.com/modelcontextprotocol/csharp-sdk | `LICENSE` read at https://raw.githubusercontent.com/modelcontextprotocol/csharp-sdk/main/LICENSE, 2026-10-26 |

The repository `LICENSE` opens with a notice that the project is moving from the MIT licence to Apache-2.0. New code is Apache-2.0. Contributions whose authors have not agreed to relicense remain MIT. That is the "mixed" licence that OQ-52 mentions. Both licences are permissive; the NuGet package declares Apache-2.0. The licence text and any `NOTICE` content of the package were not inspected. `THIRD-PARTY-NOTICES.txt` is listed in the repository; it was not read.

## Decision

1. The Autonogrammer host uses `ModelContextProtocol` 2.3.0 for every MCP client connection it makes. No hand-written JSON-RPC MCP client is kept in the repository.
2. Transports are stdio and Streamable HTTP only. `HttpClientTransport` is constructed with `TransportMode = StreamableHttp`, so the legacy SSE transport is not used by the host.
3. The protocol revision is pinned to `2025-11-25` in `McpClientConnector.ProtocolVersion`, because the code comment records that the SDK's default probe for the newest revision races under load. A move to a newer revision is a change to that constant with a test, not a silent upgrade.
4. The SDK is wrapped, not exposed: callers get tools through `McpToolRegistry` and calls through `McpToolInvoker`. A call with no approval never reaches the server, and a disabled tool is refused before approval is requested.
5. Process spawning for stdio servers happens only inside `Autonogrammer.Harness.Mcp` (an `Autonogrammer.Harness.*` project), through the SDK's `StdioClientTransport`, with an explicit working directory and an argument list.
6. The browser-hosted Blazor head does not use stdio. Harness assemblies that spawn processes stay out of `Autonogrammer.Web` (architecture rule).

## Verification route

Run by the orchestrator; none of these was run when this record was written.

- `dotnet test tests/Autonogrammer.Harness.Mcp.Tests/Autonogrammer.Harness.Mcp.Tests.csproj`. The tests start the fixture servers in `tests/Autonogrammer.Harness.Mcp.Fixtures` (a stdio server and an HTTP server on a loopback port the test picks). Tests that bear on this record: `McpHttpTests.StdioAndHttpServers_RegisterToolsInOneRegistry`, `HttpTool_IsDisabledPerSession_AndApprovedCallReturnsTheServerResult`, `UnapprovedHttpCall_IsDenied_AndTheHttpServerIsNotContacted`, `ConnectingToAnUnreachableEndpoint_Fails`, `ConnectingOverAnythingButHttp_IsRejected`; `McpToolInvokerTests.UnapprovedCall_IsDenied_AndTheServerIsNotContacted`, `DisabledTool_IsRefusedBeforeApproval_AndTheServerIsNotContacted`, `ToolDisabledForOneSession_IsCallableFromAnotherSession_WhenApproved`; `McpToolRegistryTests.RegisteredServer_ListsItsToolsWithNamesDescriptionsAndSchemas`, `DisabledTool_IsAbsentForItsSession_AndPresentForAnotherSession`.
- Resolved versions: `src/Autonogrammer.Harness.Mcp/packages.lock.json` lists `ModelContextProtocol` 2.3.0 and `ModelContextProtocol.Core` 2.3.0 (read 2026-10-26).
- Closing OQ-52 needs a recorded passing run of the project above on Linux, then on Windows and macOS for the stdio launch.
- Index and structure of this record: `tests/Autonogrammer.ArchitectureTests/DecisionRecordTests.cs`.

## Open risks

1. **Hidden process spawn.** `BannedSymbols.NoProcess.txt` bans `System.Diagnostics.Process` in Autonogrammer code. The SDK's `StdioClientTransport` starts a process inside the SDK, so that rule does not see it. The control is that `Autonogrammer.Harness.Mcp` is the only product project with a `PackageReference` to the SDK (the package flows to projects that reference it, such as `Autonogrammer.Native.Views`), and that a search of `src/`, `apps/` and `services/` on 2026-10-26 found `StdioClientTransport` only in `McpClientConnector`. A review must keep it that way.
2. **Stdio server environment.** `McpStdioServerOptions.EnvironmentVariables` sets or unsets variables on top of the SDK's launch. Whether the SDK passes the host's full environment to the server was not verified. Treat the server as able to read host secrets until it is.
3. **The SDK is young.** The 2.x line began on 2026-07-28 (NuGet version list). The connector pins a protocol revision because, per the comment in `McpClientConnector`, the SDK's default probe for the newest revision races under load. A later release can change that behaviour, so upgrades go through the Mcp test project.
4. **Meta-package weight.** `ModelContextProtocol` pulls hosting and caching abstractions that the client code does not use; `ModelContextProtocol.Core` alone may suffice. This was not tested. [INFERENCE]
5. **Approval is only as strong as the injected delegate.** `McpToolApproval` is supplied by the host. A search of `src/`, `apps/` and `services/` on 2026-10-26 found no caller of `McpClientConnector` outside `Autonogrammer.Harness.Mcp`; the native shell constructs a `McpToolRegistry` for its tool model only. No live MCP server is reached from a running head yet.
6. **Platform coverage.** No run on Windows or macOS is recorded in the repository for the stdio launch. An HTTP server that needs authentication is not covered: the connector sends no headers and runs no OAuth flow.
7. **Licence mix.** Apache-2.0 with MIT carry-over; the repository states the transition is in progress. No legal review.

## Alternatives considered and rejected

- **A hand-written MCP client over `System.Text.Json` and `HttpClient`.** Rejected: it duplicates protocol negotiation, session handling and Streamable HTTP framing that the official SDK maintains, and the repository would own every protocol revision.
- **Route every MCP call through omp.** Rejected for host-side tools: omp's MCP client is configured by the ACP `session/new.mcpServers` list (ADR-0010), so host-side approval, per-session enablement and tests would live in another process. This remains the path for tools the model calls inside an omp turn.
- **`ModelContextProtocol.Core` only.** Not adopted yet; see risk 4.
- **Another .NET MCP client package.** Not evaluated: this record found no reason to look beyond the SDK maintained by the MCP project, and no other package was verified.

## Related

- [ADR-0004](ADR-0004-cross-platform-ui.md) decision 5, [ADR-0010](ADR-0010-omp-mcp-gateway-feasibility.md), [ADR-0013](ADR-0013-plugin-isolation.md), `docs/plans/ade-harness-plan.md` (ADE-16, R-10), `docs/open-questions.md` OQ-52.
