namespace Autonogrammer.Cowork.Artifacts.Model;

/// <summary>
/// One write-once version. Numbers start at 1 and increase by exactly 1. The hash is lowercase hex SHA-256 of
/// <paramref name="Content"/>.
/// </summary>
/// <param name="ArtifactId">Owning artifact identifier.</param>
/// <param name="Number">Version number from 1.</param>
/// <param name="Content">Stored bytes. Never truncated.</param>
/// <param name="ContentSha256">Lowercase hex SHA-256 of <paramref name="Content"/>.</param>
/// <param name="ParentNumber">Previous version number, or null for version 1.</param>
/// <param name="ParentContentSha256">Previous version hash, or null for version 1.</param>
/// <param name="Author">Host-set author.</param>
/// <param name="CreatedAt">Host clock.</param>
/// <param name="ChangeNote">Optional note of at most 280 characters.</param>
public sealed record ArtifactVersionRecord(
    string ArtifactId,
    int Number,
    ReadOnlyMemory<byte> Content,
    string ContentSha256,
    int? ParentNumber,
    string? ParentContentSha256,
    ArtifactAuthor Author,
    DateTimeOffset CreatedAt,
    string? ChangeNote);
