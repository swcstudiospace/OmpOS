using System.ComponentModel.DataAnnotations;

namespace Autonogrammer.Api.Accounts;

/// <summary>The <c>Accounts</c> configuration section, including its availability and per-caller budget.</summary>
internal sealed class AccountsOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Accounts";

    /// <summary>The default number of account calls allowed per minute for one caller.</summary>
    public const int DefaultRequestsPerMinute = 120;

    /// <summary>Gets or sets a value indicating whether account routes are enabled. Defaults to false.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets how many account calls one caller may make per minute, 1 to 100000. Defaults to 120.</summary>
    [Range(1, 100_000, ErrorMessage = "Accounts:RequestsPerMinute must be between 1 and 100000.")]
    public int RequestsPerMinute { get; set; } = DefaultRequestsPerMinute;
}
