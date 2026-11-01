using System.Security.Cryptography;
using System.Text;

namespace Autonogrammer.Harness.Secrets.Os;

/// <summary>
/// <see cref="IDpapiProtector"/> over <see cref="ProtectedData"/> with <see cref="DataProtectionScope.CurrentUser"/> and one fixed application
/// entropy. The entropy is not a secret: it separates these files from other DPAPI blobs of the same user. DPAPI protects against other
/// users and against offline disk theft, not against code running as the same user. On a platform without DPAPI both methods throw
/// <see cref="PlatformNotSupportedException"/>.
/// </summary>
internal sealed class ProtectedDataProtector : IDpapiProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Autonogrammer.Harness.Secrets.Os/v1");

    /// <inheritdoc />
    public byte[] Protect(byte[] plain)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI exists only on Windows.");
        }

        return ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
    }

    /// <inheritdoc />
    public byte[] Unprotect(byte[] protectedBytes)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI exists only on Windows.");
        }

        return ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
    }
}
