namespace OmpOS.Harness.Providers;

/// <summary>What a probe of a local provider endpoint found.</summary>
public enum ProviderProbeStatus
{
    /// <summary>Nothing answered at the endpoint: the connection was refused, failed or timed out.</summary>
    NotRunning = 0,

    /// <summary>The server answered the model-list request with an OpenAI-style model list.</summary>
    Running,

    /// <summary>The server answered but refused the unauthenticated request, so it is running and needs a key.</summary>
    RunningAuthRequired,

    /// <summary>Something answered, but not with an OpenAI-style model list. It may be another service on the port.</summary>
    UnexpectedResponse,
}
