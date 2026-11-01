using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Harness.Mcp.Config;
using Autonogrammer.Presentation.Settings.Connectors;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>A secret-shaped marker in headers, arguments, environment values and URL paths never reaches a text surface.</summary>
public sealed class ConnectorSecretCanaryTests
{
    private const string Marker = "CANARY-secret-76-02";

    [Fact]
    public async Task Marker_IsAbsentFromCheckerResultCardAndDiagnostics()
    {
        var logs = new CaptureLogger();
        var checker = new McpConnectionChecker(new McpConnectionCheckOptions { Timeout = TimeSpan.FromSeconds(2) }, logger: logs);
        var spec = new McpServerSpec
        {
            Name = "remote",
            Transport = McpTransportKind.Http,
            Scope = McpScope.User,
            Format = McpConfigFormat.Autonogrammer,
            Origin = "~/.autonogrammer/mcp.json",
            Priority = 1,
            Url = new Uri("http://127.0.0.1:9/mcp/" + Marker),
            Args = [Marker + "-arg"],
            Env = new Dictionary<string, string>(StringComparer.Ordinal) { ["TOKEN"] = Marker + "-env" },
            Headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = "Bearer " + Marker },
            ConsentIdentity = "consent",
        };

        var result = await checker.CheckAsync(spec, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var card = new ConnectorCardModel(spec.Name, spec.Name, ConnectorToolState.NeedsApproval);
        var matrix = new ConnectorMatrixModel(new EmptyStore());
        var surfaces = string.Join(
            '\n',
            result.ToString(),
            result.Reason,
            spec.ToString(),
            card.ToString(),
            matrix.ToString(),
            logs.Text);

        surfaces.ShouldNotContain(Marker);
        result.Reason.ShouldBe(McpConnectionChecker.ConnectorCheckFailed);
    }

    private sealed class EmptyStore : IConnectorPermissionStore
    {
        public IReadOnlyList<string> ReadAllowTokens() => [];

        public IReadOnlyList<string> ReadDenyTokens() => [];

        public void ReplaceAllowTokens(IReadOnlyList<string> tokens)
        {
        }

        public void ReplaceDenyTokens(IReadOnlyList<string> tokens)
        {
        }
    }

    private sealed class CaptureLogger : ILogger
    {
        public string Text { get; private set; } = string.Empty;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Text += formatter(state, exception);
            if (exception is not null)
            {
                Text += exception.GetType().Name;
            }
        }
    }
}
