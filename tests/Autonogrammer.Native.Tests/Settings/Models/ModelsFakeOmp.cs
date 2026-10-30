using System.Globalization;
using System.Text;
using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Harness.Secrets;
using Autonogrammer.Native.Settings.Models;

namespace Autonogrammer.Native.Tests.Settings.Models;

/// <summary>
/// A temporary home, agent directory and state directory plus a POSIX shell script that stands in for the omp executable. The fake
/// records, for each call, its arguments, the child's <c>PI_CODING_AGENT_DIR</c> and <c>HOME</c>, the NAMES of the provider key
/// variables it was started with (never their values) and its standard input, and answers from canned files: the model listing, the
/// exit code of <c>omp token &lt;provider&gt;</c>, the <c>modelRoles</c> setting. <c>omp acp</c> answers the ACP handshake and records
/// which provider key variables were set. Calls are recorded under unique names, so the parallel <c>omp token</c> questions of one
/// load cannot overwrite each other. No omp, model or network is involved. The tree is deleted on disposal.
/// </summary>
internal sealed class ModelsFakeOmp : IDisposable
{
    public const string ValidationWarningLine = "Warning: models.yml validation failed — custom providers disabled";
    public const string ValidationErrorLine = "Failed to load config file models, Validate(models) error: Provider bad: nope";

    private const UnixFileMode OwnerExecutable = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const string Script =
        """
        #!/bin/sh
        D="__DIR__"
        if [ "$1" = "version" ]; then echo "omp 18.12.6-fake"; exit 0; fi
        mkdir -p "$D/calls"
        id="$(date +%s%N)-$$"
        for a in "$@"; do printf '%s\n' "$a"; done > "$D/calls/$id.argv"
        env | grep -E '^(PI_CODING_AGENT_DIR|HOME)=' | sort > "$D/calls/$id.env"
        env | grep -E '^AUTONOGRAMMER_PROVIDER_[A-Z0-9_]*_KEY=.' | cut -d= -f1 | sort > "$D/calls/$id.keys"
        case "$1" in
          models)
            if [ -f "$D/models.hang" ]; then exec sleep 30; fi
            if [ -f "$D/models.stderr" ]; then cat "$D/models.stderr" >&2; fi
            if grep -q BADMODEL "$PI_CODING_AGENT_DIR/models.yml" 2>/dev/null; then
              echo "__WARNING__" >&2
              echo "__ERROR__" >&2
            fi
            if [ -f "$D/models.json" ]; then cat "$D/models.json"; else echo '{"models":[]}'; fi
            if [ -f "$D/models.exit" ]; then exit "$(cat "$D/models.exit")"; fi
            exit 0 ;;
          token)
            if [ -f "$D/token.$2" ]; then code=$(cat "$D/token.$2"); else code=1; fi
            if [ "$code" = 0 ]; then echo "sk-token-output-must-be-discarded"; fi
            exit "$code" ;;
          auth)
            cat > "$D/calls/$id.stdin"
            if [ -f "$D/auth.exit" ] && [ "$(cat "$D/auth.exit")" != 0 ]; then echo "AuthTransferError" >&2; exit "$(cat "$D/auth.exit")"; fi
            provider=$(sed -n 's/.*"provider":"\([^"]*\)".*/\1/p' "$D/calls/$id.stdin")
            if [ ! -f "$D/auth.nostore" ]; then echo 0 > "$D/token.$provider"; fi
            exit 0 ;;
          config)
            if [ "$2" = get ]; then
              if [ -f "$D/roles.json" ]; then cat "$D/roles.json"; else echo '{}'; fi
              exit 0
            fi
            if [ "$2" = set ]; then printf '%s' "$4" > "$D/roles.json"; exit 0; fi ;;
          acp)
            echo $$ > "$D/acp.pid"
            cp "$D/calls/$id.keys" "$D/acp.keys.tmp"
            mv "$D/acp.keys.tmp" "$D/acp.keys"
            while IFS= read -r line; do
              rid=${line#*\"id\":}
              rid=${rid%%,*}
              case "$line" in
                *'"method":"initialize"'*) printf '{"jsonrpc":"2.0","id":%s,"result":{"protocolVersion":1,"agentCapabilities":{"loadSession":true}}}\n' "$rid" ;;
                *'"method":"session/new"'*) printf '{"jsonrpc":"2.0","id":%s,"result":{"sessionId":"native-keys"}}\n' "$rid" ;;
                *'"method":"session/close"'*) printf '{"jsonrpc":"2.0","id":%s,"result":{}}\n' "$rid" ;;
              esac
            done
            exec sleep 600 ;;
        esac
        exit 2
        """;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly StubProbeHandler _probe = new();

