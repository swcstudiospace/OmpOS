using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>Validates render-ticket keys only when artifacts are enabled. Failures name the key, never the value.</summary>
internal sealed class ArtifactApiOptionsValidator : IValidateOptions<ArtifactApiOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ArtifactApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (options.RenderTicketKeys.Count != 2 || options.RenderTicketKeys.Any(static key => ArtifactRenderTickets.KeyBytes(key) is null))
        {
            return ValidateOptionsResult.Fail(ArtifactApiOptions.SectionName + ":RenderTicketKeys is invalid.");
        }

        return ValidateOptionsResult.Success;
    }
}
