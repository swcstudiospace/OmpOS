using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Secrets.Tests;

/// <summary>
/// Verifies <see cref="OwnerOnlyFileSecretStore"/>: durability across instances, the owner-only modes of the directory and the file,
/// the atomic replacement, the refusal to reset a corrupt file, and that no secret value reaches an exception message or a rendering.
/// </summary>
[UnsupportedOSPlatform("windows")]
public sealed class OwnerOnlyFileSecretStoreTests : IDisposable
{
    private const string Canary = "file-store-canary-secret-5d0c19e7b2a4";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ompos-secrets-" + Guid.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string StorePath => Path.Combine(_root, "state", "secrets", "secrets.json");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task ASecretIsDurableAcrossStoreInstances()
    {
        var first = new OwnerOnlyFileSecretStore(StorePath);
        await first.SetAsync("OMPOS_PROVIDER_ACME_KEY", new SecretString(Canary), Token).ConfigureAwait(true);

        var second = new OwnerOnlyFileSecretStore(StorePath);

        (await second.GetAsync("OMPOS_PROVIDER_ACME_KEY", Token).ConfigureAwait(true))!.Reveal().ShouldBe(Canary);
        (await second.ListNamesAsync(Token).ConfigureAwait(true)).ShouldBe(["OMPOS_PROVIDER_ACME_KEY"]);
    }

    [Fact]
    public async Task TheFileHoldsTheValueAsPlainTextInAVersionedDocument()
    {
        var store = new OwnerOnlyFileSecretStore(StorePath);
        await store.SetAsync("name", new SecretString(Canary), Token).ConfigureAwait(true);

        using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(StorePath, Token).ConfigureAwait(true));

