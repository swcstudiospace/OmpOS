using Autonogrammer.Harness.Mcp;

namespace Autonogrammer.Harness.McpGateway.Tests;

/// <summary>A fake approval function that returns a fixed decision and counts every request it sees. Safe under concurrent calls.</summary>
internal sealed class CountingApproval
{
    private readonly Lock _gate = new();
    private readonly bool _decision;
    private readonly List<McpToolCallRequest> _requests = [];

    /// <summary>Creates the fake.</summary>
    /// <param name="decision">What every request is answered with.</param>
    public CountingApproval(bool decision)
    {
        _decision = decision;
    }

    /// <summary>The number of approval requests received.</summary>
    public int Calls
    {
        get
        {
            lock (_gate)
            {
                return _requests.Count;
            }
        }
    }

    /// <summary>The requests received, in arrival order.</summary>
    public IReadOnlyList<McpToolCallRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return _requests.ToList();
            }
        }
    }

    /// <summary>The approval function to inject.</summary>
    public McpToolApproval Approve => (request, _) =>
    {
        lock (_gate)
        {
            _requests.Add(request);
        }

        return ValueTask.FromResult(_decision);
    };
}
