namespace Autonogrammer.Harness.Mcp.Tests;

/// <summary>A fake approval function that returns a fixed decision and counts every request it sees.</summary>
internal sealed class CountingApproval
{
    private readonly bool _decision;

    /// <summary>Creates the fake.</summary>
    /// <param name="decision">What every request is answered with.</param>
    public CountingApproval(bool decision)
    {
        _decision = decision;
    }

    /// <summary>The number of approval requests received.</summary>
    public int Calls { get; private set; }

    /// <summary>The requests received, in order.</summary>
    public List<McpToolCallRequest> Requests { get; } = [];

    /// <summary>The approval function to inject.</summary>
    public McpToolApproval Approve => (request, _) =>
    {
        Calls++;
        Requests.Add(request);
        return ValueTask.FromResult(_decision);
    };
}
