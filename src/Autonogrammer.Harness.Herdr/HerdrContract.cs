namespace Autonogrammer.Harness.Herdr;

/// <summary>
/// Versioning for the herdr session contract. A consumer built against one version must refuse another.
/// </summary>
public static class HerdrContract
{
    /// <summary>
    /// The version of <see cref="IHerdrSessionClient"/> and of the records it exchanges. Any change to a member or to a
    /// record's meaning bumps this value.
    /// </summary>
    public const int ContractVersion = 1;
}
