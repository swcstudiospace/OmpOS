using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Presentation.Cowork;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Cowork;

/// <summary>The surface switch restores a stored surface only when Cowork is available, and hides itself otherwise.</summary>
public sealed class SurfaceSwitchModelTests
{
    /// <summary>Code and Cowork can both be selected while Cowork is available.</summary>
    [Fact]
    public void SelectionMovesBetweenCodeAndCowork()
    {
        var model = new SurfaceSwitchModel();
        model.SetCoworkAvailable(true);

        model.Select(SessionSurface.Cowork);
        model.Selected.ShouldBe(SessionSurface.Cowork);
        model.Persisted.ShouldBe("cowork");

        model.Select(SessionSurface.Code);
        model.Selected.ShouldBe(SessionSurface.Code);
        model.IsSwitchVisible.ShouldBeTrue();
    }

    /// <summary>Cowork unavailable keeps Code and hides the switch. Selecting Cowork does not throw.</summary>
    [Fact]
    public void UnavailableCoworkKeepsCodeAndHidesTheSwitch()
    {
        var model = new SurfaceSwitchModel();
        model.SetCoworkAvailable(true);
        model.Select(SessionSurface.Cowork);

        model.SetCoworkAvailable(false);

        model.Selected.ShouldBe(SessionSurface.Code);
        model.IsCoworkAvailable.ShouldBeFalse();
        model.IsSwitchVisible.ShouldBeFalse();
        Should.NotThrow(() => model.Select(SessionSurface.Cowork));
        model.Selected.ShouldBe(SessionSurface.Code);
    }

    /// <summary>A stored Cowork surface is restored only when Cowork is available. Otherwise Code is selected and nothing is thrown.</summary>
    [Fact]
    public void StoredSurfaceRestoresOnlyWhenCoworkIsAvailable()
    {
        var available = new SurfaceSwitchModel();
        available.Restore(SurfaceSwitchModel.Format(SessionSurface.Cowork), available: true);
        available.Selected.ShouldBe(SessionSurface.Cowork);

        var signedOut = new SurfaceSwitchModel();
        Should.NotThrow(() => signedOut.Restore("cowork", available: false));
        signedOut.Selected.ShouldBe(SessionSurface.Code);
        signedOut.IsSwitchVisible.ShouldBeFalse();

        var unusable = new SurfaceSwitchModel();
        Should.NotThrow(() => unusable.Restore("desk", available: true));
        unusable.Selected.ShouldBe(SessionSurface.Code);
        SurfaceSwitchModel.Parse("desk").ShouldBeNull();
        SurfaceSwitchModel.Parse("code").ShouldBe(SessionSurface.Code);
    }
}
