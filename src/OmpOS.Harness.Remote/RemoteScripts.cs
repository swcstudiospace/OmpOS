using System.Globalization;

namespace OmpOS.Harness.Remote;

/// <summary>
/// The POSIX shell scripts the remote file system and the remote convention discovery run through <see cref="RemoteExec"/>. Each script is one
/// line, starts with the same prelude and ends with an exit code from the <c>Exit*</c> constants. The prelude resolves the allowed root to its
/// real path with <c>cd -P</c> and <c>pwd -P</c>, and checks every path it is about to touch with <c>realpath</c>, then <c>readlink -f</c>, then a
/// <c>cd -P</c> fallback that refuses a symbolic link it cannot resolve: a path whose real location is not inside the root is never read, listed,
/// written or deleted. Every dynamic value is single-quoted with <see cref="PosixQuoting"/>.
/// </summary>
internal static class RemoteScripts
{
    /// <summary>The allowed root does not exist or cannot be entered.</summary>
    public const int ExitRootMissing = 20;

    /// <summary>The path does not exist.</summary>
    public const int ExitNotFound = 21;

    /// <summary>The real location of the path is outside the allowed root.</summary>
    public const int ExitEscapes = 22;

    /// <summary>The path is not a directory.</summary>
    public const int ExitNotDirectory = 23;

    /// <summary>The remote machine has no way to resolve the real path, so containment cannot be verified.</summary>
    public const int ExitUnverifiable = 24;

    /// <summary>The directory cannot be entered.</summary>
    public const int ExitCannotEnter = 25;

    /// <summary>The path is not a regular file.</summary>
    public const int ExitNotRegular = 27;

    /// <summary>A temporary file could not be written or moved into place.</summary>
    public const int ExitWriteFailed = 28;

    /// <summary>The file changed since it was read.</summary>
    public const int ExitStale = 29;

    /// <summary>A file or directory already exists at the path.</summary>
    public const int ExitExists = 30;

    /// <summary>The file could not be read.</summary>
    public const int ExitReadFailed = 32;

    /// <summary>The marker line that precedes the file signature in the output of <see cref="ReadWithSignature"/>.</summary>
    public const string SignatureMarker = "OMPOS_SIG ";

    /// <summary>The marker that prefixes the answer of <see cref="Stat"/>.</summary>
    public const string StatMarker = "OMPOS_STAT ";

    private const string Functions =
        """within() { p=$1; shift; for r; do if [ "$r" = / ]; then return 0; fi; case "$p" in "$r"|"$r"/*) return 0;; esac; done; return 1; }; rp() { if command -v realpath >/dev/null 2>&1; then realpath -- "$1" 2>/dev/null; elif readlink -f / >/dev/null 2>&1; then readlink -f -- "$1" 2>/dev/null; elif [ -d "$1" ]; then (cd -P -- "$1" 2>/dev/null && pwd -P); elif [ -L "$1" ]; then return 1; else rpd=$(dirname -- "$1"); rpb=$(basename -- "$1"); rpd=$(cd -P -- "$rpd" 2>/dev/null && pwd -P) || return 1; printf '%s/%s\n' "$rpd" "$rpb"; fi; }; sig() { stat -L -c '%Y %s' -- "$1" 2>/dev/null || stat -L -f '%m %z' -- "$1" 2>/dev/null; }""";

    /// <summary>Builds the lines every script starts with: the extended <c>PATH</c>, the real allowed root and the helper functions.</summary>
    /// <param name="root">The absolute allowed root on the remote machine.</param>
    /// <returns>The prelude, ending with <c>; </c>.</returns>
    public static string Prelude(string root) =>
        SshCommandBuilder.PathSetup
        + "; set -- " + PosixQuoting.Quote(root)
        + "; ro=$#; for r in \"$@\"; do x=$(cd -P -- \"$r\" 2>/dev/null && pwd -P) && set -- \"$@\" \"$x\"; done; shift \"$ro\"; [ \"$#\" -gt 0 ] || exit " + Number(ExitRootMissing)
        + "; " + Functions + "; ";

