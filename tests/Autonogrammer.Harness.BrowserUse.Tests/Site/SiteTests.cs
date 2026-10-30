#pragma warning disable CA2007
#pragma warning disable MA0004
using Autonogrammer.Harness.BrowserUse.Tests.Support;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Site;

/// <summary>Offline proof that the two-port site answers and records requests, and that the page fixtures ship beside the assembly.</summary>
public sealed class SiteTests
{
    [Fact]
    public async Task BothPorts_ServeRequests_AndAttachThemToTheLogs()
    {
        await using var site = LocalTestSite.Start();
        using var client = new HttpClient();
        var grantedUrl = "http://127.0.0.1:" + site.GrantedPort.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/granted-index.html";
        var ungrantedUrl = "http://127.0.0.1:" + site.UngrantedPort.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/ungranted-index.html";

        var granted = await client.GetStringAsync(new Uri(grantedUrl)).ConfigureAwait(true);
        var ungranted = await client.GetStringAsync(new Uri(ungrantedUrl)).ConfigureAwait(true);

        granted.ShouldBe("ok");
        ungranted.ShouldBe("ok");
        site.GrantedLog.ShouldContain(grantedUrl);
        site.UngrantedLog.ShouldContain(ungrantedUrl);
        site.UngrantedLog.ShouldNotContain(entry => entry.Contains(":" + site.GrantedPort.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/", StringComparison.Ordinal));
    }

    [Fact]
    public void PageFixtures_ShipBesideTheAssembly()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Site", "pages");
        var granted = File.ReadAllText(Path.Combine(root, "granted-index.html"));
        var ungranted = File.ReadAllText(Path.Combine(root, "ungranted-index.html"));
        granted.ShouldContain("/redirect?to=");
        granted.ShouldContain("169.254.169.254");
        granted.ShouldContain("10.1.2.3");
        ungranted.ShouldContain("must never be fetched");
    }
}
