namespace Autonogrammer.Accounts;

/// <summary>
/// How far a sign-out reaches. There is deliberately no default: the service default is <see cref="Global"/>, which also signs the website out.
/// </summary>
public enum SignOutScope
{
    /// <summary>Ends this session only.</summary>
    Local,

    /// <summary>Ends every session of the user, including the website.</summary>
    Global,

    /// <summary>Ends every session of the user except this one.</summary>
    Others,
}