        document.RootElement.GetProperty("version").GetInt32().ShouldBe(1);
        document.RootElement.GetProperty("secrets").GetProperty("name").GetString().ShouldBe(Canary);
    }

    [Fact]
    public async Task TheDirectoryIsCreatedOwnerOnlyAndTheFileIsOwnerReadWrite()
    {
        SkipWithoutUnixModes();
        var store = new OwnerOnlyFileSecretStore(StorePath);

        await store.SetAsync("name", new SecretString(Canary), Token).ConfigureAwait(true);

        File.GetUnixFileMode(Path.GetDirectoryName(StorePath)!).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.GetUnixFileMode(StorePath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Directory.GetFiles(Path.GetDirectoryName(StorePath)!).ShouldBe([StorePath]);
    }

    [Fact]
    public async Task EveryRewriteKeepsTheFileOwnerReadWrite()
    {
        SkipWithoutUnixModes();
        var store = new OwnerOnlyFileSecretStore(StorePath);
        await store.SetAsync("a", new SecretString("first"), Token).ConfigureAwait(true);
        await store.SetAsync("b", new SecretString("second"), Token).ConfigureAwait(true);
        _ = await store.RemoveAsync("a", Token).ConfigureAwait(true);

        File.GetUnixFileMode(StorePath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public async Task AFileWithLooseModeIsTightenedWhenRead()
    {
        SkipWithoutUnixModes();
        var store = new OwnerOnlyFileSecretStore(StorePath);
        await store.SetAsync("name", new SecretString("value"), Token).ConfigureAwait(true);
        File.SetUnixFileMode(StorePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        _ = await store.GetAsync("name", Token).ConfigureAwait(true);

        File.GetUnixFileMode(StorePath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public async Task ADedicatedDirectoryWithLooseModeIsTightenedOnTheNextWrite()
    {
        SkipWithoutUnixModes();
        var directory = Path.GetDirectoryName(StorePath)!;
        _ = Directory.CreateDirectory(directory);
        File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute);
        var store = new OwnerOnlyFileSecretStore(StorePath);

        await store.SetAsync("name", new SecretString("value"), Token).ConfigureAwait(true);

        File.GetUnixFileMode(directory).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [Fact]
    public async Task ASharedDirectoryWithOtherFilesKeepsItsMode()
    {
        SkipWithoutUnixModes();
        var directory = Path.GetDirectoryName(StorePath)!;
        _ = Directory.CreateDirectory(directory);
        var loose = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute;
        File.SetUnixFileMode(directory, loose);
        await File.WriteAllTextAsync(Path.Combine(directory, "unrelated.txt"), "x", Token).ConfigureAwait(true);
        var store = new OwnerOnlyFileSecretStore(StorePath);

        await store.SetAsync("name", new SecretString("value"), Token).ConfigureAwait(true);

        File.GetUnixFileMode(directory).ShouldBe(loose);
        File.GetUnixFileMode(StorePath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public async Task AFailedReplaceLeavesThePreviousContentsAndNoTemporaryFile()
    {
        var good = new OwnerOnlyFileSecretStore(StorePath);
        await good.SetAsync("name", new SecretString("before-value"), Token).ConfigureAwait(true);
        var before = await File.ReadAllBytesAsync(StorePath, Token).ConfigureAwait(true);
        var failing = new OwnerOnlyFileSecretStore(StorePath, static (_, _) => throw new IOException("disk gone: " + Canary));

        var fault = await Should.ThrowAsync<SecretStoreException>(failing.SetAsync("name", new SecretString(Canary), Token)).ConfigureAwait(true);

        fault.Message.ShouldNotContain(Canary, Case.Sensitive);
        fault.Message.ShouldContain("previous contents are unchanged", Case.Sensitive);
        (await File.ReadAllBytesAsync(StorePath, Token).ConfigureAwait(true)).ShouldBe(before);
        Directory.GetFiles(Path.GetDirectoryName(StorePath)!).ShouldBe([StorePath]);
        (await good.GetAsync("name", Token).ConfigureAwait(true))!.Reveal().ShouldBe("before-value");
    }

    [Fact]
    public async Task ACancelledWriteChangesNothing()
    {
        var store = new OwnerOnlyFileSecretStore(StorePath);
        await store.SetAsync("name", new SecretString("kept"), Token).ConfigureAwait(true);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);

        _ = await Should.ThrowAsync<OperationCanceledException>(store.SetAsync("name", new SecretString(Canary), cancelled.Token)).ConfigureAwait(true);

        (await store.GetAsync("name", Token).ConfigureAwait(true))!.Reveal().ShouldBe("kept");
        Directory.GetFiles(Path.GetDirectoryName(StorePath)!).ShouldBe([StorePath]);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("")]
    [InlineData("{\"version\":1,\"secrets\":{\"name\":")]
    public async Task ACorruptFileIsAnErrorAndIsNeverReset(string content)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
        await File.WriteAllTextAsync(StorePath, content, Token).ConfigureAwait(true);
        var store = new OwnerOnlyFileSecretStore(StorePath);

        _ = await Should.ThrowAsync<SecretStoreException>(store.GetAsync("name", Token)).ConfigureAwait(true);
        _ = await Should.ThrowAsync<SecretStoreException>(store.SetAsync("name", new SecretString(Canary), Token)).ConfigureAwait(true);
        _ = await Should.ThrowAsync<SecretStoreException>(store.RemoveAsync("name", Token)).ConfigureAwait(true);
        _ = await Should.ThrowAsync<SecretStoreException>(store.ListNamesAsync(Token)).ConfigureAwait(true);

        (await File.ReadAllTextAsync(StorePath, Token).ConfigureAwait(true)).ShouldBe(content);
    }

    [Theory]
    [InlineData("{\"version\":2,\"secrets\":{}}")]
    [InlineData("{\"version\":1}")]
    [InlineData("{\"version\":1,\"secrets\":{\"name\":\"\"}}")]
    [InlineData("{\"version\":1,\"secrets\":{\" \":\"value\"}}")]
    [InlineData("{\"version\":1,\"secrets\":{\"name\":5}}")]
    [InlineData("null")]
    public async Task AFileOfAnotherShapeIsAnErrorAndIsLeftAsItIs(string content)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
        await File.WriteAllTextAsync(StorePath, content, Token).ConfigureAwait(true);
        var store = new OwnerOnlyFileSecretStore(StorePath);

        _ = await Should.ThrowAsync<SecretStoreException>(store.SetAsync("other", new SecretString(Canary), Token)).ConfigureAwait(true);

        (await File.ReadAllTextAsync(StorePath, Token).ConfigureAwait(true)).ShouldBe(content);
    }

    [Fact]
    public async Task NoMessageOrRenderingContainsAStoredValueOrTheStoredContents()
    {
        var store = new OwnerOnlyFileSecretStore(StorePath);
        var secret = new SecretString(Canary);
        await store.SetAsync("OMPOS_PROVIDER_ACME_KEY", secret, Token).ConfigureAwait(true);
        var renderings = new List<string> { store.ToString(), secret.ToString(), $"{secret}" };

        // Damage the file after the value is in it: the corruption message must not quote the file.
        await File.WriteAllTextAsync(StorePath, "{\"version\":1,\"secrets\":{\"x\":\"" + Canary + "\",", Token).ConfigureAwait(true);
        var corrupt = await Should.ThrowAsync<SecretStoreException>(store.GetAsync("x", Token)).ConfigureAwait(true);
        renderings.Add(corrupt.Message);
        renderings.Add(corrupt.ToString());
        renderings.Add(string.Create(CultureInfo.InvariantCulture, $"{corrupt.InnerException?.Message}"));
        var invalid = await Should.ThrowAsync<ArgumentException>(store.SetAsync(" ", secret, Token)).ConfigureAwait(true);
        renderings.Add(invalid.Message);
        renderings.Add(invalid.ToString());

        renderings.ShouldAllBe(text => !text.Contains(Canary, StringComparison.Ordinal));
        store.ToString().ShouldNotContain(_root, Case.Sensitive);
    }

    [Fact]
    public void InvalidArgumentsFaultThroughTheReturnedTaskNeverSynchronously()
    {
        var store = new OwnerOnlyFileSecretStore(StorePath);

        var setNull = store.SetAsync(null!, new SecretString("v"), Token);
        var setBlank = store.SetAsync(" ", new SecretString("v"), Token);
        var setNoSecret = store.SetAsync("name", null!, Token);
        var get = store.GetAsync(string.Empty, Token);
        var remove = store.RemoveAsync(null!, Token);

        setNull.IsFaulted.ShouldBeTrue();
        setNull.Exception!.InnerException.ShouldBeOfType<ArgumentNullException>();
        setBlank.Exception!.InnerException.ShouldBeOfType<ArgumentException>();
        setNoSecret.Exception!.InnerException.ShouldBeOfType<ArgumentNullException>();
        get.Exception!.InnerException.ShouldBeOfType<ArgumentException>();
        remove.Exception!.InnerException.ShouldBeOfType<ArgumentNullException>();
        File.Exists(StorePath).ShouldBeFalse();
    }

    [Fact]
    public async Task ReplaceReadRemoveAndListBehaveLikeTheSessionStore()
    {
        var store = new OwnerOnlyFileSecretStore(StorePath);
        (await store.GetAsync("missing", Token).ConfigureAwait(true)).ShouldBeNull();
        (await store.RemoveAsync("missing", Token).ConfigureAwait(true)).ShouldBeFalse();
        (await store.ListNamesAsync(Token).ConfigureAwait(true)).ShouldBeEmpty();
        await store.SetAsync("b", new SecretString("1"), Token).ConfigureAwait(true);
        await store.SetAsync("B", new SecretString("2"), Token).ConfigureAwait(true);
        await store.SetAsync("a", new SecretString("3"), Token).ConfigureAwait(true);

        await store.SetAsync("a", new SecretString("4"), Token).ConfigureAwait(true);

        (await store.ListNamesAsync(Token).ConfigureAwait(true)).ShouldBe(["B", "a", "b"]);
        (await store.GetAsync("a", Token).ConfigureAwait(true))!.Reveal().ShouldBe("4");
        (await store.RemoveAsync("a", Token).ConfigureAwait(true)).ShouldBeTrue();
        (await store.GetAsync("a", Token).ConfigureAwait(true)).ShouldBeNull();
    }

    [Fact]
    public async Task ConcurrentWritersAreSerialisedAndNoChangeIsLost()
    {
        var store = new OwnerOnlyFileSecretStore(StorePath);

        var writes = Enumerable.Range(0, 24)
            .Select(index => store.SetAsync("name" + index.ToString(CultureInfo.InvariantCulture), new SecretString("value" + index.ToString(CultureInfo.InvariantCulture)), Token));
        await Task.WhenAll(writes).ConfigureAwait(true);

        (await store.ListNamesAsync(Token).ConfigureAwait(true)).Count.ShouldBe(24);
        (await store.GetAsync("name17", Token).ConfigureAwait(true))!.Reveal().ShouldBe("value17");
        Directory.GetFiles(Path.GetDirectoryName(StorePath)!).ShouldBe([StorePath]);
    }

    [Fact]
    public async Task ASecretWithQuotesAndNonAsciiTextRoundTrips()
    {
        var store = new OwnerOnlyFileSecretStore(StorePath);
        const string Value = "k\"ey\\with 'quotes' and \u00fcn\u00efcode \u2603";
        await store.SetAsync("name", new SecretString(Value), Token).ConfigureAwait(true);

        var read = await new OwnerOnlyFileSecretStore(StorePath).GetAsync("name", Token).ConfigureAwait(true);

        read!.Reveal().ShouldBe(Value);
        Encoding.UTF8.GetString(await File.ReadAllBytesAsync(StorePath, Token).ConfigureAwait(true)).ShouldContain("name", Case.Sensitive);
    }

    private static void SkipWithoutUnixModes()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
        }
    }
}
