using Autonogrammer.Cowork;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Native.Settings.Models;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Native.Cowork;

/// <summary>Seams for <see cref="CoworkSessionHost"/>.</summary>
internal sealed class CoworkSessionHostOptions
{
    /// <summary>Gets the state root.</summary>
    public required string StateRoot { get; init; }

    /// <summary>Gets the working directory.</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>Gets the account gate.</summary>
    public required ICoworkAccountGate Gate { get; init; }

    /// <summary>Gets the clock.</summary>
    public TimeProvider Clock { get; init; } = TimeProvider.System;

    /// <summary>Gets the transport factory.</summary>
    public Func<AcpClientOptions, IAcpTransport>? TransportFactory { get; init; }

    /// <summary>Gets the provider keys, or null.</summary>
    public ProviderKeyEnvironment? ProviderKeys { get; init; }

    /// <summary>Gets the catalog, or null when the head has no services.</summary>
    public Autonogrammer.Harness.Mcp.Catalog.McpServerCatalog? Catalog { get; init; }

    /// <summary>Gets the tool server, or null.</summary>
    public CoworkToolServer? Tools { get; init; }

    /// <summary>Gets the logger factory.</summary>
    public ILoggerFactory? LoggerFactory { get; init; }

    /// <summary>Gets the omp path.</summary>
    public string? OmpPath { get; init; }

    /// <summary>Gets the resolver of the omp each session open runs, or null to run <see cref="OmpPath"/> as given.</summary>
    public Func<CancellationToken, Task<string?>>? ResolveOmpPath { get; init; }

    /// <summary>Gets whether the session is remote.</summary>
    public bool Remote { get; init; }

    /// <summary>Gets whether a window can show an ask.</summary>
    public bool WindowAvailable { get; init; } = true;

    /// <summary>Gets the tool host options.</summary>
    public Autonogrammer.Cowork.Tools.CoworkToolHostOptions? ToolOptions { get; init; }
}
