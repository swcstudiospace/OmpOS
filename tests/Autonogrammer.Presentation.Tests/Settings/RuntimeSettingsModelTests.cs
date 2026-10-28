using Autonogrammer.Presentation.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>The Runtime section defaults, inline rejection, reset and save rules.</summary>
public sealed class RuntimeSettingsModelTests
{
    /// <summary>A fresh section idles out after 24 hours (STATE.md line 124).</summary>
    [Fact]
    public void IdleStopDefaultsTo24Hours()
    {
        var model = new RuntimeSettingsModel();

        model.IdleStopText.ShouldBe("24");
        model.Saved.IdleStop.ShouldBe(TimeSpan.FromHours(24));
        model.IsDirty.ShouldBeFalse();
    }

    /// <summary>A fresh section keeps completed sessions for 7 days (STATE.md line 124).</summary>
    [Fact]
    public void RetentionDefaultsTo7Days()
    {
        var model = new RuntimeSettingsModel();

        model.RetentionText.ShouldBe("7");
        model.Saved.Retention.ShouldBe(TimeSpan.FromDays(7));
        model.HasErrors.ShouldBeFalse();
    }

    /// <summary>Values outside the floor or ceiling, or not whole numbers, are rejected inline and cannot be saved.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("721")]
    [InlineData("-3")]
    [InlineData("")]
    [InlineData("soon")]
    [InlineData("99999999999")]
    public void OutOfRangeValueIsRejectedInline(string entry)
    {
        var model = new RuntimeSettingsModel();

        model.SetIdleStop(entry).ShouldBeFalse();
        model.IdleStopError.ShouldNotBeNull();
        model.IdleStopError.ShouldContain("1");
        model.IdleStopError.ShouldContain("720");
        model.IdleStopText.ShouldBe(entry);
        model.HasErrors.ShouldBeTrue();
        model.Draft.ShouldBeNull();

        model.TrySave().ShouldBeNull();
        model.Saved.IdleStopHours.ShouldBe(24);

        model.SetRetention("91").ShouldBeFalse();
        model.RetentionError.ShouldNotBeNull();
        model.RetentionError.ShouldContain("90");
    }

    /// <summary>The floor and the ceiling themselves are accepted.</summary>
    [Fact]
    public void BoundsAreInclusive()
    {
        var model = new RuntimeSettingsModel();

        model.SetIdleStop("1").ShouldBeTrue();
        model.SetIdleStop("720").ShouldBeTrue();
        model.SetRetention("1").ShouldBeTrue();
        model.SetRetention("90").ShouldBeTrue();

        model.HasErrors.ShouldBeFalse();
        model.Draft.ShouldBe(new RuntimeSettingsValues(720, 90));
    }

    /// <summary>Correcting a rejected entry clears its error.</summary>
    [Fact]
    public void CorrectingAnEntryClearsItsError()
    {
        var model = new RuntimeSettingsModel();
        model.SetIdleStop("0");

        model.SetIdleStop(" 12 ").ShouldBeTrue();

        model.IdleStopError.ShouldBeNull();
        model.IdleStopText.ShouldBe("12");
        model.Draft.ShouldBe(new RuntimeSettingsValues(12, 7));
    }

    /// <summary>Reset puts both entries back to the section defaults and clears errors; the saved values stay until saved.</summary>
    [Fact]
    public void ResetRestoresTheSectionDefault()
    {
        var model = new RuntimeSettingsModel(new RuntimeSettingsValues(48, 30));
        model.SetIdleStop("0");

        model.Reset();

        model.IdleStopText.ShouldBe("24");
        model.RetentionText.ShouldBe("7");
        model.HasErrors.ShouldBeFalse();
        model.Draft.ShouldBe(RuntimeSettingsValues.Defaults);
        model.IsDirty.ShouldBeTrue();
        model.Saved.ShouldBe(new RuntimeSettingsValues(48, 30));
    }

    /// <summary>Saving commits the draft and clears the dirty flag; discarding returns to the saved values.</summary>
    [Fact]
    public void SaveCommitsTheDraftAndDiscardReturnsToIt()
    {
        var model = new RuntimeSettingsModel();
        model.SetIdleStop("48");
        model.SetRetention("14");
        model.IsDirty.ShouldBeTrue();

        model.TrySave().ShouldBe(new RuntimeSettingsValues(48, 14));
        model.IsDirty.ShouldBeFalse();

        model.SetRetention("3");
        model.Discard();

        model.RetentionText.ShouldBe("14");
        model.IsDirty.ShouldBeFalse();
    }

    /// <summary>Saved values outside the range are refused at construction.</summary>
    [Fact]
    public void SavedValuesOutsideTheRangeAreRefused() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RuntimeSettingsModel(new RuntimeSettingsValues(0, 7)));

    /// <summary>The dirty flag raises a change notification when a draft entry changes.</summary>
    [Fact]
    public void EditingNotifiesTheDirtyFlag()
    {
        var model = new RuntimeSettingsModel();
        var raised = new List<string?>();
        model.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        model.SetIdleStop("30");

        raised.ShouldContain(nameof(RuntimeSettingsModel.IsDirty), StringComparer.Ordinal);
        raised.ShouldContain(nameof(RuntimeSettingsModel.IdleStopText), StringComparer.Ordinal);
    }
}