    /// <summary>Builds the script that lists one directory.</summary>
    /// <param name="root">The allowed root.</param>
    /// <param name="directory">The absolute directory.</param>
    /// <param name="maxEntries">The most entries printed; a larger directory prints a <c>T</c> line after them.</param>
    /// <param name="verifyLinks">Whether a symbolic link whose real target is outside the root, or missing, is left out of the listing.</param>
    /// <returns>The script. Each entry is one line <c>{D|F}{L|.} name</c>: directory or file, link or not.</returns>
    public static string List(string root, string directory, int maxEntries, bool verifyLinks)
    {
        var linkCheck = verifyLinks
            ? "if [ -L \"$f\" ]; then [ -e \"$f\" ] || continue; t=$(rp \"$D/$f\") || continue; within \"$t\" \"$@\" || continue; fi; "
            : string.Empty;
        var fileTest = verifyLinks ? "elif [ -f \"$f\" ]; then k=F; " : "elif [ -f \"$f\" ] || [ -L \"$f\" ]; then k=F; ";
        return Prelude(root)
            + "D=" + PosixQuoting.Quote(directory)
            + "; [ -e \"$D\" ] || exit " + Number(ExitNotFound)
            + "; [ -d \"$D\" ] || exit " + Number(ExitNotDirectory)
            + "; rd=$(rp \"$D\") || exit " + Number(ExitUnverifiable)
            + "; within \"$rd\" \"$@\" || exit " + Number(ExitEscapes)
            + "; cd -- \"$D\" 2>/dev/null || exit " + Number(ExitCannotEnter)
            + "; nl=$(printf '\\nx'); nl=${nl%x}; cnt=0; for f in .[!.]* ..?* *; do "
            + "[ -e \"$f\" ] || [ -L \"$f\" ] || continue; case \"$f\" in *\"$nl\"*) continue;; esac; "
            + "if [ -L \"$f\" ]; then lk=L; else lk=.; fi; "
            + linkCheck
            + "if [ -d \"$f\" ]; then k=D; " + fileTest + "else continue; fi; "
            + "cnt=$((cnt+1)); if [ \"$cnt\" -gt " + Number(maxEntries) + " ]; then printf 'T\\n'; break; fi; "
            + "printf '%s%s %s\\n' \"$k\" \"$lk\" \"$f\"; done";
    }

    /// <summary>Builds the script that prints the signature of a file (its modification time and size) and then its first bytes.</summary>
    /// <param name="root">The allowed root.</param>
    /// <param name="file">The absolute file.</param>
    /// <param name="maxBytes">The most content bytes printed, plus one so the caller can tell a larger file.</param>
    /// <returns>The script. The output is <c>OMPOS_SIG &lt;signature&gt;</c>, a line feed and the bytes.</returns>
    public static string ReadWithSignature(string root, string file, int maxBytes) =>
        Prelude(root)
        + "F=" + PosixQuoting.Quote(file)
        + "; [ -e \"$F\" ] || exit " + Number(ExitNotFound)
        + "; rf=$(rp \"$F\") || exit " + Number(ExitUnverifiable)
        + "; within \"$rf\" \"$@\" || exit " + Number(ExitEscapes)
        + "; [ -f \"$F\" ] || exit " + Number(ExitNotRegular)
        + "; printf '" + SignatureMarker + "%s\\n' \"$(sig \"$rf\")\"; head -c " + Number(maxBytes + 1) + " < \"$rf\" || exit " + Number(ExitReadFailed);

    /// <summary>Builds the script that answers whether a path exists inside the root, as an exit code.</summary>
    /// <param name="root">The allowed root.</param>
    /// <param name="path">The absolute path.</param>
    /// <returns>The script; exit code 0 when the path exists and its real location is inside the root.</returns>
    public static string Exists(string root, string path) =>
        Prelude(root)
        + "P=" + PosixQuoting.Quote(path)
        + "; [ -e \"$P\" ] || exit " + Number(ExitNotFound)
        + "; rq=$(rp \"$P\") || exit " + Number(ExitUnverifiable)
        + "; within \"$rq\" \"$@\" || exit " + Number(ExitEscapes);

    /// <summary>Builds the script that describes one path without reading it.</summary>
    /// <param name="root">The allowed root.</param>
    /// <param name="path">The absolute path.</param>
    /// <returns>The script. The output is <c>OMPOS_STAT kind=missing</c> or <c>OMPOS_STAT kind={dir|file|other} size=n mtime=n</c>.</returns>
    public static string Stat(string root, string path) =>
        Prelude(root)
        + "P=" + PosixQuoting.Quote(path)
        + "; [ -e \"$P\" ] || { printf '" + StatMarker + "kind=missing\\n'; exit 0; }"
        + "; rq=$(rp \"$P\") || exit " + Number(ExitUnverifiable)
        + "; within \"$rq\" \"$@\" || exit " + Number(ExitEscapes)
        + "; if [ -d \"$P\" ]; then k=dir; elif [ -f \"$P\" ]; then k=file; else k=other; fi; sz=; [ \"$k\" = file ] && sz=$(wc -c < \"$rq\" | tr -d ' ')"
        + "; mt=$(stat -L -c %Y -- \"$rq\" 2>/dev/null || stat -L -f %m -- \"$rq\" 2>/dev/null)"
        + "; printf '" + StatMarker + "kind=%s size=%s mtime=%s\\n' \"$k\" \"$sz\" \"$mt\"";

