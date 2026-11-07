using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Secrets.Os.Tests;

/// <summary>
/// Round-trips a throwaway item through the real Linux Secret Service (libsecret and a running keyring daemon such as gnome-keyring on the
/// session bus). Set <c>AUTONOGRAMMER_LIVE_KEYRING=1</c> and run the tests of the category Live. The item lives under a unique <c>app</c>
/// attribute, so no item of the application is read, listed or removed, and it is removed again at the end. When the collection is locked
/// or no Secret Service answers, the test skips and states which.
/// </summary>
[Trait("Category", "Live")]
public sealed class LiveKeyringTests
{
    private const string OptInVariable = "AUTONOGRAMMER_LIVE_KEYRING";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AThrowawayItemIsStoredReadListedAndRemoved()
    {
        Assert.SkipUnless(
            string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "1", StringComparison.Ordinal),
            $"Set {OptInVariable}=1 to run the live round trip against the real Linux Secret Service.");
        Assert.SkipUnless(OperatingSystem.IsLinux(), "The live keyring round trip needs the Linux Secret Service.");

        string suffix = Guid.NewGuid().ToString("N");
        var store = new LinuxLibsecretStore(new LibsecretNativeApi("autonogrammer-live-" + suffix), null, null);
        string name = "live-keyring-" + suffix;
        var secret = new SecretString("live-keyring-value-" + suffix);
        try
        {
            try
            {
                await store.SetAsync(name, secret, Token).ConfigureAwait(true);
            }
            catch (SecretStoreException exception) when (exception.Failure is SecretStoreFailure.Locked or SecretStoreFailure.Unavailable or SecretStoreFailure.Denied)
            {
                Assert.Skip($"The Secret Service cannot be used from this session ({exception.Failure}): {exception.Message}");
            }

            (await store.GetAsync(name, Token).ConfigureAwait(true))!.Reveal().ShouldBe(secret.Reveal());
            (await store.ListNamesAsync(Token).ConfigureAwait(true)).ShouldBe([name]);
            (await store.RemoveAsync(name, Token).ConfigureAwait(true)).ShouldBeTrue();
            (await store.GetAsync(name, Token).ConfigureAwait(true)).ShouldBeNull();
            (await store.ListNamesAsync(Token).ConfigureAwait(true)).ShouldBeEmpty();
        }
        finally
        {
            try
            {
                _ = await store.RemoveAsync(name, CancellationToken.None).ConfigureAwait(true);
            }
            catch (SecretStoreException)
            {
                // The round trip already reported its own result; cleanup of a throwaway item must not mask it.
            }
        }
    }
}
