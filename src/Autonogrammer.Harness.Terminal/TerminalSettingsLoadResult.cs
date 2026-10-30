using System.Diagnostics.CodeAnalysis;

namespace Autonogrammer.Harness.Terminal;

/// <summary>
/// The outcome of loading the terminal settings file: the settings to use and, when the file was unusable, why.
/// </summary>
/// <remarks>
/// An unusable file never enables auto-run: <see cref="Settings"/> is then the default settings, and
/// <see cref="Problem"/> says what was wrong so a head can show it.
/// </remarks>
/// <param name="Settings">The settings to use. Auto-run is on only when the file explicitly opted in.</param>
/// <param name="Problem">The reason the file was unusable, or <see langword="null"/> when it was absent or valid.</param>
public sealed record TerminalSettingsLoadResult(TerminalSettings Settings, string? Problem)
{
    /// <summary>Gets a value indicating whether the file was unusable and the defaults were used instead.</summary>
    [MemberNotNullWhen(true, nameof(Problem))]
    public bool HasProblem => Problem is not null;
}
