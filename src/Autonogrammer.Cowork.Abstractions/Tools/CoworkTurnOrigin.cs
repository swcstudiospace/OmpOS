namespace Autonogrammer.Cowork.Abstractions;

/// <summary>Who started the turn. Set on the lease by the chat core, never by the model.</summary>
public enum CoworkTurnOrigin
{
    /// <summary>The person at this machine started the turn.</summary>
    Local = 0,

    /// <summary>A dispatch from another device started the turn.</summary>
    Dispatch = 1,
}