    /// <summary>Builds the script that replaces a file with the bytes on stdin when the file still has the signature the caller read.</summary>
    /// <param name="root">The allowed root.</param>
    /// <param name="file">The absolute file.</param>
    /// <param name="signature">The signature printed by <see cref="ReadWithSignature"/>.</param>
    /// <returns>The script. The new bytes are the stdin of the process.</returns>
    public static string Replace(string root, string file, string signature) =>
        Prelude(root)
        + "F=" + PosixQuoting.Quote(file)
        + "; [ -f \"$F\" ] || exit " + Number(ExitNotFound)
        + "; rf=$(rp \"$F\") || exit " + Number(ExitUnverifiable)
        + "; within \"$rf\" \"$@\" || exit " + Number(ExitEscapes)
        + "; [ \"$(sig \"$rf\")\" = " + PosixQuoting.Quote(signature) + " ] || exit " + Number(ExitStale)
        + "; d=$(dirname -- \"$rf\"); T=\"$d/.ompos-write.$$\"; [ ! -e \"$T\" ] || exit " + Number(ExitWriteFailed)
        + "; trap 'rm -f \"$T\"' EXIT; cp -p -- \"$rf\" \"$T\" || exit " + Number(ExitWriteFailed)
        + "; cat > \"$T\" || exit " + Number(ExitWriteFailed)
        + "; [ \"$(sig \"$rf\")\" = " + PosixQuoting.Quote(signature) + " ] || exit " + Number(ExitStale)
        + "; mv -f -- \"$T\" \"$rf\" || exit " + Number(ExitWriteFailed)
        + "; trap - EXIT";

    /// <summary>Builds the script that creates a new file from the bytes on stdin, and its missing parent directories, when nothing exists at the path.</summary>
    /// <param name="root">The allowed root.</param>
    /// <param name="file">The absolute file.</param>
    /// <returns>The script. The file bytes are the stdin of the process.</returns>
    public static string Create(string root, string file) =>
        Prelude(root)
        + "F=" + PosixQuoting.Quote(file)
        + "; { [ ! -e \"$F\" ] && [ ! -L \"$F\" ]; } || exit " + Number(ExitExists)
        + "; d=$(dirname -- \"$F\"); a=$d; while [ ! -d \"$a\" ]; do a=$(dirname -- \"$a\"); done"
        + "; ra=$(rp \"$a\") || exit " + Number(ExitUnverifiable)
        + "; within \"$ra\" \"$@\" || exit " + Number(ExitEscapes)
        + "; mkdir -p -- \"$d\" || exit " + Number(ExitWriteFailed)
        + "; rd=$(rp \"$d\") || exit " + Number(ExitUnverifiable)
        + "; within \"$rd\" \"$@\" || exit " + Number(ExitEscapes)
        + "; T=\"$d/.ompos-new.$$\"; [ ! -e \"$T\" ] || exit " + Number(ExitWriteFailed)
        + "; trap 'rm -f \"$T\"' EXIT; set -C; cat > \"$T\" || exit " + Number(ExitWriteFailed) + "; set +C"
        + "; if ln -- \"$T\" \"$F\" 2>/dev/null; then rm -f \"$T\"; elif [ ! -e \"$F\" ]; then mv -- \"$T\" \"$F\" || exit " + Number(ExitWriteFailed)
        + "; else exit " + Number(ExitExists) + "; fi; trap - EXIT";

    /// <summary>Builds the script that deletes a file when it still has the signature the caller read.</summary>
    /// <param name="root">The allowed root.</param>
    /// <param name="file">The absolute file.</param>
    /// <param name="signature">The signature printed by <see cref="ReadWithSignature"/>.</param>
    /// <returns>The script.</returns>
    public static string Delete(string root, string file, string signature) =>
        Prelude(root)
        + "F=" + PosixQuoting.Quote(file)
        + "; [ -f \"$F\" ] || exit " + Number(ExitNotFound)
        + "; rf=$(rp \"$F\") || exit " + Number(ExitUnverifiable)
        + "; within \"$rf\" \"$@\" || exit " + Number(ExitEscapes)
        + "; d=$(dirname -- \"$F\"); rd=$(rp \"$d\") || exit " + Number(ExitUnverifiable)
        + "; within \"$rd\" \"$@\" || exit " + Number(ExitEscapes)
        + "; [ \"$(sig \"$rf\")\" = " + PosixQuoting.Quote(signature) + " ] || exit " + Number(ExitStale)
        + "; rm -f -- \"$F\" || exit " + Number(ExitWriteFailed);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
