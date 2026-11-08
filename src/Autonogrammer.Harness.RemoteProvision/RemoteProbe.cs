using Autonogrammer.Harness.Remote;

namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// The one read-only command that describes a remote machine. It prints one <c>AUTONOGRAMMER_PROBE key=value</c> line per fact and writes
/// nothing. It runs through the same <c>sh -c</c> path as every other remote command, with the same <c>PATH</c> extension, so it finds omp and
/// Bun where a session would find them.
/// </summary>
internal static class RemoteProbe
{
    /// <summary>The line prefix of every probe fact.</summary>
    public const string Prefix = "AUTONOGRAMMER_PROBE ";

    /// <summary>The most bytes the probe answer may carry. The script cuts the bundle record at 256 KiB, and the other facts are short.</summary>
    public const int MaxOutputBytes = 512 * 1024;

    /// <summary>The one line of POSIX shell the probe runs. Every statement ends with a semicolon, or with <c>fi</c> for the last one.</summary>
    public static string Script { get; } = string.Join(" ", Statements());

    private static string[] Statements() =>
    [
        SshCommandBuilder.PathSetup + ";",
        """p() { printf 'AUTONOGRAMMER_PROBE %s=%s\n' "$1" "$2"; };""",
        """p system "$(uname -s 2>/dev/null)";""",
        """p machine "$(uname -m 2>/dev/null)";""",
        """p home "$HOME";""",
        """for t in bash curl tar unzip git; do if command -v "$t" >/dev/null 2>&1; then p "tool_$t" yes; else p "tool_$t" no; fi; done;""",
        """if command -v sha256sum >/dev/null 2>&1 || command -v shasum >/dev/null 2>&1; then p tool_sha256 yes; else p tool_sha256 no; fi;""",
        """if command -v sha512sum >/dev/null 2>&1 || command -v shasum >/dev/null 2>&1; then p tool_sha512 yes; else p tool_sha512 no; fi;""",
        """if command -v omp >/dev/null 2>&1; then p omp_path "$(command -v omp)"; W=; if command -v timeout >/dev/null 2>&1; then W='timeout 20'; fi; p omp_version "$($W omp --version 2>&1 </dev/null | head -n 1)"; else p omp_path ABSENT; p omp_version ABSENT; fi;""",
        """if [ -x "$HOME/.bun/bin/bun" ]; then B=; if command -v timeout >/dev/null 2>&1; then B='timeout 20'; fi; p bun_version "$($B "$HOME/.bun/bin/bun" --version 2>&1 </dev/null | head -n 1)"; else p bun_version ABSENT; fi;""",
        """if [ -f "$HOME/.local/share/autonogrammer/plugins/bundle.json" ]; then p bundle "$(tr -d '\r\n' < "$HOME/.local/share/autonogrammer/plugins/bundle.json" | head -c 262144)"; else p bundle ABSENT; fi""",
    ];
}
