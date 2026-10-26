namespace OmpOS.Harness.OmpConfig;

/// <summary>
/// The model configuration of omp as one object for the head: the model listing, the credential store, the default model and the
/// <c>models.yml</c> editor, all built from one <see cref="OmpConfigOptions"/> and sharing one <see cref="OmpCommandRunner"/>.
/// </summary>
public sealed class OmpModelConfiguration
{
    /// <summary>Creates the configuration.</summary>
    /// <param name="options">The omp location, agent directory and limits.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public OmpModelConfiguration(OmpConfigOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
        Runner = new OmpCommandRunner(options);
        Catalog = new OmpModelCatalog(options, Runner);
        Credentials = new OmpCredentialStore(Runner);
        DefaultModel = new OmpDefaultModel(options, Runner);
        ModelsFile = new OmpModelsFile(options, Runner);
    }

    /// <summary>Gets the options every part was built from.</summary>
    public OmpConfigOptions Options { get; }

    /// <summary>Gets the runner every part uses to start omp.</summary>
    public OmpCommandRunner Runner { get; }

    /// <summary>Gets the model listing (<c>omp models --json</c>).</summary>
    public OmpModelCatalog Catalog { get; }

    /// <summary>Gets the credential presence check and the key import.</summary>
    public OmpCredentialStore Credentials { get; }

    /// <summary>Gets the default model (the <c>default</c> role of <c>modelRoles</c>).</summary>
    public OmpDefaultModel DefaultModel { get; }

    /// <summary>Gets the editor of the OmpOS blocks in <c>models.yml</c>.</summary>
    public OmpModelsFile ModelsFile { get; }
}
