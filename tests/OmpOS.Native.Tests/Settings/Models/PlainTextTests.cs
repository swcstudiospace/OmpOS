using OmpOS.Native.Settings.Models;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings.Models;

/// <summary>Text from omp reaches the page without file system paths, and without damage to anything that only looks like one.</summary>
public sealed class PlainTextTests
{
    [Theory]
    [InlineData("The omp executable could not be started: An error occurred trying to start process '/usr/local/bin/omp' with working directory '/tmp'.", "The omp executable could not be started: An error occurred trying to start process '<path>' with working directory '<path>'.")]
    [InlineData("models.yml at /home/me/.omp/agent/models.yml is invalid.", "models.yml at <path> is invalid.")]
    [InlineData("could not read (/var/x/models.yml).", "could not read (<path>).")]
    [InlineData("Failed at /omp", "Failed at <path>")]
    public void AnAbsolutePathIsReplaced(string text, string expected) =>
        PlainText.WithoutPaths(text).ShouldBe(expected);

    [Theory]
    [InlineData("See https://docs.example.com/guide/start for help.")]
    [InlineData("Use provider/model-id or openai/gpt-4o.")]
    [InlineData("Choose Authorization: Bearer or x / y.")]
    [InlineData("Install from curl -fsSL https://bun.sh/install | bash")]
    [InlineData("")]
    public void AddressesAndSelectorsAreLeftAlone(string text) =>
        PlainText.WithoutPaths(text).ShouldBe(text);
}
