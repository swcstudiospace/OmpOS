using Autonogrammer.Cowork;
using Autonogrammer.Harness.ComputerUse;
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Native.Composition.ComputerUse;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Computer;

/// <summary>The head registers the OS backend for its platform and the null backend elsewhere.</summary>
public sealed class ComputerUseHeadCompositionTests
{
    [Theory]
    [InlineData(ComputerPlatform.X11, typeof(Autonogrammer.Harness.ComputerUse.X11.X11Backend))]
    [InlineData(ComputerPlatform.Windows, typeof(Autonogrammer.Harness.ComputerUse.Windows.WindowsBackend))]
    [InlineData(ComputerPlatform.MacOS, typeof(Autonogrammer.Harness.ComputerUse.MacOS.MacOSBackend))]
    public void TheBackendMatchesTheInjectedPlatform(ComputerPlatform platform, Type backend)
    {
        ComputerUseHeadComposition.ChooseBackend(platform).ShouldBeOfType(backend);
    }

    [Fact]
    public async Task AnUnsupportedPlatformUsesTheNullBackend()
    {
        var result = ComputerUseHeadComposition.Build(new ComputerUseHeadOptions
        {
            Platform = ComputerPlatform.Unsupported,
            StateDirectory = Path.Combine(Path.GetTempPath(), "ag-73-06-" + Guid.NewGuid().ToString("N")),
            AccountGate = new FakeGate(signedIn: false),
        });

        result.Backend.ShouldBeOfType<NullComputerUseBackend>();
        var availability = await result.Status.GetAvailabilityAsync(CancellationToken.None);
        availability.ShouldBe(ComputerAvailability.UnsupportedPlatform);
        ComputerAvailabilityRemediation.For(availability).ShouldBe("computer.unavailable.platform");
    }

    [Fact]
    public void TheAccessFactsAdapterReadsOnlyTheAccountGate()
    {
        var gate = new FakeGate(signedIn: true);
        var adapter = new ComputerAccessFactsAdapter(gate);

        adapter.IsSignedIn.ShouldBeTrue();
        gate.SignedInReads.ShouldBe(1);
        adapter.IsCoworkSession("cowork-1").ShouldBeTrue();
        gate.SignedInReads.ShouldBe(2);
        gate.FolderReads.ShouldBe(0);
    }

    private sealed class FakeGate : ICoworkAccountGate
    {
        private readonly bool _signedIn;

        public FakeGate(bool signedIn) => _signedIn = signedIn;

        public int SignedInReads { get; private set; }

        public int FolderReads { get; private set; }

        public bool IsSignedIn
        {
            get
            {
                SignedInReads++;
                return _signedIn;
            }
        }

        public string? AccountFolderKey
        {
            get
            {
                FolderReads++;
                return null;
            }
        }
    }
}
