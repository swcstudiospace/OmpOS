using Autonogrammer.Harness.AgentLoop.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Hooks;

/// <summary>
/// A host state file that is not valid JSON is set aside as <c>.corrupt</c> and the store starts empty, while a file that exists
/// but cannot be read fails the open and is left exactly as it was, so no later write can replace its contents.
/// </summary>
public sealed class HooksHostStoreDamagedFileTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AFileThatIsNotJsonIsSetAsideAndTheStoreStartsEmpty()
    {
        var root = Directory.CreateTempSubdirectory("hooks-host-damaged-").FullName;
        try
        {
            var path = Path.Combine(root, "hooks-host.json");
            await File.WriteAllTextAsync(path, "{ not json", Token);

            using var store = await HooksHostStore.OpenAsync(path, Token).ConfigureAwait(true);

            store.RunHooks.ShouldBeTrue();
            File.Exists(path).ShouldBeFalse();
            (await File.ReadAllTextAsync(path + ".corrupt", Token).ConfigureAwait(true)).ShouldBe("{ not json");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AFileThatCannotBeReadFailsTheOpenAndIsLeftUntouched()
    {
        var root = Directory.CreateTempSubdirectory("hooks-host-locked-").FullName;
        try
        {
            var path = Path.Combine(root, "hooks-host.json");
            await File.WriteAllTextAsync(path, "{}", Token);

            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                await Should.ThrowAsync<IOException>(() => HooksHostStore.OpenAsync(path, Token));
            }

            File.Exists(path + ".corrupt").ShouldBeFalse();
            (await File.ReadAllTextAsync(path, Token)).ShouldBe("{}");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
