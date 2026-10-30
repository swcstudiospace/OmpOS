using Shouldly;
using Xunit;

namespace Autonogrammer.Workspace.Tests;

/// <summary>
/// Verifies the file-backed consent store: approvals persist, are keyed by repository and hash, and a damaged or
/// unknown-version file means no consent without throwing and is moved aside.
/// </summary>
public sealed class FileSetupConsentStoreTests : IDisposable
{
    private const string Repository = "/work/repo-a";
    private const string HashOne = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string HashTwo = "2222222222222222222222222222222222222222222222222222222222222222";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "autonogrammer-consent-" + Guid.NewGuid().ToString("N"));

    private string ConsentPath => Path.Combine(_directory, "config", "setup-consent.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task ApprovalSurvivesANewStoreOverTheSameFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var key = new SetupConsentKey(Repository, HashOne);
        using (var writer = new FileSetupConsentStore(ConsentPath))
        {
            (await writer.IsGrantedAsync(key, ct).ConfigureAwait(true)).ShouldBeFalse();
            await writer.RecordGrantAsync(key, ct).ConfigureAwait(true);
        }

        using var reader = new FileSetupConsentStore(ConsentPath);

        (await reader.IsGrantedAsync(key, ct).ConfigureAwait(true)).ShouldBeTrue();
    }

    [Fact]
    public async Task ApprovalDoesNotCoverAnotherHashOrAnotherRepository()
    {
        var ct = TestContext.Current.CancellationToken;
        using var store = new FileSetupConsentStore(ConsentPath);
        await store.RecordGrantAsync(new SetupConsentKey(Repository, HashOne), ct).ConfigureAwait(true);

        (await store.IsGrantedAsync(new SetupConsentKey(Repository, HashTwo), ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.IsGrantedAsync(new SetupConsentKey("/work/repo-b", HashOne), ct).ConfigureAwait(true)).ShouldBeFalse();
    }

    [Fact]
    public async Task ApprovingANewHashReplacesTheEarlierApprovalOfTheSameRepository()
    {
        var ct = TestContext.Current.CancellationToken;
        using var store = new FileSetupConsentStore(ConsentPath);
        await store.RecordGrantAsync(new SetupConsentKey(Repository, HashOne), ct).ConfigureAwait(true);
        await store.RecordGrantAsync(new SetupConsentKey(Repository, HashTwo), ct).ConfigureAwait(true);

        (await store.IsGrantedAsync(new SetupConsentKey(Repository, HashOne), ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.IsGrantedAsync(new SetupConsentKey(Repository, HashTwo), ct).ConfigureAwait(true)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ this is not json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{ "version": 1, "grants": null }""")]
    [InlineData("""{ "version": 1, "grants": [], "surprise": true }""")]
    [InlineData("""{ "version": 2, "grants": [ { "repository": "/work/repo-a", "setupSha256": "1111111111111111111111111111111111111111111111111111111111111111", "grantedAt": "2026-10-26T00:00:00+00:00" } ] }""")]
    public async Task DamagedOrUnknownVersionFileIsNoConsentIsQuarantinedAndNeverThrows(string content)
    {
        var ct = TestContext.Current.CancellationToken;
        var key = new SetupConsentKey(Repository, HashOne);
        Directory.CreateDirectory(Path.GetDirectoryName(ConsentPath)!);
        await File.WriteAllTextAsync(ConsentPath, content, ct).ConfigureAwait(true);
        using var store = new FileSetupConsentStore(ConsentPath);

        var granted = await store.IsGrantedAsync(key, ct).ConfigureAwait(true);

        granted.ShouldBeFalse();
        File.Exists(ConsentPath).ShouldBeFalse();
        var quarantined = Directory.GetFiles(Path.GetDirectoryName(ConsentPath)!, "setup-consent.json.quarantined-*").ShouldHaveSingleItem();
        (await File.ReadAllTextAsync(quarantined, ct).ConfigureAwait(true)).ShouldBe(content);

        await store.RecordGrantAsync(key, ct).ConfigureAwait(true);

        (await store.IsGrantedAsync(key, ct).ConfigureAwait(true)).ShouldBeTrue();
    }

    [Fact]
    public async Task SavedFileIsVersionedAndLeavesNoTemporaryFileBehind()
    {
        var ct = TestContext.Current.CancellationToken;
        using var store = new FileSetupConsentStore(ConsentPath);

        await store.RecordGrantAsync(new SetupConsentKey(Repository, HashOne), ct).ConfigureAwait(true);

        var text = await File.ReadAllTextAsync(ConsentPath, ct).ConfigureAwait(true);
        text.ShouldContain("\"version\": 1");
        text.ShouldContain(HashOne);
        Directory.GetFiles(Path.GetDirectoryName(ConsentPath)!).ShouldHaveSingleItem().ShouldBe(ConsentPath);
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(ConsentPath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public void DefaultPathIsUnderTheUsersAutonogrammerConfigurationDirectory()
    {
        var path = FileSetupConsentStore.DefaultPath();

        Path.GetFileName(path).ShouldBe("setup-consent.json");
        Path.GetFileName(Path.GetDirectoryName(path)).ShouldBe("Autonogrammer");
        Path.IsPathRooted(path).ShouldBeTrue();
    }
}
