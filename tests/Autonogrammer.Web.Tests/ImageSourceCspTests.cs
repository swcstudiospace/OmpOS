using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>
/// D-27: a data image needs no CSP widening. The four sources already carry <c>img-src 'self' data:</c> and nothing broader.
/// </summary>
public sealed class ImageSourceCspTests
{
    public static TheoryData<string> Sources => new()
    {
        "vercel.ts",
        "scripts/vercel/build-web.sh",
        "scripts/vercel/build-demo.sh",
        "scripts/web-demo/serve-demo.sh",
    };

    [Theory]
    [MemberData(nameof(Sources))]
    public void ImageSourceIsSelfAndDataOnly(string relativePath)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot, relativePath));
        var directive = ImgSrc(text);

        directive.ShouldBe("img-src 'self' data:");
        directive.ShouldNotContain("blob:");
        directive.ShouldNotContain("https:");
        directive.ShouldNotContain("*");
    }

    private static string ImgSrc(string text)
    {
        var start = text.IndexOf("img-src", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        var end = text.IndexOfAny(['\n', ';', '"'], start);
        return end < 0 ? text[start..] : text[start..end];
    }

    private static string RepoRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Autonogrammer.slnx")))
            {
                directory = directory.Parent;
            }

            directory.ShouldNotBeNull("Autonogrammer.slnx must be reachable by walking up from the test output");
            return directory.FullName;
        }
    }
}
