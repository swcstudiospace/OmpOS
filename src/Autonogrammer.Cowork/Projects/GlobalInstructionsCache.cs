namespace Autonogrammer.Cowork.Projects;

/// <summary>
/// The last global instructions the account API returned, kept in the account's own folder. A start while offline serves this same
/// copy read-only (D-06, D-07). The file never leaves the device and never names another account.
/// </summary>
public sealed class GlobalInstructionsCache
{
    /// <summary>The file name inside the account folder.</summary>
    public const string FileName = "global-instructions.json";

    /// <summary>The largest file that is read or written. The instructions cap is 8 KiB, so this is far above any valid document.</summary>
    public const int MaxFileBytes = 64 * 1024;

    private readonly CoworkJsonFile<GlobalInstructionsDocument> _file;

    /// <summary>Initializes a new instance of the <see cref="GlobalInstructionsCache"/> class.</summary>
    /// <param name="stateRoot">The state directory.</param>
    /// <param name="accountFolderKey">The account folder key.</param>
    public GlobalInstructionsCache(string stateRoot, string accountFolderKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountFolderKey);
        _file = new CoworkJsonFile<GlobalInstructionsDocument>(
            Path.Combine(CoworkAccountFolder.AccountDirectory(stateRoot, accountFolderKey), FileName),
            MaxFileBytes,
            static () => new GlobalInstructionsDocument(),
            GlobalInstructionsJsonContext.Default.GlobalInstructionsDocument);
    }

    /// <summary>Gets the full path of the file.</summary>
    public string FilePath => _file.FilePath;

    /// <summary>Reads the cached instructions. Returns null when none are stored or the file cannot be read.</summary>
    /// <returns>The instructions, or null.</returns>
    public string? Read()
    {
        try
        {
            return _file.Read().Instructions;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>Replaces the cached instructions. The cache is best effort: a write that fails leaves the in-memory copy in place.</summary>
    /// <param name="instructions">The instructions the API returned, or null.</param>
    /// <returns>True when the file was written.</returns>
    public bool Write(string? instructions)
    {
        try
        {
            _file.Update(document => document.Instructions = instructions);
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
