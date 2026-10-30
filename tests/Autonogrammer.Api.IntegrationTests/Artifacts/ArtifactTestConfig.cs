using System.Text;

namespace Autonogrammer.Api.IntegrationTests.Artifacts;

/// <summary>Shared artifact host settings.</summary>
internal static class ArtifactTestConfig
{
    /// <summary>Returns a development host with artifacts enabled and two ticket keys.</summary>
    /// <returns>The configuration.</returns>
    public static Dictionary<string, string?> Enabled()
    {
        var config = JwtTestHost.DevelopmentConfig();
        config["Cowork:Artifacts:Enabled"] = "true";
        config["Cowork:Artifacts:RenderTicketKeys:0"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("render-ticket-key-one-0123456789abcdef"));
        config["Cowork:Artifacts:RenderTicketKeys:1"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("render-ticket-key-two-0123456789abcdef"));
        return config;
    }
}
