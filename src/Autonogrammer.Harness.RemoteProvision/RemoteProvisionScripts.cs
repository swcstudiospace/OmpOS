using System.Text;
using Autonogrammer.Harness.Remote;

namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// Writes the POSIX shell scripts that install one item on the remote machine. Each script runs with <c>bash -s</c> and reads itself from stdin.
/// Every value that is not a literal is quoted with <see cref="PosixQuoting.QuoteValue"/>, and every command that could read stdin gets
/// <c>&lt;/dev/null</c>, so no command eats the rest of the script. A script verifies its download before it unpacks it, installs under
/// <c>$HOME</c> only, and writes its marker last.
/// </summary>
internal static class RemoteProvisionScripts
{
    /// <summary>The most stdout bytes an install script may write. It prints nothing on success; a failure goes to stderr.</summary>
    public const int MaxOutputBytes = 8 * 1024;

    private const string Curl = "curl --fail --silent --show-error --location --proto '=https' --tlsv1.2 --max-time 900";
    private const string MarkerDirectory = "\"$HOME/.local/share/autonogrammer/provision\"";
    private const string OmpLinkLine = "link=\"$HOME/.local/bin/omp\"";
    private const string OmpRootLine = "root=\"$HOME/.local/share/autonogrammer/omp\"";
    private const string PluginRootLine = "root=\"$HOME/.local/share/autonogrammer/plugins\"";

