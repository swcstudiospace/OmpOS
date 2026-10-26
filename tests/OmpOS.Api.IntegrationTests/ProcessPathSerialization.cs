using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>Collection for tests that mutate the process PATH. Its tests never run in parallel with another collection.</summary>
[CollectionDefinition(ProcessPathSerialization.Name, DisableParallelization = true)]
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "xUnit1027 requires collection definition classes to be public.")]
public static class ProcessPathSerialization
{
    /// <summary>The collection name.</summary>
    public const string Name = "OmpOS.Api.ProcessPath";
}
