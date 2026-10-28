using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.About;

/// <summary>
/// What the About window shows: the product, the version and revision of the running build, the licence, where the source is, and the
/// notices that travel with the build. It is the desktop head's side of the AGPL-3.0 source offer (<c>docs/licensing.md</c>, ADR-0018): the
/// fields match the <c>GET /source</c> answer of the API, so a user sees the same licence, version, revision and source address on every head.
/// </summary>
public sealed partial class AboutModel : ObservableModel
{
    /// <summary>The SPDX identifier of the licence.</summary>
    public const string LicenceId = "AGPL-3.0-or-later";

    /// <summary>The address of the licence text.</summary>
    public const string LicenceAddress = "https://www.gnu.org/licenses/agpl-3.0.html";

    /// <summary>The address of the source repository.</summary>
    public const string SourceAddress = "https://github.com/swcstudiospace/OmpOS";

    private const string DefaultReference = "main";

    private readonly string _platform;
    private string _status = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="AboutModel"/> class.</summary>
    /// <param name="version">The version of the build, without build metadata.</param>
    /// <param name="revision">The git commit the build was made from, or <see cref="AboutCopy.Unknown"/>.</param>
    /// <param name="platform">A line that names the operating system and runtime, or an empty string to leave it out of the copied text.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public AboutModel(string version, string revision, string platform)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(platform);
        Version = version;
        Revision = revision;
        _platform = platform;

        var reference = string.Equals(revision, AboutCopy.Unknown, StringComparison.Ordinal) ? DefaultReference : revision;
        var links = new List<AboutLink>
        {
            new(AboutCopy.SourceLinkLabel, SourceAddress, AboutCopy.OpenInBrowser(AboutCopy.SourceLinkLabel)),
            new(AboutCopy.LicenceLinkLabel, LicenceAddress, AboutCopy.OpenInBrowser(AboutCopy.LicenceLinkLabel)),
        };
        foreach (var file in AboutCopy.NoticeFiles)
        {
            links.Add(new AboutLink(file, SourceAddress + "/blob/" + reference + "/" + file, AboutCopy.OpenInBrowser(file)));
        }

        Links = links;
    }

    /// <summary>Gets the product name.</summary>
    public string ProductName { get; } = AboutCopy.ProductName;

    /// <summary>Gets the version of the build, without build metadata.</summary>
    public string Version { get; }

    /// <summary>Gets the git commit the build was made from, lower-case, or <c>unknown</c> when the build did not record one.</summary>
    public string Revision { get; }

    /// <summary>Gets the SPDX identifier of the licence.</summary>
    public string Licence { get; } = LicenceId;

    /// <summary>Gets the address of the licence text.</summary>
    public string LicenceLocation { get; } = LicenceAddress;

    /// <summary>Gets the address of the source repository.</summary>
    public string SourceLocation { get; } = SourceAddress;

    /// <summary>Gets the file names of the notices that travel with the build.</summary>
    public IReadOnlyList<string> NoticeFiles { get; } = AboutCopy.NoticeFiles;

    /// <summary>Gets the sentence that offers the source of this build.</summary>
    public string SourceOffer { get; } = AboutCopy.SourceOffer;

    /// <summary>Gets the addresses the window offers: the source tree, the licence text and each notice file at the build's revision.</summary>
    public IReadOnlyList<AboutLink> Links { get; }

    /// <summary>Gets the announcement of the latest copy or of an address that could not be opened, or an empty string before either.</summary>
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    /// <summary>Gets the text "Copy version info" puts on the clipboard.</summary>
    public string VersionInfoText
    {
        get
        {
            var text = new StringBuilder();
            text.Append(ProductName).Append(' ').Append(Version).Append('\n')
                .Append(AboutCopy.RevisionLabel).Append(": ").Append(Revision).Append('\n')
                .Append(AboutCopy.LicenceLabel).Append(": ").Append(Licence).Append(" (").Append(LicenceLocation).Append(")\n")
                .Append(AboutCopy.SourceLabel).Append(": ").Append(SourceLocation);
            if (_platform.Length > 0)
            {
                text.Append('\n').Append(_platform);
            }

            return text.ToString();
        }
    }

    /// <summary>Creates the model of a build from its informational version.</summary>
    /// <param name="informationalVersion">The assembly informational version, for example <c>0.15.0+0123abc</c>, or null.</param>
    /// <param name="platform">A line that names the operating system and runtime, or an empty string.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="platform"/> is null.</exception>
    public static AboutModel Create(string? informationalVersion, string platform) =>
        new(VersionOf(informationalVersion), RevisionOf(informationalVersion), platform);

    /// <summary>Creates the model of the build an assembly belongs to.</summary>
    /// <param name="assembly">The assembly whose informational version names the build.</param>
    /// <param name="platform">A line that names the operating system and runtime, or an empty string.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static AboutModel ForAssembly(Assembly assembly, string platform)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return Create(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, platform);
    }

    /// <summary>Takes the version out of an informational version: everything before the build metadata that follows the first <c>+</c>.</summary>
    /// <param name="informationalVersion">The informational version, or null.</param>
    /// <returns>The version, or <c>unknown</c> when there is none.</returns>
    public static string VersionOf(string? informationalVersion)
    {
        var text = informationalVersion?.Trim() ?? string.Empty;
        var plus = text.IndexOf('+', StringComparison.Ordinal);
        var version = (plus >= 0 ? text[..plus] : text).Trim();
        return version.Length == 0 ? AboutCopy.Unknown : version;
    }

    /// <summary>
    /// Takes the revision out of an informational version: the first dot-separated part of the build metadata, when it is a hexadecimal object name of 7 to 64 digits
    /// (the rule of the API's <c>GET /source</c>).
    /// </summary>
    /// <param name="informationalVersion">The informational version, or null.</param>
    /// <returns>The revision in lower case, or <c>unknown</c> when the version carries none.</returns>
    public static string RevisionOf(string? informationalVersion)
    {
        var text = informationalVersion?.Trim() ?? string.Empty;
        var plus = text.IndexOf('+', StringComparison.Ordinal);
        if (plus < 0)
        {
            return AboutCopy.Unknown;
        }

        var metadata = text[(plus + 1)..];
        var dot = metadata.IndexOf('.', StringComparison.Ordinal);
        var candidate = dot >= 0 ? metadata[..dot] : metadata;
        return ObjectName().IsMatch(candidate) ? new string([.. candidate.Select(char.ToLowerInvariant)]) : AboutCopy.Unknown;
    }

    /// <summary>Puts <see cref="VersionInfoText"/> on a clipboard and announces how it went.</summary>
    /// <param name="clipboard">Where the text goes.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><see langword="true"/> when the text was copied; <see langword="false"/> when the clipboard refused it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="clipboard"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The write was cancelled.</exception>
    public async Task<bool> CopyVersionInfoAsync(IAboutClipboard clipboard, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clipboard);

        // Clearing first makes the result a change of the live region even when it repeats the last one.
        Status = string.Empty;
        try
        {
            await clipboard.SetTextAsync(VersionInfoText, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            Status = AboutCopy.CopyFailed;
            return false;
        }

        Status = AboutCopy.Copied;
        return true;
    }

    /// <summary>Announces that an address could not be opened, so a user who cannot reach the browser knows to use the copy button instead.</summary>
    /// <param name="link">The link that failed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="link"/> is null.</exception>
    public void ReportOpenFailed(AboutLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        Status = string.Empty;
        Status = AboutCopy.OpenFailed(link.Label);
    }

    [GeneratedRegex("^[0-9a-fA-F]{7,64}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ObjectName();
}
