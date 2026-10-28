using OmpOS.Native.Cloud;
using OmpOS.Native.Views;

namespace OmpOS.Native.Tests.Cloud;

/// <summary>A cloud host wired to a scripted control plane. It owns the handler, the client and the host and disposes them together.</summary>
internal sealed class CloudHostRig : IDisposable
{
    /// <summary>The bearer token the rig's user is signed in with.</summary>
    public const string BearerToken = "token-for-the-tests";

    private readonly ScriptedCloudHandler _handler;
    private readonly HttpClient _http;

    /// <summary>Initializes a new instance of the <see cref="CloudHostRig"/> class.</summary>
    /// <param name="respond">Answers each request.</param>
    /// <param name="api">The API address, or null for an installation without one. Defaults to a test origin.</param>
    /// <param name="launcher">The browser launcher, or null.</param>
    /// <param name="clock">The clock of the poll, or null for the system clock.</param>
    /// <param name="signedIn">Whether the user has a token.</param>
    /// <param name="useDefaultApi">Whether a null <paramref name="api"/> means the default origin instead of none.</param>
    public CloudHostRig(
        Func<ScriptedCloudHandler.RecordedCall, HttpResponseMessage> respond,
        Uri? api = null,
        IUrlLauncher? launcher = null,
        TimeProvider? clock = null,
        bool signedIn = true,
        bool useDefaultApi = true)
    {
        _handler = new ScriptedCloudHandler(respond);
        _http = new HttpClient(_handler, disposeHandler: false);
        Host = new ApiCloudAgentsHost(
            _http,
            api ?? (useDefaultApi ? new Uri("https://api.example.test/", UriKind.Absolute) : null),
            clock ?? TimeProvider.System,
            signedIn ? static _ => ValueTask.FromResult<string?>(BearerToken) : null,
            launcher);
    }

    /// <summary>Gets the host under test.</summary>
    public ApiCloudAgentsHost Host { get; }

    /// <summary>Gets the requests the control plane saw, in arrival order.</summary>
    public IReadOnlyList<ScriptedCloudHandler.RecordedCall> Calls => _handler.Calls;

    /// <inheritdoc />
    public void Dispose()
    {
        Host.Dispose();
        _http.Dispose();
        _handler.Dispose();
    }
}
