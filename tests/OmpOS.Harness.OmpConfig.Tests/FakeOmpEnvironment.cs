using System.Globalization;
using System.Text;
using OmpOS.Harness.OmpLauncher;

namespace OmpOS.Harness.OmpConfig.Tests;

/// <summary>
/// A temporary agent directory plus a POSIX shell script that stands in for the omp executable. The fake records the arguments, the
/// child's <c>PI_CODING_AGENT_DIR</c> and <c>HOME</c> and its standard input of every call into files, and answers from canned
/// files the test writes: the model listing and its standard error, the exit code of <c>omp token &lt;provider&gt;</c>, the
/// <c>modelRoles</c> setting. Its <c>models</c> command prints omp's <c>models.yml</c> validation warning when the file contains the
/// text <c>BADMODEL</c>. No omp, model or network is involved. The directory is deleted on disposal.
/// </summary>
internal sealed class FakeOmpEnvironment : IDisposable
{
    public const string ValidationWarningLine = "Warning: models.yml validation failed — custom providers disabled";
    public const string ValidationErrorLine = "Failed to load config file models, Validate(models) error: Provider bad: nope";

    private const string Script =
        """
        #!/bin/sh
        D="__DIR__"
        mkdir -p "$D/calls"
        n=$(ls "$D/calls" | grep -c '\.argv$')
        n=$((n+1))
        for a in "$@"; do printf '%s\n' "$a"; done > "$D/calls/$n.argv"
        env | grep -E '^(PI_CODING_AGENT_DIR|HOME)=' | sort > "$D/calls/$n.env"
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
            if [ -f "$D/token.$2.hang" ]; then exec sleep 30; fi
            if [ -f "$D/token.$2" ]; then code=$(cat "$D/token.$2"); else code=1; fi
            if [ "$code" = 0 ]; then echo "sk-token-output-must-be-discarded"; fi
            exit "$code" ;;
          auth)
            cat > "$D/calls/$n.stdin"
            if [ -f "$D/auth.exit" ] && [ "$(cat "$D/auth.exit")" != 0 ]; then echo "AuthTransferError" >&2; exit "$(cat "$D/auth.exit")"; fi
            provider=$(sed -n 's/.*"provider":"\([^"]*\)".*/\1/p' "$D/calls/$n.stdin")
            if [ ! -f "$D/auth.nostore" ]; then echo 0 > "$D/token.$provider"; fi
            exit 0 ;;
          stdin)
            cat > "$D/calls/$n.stdin"
            exit 0 ;;
          config)
            if [ "$2" = get ]; then
              if [ -f "$D/roles.json" ]; then cat "$D/roles.json"; else echo '{}'; fi
              exit 0
            fi
            if [ "$2" = set ]; then printf '%s' "$4" > "$D/roles.json"; exit 0; fi ;;
          hang)
            exec sleep 30 ;;
        esac
        exit 2
        """;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public FakeOmpEnvironment()
    {
        Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ompos-ompconfig-" + Guid.NewGuid().ToString("N")));
        Home = Path.Combine(Root, "home");
        AgentDirectory = Path.Combine(Home, ".omp", "agent");
        FakeDirectory = Path.Combine(Root, "fake");
        _ = Directory.CreateDirectory(AgentDirectory);
        _ = Directory.CreateDirectory(FakeDirectory);
        ExecutablePath = Path.Combine(FakeDirectory, "omp");
        var text = Script
            .Replace("__DIR__", FakeDirectory, StringComparison.Ordinal)
            .Replace("__WARNING__", ValidationWarningLine, StringComparison.Ordinal)
            .Replace("__ERROR__", ValidationErrorLine, StringComparison.Ordinal)
            .ReplaceLineEndings("\n");
        File.WriteAllText(ExecutablePath, text, Utf8);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(ExecutablePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>Gets the root of the temporary tree.</summary>
    public string Root { get; }

    /// <summary>Gets the home directory given to the fake as <c>HOME</c>.</summary>
    public string Home { get; }

    /// <summary>Gets the agent directory the fake is told about.</summary>
    public string AgentDirectory { get; }

    /// <summary>Gets the directory of the fake script and its canned files.</summary>
    public string FakeDirectory { get; }

    /// <summary>Gets the absolute path of the fake omp executable.</summary>
    public string ExecutablePath { get; }

    /// <summary>Gets the path of <c>models.yml</c> in the agent directory.</summary>
    public string ModelsYmlPath => Path.Combine(AgentDirectory, "models.yml");

    /// <summary>Builds options that run the fake, with this agent directory and home.</summary>
    /// <param name="clock">The clock, or null for the system clock.</param>
    /// <param name="timeout">The command timeout, or null for the default.</param>
    /// <param name="maxOutputBytes">The output limit, or null for the default.</param>
    /// <returns>The options.</returns>
    public OmpConfigOptions Options(TimeProvider? clock = null, TimeSpan? timeout = null, int? maxOutputBytes = null) =>
        new()
        {
            Locator = new OmpLocatorOptions(ExecutablePath, [], []),
            AgentDirectory = AgentDirectory,
            Home = Home,
            TimeProvider = clock,
            CommandTimeout = timeout ?? OmpConfigOptions.DefaultCommandTimeout,
            MaxOutputBytes = maxOutputBytes ?? OmpConfigOptions.DefaultMaxOutputBytes,
        };

    /// <summary>Builds options for a machine with no omp at all.</summary>
    /// <returns>The options.</returns>
    public OmpConfigOptions OptionsWithoutOmp() =>
        new()
        {
            Locator = new OmpLocatorOptions(null, [], []),
            AgentDirectory = AgentDirectory,
            Home = Home,
        };

    /// <summary>Sets the standard output of <c>omp models --json</c>.</summary>
    /// <param name="json">The text to print.</param>
    public void SetModelsJson(string json) => File.WriteAllText(Path.Combine(FakeDirectory, "models.json"), json, Utf8);

    /// <summary>Sets what <c>omp models</c> writes to standard error.</summary>
    /// <param name="text">The text to print.</param>
    public void SetModelsStandardError(string text) => File.WriteAllText(Path.Combine(FakeDirectory, "models.stderr"), text, Utf8);

    /// <summary>Sets the exit code of <c>omp models</c>.</summary>
    /// <param name="exitCode">The exit code.</param>
    public void SetModelsExit(int exitCode) =>
        File.WriteAllText(Path.Combine(FakeDirectory, "models.exit"), exitCode.ToString(CultureInfo.InvariantCulture), Utf8);

    /// <summary>Sets the exit code of <c>omp token &lt;provider&gt;</c>.</summary>
    /// <param name="provider">The provider id.</param>
    /// <param name="exitCode">The exit code.</param>
    public void SetTokenExit(string provider, int exitCode) =>
        File.WriteAllText(Path.Combine(FakeDirectory, "token." + provider), exitCode.ToString(CultureInfo.InvariantCulture), Utf8);

    /// <summary>Sets the exit code of <c>omp auth import -</c>; non-zero makes it refuse the import.</summary>
    /// <param name="exitCode">The exit code.</param>
    public void SetAuthExit(int exitCode) =>
        File.WriteAllText(Path.Combine(FakeDirectory, "auth.exit"), exitCode.ToString(CultureInfo.InvariantCulture), Utf8);

    /// <summary>Makes <c>omp models</c> hang until it is killed.</summary>
    public void SetModelsHangs() => File.WriteAllText(Path.Combine(FakeDirectory, "models.hang"), "1", Utf8);

    /// <summary>Makes <c>omp auth import -</c> succeed without making the provider resolvable afterwards.</summary>
    public void SetAuthStoresNothing() => File.WriteAllText(Path.Combine(FakeDirectory, "auth.nostore"), "1", Utf8);

    /// <summary>Makes <c>omp token &lt;provider&gt;</c> hang until it is killed.</summary>
    /// <param name="provider">The provider id.</param>
    public void SetTokenHangs(string provider) => File.WriteAllText(Path.Combine(FakeDirectory, "token." + provider + ".hang"), "1", Utf8);

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

    /// <summary>Writes <c>models.yml</c> byte for byte.</summary>
    /// <param name="bytes">The content.</param>
    public void WriteModelsYml(byte[] bytes) => File.WriteAllBytes(ModelsYmlPath, bytes);

    /// <summary>Writes <c>models.yml</c> as UTF-8 without a byte order mark.</summary>
    /// <param name="text">The content.</param>
    public void WriteModelsYml(string text) => File.WriteAllText(ModelsYmlPath, text, Utf8);

    /// <summary>Reads <c>models.yml</c> as bytes, or null when it does not exist.</summary>
    /// <returns>The bytes.</returns>
    public byte[]? ReadModelsYml() => File.Exists(ModelsYmlPath) ? File.ReadAllBytes(ModelsYmlPath) : null;

    /// <summary>Reads a file of the agent directory.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The bytes.</returns>
    public byte[] ReadAgentFile(string name) => File.ReadAllBytes(Path.Combine(AgentDirectory, name));

    /// <summary>Writes a UTF-8 text file into the agent directory.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="text">The content.</param>
    public void WriteAgentFile(string name, string text) => File.WriteAllText(Path.Combine(AgentDirectory, name), text, Utf8);

    /// <summary>Lists the backup files in the agent directory, oldest name first.</summary>
    /// <returns>The file names.</returns>
    public string[] Backups()
    {
        var names = Directory.GetFiles(AgentDirectory, OmpModelsFile.BackupPrefix + "*").Select(Path.GetFileName).OfType<string>().ToArray();
        Array.Sort(names, StringComparer.Ordinal);
        return names;
    }

    /// <summary>Lists every file in the agent directory, by name.</summary>
    /// <returns>The file names.</returns>
    public string[] AgentFiles() =>
        [.. Directory.GetFiles(AgentDirectory).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)];

    /// <summary>Tells whether the fake started at least once: its environment record exists, so it is past its own start-up.</summary>
    /// <returns>True once a call began.</returns>
    public bool HasStarted()
    {
        var directory = Path.Combine(FakeDirectory, "calls");
        return Directory.Exists(directory) && Directory.GetFiles(directory, "*.env").Length > 0;
    }

    /// <summary>Reads every recorded call, in the order they happened.</summary>
    /// <returns>The calls.</returns>
    public List<FakeCall> Calls()
    {
        var directory = Path.Combine(FakeDirectory, "calls");
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var numbers = Directory.GetFiles(directory, "*.argv")
            .Select(path => int.Parse(Path.GetFileNameWithoutExtension(path), CultureInfo.InvariantCulture))
            .Order()
            .ToArray();
        var calls = new List<FakeCall>(numbers.Length);
        foreach (var number in numbers)
        {
            var stem = Path.Combine(directory, number.ToString(CultureInfo.InvariantCulture));
            var stdinPath = stem + ".stdin";
            calls.Add(new FakeCall(
                File.ReadAllLines(stem + ".argv"),
                File.ReadAllLines(stem + ".env"),
                File.Exists(stdinPath) ? File.ReadAllText(stdinPath, Utf8) : null));
        }

        return calls;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
