using System.Reflection;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Secrets.Tests;

/// <summary>
/// Verifies how a <see cref="SecretString"/> is built, how its value is read back, and which conversions it refuses.
/// </summary>
public sealed class SecretStringTests
{
    [Fact]
    public void RevealReturnsTheValueSuppliedAtConstruction()
    {
        new SecretString("fixture-secret-7f3a").Reveal().ShouldBe("fixture-secret-7f3a");
    }

    [Fact]
    public void NullValueIsRefused()
    {
        Should.Throw<ArgumentNullException>(() => new SecretString(null!));
    }

    [Fact]
    public void EmptyValueIsRefused()
    {
        Should.Throw<ArgumentException>(() => new SecretString(string.Empty));
    }

    [Fact]
    public void ExposesNoImplicitConversionAndNoFormattingInterface()
    {
        typeof(SecretString).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => string.Equals(method.Name, "op_Implicit", StringComparison.Ordinal))
            .ShouldBeEmpty();
        typeof(SecretString).GetInterfaces()
            .Where(type => type == typeof(IFormattable) || type == typeof(ISpanFormattable))
            .ShouldBeEmpty();
    }
}
