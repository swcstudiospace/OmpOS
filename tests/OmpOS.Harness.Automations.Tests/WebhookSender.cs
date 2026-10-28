using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OmpOS.Harness.Automations.Hosted;

namespace OmpOS.Harness.Automations.Tests;

/// <summary>
/// Signs webhook deliveries the way a sender does, with its own HMAC code: a test that signed with the library's code could not notice
/// a change of the wire format.
/// </summary>
internal static class WebhookSender
{
    /// <summary>The secret the tests sign with.</summary>
    public const string Secret = "whsec_unit_test_secret_value";

    /// <summary>Computes <c>sha256=</c> plus the lower-case hex HMAC of the bytes.</summary>
    /// <param name="secret">The key.</param>
    /// <param name="data">The signed bytes.</param>
    /// <returns>The header value.</returns>
    public static string Sign(string secret, byte[] data) =>
        "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), data));

    /// <summary>Builds a generic delivery signed over <c>timestamp.body</c>.</summary>
    /// <param name="body">The body text.</param>
    /// <param name="sentAt">The instant written to the timestamp header.</param>
    /// <param name="secret">The signing secret.</param>
    /// <param name="delivery">An optional delivery identifier.</param>
    /// <returns>The request.</returns>
    public static WebhookRequest Generic(string body, DateTimeOffset sentAt, string secret = Secret, string? delivery = null)
    {
        var timestamp = sentAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var bytes = Encoding.UTF8.GetBytes(body);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-OmpOS-Timestamp"] = timestamp,
            ["X-OmpOS-Signature"] = Sign(secret, Encoding.UTF8.GetBytes(timestamp + "." + body)),
        };
        if (delivery is not null)
        {
            headers["X-OmpOS-Delivery"] = delivery;
        }

        return new WebhookRequest(bytes, headers);
    }

    /// <summary>Builds a GitHub delivery signed over the body.</summary>
    /// <param name="eventName">The <c>X-GitHub-Event</c> value.</param>
    /// <param name="body">The body text.</param>
    /// <param name="secret">The signing secret.</param>
    /// <param name="delivery">The <c>X-GitHub-Delivery</c> value.</param>
    /// <returns>The request.</returns>
    public static WebhookRequest GitHub(string eventName, string body, string secret = Secret, string delivery = "delivery-1")
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Hub-Signature-256"] = Sign(secret, bytes),
            ["X-GitHub-Event"] = eventName,
            ["X-GitHub-Delivery"] = delivery,
        };
        return new WebhookRequest(bytes, headers);
    }

    /// <summary>Returns a copy of a request with one header replaced or added.</summary>
    /// <param name="request">The request.</param>
    /// <param name="name">The header name.</param>
    /// <param name="value">The new value.</param>
    /// <returns>The changed request.</returns>
    public static WebhookRequest With(WebhookRequest request, string name, string value)
    {
        var headers = new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase) { [name] = value };
        return new WebhookRequest(request.Body, headers);
    }

    /// <summary>Returns a copy of a request without one header.</summary>
    /// <param name="request">The request.</param>
    /// <param name="name">The header name.</param>
    /// <returns>The changed request.</returns>
    public static WebhookRequest Without(WebhookRequest request, string name)
    {
        var headers = new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase);
        _ = headers.Remove(name);
        return new WebhookRequest(request.Body, headers);
    }

    /// <summary>A GitHub push payload to <paramref name="branch"/>.</summary>
    /// <param name="branch">The pushed branch.</param>
    /// <param name="deleted">Whether the push deleted the branch.</param>
    /// <returns>The JSON text.</returns>
    public static string PushPayload(string branch, bool deleted = false) =>
        $$"""
        {"ref":"refs/heads/{{branch}}","after":"abc123def456","deleted":{{(deleted ? "true" : "false")}},"repository":{"full_name":"acme/widgets"},"pusher":{"name":"octocat"},"commits":[{"id":"abc123def4567890","message":"Fix the thing\n\nLong body"}]}
        """;

    /// <summary>A GitHub pull request payload.</summary>
    /// <param name="action">The action.</param>
    /// <param name="baseBranch">The base branch.</param>
    /// <param name="merged">Whether the pull request was merged.</param>
    /// <param name="headRepository">The head repository; the base repository is <c>acme/widgets</c>.</param>
    /// <param name="title">The pull request title.</param>
    /// <returns>The JSON text.</returns>
    public static string PullRequestPayload(string action, string baseBranch = "main", bool merged = false, string headRepository = "acme/widgets", string title = "Add a feature") =>
        $$$$"""
        {"action":"{{{{action}}}}","pull_request":{"base":{"ref":"{{{{baseBranch}}}}","repo":{"full_name":"acme/widgets"}},"head":{"ref":"feature","repo":{"full_name":"{{{{headRepository}}}}"}},"number":42,"title":"{{{{title}}}}","merged":{{{{(merged ? "true" : "false")}}}},"html_url":"https://github.com/acme/widgets/pull/42","user":{"login":"octocat"}}}
        """;
}
