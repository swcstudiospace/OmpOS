using System.Text;
using Autonogrammer.Contracts.WorkspaceSettings;

namespace Autonogrammer.Api.Services.WorkspaceSettings;

/// <summary>
/// Reads one text file of the workspace. The file must stay inside the workspace folder after every link is resolved, must not have a secret name, must
/// not be larger than <see cref="MaxFileBytes"/> and must be UTF-8 text: a binary file is refused.
/// </summary>
internal sealed class WorkspaceFileReader
{
    /// <summary>The largest file the endpoint returns: 1 MiB.</summary>
    public const int MaxFileBytes = 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly WorkspaceAccess _access;

    /// <summary>Initializes a new instance of the <see cref="WorkspaceFileReader"/> class.</summary>
    /// <param name="access">What the endpoints may read.</param>
    public WorkspaceFileReader(WorkspaceAccess access)
    {
        _access = access;
    }

    /// <summary>Reads a file.</summary>
    /// <param name="path">The workspace-relative path of the file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text or the problem to answer with.</returns>
    public async Task<WorkspaceOutcome<WorkspaceFileResponse>> ReadAsync(string? path, CancellationToken cancellationToken)
    {
        if (_access.ExistingRoot is not { } root)
        {
            return WorkspaceOutcome<WorkspaceFileResponse>.Failure(WorkspaceTreeReader.NotConfigured());
        }

        var resolved = WorkspacePathResolver.Resolve(root, path, allowEmpty: false);
        if (resolved.Problem is not null || resolved.Value is not { } target)
        {
            return WorkspaceOutcome<WorkspaceFileResponse>.Failure(resolved.Problem!);
        }

        try
        {
            if (!File.Exists(target.Real))
            {
                return WorkspacePathResolver.NotFound<WorkspaceFileResponse>();
            }

            var size = new FileInfo(target.Real).Length;
            if (size > MaxFileBytes)
            {
                return TooLarge();
            }

            var buffer = new byte[(int)size + 1];
            var read = 0;
            using (var stream = new FileStream(target.Real, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                int count;
                while (read < buffer.Length && (count = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    read += count;
                }
            }

            if (read > MaxFileBytes)
            {
                return TooLarge();
            }

            return Decode(target.Relative, buffer, read);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return WorkspacePathResolver.NotFound<WorkspaceFileResponse>();
        }
    }

    private static WorkspaceOutcome<WorkspaceFileResponse> TooLarge() =>
        WorkspaceOutcome<WorkspaceFileResponse>.Failure(WebProblems.Of(
            StatusCodes.Status413PayloadTooLarge,
            "file_too_large",
            "File too large.",
            "The file is larger than the 1 MiB the workspace file endpoint returns."));

    private static WorkspaceOutcome<WorkspaceFileResponse> Decode(string relative, byte[] buffer, int length)
    {
        var binary = WorkspaceOutcome<WorkspaceFileResponse>.Failure(WebProblems.Of(
            StatusCodes.Status415UnsupportedMediaType,
            "binary_file",
            "Binary file.",
            "The workspace file endpoint returns UTF-8 text only."));
        if (Array.IndexOf(buffer, (byte)0, 0, length) >= 0)
        {
            return binary;
        }

        var offset = length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF ? 3 : 0;
        try
        {
            var text = StrictUtf8.GetString(buffer, offset, length - offset);
            return WorkspaceOutcome<WorkspaceFileResponse>.Success(new WorkspaceFileResponse { Path = relative, Size = length, Content = text });
        }
        catch (DecoderFallbackException)
        {
            return binary;
        }
    }
}