    /// <summary>Builds the script that installs the pinned Bun archive into <c>~/.bun/bin/bun</c>.</summary>
    /// <param name="action">The Bun install.</param>
    /// <param name="gitHubBase">The GitHub base address the Bun release is read from.</param>
    /// <returns>The script text.</returns>
    public static string Bun(BunInstallAction action, Uri gitHubBase)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(gitHubBase);
        var url = new Uri(gitHubBase, "oven-sh/bun/releases/download/bun-v" + action.Version + "/" + action.Asset.FileName).AbsoluteUri;
        string[] body =
        [
            $$"""{{Curl}} --output "$tmp/bun.zip" {{Q(url)}} </dev/null || fail {{Q("the Bun download failed")}}""",
            $$"""[ "$(sha256_of "$tmp/bun.zip")" = {{Q(action.Asset.Sha256)}} ] || fail {{Q("the Bun archive does not match its pinned SHA-256")}}""",
            """mkdir "$tmp/bun" && unzip -q "$tmp/bun.zip" -d "$tmp/bun" </dev/null || fail 'the Bun archive could not be unpacked'""",
            """bin=$(find "$tmp/bun" -type f -name bun | head -n 1)""",
            """[ -n "$bin" ] || fail 'the Bun archive has no bun executable'""",
            """mkdir -p "$HOME/.bun/bin" && cp "$bin" "$HOME/.bun/bin/.bun-new.$$" && chmod 0755 "$HOME/.bun/bin/.bun-new.$$" && mv -f "$HOME/.bun/bin/.bun-new.$$" "$HOME/.bun/bin/bun" || fail 'Bun could not be installed'""",
            $$"""[ "$("$HOME/.bun/bin/bun" --version </dev/null 2>&1 | head -n 1)" = {{Q(action.Version)}} ] || fail {{Q("the installed Bun does not report the pinned version")}}""",
            $$"""mkdir -p {{MarkerDirectory}} && printf '%s\n' {{Q(action.Version)}} > {{MarkerDirectory}}/bun.version""",
        ];
        return Join(Header(action.PlatformKey).Concat(body));
    }

    /// <summary>
    /// Builds the script that installs one omp release. The tarball is verified against the registry's SHA-512 and checked for unsafe paths before
    /// it is unpacked; it is built with the pinned Bun and proven by its own <c>--version</c> before it is linked from <c>~/.local/bin/omp</c>.
    /// </summary>
    /// <param name="action">The omp install.</param>
    /// <returns>The script text.</returns>
    public static string Omp(OmpInstallAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var version = action.Release.Version;
        string[] body =
        [
            OmpLinkLine,
            """if [ -e "$link" ] && [ ! -L "$link" ]; then fail 'a file that is not a link made by Autonogrammer is at ~/.local/bin/omp; it was not replaced'; fi""",
            """[ -x "$HOME/.bun/bin/bun" ] || fail 'Bun is not installed'""",
            $$"""{{Curl}} --output "$tmp/omp.tgz" {{Q(action.Release.TarballUri.AbsoluteUri)}} </dev/null || fail {{Q("the Omp download failed")}}""",
            $$"""[ "$(sha512_of "$tmp/omp.tgz")" = {{Q(LowerHex(action.Release.Sha512Hex))}} ] || fail {{Q("the Omp tarball does not match the registry SHA-512")}}""",
            """tar -tzf "$tmp/omp.tgz" > "$tmp/omp.list" </dev/null || fail 'the Omp tarball could not be read'""",
            """if grep -Eq '(^|/)\.\.(/|$)|^/' "$tmp/omp.list"; then fail 'the Omp tarball has an unsafe path'; fi""",
            """mkdir "$tmp/src" && tar -xzf "$tmp/omp.tgz" -C "$tmp/src" --strip-components=1 </dev/null || fail 'the Omp tarball could not be unpacked'""",
            """[ -f "$tmp/src/dist/cli.js" ] || fail 'the Omp package has no dist/cli.js entry point'""",
            """(cd "$tmp/src" && "$HOME/.bun/bin/bun" install --production </dev/null) || fail 'bun install failed for Omp'""",
            """mkdir -p "$tmp/src/bin" && (cd "$tmp/src" && "$HOME/.bun/bin/bun" build --compile dist/cli.js --outfile bin/omp </dev/null) || fail 'bun build --compile failed for Omp'""",
            $$"""case "$("$tmp/src/bin/omp" --version </dev/null 2>&1 | head -n 1)" in *{{Q(version)}}*) ;; *) fail {{Q("the built omp does not report the release version")}};; esac""",
            OmpRootLine,
            $$"""dest="$root"/{{Q(version)}}""",
            """mkdir -p "$root" "$HOME/.local/bin" && rm -rf "$dest.part" && mv "$tmp/src" "$dest.part" || fail 'the Omp build could not be moved into place'""",
            """rm -rf "$dest" && mv "$dest.part" "$dest" || fail 'the Omp build could not be activated'""",
            """for old in "$root"/*; do [ -d "$old" ] || continue; case "$(basename "$old")" in [0-9]*) ;; *) continue;; esac; [ "$old" = "$dest" ] || rm -rf "$old"; done""",
            """rm -f "$link" && ln -s "$dest/bin/omp" "$link" || fail 'the omp link could not be made'""",
            """[ "$(command -v omp)" = "$link" ] || fail 'another omp earlier on the path shadows ~/.local/bin/omp'""",
            $$"""case "$("$link" --version </dev/null 2>&1 | head -n 1)" in *{{Q(version)}}*) ;; *) fail {{Q("the installed omp does not report the release version")}};; esac""",
            $$"""mkdir -p {{MarkerDirectory}} && printf '%s\n' {{Q(version)}} > {{MarkerDirectory}}/omp.version""",
        ];
        return Join(Header(action.PlatformKey).Concat(body));
    }

    /// <summary>
    /// Builds the script that installs one plugin at its pinned commit. Git verifies the commit before the directory is replaced. The bundle record
    /// is rewritten last, so a failed clone leaves the record naming the old state, and the next run sees the drift again.
    /// </summary>
    /// <param name="action">The plugin install.</param>
    /// <param name="bundleJson">The bundle record that holds every entry the machine should have after this install, as compact JSON.</param>
    /// <returns>The script text.</returns>
    public static string Plugin(PluginInstallAction action, string bundleJson)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(bundleJson);
        var record = action.Record;
        string[] body =
        [
            PluginRootLine,
            "mkdir -p \"$root\"",
            "GIT_TERMINAL_PROMPT=0; export GIT_TERMINAL_PROMPT",
            $$"""git clone --quiet --no-checkout -- {{Q(record.Source)}} "$tmp/plugin" </dev/null || fail {{Q("the plugin repository could not be cloned")}}""",
            $$"""git -C "$tmp/plugin" checkout --quiet --detach {{Q(record.Commit)}} </dev/null || fail {{Q("the pinned commit is not in the plugin repository")}}""",
            $$"""[ "$(git -C "$tmp/plugin" rev-parse HEAD </dev/null)" = {{Q(record.Commit)}} ] || fail {{Q("the plugin checkout is not the pinned commit")}}""",
            "rm -rf \"$tmp/plugin/.git\"",
            $$"""rm -rf "$root"/{{Q(record.Dir)}} && mv "$tmp/plugin" "$root"/{{Q(record.Dir)}} || fail {{Q("the plugin could not be put in place")}}""",
            $$"""printf '%s' {{Q(bundleJson)}} > "$root/bundle.json.new" && mv -f "$root/bundle.json.new" "$root/bundle.json" || fail {{Q("the plugin bundle record could not be written")}}""",
        ];
        return Join(Header(platformKey: null).Concat(body));
    }

    private static IEnumerable<string> Header(string? platformKey)
    {
        string[] lines =
        [
            "set -eu",
            "umask 022",
            SshCommandBuilder.PathSetup,
            """fail() { printf '%s\n' "autonogrammer-provision: $1" >&2; exit 1; }""",
            """sha256_of() { if command -v sha256sum >/dev/null 2>&1; then sha256sum "$1" | cut -d ' ' -f 1; else shasum -a 256 "$1" | cut -d ' ' -f 1; fi; }""",
            """sha512_of() { if command -v sha512sum >/dev/null 2>&1; then sha512sum "$1" | cut -d ' ' -f 1; else shasum -a 512 "$1" | cut -d ' ' -f 1; fi; }""",
            """tmp=$(mktemp -d "${TMPDIR:-/tmp}/autonogrammer-provision.XXXXXX") || fail 'cannot create a temporary directory'""",
            """trap 'rm -rf "$tmp"' EXIT""",
        ];
        foreach (var line in lines)
        {
            yield return line;
        }

        if (platformKey is not null)
        {
            var (kernel, machine) = RemotePlatform.UnameFor(platformKey);
            yield return $$"""[ "$(uname -s)/$(uname -m)" = {{Q(kernel + "/" + machine)}} ] || fail {{Q("this script does not run on this platform")}}""";
        }
    }

    private static string Q(string value) => PosixQuoting.QuoteValue(value);

    private static string LowerHex(string hex) => new(hex.Select(static character => char.ToLowerInvariant(character)).ToArray());

    private static string Join(IEnumerable<string> lines)
    {
        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            _ = builder.Append(line).Append('\n');
        }

        return builder.ToString();
    }
}
