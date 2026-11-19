using Autonogrammer.Presentation.Cloud;
using Autonogrammer.Presentation.Remote;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>The cloud option of the New session dialog says what the cloud agents service reports, and never that cloud agents are missing from the app.</summary>
public sealed class CloudOptionAvailabilityRegressionTests
{
    /// <summary>Without a cloud agents service the option says it is not connected to this app.</summary>
    [Fact]
    public void WithoutAServiceTheOptionSaysItIsNotConnected()
    {
        var model = new NewSessionTargetModel([]);

        model.Select(NewSessionTargetModel.CloudId);

        model.Validation.Message.ShouldBe(RemoteCopy.CloudNotConnected);
        model.Validation.Message.ShouldNotContain("in this build", Case.Sensitive);
    }

    /// <summary>A service that is not set up gives its own reason for the option.</summary>
    [Fact]
    public void AnUnconfiguredServiceGivesItsReason()
    {
        var model = new NewSessionTargetModel([]);
        model.SetCloud(CloudSetup.NotConfigured("Sign in to the cloud service first."));

        model.Select(NewSessionTargetModel.CloudId);

        model.Validation.Message.ShouldBe("Sign in to the cloud service first.");
    }

    /// <summary>A service that is not set up and gives no reason uses the standard sentence.</summary>
    [Fact]
    public void AnUnconfiguredServiceWithoutAReasonUsesTheStandardSentence()
    {
        var model = new NewSessionTargetModel([]);
        model.SetCloud(CloudSetup.NotConfigured());

        model.Select(NewSessionTargetModel.CloudId);

        model.Validation.Message.ShouldBe(CloudCopy.NotConfiguredShort);
    }

    /// <summary>A configured service is not reported as missing; the picker points at the cloud commands and still cannot start a cloud agent.</summary>
    [Fact]
    public void AConfiguredServicePointsAtTheCloudCommandsAndCannotStartHere()
    {
        var model = new NewSessionTargetModel([]);
        model.SetCloud(new CloudSetup { IsConfigured = true });

        model.Select(NewSessionTargetModel.CloudId);

        model.Validation.Message.ShouldBe(RemoteCopy.CloudStartElsewhere);
        model.CanStart.ShouldBeFalse();
        model.TryGetTarget(out var target).ShouldBeFalse();
        target.ShouldBeNull();
    }
}
