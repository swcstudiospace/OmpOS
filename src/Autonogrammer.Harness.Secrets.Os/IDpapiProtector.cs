namespace Autonogrammer.Harness.Secrets.Os;

/// <summary>Encrypts bytes so only the current user can decrypt them. The Windows implementation is DPAPI; tests substitute a reversible fake.</summary>
internal interface IDpapiProtector
{
    /// <summary>Encrypts bytes for the current user.</summary>
    /// <param name="plain">The bytes to protect.</param>
    /// <returns>The protected bytes.</returns>
    /// <exception cref="PlatformNotSupportedException">The platform has no DPAPI.</exception>
    byte[] Protect(byte[] plain);

    /// <summary>Decrypts bytes protected by <see cref="Protect"/>.</summary>
    /// <param name="protectedBytes">The protected bytes.</param>
    /// <returns>The plain bytes.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">The bytes are damaged or were protected for another user.</exception>
    /// <exception cref="PlatformNotSupportedException">The platform has no DPAPI.</exception>
    byte[] Unprotect(byte[] protectedBytes);
}
