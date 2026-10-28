using System.Runtime.Versioning;
using System.Text;

namespace OmpOS.AgentWorker.Tests;

/// <summary>
/// A shell script that stands in for <c>omp acp</c> in the hooks tests. Like <see cref="FakeOmpScript"/> it answers <c>initialize</c> with a fixed
/// result and echoes every other request inside a result; before it reads its first line it writes what the worker gave it:
/// <c>--hooks-file F</c> receives the bridge URL and the bridge token (two lines, empty when the environment variable is unset) and
/// <c>--argv-file F</c> receives the arguments after <c>acp</c>. The tests play the omp extension by posting to the URL themselves.
/// </summary>
[SupportedOSPlatform("linux")]
internal static class HooksFakeOmpScript
{
    private const string Body = """
        #!/bin/sh
        argv_all="$*"
        shift
        hooksfile=""; argvfile=""
        while [ "$#" -gt 0 ]; do
          case "$1" in
            --hooks-file) hooksfile="$2"; shift 2 ;;
            --argv-file) argvfile="$2"; shift 2 ;;
            *) shift ;;
          esac
        done
        if [ -n "$argvfile" ]; then printf '%s\n' "$argv_all" > "$argvfile"; fi
        if [ -n "$hooksfile" ]; then printf '%s\n%s\n' "$OMPOS_HOOKS_URL" "$OMPOS_HOOKS_TOKEN" > "$hooksfile.tmp" && mv "$hooksfile.tmp" "$hooksfile"; fi
        while IFS= read -r line; do
          id=$(printf '%s' "$line" | sed -n 's/.*"id":\([0-9][0-9]*\).*/\1/p')
          case "$line" in
            *'"method":"initialize"'*)
              printf '{"jsonrpc":"2.0","id":%s,"result":{"protocolVersion":1,"agentCapabilities":{},"authMethods":[]}}\n' "${id:-null}" ;;
            *)
              printf '{"jsonrpc":"2.0","id":%s,"result":{"echo":%s}}\n' "${id:-null}" "$line" ;;
          esac
        done
        """;

    /// <summary>Writes the script into <paramref name="directory"/> and makes it executable.</summary>
    /// <param name="directory">An existing directory.</param>
    /// <returns>The absolute path of the script.</returns>
    public static string Write(string directory)
    {
        LinuxOnly.Require();
        var path = Path.Combine(directory, "hooks-fake-omp");
        File.WriteAllText(path, Body.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }
}
