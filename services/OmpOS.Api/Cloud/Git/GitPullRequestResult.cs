namespace OmpOS.Api.Cloud;

/// <summary>A pull request the host created.</summary>
/// <param name="Number">The number.</param>
/// <param name="Url">The web URL.</param>
/// <param name="Draft">Whether it is a draft.</param>
internal sealed record GitPullRequestResult(int Number, Uri Url, bool Draft);
