using System.Text.Json;

namespace Autonogrammer.Harness.OmpConfig;

/// <summary>
/// Reads and sets omp's default model, which is the <c>default</c> role of the <c>modelRoles</c> setting. omp takes the whole
/// map as one JSON argument (<c>omp config set modelRoles '{...}'</c>; <c>modelRoles.default</c> cannot be set alone), so a
/// change reads the whole map, replaces the one role and writes the whole map back: the other roles survive.
/// </summary>
public sealed class OmpDefaultModel
{
    /// <summary>The longest selector accepted.</summary>
    public const int MaxSelectorLength = 256;

    // One writer at a time per process: read-modify-write of the map must not interleave. Static so the field needs no disposal.
    private static readonly SemaphoreSlim WriteGate = new(1, 1);

    private readonly OmpCommandRunner _runner;
    private readonly OmpModelCatalog _catalog;

    /// <summary>Creates the default model editor.</summary>
    /// <param name="options">The omp location and agent directory.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public OmpDefaultModel(OmpConfigOptions options)
        : this(options, new OmpCommandRunner(options))
    {
    }

    /// <summary>Creates the default model editor with a shared runner.</summary>
    /// <param name="options">The omp location and agent directory.</param>
    /// <param name="runner">The runner that starts omp.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public OmpDefaultModel(OmpConfigOptions options, OmpCommandRunner runner)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(runner);
        _runner = runner;
        _catalog = new OmpModelCatalog(options, runner);
    }

    /// <summary>Tells whether <paramref name="selector"/> has the shape <c>provider/model-id</c> this type accepts.</summary>
    /// <param name="selector">The selector; null is not valid.</param>
    /// <returns>True when the provider part is a valid provider id, the model part is not empty, there is no whitespace or control character and the whole is at most <see cref="MaxSelectorLength"/> characters.</returns>
    public static bool IsValidSelector(string? selector)
    {
        if (string.IsNullOrEmpty(selector) || selector.Length > MaxSelectorLength)
        {
            return false;
        }

        var slash = selector.IndexOf('/', StringComparison.Ordinal);
        if (slash <= 0 || slash == selector.Length - 1 || !OmpProviderId.IsValid(selector[..slash]))
        {
            return false;
        }

        foreach (var character in selector)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads the <c>modelRoles</c> setting with <c>omp config get modelRoles</c>.</summary>
    /// <param name="cancellationToken">Cancels the read; a cancelled read throws.</param>
    /// <returns>The roles, or an <see cref="OmpModelRoles"/> whose <see cref="OmpModelRoles.Problem"/> says why they could not be read.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<OmpModelRoles> GetAsync(CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(new OmpCommandRequest(["config", "get", "modelRoles"]), cancellationToken).ConfigureAwait(false);
        if (result.StartFailure is not null)
        {
            return Unreadable(result.StartFailure);
        }

        if (result.TimedOut)
        {
            return Unreadable("omp did not answer within the time limit.");
        }

        if (result.ExitCode != 0)
        {
            return Unreadable($"omp config get modelRoles exited with code {result.ExitCode}.");
        }

        var text = result.StandardOutput.Trim();
        if (text.Length == 0)
        {
            return new OmpModelRoles(new Dictionary<string, string>(StringComparer.Ordinal), null, null);
        }

        Dictionary<string, string>? roles;
        try
        {
            roles = JsonSerializer.Deserialize(text, OmpConfigJsonContext.Default.DictionaryStringString);
        }
        catch (JsonException)
        {
            return Unreadable("omp printed model roles that are not a JSON object of selectors.");
        }

        if (roles is null)
        {
            return Unreadable("omp printed no model roles.");
        }

        return new OmpModelRoles(roles, roles.TryGetValue(OmpModelRoles.DefaultRole, out var defaultSelector) ? defaultSelector : null, null);
    }

    /// <summary>
    /// Sets the <c>default</c> role to <paramref name="selector"/> and leaves every other role as it was. The selector must have
    /// the <c>provider/model-id</c> shape and, when omp's listing is complete, appear in it; otherwise nothing is written. After
    /// the write the setting is read back to confirm it.
    /// </summary>
    /// <param name="selector">The <c>provider/model-id</c> selector.</param>
    /// <param name="cancellationToken">Cancels the change; a cancelled change throws.</param>
    /// <returns>What happened.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<OmpDefaultModelResult> SetDefaultAsync(string selector, CancellationToken cancellationToken)
    {
        if (!IsValidSelector(selector))
        {
            return new OmpDefaultModelResult(
                OmpDefaultModelStatus.InvalidSelector,
                "The model selector must look like provider/model-id, without spaces, at most 256 characters.");
        }

        await WriteGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var listing = await _catalog.ListAsync(cancellationToken).ConfigureAwait(false);
            if (listing.IsComplete && !listing.Models.Any(model => string.Equals(model.Selector, selector, StringComparison.Ordinal)))
            {
                return new OmpDefaultModelResult(
                    OmpDefaultModelStatus.UnknownModel,
                    $"omp does not list {selector}. omp lists only the models of providers it has a credential for.");
            }

            var current = await GetAsync(cancellationToken).ConfigureAwait(false);
            if (!current.WasRead)
            {
                return new OmpDefaultModelResult(OmpDefaultModelStatus.Failed, $"The current model roles could not be read: {current.Problem}");
            }

            var roles = new Dictionary<string, string>(current.Roles, StringComparer.Ordinal)
            {
                [OmpModelRoles.DefaultRole] = selector,
            };
            var json = JsonSerializer.Serialize(roles, OmpConfigJsonContext.Default.DictionaryStringString);
            var write = await _runner.RunAsync(new OmpCommandRequest(["config", "set", "modelRoles", json]), cancellationToken).ConfigureAwait(false);
            if (write.StartFailure is not null || write.TimedOut || write.ExitCode != 0)
            {
                return new OmpDefaultModelResult(OmpDefaultModelStatus.Failed, "omp did not accept the new default model.");
            }

            var confirmed = await GetAsync(cancellationToken).ConfigureAwait(false);
            return confirmed.WasRead && string.Equals(confirmed.DefaultSelector, selector, StringComparison.Ordinal)
                ? new OmpDefaultModelResult(OmpDefaultModelStatus.Set, $"The default model is now {selector}.")
                : new OmpDefaultModelResult(OmpDefaultModelStatus.Failed, "omp accepted the change but does not report the new default model.");
        }
        finally
        {
            _ = WriteGate.Release();
        }
    }

    private static OmpModelRoles Unreadable(string problem) =>
        new(new Dictionary<string, string>(StringComparer.Ordinal), null, problem);
}
