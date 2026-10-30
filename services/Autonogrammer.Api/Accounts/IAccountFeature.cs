namespace Autonogrammer.Api.Accounts;

/// <summary>A module that the account menu may show. The key is the only thing the account response lists.</summary>
internal interface IAccountFeature
{
    /// <summary>Gets the module key, such as <c>cowork</c> or <c>dispatch</c>.</summary>
    string Key { get; }

    /// <summary>The Cowork feature. Dispatch registers its own feature in phase 74.</summary>
    internal sealed class Cowork : IAccountFeature
    {
        /// <inheritdoc />
        public string Key => "cowork";
    }
}
