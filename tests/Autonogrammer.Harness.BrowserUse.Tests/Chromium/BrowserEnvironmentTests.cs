using Autonogrammer.Harness.BrowserUse.Chromium;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Chromium;

/// <summary>The browser allow-list drops secrets and the names the engine policy keeps.</summary>
public sealed class BrowserEnvironmentTests
{
    [Fact]
    public void Build_DropsSecretsAndEngineNames_AndKeepsDisplayNames()
    {
        var parent = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["OMP_API_KEY"] = "planted-not-a-secret-1",
            ["FOO_TOKEN"] = "planted-not-a-secret-2",
            ["BAR_SECRET"] = "planted-not-a-secret-3",
            ["AUTONOGRAMMER_X"] = "planted-not-a-secret-4",
            ["SERVICE_KEY"] = "planted-not-a-secret-5",
            ["DB_SECRET"] = "planted-not-a-secret-6",
            ["ACCESS_TOKEN"] = "planted-not-a-secret-7",
            ["OTHER_KEY"] = "planted-not-a-secret-8",
            ["OTHER_SECRET"] = "planted-not-a-secret-9",
            ["OTHER_TOKEN"] = "planted-not-a-secret-10",
            ["HTTP_PROXY"] = "planted-not-a-secret-11",
            ["HTTPS_PROXY"] = "planted-not-a-secret-12",
            ["NO_PROXY"] = "planted-not-a-secret-13",
            ["http_proxy"] = "planted-not-a-secret-14",
            ["https_proxy"] = "planted-not-a-secret-15",
            ["no_proxy"] = "planted-not-a-secret-16",
            ["NODE_EXTRA_CA_CERTS"] = "planted-not-a-secret-17",
            ["SSL_CERT_FILE"] = "planted-not-a-secret-18",
            ["SSL_CERT_DIR"] = "planted-not-a-secret-19",
            ["PI_CODING_AGENT_DIR"] = "planted-not-a-secret-20",
            ["OMP_HOME"] = "planted-not-a-secret-21",
            ["USER"] = "planted-not-a-secret-22",
            ["LOGNAME"] = "planted-not-a-secret-23",
            ["SHELL"] = "planted-not-a-secret-24",
            ["TERM"] = "planted-not-a-secret-25",
            ["TZ"] = "planted-not-a-secret-26",
            ["ALL_PROXY"] = "planted-not-a-secret-27",
            ["PATH"] = "/usr/bin",
            ["HOME"] = "/home/agent",
            ["LANG"] = "C.UTF-8",
            ["LC_ALL"] = "C.UTF-8",
            ["DISPLAY"] = ":1",
        };

        var child = BrowserEnvironment.Build(parent);
        child.Keys.ShouldBe(["PATH", "HOME", "LANG", "LC_ALL", "DISPLAY"], ignoreOrder: true);
        child.Values.ShouldNotContain(value => value.StartsWith("planted-not-a-secret-", StringComparison.Ordinal));
    }
}
