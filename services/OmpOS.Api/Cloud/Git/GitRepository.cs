namespace OmpOS.Api.Cloud;

/// <summary>A repository on a git host, parsed from a clone URL.</summary>
/// <param name="Host">The host name, lower case.</param>
/// <param name="Owner">The owner or organization.</param>
/// <param name="Name">The repository name without a <c>.git</c> suffix.</param>
internal sealed record GitRepository(string Host, string Owner, string Name)
{
    /// <summary>Gets <c>owner/name</c>.</summary>
    public string FullName => Owner + "/" + Name;

    /// <summary>Gets the canonical HTTPS clone URL, without a credential.</summary>
    public string CloneUrl => "https://" + Host + "/" + Owner + "/" + Name;

    /// <summary>Gets a directory name for the checkout: the repository name.</summary>
    public string DirectoryName => Name;
}
