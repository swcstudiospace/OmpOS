namespace OmpOS.Harness.OmpConfig;

/// <summary>The top-level object of <c>omp models --json</c>: <c>{"models":[...]}</c>. Members omp adds later are ignored.</summary>
internal sealed class OmpModelsDocument
{
    /// <summary>Gets or sets the model rows.</summary>
    public List<OmpModelRow>? Models { get; set; }
}
