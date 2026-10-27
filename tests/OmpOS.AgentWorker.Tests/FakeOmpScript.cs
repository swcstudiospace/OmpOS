using System.Runtime.Versioning;
using System.Text;

namespace OmpOS.AgentWorker.Tests;

/// <summary>
/// A shell script that stands in for <c>omp acp</c>. It speaks just enough ACP for the bridge tests: it answers <c>initialize</c> with a
/// fixed result and echoes every other request inside a result. Flags after <c>acp</c> select what it records and how it misbehaves:
/// <c>--pidfile F</c>, <c>--argv-file F</c>, <c>--cwd-file F</c> write facts to files; <c>--stderr-env NAME</c> prints that environment
/// variable's value on stderr; <c>--mode</c> is <c>echo</c> (default), <c>stubborn</c> (ignores stdin EOF, never reads), <c>garbage</c>
/// (answers with a non-JSON line), <c>big</c> (answers with a line over 1 MiB) or <c>exit-on-first</c> (exits with code 3 on the first line).
/// </summary>
[SupportedOSPlatform("linux")]
internal static class FakeOmpScript
{
    private const string Body = """
        #!/bin/sh
        argv_all="$*"
        shift
        pidfile=""; argvfile=""; cwdfile=""; mode="echo"; stderrenv=""
        while [ "$#" -gt 0 ]; do
          case "$1" in
            --pidfile) pidfile="$2"; shift 2 ;;
            --argv-file) argvfile="$2"; shift 2 ;;
            --cwd-file) cwdfile="$2"; shift 2 ;;
            --mode) mode="$2"; shift 2 ;;
            --stderr-env) stderrenv="$2"; shift 2 ;;
            *) shift ;;
          esac
        done
        if [ -n "$pidfile" ]; then printf '%s\n' "$$" > "$pidfile"; fi
        if [ -n "$argvfile" ]; then printf '%s\n' "$argv_all" > "$argvfile"; fi
        if [ -n "$cwdfile" ]; then pwd > "$cwdfile"; fi
        if [ -n "$stderrenv" ]; then eval "printf 'env: %s\n' \"\${$stderrenv}\"" >&2; fi
        if [ "$mode" = "stubborn" ]; then exec sleep 600; fi
        while IFS= read -r line; do
          id=$(printf '%s' "$line" | sed -n 's/.*"id":\([0-9][0-9]*\).*/\1/p')
          case "$mode" in
            exit-on-first) exit 3 ;;
            sink) printf '{"jsonrpc":"2.0","id":%s,"result":{}}\n' "${id:-null}" ;;
            garbage) printf 'this is not json\n' ;;
            big) head -c 1100000 /dev/zero | tr '\0' 'a'; printf '\n' ;;
            *)
              case "$line" in
                *'"method":"initialize"'*)
                  printf '{"jsonrpc":"2.0","id":%s,"result":{"protocolVersion":1,"agentCapabilities":{},"authMethods":[]}}\n' "${id:-null}" ;;
                *)
                  printf '{"jsonrpc":"2.0","id":%s,"result":{"echo":%s}}\n' "${id:-null}" "$line" ;;
              esac ;;
          esac
        done
        """;

    /// <summary>Writes the script into <paramref name="directory"/> and makes it executable.</summary>
    /// <param name="directory">An existing directory.</param>
    /// <returns>The absolute path of the script.</returns>
    public static string Write(string directory)
    {
        LinuxOnly.Require();
        var path = Path.Combine(directory, "fake-omp");
        File.WriteAllText(path, Body.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }
}
