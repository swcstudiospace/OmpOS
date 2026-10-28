using System.Net;
using Autonogrammer.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings.Models;

/// <summary>
/// The connection test of <see cref="Autonogrammer.Native.Settings.Models.ModelsSettingsSource"/>: a local engine or a custom provider on a
/// loopback or plain http endpoint is asked for its model list without any credential; every other provider is not testable and
/// answers with a blank message, so the page shows its own fixed sentence. No network is involved: the HTTP handler is a stub.
/// </summary>
public sealed class ModelsSettingsSourceProbeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ARunningLocalEngineIsReachableAndTheRequestCarriesNoCredential()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();

        var outcome = await composition.Source.TestAsync("ollama", Token).ConfigureAwait(true);

        outcome.Status.ShouldBe(ProviderTestStatus.Reachable);
        outcome.Message.ShouldContain("2 model(s)", Case.Sensitive);
        fake.Probe.Requests.ShouldBe([new Uri("http://127.0.0.1:11434/v1/models")]);
        fake.Probe.SentCredentials.ShouldBeFalse();
    }

    [Fact]
    public async Task ALocalEngineThatDoesNotAnswerIsNotRunning()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        fake.Probe.Refuse = true;
        using var composition = fake.Compose();

        var outcome = await composition.Source.TestAsync("lm-studio", Token).ConfigureAwait(true);

        outcome.Status.ShouldBe(ProviderTestStatus.NotRunning);
        outcome.Message.ShouldContain("not running", Case.Sensitive);
        fake.Probe.Requests.ShouldBe([new Uri("http://127.0.0.1:1234/v1/models")]);
    }

    [Fact]
    public async Task AKeyedCustomProviderOnAPlainHttpEndpointIsProbedWithoutItsKeyAndAnAuthRefusalIsNotAFailureOfTheKey()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        fake.Probe.Status = HttpStatusCode.Unauthorized;
        using var composition = fake.Compose();
        var form = ModelsForms.Gateway("lan") with
        {
            Protocol = CustomProviderProtocol.OpenAiCompatible,
            BaseEndpoint = new Uri("http://192.168.1.20:8000/v1"),
            Auth = CustomProviderAuth.ApiKey,
            AllowRemotePlainHttp = true,
        };
        (await composition.Source.SaveCustomProviderAsync(form, Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        (await composition.Source.SaveApiKeyAsync("lan", ModelsForms.Canary, Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();

        var outcome = await composition.Source.TestAsync("lan", Token).ConfigureAwait(true);

        outcome.Status.ShouldBe(ProviderTestStatus.Unauthorized);
        outcome.Message.ShouldContain("refused the request", Case.Sensitive);
        outcome.Message.ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
        fake.Probe.Requests.ShouldBe([new Uri("http://192.168.1.20:8000/v1/models")]);
        fake.Probe.SentCredentials.ShouldBeFalse("the test asks whether something answers, it never validates a key");
    }

    [Fact]
    public async Task ASomethingElseOnThePortIsAFailure()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        fake.Probe.Body = "<html>hello</html>";
        using var composition = fake.Compose();
        (await composition.Source.SaveCustomProviderAsync(ModelsForms.Local("acme"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();

        var outcome = await composition.Source.TestAsync("acme", Token).ConfigureAwait(true);

        outcome.Status.ShouldBe(ProviderTestStatus.Failed);
        outcome.Message.ShouldContain("did not answer with an OpenAI-style model list", Case.Sensitive);
    }

    [Fact]
    public async Task AServerErrorIsWordedByTheErrorMapper()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        fake.Probe.Status = HttpStatusCode.ServiceUnavailable;
        using var composition = fake.Compose();
        (await composition.Source.SaveCustomProviderAsync(ModelsForms.Local("acme"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();

        var outcome = await composition.Source.TestAsync("acme", Token).ConfigureAwait(true);

        outcome.Status.ShouldBe(ProviderTestStatus.Failed);
        outcome.Message.ShouldContain("failed on its side (HTTP 503)", Case.Sensitive);
        outcome.Message.ShouldContain("not the cause", Case.Sensitive);
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("anthropic")]
    [InlineData("groq")]
    [InlineData("made-up")]
    public async Task ACloudOrBuiltInProviderIsNotTestableAndTheMessageIsBlank(string providerId)
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();

        var outcome = await composition.Source.TestAsync(providerId, Token).ConfigureAwait(true);

        outcome.Status.ShouldBe(ProviderTestStatus.NotTestable);
        outcome.Message.ShouldBeEmpty();
        fake.Probe.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ACustomProviderOnARemoteHttpsEndpointIsNotTestableAndSoIsAnAnthropicEndpoint()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        var remote = ModelsForms.Gateway("remote") with { Protocol = CustomProviderProtocol.OpenAiCompatible, BaseEndpoint = new Uri("https://api.example.com/v1") };
        var anthropicLocal = ModelsForms.Gateway("anthro") with { BaseEndpoint = new Uri("http://127.0.0.1:9000") };
        (await composition.Source.SaveCustomProviderAsync(remote, Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        (await composition.Source.SaveCustomProviderAsync(anthropicLocal, Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();

        var remoteOutcome = await composition.Source.TestAsync("remote", Token).ConfigureAwait(true);
        var anthropicOutcome = await composition.Source.TestAsync("anthro", Token).ConfigureAwait(true);

        remoteOutcome.Status.ShouldBe(ProviderTestStatus.NotTestable);
        remoteOutcome.Message.ShouldBeEmpty();
        anthropicOutcome.Status.ShouldBe(ProviderTestStatus.NotTestable);
        anthropicOutcome.Message.ShouldBeEmpty();
        fake.Probe.Requests.ShouldBeEmpty();
    }

    private static void SkipUnlessLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fake omp is a POSIX shell script.");
        }
    }
}
