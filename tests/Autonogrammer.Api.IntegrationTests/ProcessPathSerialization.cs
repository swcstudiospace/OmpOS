using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>Collection for tests that mutate process-wide state: the PATH, and the environment every host reads (the OTLP exporter variables). Its tests never run in parallel with another collection.</summary>
[CollectionDefinition(ProcessPathSerialization.Name, DisableParallelization = true)]
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "xUnit1027 requires collection definition classes to be public.")]
public static class ProcessPathSerialization
{
    /// <summary>The collection name.</summary>
    public const string Name = "Autonogrammer.Api.ProcessPath";
}
