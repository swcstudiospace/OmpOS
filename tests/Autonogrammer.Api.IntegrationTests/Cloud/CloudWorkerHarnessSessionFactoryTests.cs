using Autonogrammer.Api.Cloud;
using Autonogrammer.Api.Cloud.Options;
using Autonogrammer.Harness.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Cloud;

/// <summary>The decision that keeps a cloud agent's run off the local harness, and the id rule it rests on.</summary>
public sealed class CloudWorkerHarnessSessionFactoryTests
{
    /// <summary>A session that is not a cloud agent's gets the wrapped factory's session, and the prompt-only overload is passed straight through.</summary>
    [Fact]
    public void NonCloudSessions_GoToTheWrappedFactory()
    {
        var inner = new RecordingFactory();
        using var provider = Provider(enabled: true);
        var factory = new CloudWorkerHarnessSessionFactory(inner, provider);

        var session = factory.CreateSession("p", "0123456789abcdef0123456789abcdef", "run-1");
        session.ShouldBeSameAs(inner.Created.ShouldNotBeNull());
        factory.CreateSession("p").ShouldNotBeNull();

        inner.Calls.ShouldBe(2);
    }

    /// <summary>A cloud agent's run never reaches the wrapped factory: with cloud off it fails with a stable context, and nothing runs locally.</summary>
    [Fact]
    public void CloudSessions_NeverReachTheWrappedFactory()
    {
        var inner = new RecordingFactory();
        using var provider = Provider(enabled: false);
        var factory = new CloudWorkerHarnessSessionFactory(inner, provider);

        var failure = Should.Throw<ProcessStartHarnessException>(() => factory.CreateSession("p", CloudSessionIds.Prefix + "abc", "run-1"));

        failure.Context.ShouldBe("cloud_disabled");
        inner.Calls.ShouldBe(0);
    }

    /// <summary>The cloud prefix marks exactly the ids made for cloud agents, and a new id is a valid worker token subject.</summary>
    [Fact]
    public void SessionIds_CarryTheCloudPrefix()
    {
        var id = CloudSessionIds.New();

        CloudSessionIds.IsCloud(id).ShouldBeTrue();
        id.Length.ShouldBe(38);
        id.All(static character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-').ShouldBeTrue();
        CloudSessionIds.IsCloud(Guid.NewGuid().ToString("N")).ShouldBeFalse();
        CloudSessionIds.IsCloud(null).ShouldBeFalse();
        CloudSessionIds.IsCloud("Cloud-upper").ShouldBeFalse();
    }

    private static ServiceProvider Provider(bool enabled)
    {
        var services = new ServiceCollection();
        services.Configure<CloudOptions>(options => options.Enabled = enabled);
        return services.BuildServiceProvider();
    }

    private sealed class RecordingFactory : IHarnessSessionFactory
    {
        public int Calls { get; private set; }

        public IOmpSession? Created { get; private set; }

        public IOmpSession CreateSession(string prompt)
        {
            Calls++;
            Created = new StubSession();
            return Created;
        }
    }

    private sealed class StubSession : IOmpSession
    {
        public HarnessState State => HarnessState.Disconnected;

        public IReadOnlyDictionary<string, string> AdvertisedCapabilities { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public Task CreateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ResumeAsync(string sessionId, CancellationToken cancellationToken) => Task.CompletedTask;

        public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(string prompt, PromptOptions options, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield break;
        }

        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
