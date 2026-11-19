using System.Collections.ObjectModel;
using Autonogrammer.Contracts.Environments;

namespace Autonogrammer.Presentation.Remote;

/// <summary>The one place where an environment editor turns the validator's issues of a candidate into the findings it shows.</summary>
internal static class RemoteFieldIssues
{
    /// <summary>Appends the validator's issues of the candidate, one finding per editor field. A field that already has a finding keeps it.</summary>
    /// <param name="found">The findings so far, which the validator's issues are appended to.</param>
    /// <param name="candidate">The environment as the editor would save it.</param>
    /// <param name="fieldOf">Maps a validator property path to the editor field that must change.</param>
    public static void AppendValidation(List<RemoteFieldIssue> found, EnvironmentProfile candidate, Func<string, string> fieldOf)
    {
        var reported = new HashSet<string>(found.Select(issue => issue.Field), StringComparer.Ordinal);
        foreach (var issue in EnvironmentProfileValidator.Validate(candidate))
        {
            var field = fieldOf(issue.Property);
            if (reported.Add(field))
            {
                found.Add(new RemoteFieldIssue(field, issue.Message));
            }
        }
    }

    /// <summary>Replaces the findings the editor shows with the given ones.</summary>
    /// <param name="shown">The findings the editor shows.</param>
    /// <param name="found">The findings to show.</param>
    public static void Replace(ObservableCollection<RemoteFieldIssue> shown, IReadOnlyList<RemoteFieldIssue> found)
    {
        shown.Clear();
        foreach (var issue in found)
        {
            shown.Add(issue);
        }
    }
}
