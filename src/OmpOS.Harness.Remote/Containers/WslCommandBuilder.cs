using OmpOS.Contracts.Environments;

namespace OmpOS.Harness.Remote.Containers;

/// <summary>
/// Builds the <c>wsl.exe</c> argument lists of OmpOS. A session in a WSL distribution runs <c>wsl.exe -d &lt;distro&gt; [-u user] --exec sh -c '&lt;script&gt;'</c>: the script is
/// the same POSIX bootstrap that the ssh builder sends, <c>--exec</c> runs <c>sh</c> directly without the user's login shell so no second layer of shell parsing
/// touches the script, and secrets travel in <see cref="SshInvocation.Preamble"/> on the stdin of wsl.exe. The arguments are a list (no local shell). The
/// working directory is entered by the script (<c>cd</c> with exit code 71), not by <c>--cd</c>, so a missing folder is reported by the same diagnosis as on ssh.
/// WSL exists on Windows only: the builder itself runs anywhere, and a run on another system fails with the "wsl.exe is not available" diagnosis.
/// UNVERIFIED on a real Windows machine: <c>wsl.exe -d &lt;distro&gt; --exec sh -c 'printf "%s\n" "$1"' x 'a b'</c> must print <c>a b</c> (one argument per word after <c>--exec</c>).
/// </summary>
public sealed class WslCommandBuilder
{
    private readonly WslClientOptions _options;
    private readonly SshCommandBuilder _bootstrap;

    /// <summary>Initializes a new instance of the <see cref="WslCommandBuilder"/> class.</summary>
    /// <param name="options">How wsl.exe is run, or <see langword="null"/> for the defaults.</param>
    /// <exception cref="ArgumentException">The wsl.exe path is blank.</exception>
    public WslCommandBuilder(WslClientOptions? options = null)
        : this(options, markerFactory: null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="WslCommandBuilder"/> class with a fixed way of choosing the end-of-section marker.</summary>
    /// <param name="options">How wsl.exe is run, or <see langword="null"/> for the defaults.</param>
    /// <param name="markerFactory">Creates the marker line that ends a section of the stdin preamble. Null uses a random value.</param>
    internal WslCommandBuilder(WslClientOptions? options, Func<string>? markerFactory)
    {
        _options = options ?? new WslClientOptions();
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.WslPath, nameof(options));
        _bootstrap = new SshCommandBuilder(options: null, markerFactory);
    }

    /// <summary>Builds the wsl.exe invocation that runs <c>omp acp</c> in the distribution with its stdio carried by wsl.exe.</summary>
    /// <param name="profile">The distribution.</param>
    /// <param name="launch">What to run there. It carries no reverse forwards.</param>
    /// <returns>The invocation: the arguments never hold a secret, the preamble holds the environment and files.</returns>
    /// <exception cref="ArgumentException">The profile or the launch holds a value that is not safe to pass to wsl.exe or to a shell, or the launch has reverse forwards.</exception>
    public SshInvocation BuildAcp(WslProfile profile, RemoteLaunch launch)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(launch);
        ExecScriptRules.RequireValid(profile);
        ExecScriptRules.RequireNoForwards(launch);
        var (script, preamble) = _bootstrap.BuildBootstrap(launch);
        return new SshInvocation { FileName = _options.WslPath, Arguments = ExecArguments(profile, script), Preamble = preamble };
    }

    /// <summary>Builds the wsl.exe invocation that runs a short POSIX script in the distribution without a terminal (the preflight and the file system).</summary>
    /// <param name="profile">The distribution.</param>
    /// <param name="script">One line of POSIX shell. It is passed to <c>sh -c</c> in the distribution.</param>
    /// <returns>The invocation, without a preamble.</returns>
    /// <exception cref="ArgumentException">The profile is not valid or the script is blank or spans several lines.</exception>
    public SshInvocation BuildProbe(WslProfile profile, string script)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ExecScriptRules.RequireOneLine(script);
        ExecScriptRules.RequireValid(profile);
        return new SshInvocation { FileName = _options.WslPath, Arguments = ExecArguments(profile, script) };
    }

    /// <summary>
    /// Builds the wsl.exe invocation of an interactive terminal in the distribution. Nothing secret may be in the script: stdin is the user's keyboard, so no
    /// preamble can travel. wsl.exe attaches to the pseudo-terminal it is started in.
    /// </summary>
    /// <param name="profile">The distribution.</param>
    /// <param name="script">One line of POSIX shell, for example <c>cd '/work' &amp;&amp; exec "${SHELL:-/bin/sh}" -l</c>.</param>
    /// <returns>The invocation, without a preamble.</returns>
    /// <exception cref="ArgumentException">The profile is not valid, or the script is blank or spans several lines.</exception>
    public SshInvocation BuildTerminal(WslProfile profile, string script) => BuildProbe(profile, script);

    /// <summary>Builds <c>wsl.exe --list --quiet</c>: the names of the installed distributions, one per line (UTF-16 text).</summary>
    /// <returns>The invocation.</returns>
    public SshInvocation BuildList() => new() { FileName = _options.WslPath, Arguments = ["--list", "--quiet"] };

    private static List<string> ExecArguments(WslProfile profile, string script)
    {
        var arguments = new List<string> { "-d", profile.Distro };
        if (profile.User is { } user)
        {
            arguments.Add("-u");
            arguments.Add(user);
        }

        arguments.Add("--exec");
        arguments.Add("sh");
        arguments.Add("-c");
        arguments.Add(script);
        return arguments;
    }
}
