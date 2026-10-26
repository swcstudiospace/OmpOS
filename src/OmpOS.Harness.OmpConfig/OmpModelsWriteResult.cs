namespace OmpOS.Harness.OmpConfig;

/// <summary>The result of <see cref="OmpModelsFile.UpsertAsync"/> or <see cref="OmpModelsFile.RemoveAsync"/>.</summary>
/// <param name="Status">What happened.</param>
/// <param name="Message">A plain sentence for the user. It contains no secret and no path outside the agent directory.</param>
public sealed record OmpModelsWriteResult(OmpModelsWriteStatus Status, string Message)
{
    /// <summary>Gets a value indicating whether <c>models.yml</c> now says what was asked (written or already so).</summary>
    public bool IsSatisfied => Status is OmpModelsWriteStatus.Written or OmpModelsWriteStatus.Unchanged;
}
