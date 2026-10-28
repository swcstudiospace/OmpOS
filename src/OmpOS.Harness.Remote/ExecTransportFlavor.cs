using OmpOS.Contracts.Environments;
using OmpOS.Harness.Remote.Containers;

namespace OmpOS.Harness.Remote;

/// <summary>
/// What differs between the client programs that carry an ACP session through <see cref="SshAcpTransport"/>: the name that appears in messages and exception
/// contexts, the diagnosis of a client that cannot be started, and the rules that turn an exit code and stderr text into a diagnosis. The process runner, the
/// framing, the preamble and the noise handling are the same for ssh, podman and wsl.exe.
/// </summary>
internal sealed class ExecTransportFlavor
{
    private ExecTransportFlavor(string name, Func<RemoteDiagnosis> missingClient, Func<int, string, RemoteDiagnosis?> fromFailure)
    {
        Name = name;
        MissingClient = missingClient;
        FromFailure = fromFailure;
    }

    /// <summary>Gets the flavor of the ssh client.</summary>
    public static ExecTransportFlavor Ssh { get; } = new(
        "ssh",
        () => RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.SshMissing),
        RemoteDiagnosisCatalog.FromFailure);

    /// <summary>Gets the flavor of the podman client.</summary>
    public static ExecTransportFlavor Podman { get; } = new(
        "podman",
        () => RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.ContainerRuntimeMissing),
        ContainerDiagnosisCatalog.FromPodmanFailure);

    /// <summary>Gets the flavor of the wsl.exe client.</summary>
    public static ExecTransportFlavor Wsl { get; } = new(
        "wsl",
        () => RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.WslMissing),
        ContainerDiagnosisCatalog.FromWslFailure);

    /// <summary>Gets the name of the client, as it appears in messages: <c>ssh</c>, <c>podman</c> or <c>wsl</c>.</summary>
    public string Name { get; }

    /// <summary>Gets the diagnosis of a client program that cannot be started.</summary>
    public Func<RemoteDiagnosis> MissingClient { get; }

    /// <summary>Gets the rule that explains a failed run from its exit code and stderr text; it returns <see langword="null"/> when the failure is not recognised.</summary>
    public Func<int, string, RemoteDiagnosis?> FromFailure { get; }
}
