using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Autonogrammer.Sessions.Postgres.Tests;

/// <summary>Shares one <see cref="PostgresServerFixture"/> across every test class in the PostgreSQL test collection.</summary>
[CollectionDefinition(Name)]
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "xUnit1027 requires collection definition classes to be public; xUnit does not discover an internal definition, so its fixture would never be created.")]
public sealed class PostgresServerGroup : ICollectionFixture<PostgresServerFixture>
{
    /// <summary>The collection name.</summary>
    public const string Name = "PostgreSQL server";
}
