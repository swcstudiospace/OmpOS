using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests;

/// <summary>Account folder keys never expose or share a subject.</summary>
public sealed class CoworkAccountFolderTests
{
    [Fact]
    public void TwoSubjectsNeverShareAFileAndTheFolderNameOmitsTheSubject()
    {
        const string first = "account-sub-alpha";
        const string second = "account-sub-beta";
        var root = Path.Combine(Path.GetTempPath(), "cowork-folder-" + Guid.NewGuid().ToString("N"));
        try
        {
            var left = CoworkAccountFolder.KeyFromSubject(first);
            var right = CoworkAccountFolder.KeyFromSubject(second);
            string.Equals(left, right, StringComparison.Ordinal).ShouldBeFalse();
            left.Length.ShouldBe(CoworkAccountFolder.KeyLength);
            right.Length.ShouldBe(CoworkAccountFolder.KeyLength);
            left.ShouldNotContain(first);
            right.ShouldNotContain(second);
            left.Any(char.IsUpper).ShouldBeFalse();

            var leftDirectory = CoworkAccountFolder.AccountDirectory(root, left);
            var rightDirectory = CoworkAccountFolder.AccountDirectory(root, right);
            string.Equals(leftDirectory, rightDirectory, StringComparison.Ordinal).ShouldBeFalse();
            Path.GetFileName(leftDirectory).ShouldBe(left);
            leftDirectory.ShouldNotContain(first);
            rightDirectory.ShouldNotContain(second);

            Directory.CreateDirectory(leftDirectory);
            Directory.CreateDirectory(rightDirectory);
            File.WriteAllText(Path.Combine(leftDirectory, "sessions.json"), "left");
            File.WriteAllText(Path.Combine(rightDirectory, "sessions.json"), "right");
            File.ReadAllText(Path.Combine(leftDirectory, "sessions.json")).ShouldBe("left");
            File.ReadAllText(Path.Combine(rightDirectory, "sessions.json")).ShouldBe("right");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
