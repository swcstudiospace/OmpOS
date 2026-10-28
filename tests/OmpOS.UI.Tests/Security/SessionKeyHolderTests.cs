using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OmpOS.UI.Security;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests.Security;

/// <summary>The web provider-key holder (phase 43, VX-10): memory only, cleared on dispose, never in a log or transcript.</summary>
public sealed partial class SessionKeyHolderTests
{
    private const string CanaryKey = "sk-canary-fixture-7f3a91c2d5e8";

    private static readonly string[] BrowserStorageTokens = ["localStorage", "sessionStorage", "indexedDB", "document.cookie"];

    /// <summary>The key is returned while held, no holder shares it with another, and nothing about the type can persist it.</summary>
    [Fact]
    public void KeyIsHeldInMemoryOnly()
    {
        using var holder = new SessionKeyHolder();
        holder.IsSet.ShouldBeFalse();
        holder.Set(CanaryKey);

        holder.IsSet.ShouldBeTrue();
        holder.Reveal().ShouldBe(CanaryKey);

        // A page reload makes a new holder: it starts empty, so nothing survived the old one.
        using var afterReload = new SessionKeyHolder();
        afterReload.IsSet.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => afterReload.Reveal());

        // The holder keeps the key in instance state only: no static field, and no field that could be a storage handle.
        Type type = typeof(SessionKeyHolder);
        type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(field => !field.IsLiteral)
            .ShouldBeEmpty();
        type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(field => field.FieldType)
            .ShouldBe([typeof(Lock), typeof(char[]), typeof(bool)], ignoreOrder: true);