    public ModelsFakeOmp()
    {
        Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "autonogrammer-models-" + Guid.NewGuid().ToString("N")));
        Home = Path.Combine(Root, "home");
        AgentDirectory = Path.Combine(Home, ".omp", "agent");
        FakeDirectory = Path.Combine(Root, "fake");
        StateRoot = Path.Combine(Root, "state");
        _ = Directory.CreateDirectory(AgentDirectory);
        _ = Directory.CreateDirectory(FakeDirectory);
        _ = Directory.CreateDirectory(StateRoot);
        ExecutablePath = Path.Combine(FakeDirectory, "omp");
        var text = Script
            .Replace("__DIR__", FakeDirectory, StringComparison.Ordinal)
            .Replace("__WARNING__", ValidationWarningLine, StringComparison.Ordinal)
            .Replace("__ERROR__", ValidationErrorLine, StringComparison.Ordinal)
            .ReplaceLineEndings("\n");
        File.WriteAllText(ExecutablePath, text, Utf8);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(ExecutablePath, OwnerExecutable);
        }
    }

    /// <summary>Gets the connection-test handler the composition uses unless a test passes its own.</summary>
    public StubProbeHandler Probe => _probe;

    /// <summary>Gets the root of the temporary tree.</summary>
    public string Root { get; }

    /// <summary>Gets the home directory given to the fake as <c>HOME</c>.</summary>
    public string Home { get; }

    /// <summary>Gets the agent directory the fake is told about.</summary>
    public string AgentDirectory { get; }

    /// <summary>Gets the directory of the fake script and its canned files.</summary>
    public string FakeDirectory { get; }

    /// <summary>Gets the Autonogrammer state directory the composition keeps its files in.</summary>
    public string StateRoot { get; }

    /// <summary>Gets the absolute path of the fake omp executable.</summary>
    public string ExecutablePath { get; }

    /// <summary>Gets the locator that finds the fake.</summary>
    public OmpLocatorOptions Locator => new(ExecutablePath, [], []);

    /// <summary>Gets the path of <c>models.yml</c> in the agent directory.</summary>
    public string ModelsYmlPath => Path.Combine(AgentDirectory, "models.yml");

    /// <summary>Gets the path of the Autonogrammer provider key store.</summary>
    public string SecretsPath => Path.Combine(StateRoot, ModelsSettingsComposition.SecretsDirectoryName, ModelsSettingsComposition.SecretsFileName);

    /// <summary>Gets the path of the registry of custom provider details.</summary>
    public string DetailsPath => Path.Combine(StateRoot, ModelsSettingsComposition.DetailsFileName);

    /// <summary>Builds a model row of <c>omp models --json</c>.</summary>
    /// <param name="provider">The provider id.</param>
    /// <param name="model">The model id.</param>
    /// <returns>The JSON object text.</returns>
    public static string Row(string provider, string model) =>
        $"{{\"provider\":\"{provider}\",\"kind\":\"chat\",\"id\":\"{model}\",\"selector\":\"{provider}/{model}\",\"name\":\"{model}\",\"contextWindow\":1000,\"maxTokens\":100,\"reasoning\":false,\"input\":[\"text\"]}}";

    /// <summary>Builds the composition over this fake.</summary>
    /// <param name="probe">The handler of the connection test, or null for one that refuses every request.</param>
    /// <param name="secrets">The key store, or null for the owner-only file in the state directory.</param>
    /// <param name="locator">How omp is located, or null for the fake.</param>
    /// <returns>The composition. The caller disposes it.</returns>
    public ModelsSettingsComposition Compose(HttpMessageHandler? probe = null, ISecretStore? secrets = null, OmpLocatorOptions? locator = null) =>
        ModelsSettingsComposition.Create(new ModelsSettingsCompositionOptions
        {
            StateRoot = StateRoot,
            OmpLocator = locator ?? Locator,
            AgentDirectory = AgentDirectory,
            Home = Home,
            ProbeHandler = probe ?? _probe,
            SecretStore = secrets,
        });

    /// <summary>Sets the standard output of <c>omp models --json</c>.</summary>
    /// <param name="json">The text to print.</param>
    public void SetModelsJson(string json) => File.WriteAllText(Path.Combine(FakeDirectory, "models.json"), json, Utf8);

    /// <summary>Sets the models of the listing from rows.</summary>
    /// <param name="rows">The rows built with <see cref="Row"/>.</param>
    public void SetModels(params string[] rows) => SetModelsJson("{\"models\":[" + string.Join(',', rows) + "]}");

    /// <summary>Sets what <c>omp models</c> writes to standard error.</summary>
    /// <param name="text">The text to print.</param>
    public void SetModelsStandardError(string text) => File.WriteAllText(Path.Combine(FakeDirectory, "models.stderr"), text, Utf8);

    /// <summary>Sets the exit code of <c>omp token &lt;provider&gt;</c>.</summary>
    /// <param name="provider">The provider id.</param>
    /// <param name="exitCode">The exit code.</param>
    public void SetTokenExit(string provider, int exitCode) =>
        File.WriteAllText(Path.Combine(FakeDirectory, "token." + provider), exitCode.ToString(CultureInfo.InvariantCulture), Utf8);

    /// <summary>Sets the exit code of <c>omp auth import -</c>; non-zero makes it refuse the import.</summary>
    /// <param name="exitCode">The exit code.</param>
    public void SetAuthExit(int exitCode) =>
        File.WriteAllText(Path.Combine(FakeDirectory, "auth.exit"), exitCode.ToString(CultureInfo.InvariantCulture), Utf8);

    /// <summary>Makes <c>omp auth import -</c> succeed without making the provider resolvable afterwards.</summary>
    public void SetAuthStoresNothing() => File.WriteAllText(Path.Combine(FakeDirectory, "auth.nostore"), "1", Utf8);

    /// <summary>Sets the stored <c>modelRoles</c> JSON.</summary>
    /// <param name="json">The JSON object.</param>
    public void SetRoles(string json) => File.WriteAllText(Path.Combine(FakeDirectory, "roles.json"), json, Utf8);

    /// <summary>Reads the stored <c>modelRoles</c> JSON, or null when nothing was stored.</summary>
    /// <returns>The JSON text.</returns>
    public string? ReadRoles()
    {
        var path = Path.Combine(FakeDirectory, "roles.json");
        return File.Exists(path) ? File.ReadAllText(path, Utf8) : null;
    }

    /// <summary>Writes <c>models.yml</c> as UTF-8 without a byte order mark.</summary>
    /// <param name="text">The content.</param>
    public void WriteModelsYml(string text) => File.WriteAllText(ModelsYmlPath, text, Utf8);

    /// <summary>Reads <c>models.yml</c>, or null when it does not exist.</summary>
    /// <returns>The text.</returns>
    public string? ReadModelsYml() => File.Exists(ModelsYmlPath) ? File.ReadAllText(ModelsYmlPath, Utf8) : null;

    /// <summary>Reads the names of the provider key variables the last <c>omp acp</c> started with, or null before one started.</summary>
    /// <returns>The names, in ordinal order.</returns>
    public IReadOnlyList<string>? AcpKeyNames()
    {
        var path = Path.Combine(FakeDirectory, "acp.keys");
        return File.Exists(path) ? File.ReadAllLines(path) : null;
    }

    /// <summary>Reads the process id of the last <c>omp acp</c> the fake started: the shell, which stays the same process when it ends its protocol loop and sleeps. It is written before the key names, so it is known once <see cref="AcpKeyNames"/> is not null.</summary>
    /// <returns>The process id, or null before one started.</returns>
    public int? AcpProcessId()
    {
        var path = Path.Combine(FakeDirectory, "acp.pid");
        return File.Exists(path) && int.TryParse(File.ReadAllText(path).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    /// <summary>Reads every recorded call, in the order they began.</summary>
    /// <returns>The calls.</returns>
    public List<ModelsFakeCall> Calls()
    {
        var directory = Path.Combine(FakeDirectory, "calls");
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var stems = Directory.GetFiles(directory, "*.argv")
            .Select(static path => path[..^".argv".Length])
            .Order(StringComparer.Ordinal)
            .ToArray();
        var calls = new List<ModelsFakeCall>(stems.Length);
        foreach (var stem in stems)
        {
            var stdinPath = stem + ".stdin";
            calls.Add(new ModelsFakeCall(
                File.ReadAllLines(stem + ".argv"),
                File.Exists(stem + ".env") ? File.ReadAllLines(stem + ".env") : [],
                File.Exists(stem + ".keys") ? File.ReadAllLines(stem + ".keys") : [],
                File.Exists(stdinPath) ? File.ReadAllText(stdinPath, Utf8) : null));
        }

        return calls;
    }

    /// <summary>
    /// Reads every file under the tree as text, so a test can scan for a secret. The recorded standard input of an <c>omp auth import</c>
    /// call (<c>*.stdin</c>) is skipped: that is where the key goes by design, and the tests that import one assert it. So is the fake's own script.
    /// </summary>
    /// <param name="exceptions">Paths that are allowed to hold the secret, which are skipped.</param>
    /// <returns>Path to content.</returns>
    public Dictionary<string, string> ReadEverything(params string[] exceptions) =>
        Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .Where(path => !exceptions.Contains(path, StringComparer.Ordinal))
            .Where(static path => !path.EndsWith(".stdin", StringComparison.Ordinal))
            .Where(static path => !path.EndsWith("/fake/omp", StringComparison.Ordinal))
            .ToDictionary(static path => path, static path => File.ReadAllText(path, Utf8), StringComparer.Ordinal);

    /// <inheritdoc />
    public void Dispose()
    {
        _probe.Dispose();
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
