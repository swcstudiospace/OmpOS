using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Secrets.Os.Tests;

/// <summary>Verifies how the Linux store classifies libsecret failures, the attribute set it writes, and the hand-marshalled native strings.</summary>
public sealed class LinuxLibsecretTests
{
    [Theory]
    [InlineData("Secret", 2, SecretStoreFailure.Locked)]
    [InlineData("Secret", 3, SecretStoreFailure.Unavailable)]
    [InlineData("Secret", 1, SecretStoreFailure.Other)]
    [InlineData("Secret", 5, SecretStoreFailure.Other)]
    [InlineData("DBus", 2, SecretStoreFailure.Unavailable)]
    [InlineData("DBus", 3, SecretStoreFailure.Unavailable)]
    [InlineData("DBus", 11, SecretStoreFailure.Unavailable)]
    [InlineData("DBus", 9, SecretStoreFailure.Denied)]
    [InlineData("DBus", 10, SecretStoreFailure.Denied)]
    [InlineData("DBus", 0, SecretStoreFailure.Other)]
    [InlineData("Gio", 0, SecretStoreFailure.Unavailable)]
    [InlineData("Gio", 36, SecretStoreFailure.Unavailable)]
    [InlineData("Gio", 14, SecretStoreFailure.Denied)]
    [InlineData("Other", 7, SecretStoreFailure.Other)]
    public void ANativeErrorIsClassifiedByDomainAndCode(string domain, int code, SecretStoreFailure expected)
    {
        LinuxLibsecretStore.ClassifyFailure(Enum.Parse<LibsecretErrorDomain>(domain), code).ShouldBe(expected);
    }

    [Fact]
    public void ALockedCollectionIsLockedAndAnAbsentServiceIsUnavailable()
    {
        LinuxLibsecretStore.ClassifyFailure(LibsecretErrorDomain.Secret, 2).ShouldBe(SecretStoreFailure.Locked);
        LinuxLibsecretStore.ClassifyFailure(LibsecretErrorDomain.DBus, 2).ShouldBe(SecretStoreFailure.Unavailable);
    }

    [Fact]
    public void ANativeErrorBecomesATypedExceptionWithoutTheNativeText()
    {
        SecretStoreException locked = LinuxLibsecretStore.Failed(LibsecretErrorDomain.Secret, 2);
        SecretStoreException absent = LinuxLibsecretStore.Failed(LibsecretErrorDomain.DBus, 2);

        locked.Failure.ShouldBe(SecretStoreFailure.Locked);
        locked.Message.ShouldBe("The Linux Secret Service is locked.");
        absent.Failure.ShouldBe(SecretStoreFailure.Unavailable);
        absent.Message.ShouldBe("The Linux Secret Service is not available on this device.");
        LinuxLibsecretStore.Failed(LibsecretErrorDomain.Other, 7).Message.ShouldBe("The Linux Secret Service reported an error.");
    }

    [Fact]
    public void ItemsCarryTheAutonogrammerAppAndNameAttributes()
    {
        LibsecretAttributes.For("provider.openai").ShouldBe([("app", "autonogrammer"), ("name", "provider.openai")]);
        LibsecretAttributes.ForAll().ShouldBe([("app", "autonogrammer")]);
        LibsecretAttributes.For("x", "autonogrammer-live-1").ShouldBe([("app", "autonogrammer-live-1"), ("name", "x")]);
        LibsecretAttributes.SchemaName.ShouldBe("org.autonogrammer.Secret");
    }

    [Fact]
    public void ANativeStringIsAnExactNulTerminatedUtf8Copy()
    {
        using NativeUtf8 native = NativeUtf8.Create("pr\u00fcfung-\U0001F511", "name");

        native.Length.ShouldBe(System.Text.Encoding.UTF8.GetByteCount("pr\u00fcfung-\U0001F511"));
        NativeUtf8.Read(native.Pointer).ShouldBe("pr\u00fcfung-\U0001F511");
        System.Runtime.InteropServices.Marshal.ReadByte(native.Pointer, native.Length).ShouldBe((byte)0);
    }

    [Fact]
    public void ANativeStringIsRefusedWhenItHoldsNulOrALoneSurrogateWithoutNamingTheText()
    {
        ArgumentException nul = Should.Throw<ArgumentException>(() => NativeUtf8.Create("secret\0value", "secret"));
        ArgumentException surrogate = Should.Throw<ArgumentException>(() => NativeUtf8.Create("secret\uD800value", "secret"));

        nul.ParamName.ShouldBe("secret");
        nul.Message.ShouldNotContain("value");
        surrogate.Message.ShouldNotContain("value");
        NativeUtf8.Read(IntPtr.Zero).ShouldBeNull();
    }

    [Fact]
    public void ADisposedNativeStringCannotBeUsed()
    {
        NativeUtf8 native = NativeUtf8.Create("value", "name");

        native.Dispose();
        native.Dispose();

        Should.Throw<ObjectDisposedException>(() => native.Pointer);
    }
}
