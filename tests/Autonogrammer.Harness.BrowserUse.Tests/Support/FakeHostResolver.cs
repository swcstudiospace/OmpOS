using System.Net;
using Autonogrammer.Harness.BrowserUse.Egress;

namespace Autonogrammer.Harness.BrowserUse.Tests.Support;

/// <summary>Scripts resolver answers. Each call consumes one answer so a flip is observable.</summary>
internal sealed class FakeHostResolver : IHostResolver
{
    private readonly Queue<IReadOnlyList<IPAddress>> _answers = new();

    /// <summary>Gets how many times the resolver was called.</summary>
    public int Calls { get; private set; }

    /// <summary>Queues one answer set.</summary>
    /// <param name="addresses">The addresses returned together.</param>
    public void Enqueue(params IPAddress[] addresses) => _answers.Enqueue(addresses);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        if (_answers.Count == 0)
        {
            throw new InvalidOperationException("No scripted answer remains.");
        }

        return ValueTask.FromResult(_answers.Dequeue());
    }
}
