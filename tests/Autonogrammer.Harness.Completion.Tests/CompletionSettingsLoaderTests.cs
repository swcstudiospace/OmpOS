using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Completion.Tests;

/// <summary>
/// Verifies how completion settings are read from the environment and a JSON file: nothing configured is off, a valid
/// endpoint turns completion on, and an invalid one fails loudly.
/// </summary>
public sealed class CompletionSettingsLoaderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("autonogrammer-completion-").FullName;

    /// <summary>ADE-13: with no environment and no file, the loader returns settings with completion off.</summary>
    [Fact]
    public void NoConfigurationLoadsNone()
    {
        var loader = new CompletionSettingsLoader(Variables());

        var settings = loader.Load();

        settings.ShouldBeSameAs(CompletionSettings.None);
        settings.IsConfigured.ShouldBeFalse();
    }

    /// <summary>ADE-13: a white space variable counts as not configured.</summary>
    [Fact]
    public void BlankVariablesLoadNone()
    {
        var loader = new CompletionSettingsLoader(Variables(
            (CompletionSettingsLoader.EndpointVariableName, "  "),
            (CompletionSettingsLoader.FileVariableName, string.Empty)));

        loader.Load().IsConfigured.ShouldBeFalse();
    }

    /// <summary>ADE-13: an endpoint in the environment turns completion on for that endpoint.</summary>
    [Fact]
    public void AConfiguredEndpointLoadsForEndpoint()
    {
        var loader = new CompletionSettingsLoader(Variables(
            (CompletionSettingsLoader.EndpointVariableName, "https://provider.example/v1/complete")));

        var settings = loader.Load();

        settings.IsConfigured.ShouldBeTrue();
        settings.Endpoint.ShouldBe(new Uri("https://provider.example/v1/complete"));
    }

    /// <summary>ADE-13: an endpoint in the named JSON file turns completion on for that endpoint.</summary>
    [Fact]
    public void AnEndpointInTheNamedFileLoadsForEndpoint()
    {
        var path = WriteFile("completion.json", """{"endpoint":"http://127.0.0.1:9000/complete","ignored":true}""");
        var loader = new CompletionSettingsLoader(Variables((CompletionSettingsLoader.FileVariableName, path)));

        var settings = loader.Load();

        settings.Endpoint.ShouldBe(new Uri("http://127.0.0.1:9000/complete"));
    }

    /// <summary>The environment endpoint wins over the file.</summary>
    [Fact]
    public void TheEnvironmentEndpointWinsOverTheFile()
    {
        var path = WriteFile("completion.json", """{"endpoint":"http://file.example/complete"}""");
        var loader = new CompletionSettingsLoader(Variables(
            (CompletionSettingsLoader.EndpointVariableName, "http://env.example/complete"),
            (CompletionSettingsLoader.FileVariableName, path)));

        loader.Load().Endpoint.ShouldBe(new Uri("http://env.example/complete"));
    }

    /// <summary>A file that sets no endpoint configures nothing.</summary>
    [Fact]
    public void AFileWithoutAnEndpointLoadsNone()
    {
        var path = WriteFile("completion.json", "{}");

        CompletionSettingsLoader.LoadFile(path).IsConfigured.ShouldBeFalse();
    }

    /// <summary>ADE-13: an invalid endpoint in the environment fails loudly, names the variable, and does not repeat the value.</summary>
    [Fact]
    public void InvalidEndpointFailsLoudly()
    {
        var loader = new CompletionSettingsLoader(Variables(
            (CompletionSettingsLoader.EndpointVariableName, "ftp://user:secret-key@provider.example/complete")));

        var failure = Should.Throw<InvalidDataException>(() => loader.Load());

        failure.Message.ShouldContain(CompletionSettingsLoader.EndpointVariableName);
        failure.Message.ShouldNotContain("secret-key");
    }

    /// <summary>ADE-13: an invalid endpoint in a file fails loudly and names the file and the field.</summary>
    [Fact]
    public void InvalidEndpointInAFileNamesTheFileAndField()
    {
        var path = WriteFile("completion.json", """{"endpoint":"not a url"}""");

        var failure = Should.Throw<InvalidDataException>(() => CompletionSettingsLoader.LoadFile(path));

        failure.Message.ShouldContain(path);
        failure.Message.ShouldContain(CompletionSettingsLoader.EndpointFieldName);
        failure.Message.ShouldNotContain("not a url");
    }

    /// <summary>A relative endpoint is invalid.</summary>
    [Fact]
    public void ARelativeEndpointFailsLoudly()
    {
        var loader = new CompletionSettingsLoader(Variables(
            (CompletionSettingsLoader.EndpointVariableName, "/v1/complete")));

        Should.Throw<InvalidDataException>(() => loader.Load());
    }

    /// <summary>A named file that does not exist fails loudly with its path, instead of silently turning completion off.</summary>
    [Fact]
    public void AMissingNamedFileFailsLoudly()
    {
        var path = Path.Combine(_directory, "missing.json");
        var loader = new CompletionSettingsLoader(Variables((CompletionSettingsLoader.FileVariableName, path)));

        var failure = Should.Throw<InvalidDataException>(() => loader.Load());

        failure.Message.ShouldContain(path);
    }

    /// <summary>A file that is not JSON fails loudly with its path.</summary>
    [Fact]
    public void AFileThatIsNotJsonFailsLoudly()
    {
        var path = WriteFile("completion.json", "endpoint = https://provider.example/complete");

        var failure = Should.Throw<InvalidDataException>(() => CompletionSettingsLoader.LoadFile(path));

        failure.Message.ShouldContain(path);
    }

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static Func<string, string?> Variables(params (string Name, string Value)[] variables) =>
        name => variables.Where(variable => string.Equals(variable.Name, name, StringComparison.Ordinal))
            .Select(static variable => variable.Value)
            .FirstOrDefault();

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }
}
