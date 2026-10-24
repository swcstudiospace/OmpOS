using System.Runtime.Versioning;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Herdr.Tests;

public sealed class HerdrSessionStoreTests
{
    private static readonly CancellationToken NoCancellation = CancellationToken.None;

    private const UnixFileMode GroupAndOtherBits =
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    [Fact]
    public async Task OpeningAMissingFileStartsEmpty()
    {
        using var directory = new TempDirectory();
        using var store = await HerdrSessionStore.OpenAsync(Path.Combine(directory.Path, "sessions.json"), NoCancellation).ConfigureAwait(true);

        store.Sessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpsertPersistsEveryFieldAndReopeningReadsItBack()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "sessions.json");
        var alpha = new HerdrStoredSession(new HerdrSessionId("alpha"), "local-omp", new NativeSessionReference("omp-session-1"), HerdrSessionState.Paused, 7);
        var beta = new HerdrStoredSession(new HerdrSessionId("beta"), "remote-omp", null, HerdrSessionState.Lost, 2);

        using (var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true))
        {
            await store.UpsertAsync(beta, NoCancellation).ConfigureAwait(true);
            await store.UpsertAsync(alpha, NoCancellation).ConfigureAwait(true);
        }

        using var reopened = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        reopened.Sessions.ShouldBe(new[] { alpha, beta });
    }

    [Fact]
    public async Task ReplacingARecordLeavesOnlyTheStoreFileInItsDirectory()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "sessions.json");
        var id = new HerdrSessionId("alpha");
        using var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);

        await store.UpsertAsync(new HerdrStoredSession(id, "local-omp", null, HerdrSessionState.Running, 1), NoCancellation).ConfigureAwait(true);
        await store.UpsertAsync(new HerdrStoredSession(id, "local-omp", null, HerdrSessionState.Paused, 2), NoCancellation).ConfigureAwait(true);

        store.Find(id)!.LastKnownState.ShouldBe(HerdrSessionState.Paused);
        Directory.GetFiles(directory.Path).ShouldBe(new[] { path });
    }

    [Fact]
    public async Task AFailedReplaceLeavesTheStoreUnchangedAndNoTemporaryFile()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "sessions.json");
        using var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        Directory.CreateDirectory(path);

        var failure = await StoreFileFailure.CaptureAsync(async () =>
        {
            await store.UpsertAsync(new HerdrStoredSession(new HerdrSessionId("alpha"), "local-omp", null, HerdrSessionState.Running, 1), NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);

        failure.GetType().ShouldBe(OperatingSystem.IsWindows() ? typeof(UnauthorizedAccessException) : typeof(IOException));
        store.Sessions.ShouldBeEmpty();
        Directory.Exists(path).ShouldBeTrue();
        Directory.GetFileSystemEntries(directory.Path).ShouldBe(new[] { path });
    }

    [Fact]
    public async Task AStorePathThatIsADirectoryFailsToOpenInsteadOfReadingAsEmpty()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "sessions.json");
        Directory.CreateDirectory(path);

        var failure = await StoreFileFailure.CaptureAsync(async () =>
        {
            using var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);

        (failure is IOException or UnauthorizedAccessException).ShouldBeTrue();
        Directory.Exists(path).ShouldBeTrue();
    }

    [Fact]
    public async Task AnUnreadableFileIsRefusedAndNeverRewritten()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "sessions.json");
        var original = "{ this is not json"u8.ToArray();
        await File.WriteAllBytesAsync(path, original, NoCancellation).ConfigureAwait(true);

        await Should.ThrowAsync<JsonException>(async () =>
        {
            using var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);

        (await File.ReadAllBytesAsync(path, NoCancellation).ConfigureAwait(true)).ShouldBe(original);
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public async Task TheStoreFileHasNoGroupOrOtherPermissions()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "sessions.json");
        using var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);

        await store.UpsertAsync(new HerdrStoredSession(new HerdrSessionId("alpha"), "local-omp", new NativeSessionReference("omp-session-1"), HerdrSessionState.Running, 1), NoCancellation).ConfigureAwait(true);

        (File.GetUnixFileMode(path) & GroupAndOtherBits).ShouldBe(UnixFileMode.None);
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public async Task TheDirectoryTheStoreCreatesHasNoGroupOrOtherPermissions()
    {
        using var directory = new TempDirectory();
        var storeDirectory = Path.Combine(directory.Path, "state");
        var path = Path.Combine(storeDirectory, "sessions.json");
        using var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);

        await store.UpsertAsync(new HerdrStoredSession(new HerdrSessionId("alpha"), "local-omp", null, HerdrSessionState.Running, 1), NoCancellation).ConfigureAwait(true);

        (File.GetUnixFileMode(storeDirectory) & GroupAndOtherBits).ShouldBe(UnixFileMode.None);
    }

    [Theory]
    [InlineData("{\"version\":1,\"sessions\":[],\"surprise\":true}")]
    [InlineData("{\"version\":2,\"sessions\":[]}")]
    [InlineData("{\"version\":1,\"sessions\":null}")]
    [InlineData("{\"version\":1,\"sessions\":[null]}")]
    [InlineData("{\"version\":1,\"sessions\":[{\"sessionId\":\"alpha\",\"providerId\":\"local-omp\",\"nativeReference\":null,\"lastKnownState\":\"Bogus\",\"lastSequence\":1}]}")]
    [InlineData("{\"version\":1,\"sessions\":[{\"sessionId\":\"alpha\",\"providerId\":\"local-omp\",\"nativeReference\":null,\"lastKnownState\":\"Running\",\"lastSequence\":-1}]}")]
    [InlineData("{\"version\":1,\"sessions\":[{\"sessionId\":\"../escape\",\"providerId\":\"local-omp\",\"nativeReference\":null,\"lastKnownState\":\"Running\",\"lastSequence\":1}]}")]
    [InlineData("{\"version\":1,\"sessions\":[{\"sessionId\":\"alpha\",\"providerId\":\" \",\"nativeReference\":null,\"lastKnownState\":\"Running\",\"lastSequence\":1}]}")]
    [InlineData("{\"version\":1,\"sessions\":[{\"sessionId\":\"alpha\",\"providerId\":\"local-omp\",\"nativeReference\":null,\"lastKnownState\":\"Running\",\"lastSequence\":1},{\"sessionId\":\"alpha\",\"providerId\":\"local-omp\",\"nativeReference\":null,\"lastKnownState\":\"Running\",\"lastSequence\":2}]}")]
    public async Task AStoreThatIsNotAValidDocumentIsRefused(string json)
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "sessions.json");
        await File.WriteAllTextAsync(path, json, NoCancellation).ConfigureAwait(true);

        await Should.ThrowAsync<JsonException>(async () =>
        {
            using var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }
}
