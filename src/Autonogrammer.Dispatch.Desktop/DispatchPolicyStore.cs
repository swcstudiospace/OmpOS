using System.Text.Json;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>The durable desktop dispatch policy. A missing file means the defaults, every capability off.</summary>
public sealed class DispatchPolicyStore
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private DispatchPolicy _current;

    /// <summary>Opens the store.</summary>
    /// <param name="path">The state file.</param>
    /// <exception cref="InvalidDataException">The file exists but is not a policy document. It is never reset silently.</exception>
    public DispatchPolicyStore(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _current = Load(_path);
    }

    /// <summary>Gets the policy in force.</summary>
    public DispatchPolicy Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <summary>Replaces the policy and persists it. The change takes effect for the next command.</summary>
    /// <param name="policy">The new policy.</param>
    public void Save(DispatchPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        lock (_gate)
        {
            DispatchStateFile.WriteAtomic(_path, JsonSerializer.Serialize(policy, DispatchDesktopJson.Default.DispatchPolicy));
            _current = policy;
        }
    }

    private static DispatchPolicy Load(string path)
    {
        var text = DispatchStateFile.ReadTextOrNull(path);
        if (text is null)
        {
            return new DispatchPolicy();
        }

        try
        {
            return JsonSerializer.Deserialize(text, DispatchDesktopJson.Default.DispatchPolicy)
                ?? throw new InvalidDataException("The dispatch policy file is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The dispatch policy file is not valid JSON.", ex);
        }
    }
}
