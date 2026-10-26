namespace OmpOS.Presentation.Composer;

/// <summary>
/// Answers what a provider supports, for the composer's model selection. Presentation cannot reference the provider harness
/// (it sits in the same layer), so a head implements this interface over its provider catalogue.
/// </summary>
public interface IProviderCapabilitySource
{
    /// <summary>Finds the capability record of one provider.</summary>
    /// <param name="providerId">The provider identifier. Matching is ordinal.</param>
    /// <returns>The record, or <see langword="null"/> when the source does not know the provider.</returns>
    ProviderCapability? Find(string providerId);
}
