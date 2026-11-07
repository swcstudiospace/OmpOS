using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Sessions;

namespace Autonogrammer.Api.Services;

/// <summary>
/// The only reader of the session list for the Kanban board, the Code session catalog and the catalog change scan.
/// Every list it makes asks for <see cref="SessionSurface.Code"/>, so a Cowork row cannot become a board card.
/// </summary>
internal sealed class CodeBoardSource
{
    /// <summary>The page size of one board read. The repository never returns more than this.</summary>
    public const int PageSize = 200;

    private readonly ISessionRepository _repository;

    /// <summary>Initializes a new instance of the <see cref="CodeBoardSource"/> class.</summary>
    /// <param name="repository">The session store.</param>
    public CodeBoardSource(ISessionRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Lists one owner's Code sessions, oldest first, including closed ones. Another owner's rows are never returned.</summary>
    /// <param name="ownerId">The owner identifier.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The Code sessions. Empty when the owner has none.</returns>
    public async Task<IReadOnlyList<Session>> ListAsync(string ownerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        var page = await _repository.ListByOwnerAsync(ownerId, SessionSurface.Code, includeClosed: true, PageSize, cursor: null, cancellationToken).ConfigureAwait(false);
        var found = new List<Session>(page.Count);
        foreach (var header in page)
        {
            found.Add(await _repository.GetAsync(header.Id, cancellationToken).ConfigureAwait(false) ?? header);
        }

        found.Sort(static (left, right) => left.CreatedAt.CompareTo(right.CreatedAt));
        return found;
    }
}