        // It refuses to serialise, so it cannot be written to a JSON document or a storage entry.
        Should.Throw<NotSupportedException>(() => JsonSerializer.Serialize(holder, SessionKeyJsonContext.Default.SessionKeyHolder));
    }

    /// <summary>
    /// The web head and the shared UI never call a browser storage API. Comments are ignored, so documentation may name the APIs it
    /// forbids; string literals are not, because a Blazor head reaches browser storage through JS interop by name
    /// (<c>InvokeAsync("localStorage.setItem", ...)</c>).
    /// </summary>
    [Fact]
    public void NoWebHeadSourceUsesBrowserStorage()
    {
        string root = FindRepoRoot();
        string[] files = new[] { Path.Combine(root, "src", "OmpOS.UI"), Path.Combine(root, "apps", "OmpOS.Web") }
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path))
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal)
                || path.EndsWith(".razor", StringComparison.Ordinal)
                || path.EndsWith(".js", StringComparison.Ordinal)
                || path.EndsWith(".html", StringComparison.Ordinal))
            .ToArray();

        files.ShouldNotBeEmpty();
        string[] offenders = files
            .Where(path => !IsDisplayPreferenceScript(path))
            .Where(path => ContainsStorageToken(WithoutComments(path, File.ReadAllText(path))))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();
        offenders.ShouldBeEmpty("the web head must not write browser storage; provider keys live only in SessionKeyHolder");
    }

    /// <summary>
    /// The two scripts that keep the display preferences (theme and motion, never a secret) are the only web head files allowed to name
    /// a browser storage API. They must not mention a key or a secret.
    /// </summary>
    [Fact]
    public void OnlyTheDisplayPreferenceScriptsTouchBrowserStorageAndHoldNoSecret()
    {
        string root = FindRepoRoot();
        foreach (string name in DisplayPreferenceScripts)
        {
            string text = File.ReadAllText(Path.Combine(root, "src", "OmpOS.UI", "wwwroot", "scripts", name));
            WithoutComments(name, text).ShouldNotContain("apikey", Case.Insensitive);
            WithoutComments(name, text).ShouldNotContain("secret", Case.Insensitive);
            WithoutComments(name, text).ShouldNotContain("token", Case.Insensitive);
        }
    }

    private static readonly string[] DisplayPreferenceScripts = ["ompos-theme.js", "theme-boot.js"];

    private static bool IsDisplayPreferenceScript(string path) =>
        string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "scripts", StringComparison.Ordinal)
        && DisplayPreferenceScripts.Contains(Path.GetFileName(path), StringComparer.Ordinal);

    /// <summary>Dispose overwrites the buffer, forgets the key and refuses further use.</summary>
    [Fact]
    public void KeyIsClearedOnDispose()
    {
        var holder = new SessionKeyHolder();
        holder.Set(CanaryKey);
        char[] buffer = HeldBuffer(holder);
        buffer.ShouldBe(CanaryKey.ToCharArray());

        holder.Dispose();

        buffer.ShouldAllBe(character => character == '\0');
        holder.IsSet.ShouldBeFalse();
        Should.Throw<ObjectDisposedException>(() => holder.Reveal());
        Should.Throw<ObjectDisposedException>(() => holder.Set("replacement"));
    }

    /// <summary>Clear overwrites the buffer and leaves the holder reusable; replacing the key overwrites the old one.</summary>
    [Fact]
    public void ClearAndReplaceOverwriteTheOldKey()
    {
        using var holder = new SessionKeyHolder();
        holder.Set(CanaryKey);
        char[] first = HeldBuffer(holder);

        holder.Set("second-key");
        first.ShouldAllBe(character => character == '\0');
        holder.Reveal().ShouldBe("second-key");

        char[] second = HeldBuffer(holder);
        holder.Clear();

        second.ShouldAllBe(character => character == '\0');
        holder.IsSet.ShouldBeFalse();
        holder.Set("third-key");
        holder.Reveal().ShouldBe("third-key");
    }

    /// <summary>An empty or missing key is refused rather than held as "no key".</summary>
    [Fact]
    public void EmptyOrNullKeyIsRefused()
    {
        using var holder = new SessionKeyHolder();

        Should.Throw<ArgumentException>(() => holder.Set(string.Empty));
        Should.Throw<ArgumentNullException>(() => holder.Set(null!));
        holder.IsSet.ShouldBeFalse();
    }

    /// <summary>The key never reaches a log line, a transcript, an exception message or a JSON error, however the holder is formatted.</summary>
    [Fact]
    public void KeyNeverReachesALogOrTranscriptCanary()
    {
        var log = new StringBuilder();
        var transcript = new StringBuilder();
        var holder = new SessionKeyHolder();
        holder.Set(CanaryKey);

        _ = log.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"provider call with key {holder}");
        _ = log.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "format {0}", holder));
        _ = log.AppendLine(holder.ToString());
        _ = transcript.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"user configured {holder}");
        NotSupportedException serialisationFailure = Should.Throw<NotSupportedException>(
            () => JsonSerializer.Serialize(holder, SessionKeyJsonContext.Default.SessionKeyHolder));
        _ = log.AppendLine(serialisationFailure.ToString());
        _ = log.AppendLine(DebuggerView(holder));

        holder.Dispose();
        ObjectDisposedException disposedFailure = Should.Throw<ObjectDisposedException>(() => holder.Reveal());
        _ = log.AppendLine(disposedFailure.ToString());

        log.ToString().ShouldNotContain(CanaryKey, Case.Sensitive);
        log.ToString().ShouldContain(SessionKeyHolder.RedactionMarker);
        transcript.ToString().ShouldNotContain(CanaryKey, Case.Sensitive);
    }

    private static string DebuggerView(SessionKeyHolder holder)
    {
        var attribute = typeof(SessionKeyHolder).GetCustomAttribute<System.Diagnostics.DebuggerDisplayAttribute>();
        attribute.ShouldNotBeNull();
        return attribute.Value.Replace("{ToString(),nq}", holder.ToString(), StringComparison.Ordinal);
    }

    private static char[] HeldBuffer(SessionKeyHolder holder)
    {
        FieldInfo field = typeof(SessionKeyHolder).GetField("_buffer", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SessionKeyHolder no longer keeps its key in a _buffer field.");
        return (char[])field.GetValue(holder)!;
    }

    private static bool ContainsStorageToken(string code) =>
        BrowserStorageTokens.Any(token => code.Contains(token, StringComparison.OrdinalIgnoreCase));

    /// <summary>Removes comments from C#, Razor, JavaScript and HTML source while keeping every string literal intact.</summary>
    private static string WithoutComments(string path, string text)
    {
        if (path.EndsWith(".html", StringComparison.Ordinal))
        {
            return HtmlComment().Replace(text, string.Empty);
        }

        if (path.EndsWith(".razor", StringComparison.Ordinal))
        {
            text = RazorComment().Replace(HtmlComment().Replace(text, string.Empty), string.Empty);
        }

        // Apostrophes are ordinary text in Razor markup, so only code files treat a single quote as a literal delimiter.
        bool singleQuotesDelimit = !path.EndsWith(".razor", StringComparison.Ordinal);
        var code = new StringBuilder(text.Length);
        char quote = '\0';
        for (var index = 0; index < text.Length; index++)
        {
            char current = text[index];
            char next = index + 1 < text.Length ? text[index + 1] : '\0';
            if (quote != '\0')
            {
                _ = code.Append(current);
                if (current == '\\' && next != '\0')
                {
                    _ = code.Append(next);
                    index++;
                }
                else if (current == quote)
                {
                    quote = '\0';
                }
            }
            else if (current == '/' && next == '/')
            {
                while (index < text.Length && text[index] != '\n')
                {
                    index++;
                }

                _ = code.Append('\n');
            }
            else if (current == '/' && next == '*')
            {
                int end = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
                index = end < 0 ? text.Length : end + 1;
            }
            else
            {
                if (current is '"' or '`' || (current == '\'' && singleQuotesDelimit))
                {
                    quote = current;
                }

                _ = code.Append(current);
            }
        }

        return code.ToString();
    }

    [GeneratedRegex("<!--.*?-->", RegexOptions.Singleline, matchTimeoutMilliseconds: 2000)]
    private static partial Regex HtmlComment();

    [GeneratedRegex(@"@\*.*?\*@", RegexOptions.Singleline, matchTimeoutMilliseconds: 2000)]
    private static partial Regex RazorComment();

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OmpOS.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("OmpOS.slnx was not found above " + AppContext.BaseDirectory);
    }
}
