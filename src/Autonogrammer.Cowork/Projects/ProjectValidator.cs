using System.Globalization;
using System.Text;

namespace Autonogrammer.Cowork.Projects;

/// <summary>D-03 limits for a Cowork project. The agent never writes instructions through this type.</summary>
public static class ProjectValidator
{
    /// <summary>The shortest name, in characters.</summary>
    public const int NameMinChars = 1;

    /// <summary>The longest name, in characters.</summary>
    public const int NameMaxChars = 100;

    /// <summary>The longest description, in bytes.</summary>
    public const int DescriptionMaxBytes = 16 * 1024;

    /// <summary>The longest instructions, in bytes. 16 KiB.</summary>
    public const int InstructionsMaxBytes = 16 * 1024;

    /// <summary>The most https context links.</summary>
    public const int ContextUrlMaxCount = 20;

    /// <summary>The most projects on one account.</summary>
    public const int MaxProjectsPerAccount = 200;

    /// <summary>Validates one project. <see cref="CoworkProject.UseAccountMemory"/> defaults to false on the record.</summary>
    /// <param name="project">The project.</param>
    /// <returns>The errors. Empty means the project is valid.</returns>
    public static IReadOnlyList<string> Validate(CoworkProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var errors = new List<string>();
        var nameLength = project.Name?.Length ?? 0;
        if (string.IsNullOrWhiteSpace(project.Name) || nameLength < NameMinChars || nameLength > NameMaxChars)
        {
            errors.Add(string.Create(CultureInfo.InvariantCulture, $"Name must be {NameMinChars} to {NameMaxChars} characters."));
        }

        if (Utf8Length(project.Description) > DescriptionMaxBytes)
        {
            errors.Add(string.Create(CultureInfo.InvariantCulture, $"Description must be at most {DescriptionMaxBytes} bytes."));
        }

        if (Utf8Length(project.Instructions) > InstructionsMaxBytes)
        {
            errors.Add(string.Create(CultureInfo.InvariantCulture, $"Instructions must be at most {InstructionsMaxBytes} bytes (16 KiB)."));
        }

        var urls = project.ContextUrls ?? [];
        if (urls.Count > ContextUrlMaxCount)
        {
            errors.Add(string.Create(CultureInfo.InvariantCulture, $"Context URLs must be at most {ContextUrlMaxCount} https links."));
        }

        foreach (var url in urls)
        {
            if (!IsHttps(url))
            {
                errors.Add("Context URLs must be https links.");
                break;
            }
        }

        return errors;
    }

    /// <summary>Validates how many projects one account may hold.</summary>
    /// <param name="count">The count.</param>
    /// <returns>The errors.</returns>
    public static IReadOnlyList<string> ValidateCount(int count)
    {
        if (count > MaxProjectsPerAccount)
        {
            return [string.Create(CultureInfo.InvariantCulture, $"At most {MaxProjectsPerAccount} projects per account.")];
        }

        return [];
    }

    private static bool IsHttps(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed) && string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);

    private static int Utf8Length(string? value) => value is null ? 0 : Encoding.UTF8.GetByteCount(value);
}
