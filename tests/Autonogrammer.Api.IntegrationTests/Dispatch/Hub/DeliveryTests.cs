using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autonogrammer.Api.Dispatch;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>Delivery, device reports and the sweeper: queued commands reach the device in order, every device report applies the D-23 table, and the sweeper expires and fails overdue commands (D-21 to D-27).</summary>
public sealed class DeliveryTests
{
    /// <summary>A queued command reaches the connected device as a command frame whose signature verifies, and whose payload is the body that was sent.</summary>
    [Fact]
    public async Task A_queued_command_reaches_the_connected_device_as_a_frame_its_command_key_signs()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(socket, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");

        using var owner = DispatchTestClient.As(host);
        var accepted = await DispatchTestClient.SendAsync(owner, HttpMethod.Post, "/dispatch/devices/" + device.DeviceId + "/commands", DispatchTestClient.CommandJson(payloadJson: "{\"n\":1}"), ct).ConfigureAwait(true);
        accepted.Status.ShouldBe(HttpStatusCode.Accepted);

        var frame = await HubTestSockets.ReceiveJsonAsync(socket, ct).ConfigureAwait(true);
        frame.GetProperty("t").GetString().ShouldBe("cmd");
        var store = host.Services.GetRequiredService<IDispatchStore>();
        (await store.GetCommandAsync(DispatchTestClient.Owner, frame.GetProperty("commandId").GetString()!, ct).ConfigureAwait(true))!.State.ShouldBe(DispatchCommandState.Delivered);
        frame.GetProperty("commandId").GetString().ShouldBe(HubTestSockets.Field(accepted.Body, "commandId"));
        Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(frame.GetProperty("payload").GetString()!)).ShouldBe("{\"n\":1}");
        HubTestSockets.VerifyCommand(frame, device).ShouldBeTrue();
    }

    /// <summary>A command the device was sent but never applied is delivered again after a reconnect from the sequence the device has applied (D-22, at-least-once).</summary>
    [Fact]
    public async Task A_delivered_command_the_device_never_applied_is_redelivered_after_a_reconnect()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        using var owner = DispatchTestClient.As(host);
        string commandId;
        using (var first = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true))
        {
            (await HubTestSockets.HelloAsync(first, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");
            var accepted = await DispatchTestClient.SendAsync(owner, HttpMethod.Post, "/dispatch/devices/" + device.DeviceId + "/commands", DispatchTestClient.CommandJson(), ct).ConfigureAwait(true);
            accepted.Status.ShouldBe(HttpStatusCode.Accepted);
            commandId = HubTestSockets.Field(accepted.Body, "commandId");
            (await HubTestSockets.ReceiveJsonAsync(first, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("cmd");
            await first.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", ct).ConfigureAwait(true);
        }

        using var second = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(second, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");
        var redelivered = await HubTestSockets.ReceiveJsonAsync(second, ct).ConfigureAwait(true);
        redelivered.GetProperty("t").GetString().ShouldBe("cmd");
        redelivered.GetProperty("commandId").GetString().ShouldBe(commandId);
    }

    /// <summary>A command past its expiry is never delivered, and the sweeper moves it to expired.</summary>
    [Fact]
    public async Task A_command_past_its_expiry_is_never_delivered_and_the_sweeper_expires_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        var store = host.Services.GetRequiredService<IDispatchStore>();
        var now = DateTimeOffset.UtcNow;
        await store.EnqueueAsync(new EnqueueDispatchCommand(DispatchTestClient.Owner, device.DeviceId, "01STALECOMMAND0000000000000", "stale-key", "fp-stale", DispatchCapability.Sessions, DispatchOp.SessionList, "{}", now.AddMinutes(-10), now.AddMinutes(-5)), ct).ConfigureAwait(true);

        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(socket, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");
        await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(true);
        (await store.GetCommandAsync(DispatchTestClient.Owner, "01STALECOMMAND0000000000000", ct).ConfigureAwait(true)).ShouldNotBeNull().State.ShouldBe(DispatchCommandState.Queued);

        var swept = await host.Services.GetRequiredService<DispatchSweeper>().SweepOnceAsync(ct).ConfigureAwait(true);
        swept.ShouldBe(1);
        var row = (await store.GetCommandAsync(DispatchTestClient.Owner, "01STALECOMMAND0000000000000", ct).ConfigureAwait(true)).ShouldNotBeNull();
        row.State.ShouldBe(DispatchCommandState.Expired);
        row.Reason.ShouldBe(DispatchFailureReason.Expired);
    }

    /// <summary>An offline command expires exactly 300 seconds after it was issued, and the expiry lands on the account feed.</summary>
    [Fact]
    public async Task An_offline_command_expires_after_300_seconds_and_the_expiry_lands_on_the_feed()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var host = await DispatchApiHost.StartAsync(ct, registerServices: services => services.AddSingleton<TimeProvider>(clock)).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        using var owner = DispatchTestClient.As(host);
        var accepted = await DispatchTestClient.SendAsync(owner, HttpMethod.Post, "/dispatch/devices/" + device.DeviceId + "/commands", DispatchTestClient.CommandJson(), ct).ConfigureAwait(true);
        accepted.Status.ShouldBe(HttpStatusCode.Accepted);
        var commandId = HubTestSockets.Field(accepted.Body, "commandId");
        var sweeper = host.Services.GetRequiredService<DispatchSweeper>();
        var store = host.Services.GetRequiredService<IDispatchStore>();

        clock.Advance(TimeSpan.FromSeconds(299));
        (await sweeper.SweepOnceAsync(ct).ConfigureAwait(true)).ShouldBe(0);
        (await store.GetCommandAsync(DispatchTestClient.Owner, commandId, ct).ConfigureAwait(true)).ShouldNotBeNull().State.ShouldBe(DispatchCommandState.Queued);

        clock.Advance(TimeSpan.FromSeconds(2));
        (await sweeper.SweepOnceAsync(ct).ConfigureAwait(true)).ShouldBe(1);
        (await store.GetCommandAsync(DispatchTestClient.Owner, commandId, ct).ConfigureAwait(true)).ShouldNotBeNull().State.ShouldBe(DispatchCommandState.Expired);

        var feed = await store.ReadFeedAsync(DispatchTestClient.Owner, 0, ct).ConfigureAwait(true);
        feed.Events.Any(entry => entry.Payload is not null && entry.Payload.Contains(commandId, StringComparison.Ordinal)).ShouldBeTrue();
    }

    /// <summary>A command the device is running for longer than its run limit is failed timeout by the sweeper; a session command has a sixty-second limit.</summary>
    [Fact]
    public async Task A_running_session_command_past_its_sixty_second_limit_is_failed_timeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var host = await DispatchApiHost.StartAsync(ct, registerServices: services => services.AddSingleton<TimeProvider>(clock)).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var store = host.Services.GetRequiredService<IDispatchStore>();
        (await store.CreateDeviceAsync(new CreateDispatchDevice(DispatchTestClient.Owner, "device-run", "device-run", null, "jti", null, [], null), ct).ConfigureAwait(true)).Status.ShouldBe(DispatchDeviceCreateStatus.Created);
        var issued = clock.GetUtcNow();
        await store.EnqueueAsync(new EnqueueDispatchCommand(DispatchTestClient.Owner, "device-run", "01RUNNINGSESSION000000000000", "run-key", "fp-run", DispatchCapability.Sessions, DispatchOp.SessionList, "{}", issued, issued.AddMinutes(5)), ct).ConfigureAwait(true);
        await CommandStates.AdvanceAsync(store, DispatchTestClient.Owner, "01RUNNINGSESSION000000000000", CommandStates.ToRunning, ct).ConfigureAwait(true);

        clock.Advance(TimeSpan.FromSeconds(59));
        (await host.Services.GetRequiredService<DispatchSweeper>().SweepOnceAsync(ct).ConfigureAwait(true)).ShouldBe(0);
        clock.Advance(TimeSpan.FromSeconds(2));
        (await host.Services.GetRequiredService<DispatchSweeper>().SweepOnceAsync(ct).ConfigureAwait(true)).ShouldBe(1);

        var row = (await store.GetCommandAsync(DispatchTestClient.Owner, "01RUNNINGSESSION000000000000", ct).ConfigureAwait(true)).ShouldNotBeNull();
        row.State.ShouldBe(DispatchCommandState.Failed);
        row.Reason.ShouldBe(DispatchFailureReason.Timeout);
        var cancellation = (await store.ListQueuedAfterAsync(DispatchTestClient.Owner, "device-run", 0, ct).ConfigureAwait(true)).Single();
        cancellation.Op.ShouldBe(DispatchOp.CommandCancel);
        await CommandStates.AdvanceAsync(store, DispatchTestClient.Owner, cancellation.CommandId, CommandStates.ToRunning, ct).ConfigureAwait(true);
        clock.Advance(TimeSpan.FromSeconds(61));
        (await host.Services.GetRequiredService<DispatchSweeper>().SweepOnceAsync(ct).ConfigureAwait(true)).ShouldBe(1);
        (await store.GetCommandAsync(DispatchTestClient.Owner, cancellation.CommandId, ct).ConfigureAwait(true))!.Reason.ShouldBe(DispatchFailureReason.Timeout);
        (await store.ListQueuedAfterAsync(DispatchTestClient.Owner, "device-run", 0, ct).ConfigureAwait(true)).ShouldBeEmpty();
    }

    /// <summary>A coworking task that runs for longer than four hours is failed timeout by the sweeper, and not before.</summary>
    [Fact]
    public async Task A_running_task_past_its_four_hour_limit_is_failed_timeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var host = await DispatchApiHost.StartAsync(ct, registerServices: services => services.AddSingleton<TimeProvider>(clock)).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var store = host.Services.GetRequiredService<IDispatchStore>();
        (await store.CreateDeviceAsync(new CreateDispatchDevice(DispatchTestClient.Owner, "device-task", "device-task", null, "jti", null, [], null), ct).ConfigureAwait(true)).Status.ShouldBe(DispatchDeviceCreateStatus.Created);
        var issued = clock.GetUtcNow();
        await store.EnqueueAsync(new EnqueueDispatchCommand(DispatchTestClient.Owner, "device-task", "01LONGTASK00000000000000000", "task-key", "fp-task", DispatchCapability.Cowork, DispatchOp.CoworkTask, "{}", issued, issued.AddMinutes(5)), ct).ConfigureAwait(true);
        await CommandStates.AdvanceAsync(store, DispatchTestClient.Owner, "01LONGTASK00000000000000000", CommandStates.ToRunning, ct).ConfigureAwait(true);

        clock.Advance(TimeSpan.FromHours(4) - TimeSpan.FromMinutes(1));
        (await host.Services.GetRequiredService<DispatchSweeper>().SweepOnceAsync(ct).ConfigureAwait(true)).ShouldBe(0);
        clock.Advance(TimeSpan.FromMinutes(2));
        (await host.Services.GetRequiredService<DispatchSweeper>().SweepOnceAsync(ct).ConfigureAwait(true)).ShouldBe(1);

        var row = (await store.GetCommandAsync(DispatchTestClient.Owner, "01LONGTASK00000000000000000", ct).ConfigureAwait(true)).ShouldNotBeNull();
        row.State.ShouldBe(DispatchCommandState.Failed);
        row.Reason.ShouldBe(DispatchFailureReason.Timeout);
    }

    /// <summary>A command that waits for local approval longer than the approval timeout is failed approval_timeout by the sweeper.</summary>
    [Fact]
    public async Task A_command_awaiting_approval_past_the_approval_timeout_is_failed_approval_timeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var host = await DispatchApiHost.StartAsync(ct, registerServices: services => services.AddSingleton<TimeProvider>(clock)).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var store = host.Services.GetRequiredService<IDispatchStore>();
        (await store.CreateDeviceAsync(new CreateDispatchDevice(DispatchTestClient.Owner, "device-approve", "device-approve", null, "jti", null, [], null), ct).ConfigureAwait(true)).Status.ShouldBe(DispatchDeviceCreateStatus.Created);
        var issued = clock.GetUtcNow();
        await store.EnqueueAsync(new EnqueueDispatchCommand(DispatchTestClient.Owner, "device-approve", "01APPROVAL0000000000000000", "approve-key", "fp-approve", DispatchCapability.Sessions, DispatchOp.SessionSteer, "{}", issued, issued.AddMinutes(5)), ct).ConfigureAwait(true);
        await CommandStates.AdvanceAsync(store, DispatchTestClient.Owner, "01APPROVAL0000000000000000", CommandStates.ToRunning.Take(2).Append((DispatchCommandState.Received, DispatchCommandState.AwaitingApproval)), ct).ConfigureAwait(true);

        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        (await host.Services.GetRequiredService<DispatchSweeper>().SweepOnceAsync(ct).ConfigureAwait(true)).ShouldBe(1);

        var row = (await store.GetCommandAsync(DispatchTestClient.Owner, "01APPROVAL0000000000000000", ct).ConfigureAwait(true)).ShouldNotBeNull();
        row.State.ShouldBe(DispatchCommandState.Failed);
        row.Reason.ShouldBe(DispatchFailureReason.ApprovalTimeout);
    }

    /// <summary>A device acknowledgement moves a command forward through the table, and an acknowledgement the table does not allow changes nothing.</summary>
    [Fact]
    public async Task A_device_ack_moves_the_command_forward_and_an_out_of_table_ack_changes_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(socket, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");
        using var owner = DispatchTestClient.As(host);
        var accepted = await DispatchTestClient.SendAsync(owner, HttpMethod.Post, "/dispatch/devices/" + device.DeviceId + "/commands", DispatchTestClient.CommandJson(), ct).ConfigureAwait(true);
        var commandId = HubTestSockets.Field(accepted.Body, "commandId");
        (await HubTestSockets.ReceiveJsonAsync(socket, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("cmd");
        var store = host.Services.GetRequiredService<IDispatchStore>();

        await HubFrames.SendTextAsync(socket, "{\"v\":1,\"t\":\"ack\",\"commandId\":\"" + commandId + "\",\"state\":\"received\"}", ct).ConfigureAwait(true);
        (await CommandStates.WaitForAsync(store, DispatchTestClient.Owner, commandId, DispatchCommandState.Received, ct).ConfigureAwait(true)).ShouldBeTrue();

        await HubFrames.SendTextAsync(socket, "{\"v\":1,\"t\":\"ack\",\"commandId\":\"" + commandId + "\",\"state\":\"completed\"}", ct).ConfigureAwait(true);
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(true);
        (await store.GetCommandAsync(DispatchTestClient.Owner, commandId, ct).ConfigureAwait(true)).ShouldNotBeNull().State.ShouldBe(DispatchCommandState.Received);
    }

    /// <summary>Null required fields do not close the device link or prevent later valid frames from being applied.</summary>
    [Fact]
    public async Task Null_device_frame_fields_do_not_break_the_link()
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(5));
        var ct = bound.Token;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(socket, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");
        using var owner = DispatchTestClient.As(host);
        var accepted = await DispatchTestClient.SendAsync(owner, HttpMethod.Post, "/dispatch/devices/" + device.DeviceId + "/commands", DispatchTestClient.CommandJson(), ct).ConfigureAwait(true);
        var commandId = HubTestSockets.Field(accepted.Body, "commandId");
        _ = await HubTestSockets.ReceiveJsonAsync(socket, ct).ConfigureAwait(true);
        var store = host.Services.GetRequiredService<IDispatchStore>();
        await HubFrames.SendTextAsync(socket, "{\"v\":1,\"t\":\"ack\",\"commandId\":null,\"state\":\"received\"}", ct).ConfigureAwait(true);
        await HubFrames.SendTextAsync(socket, "{\"v\":1,\"t\":\"res\",\"commandId\":null,\"state\":\"failed\"}", ct).ConfigureAwait(true);
        await HubFrames.SendTextAsync(socket, "{\"v\":1,\"t\":\"ack\",\"commandId\":\"" + commandId + "\",\"state\":\"received\"}", ct).ConfigureAwait(true);
        (await CommandStates.WaitForAsync(store, DispatchTestClient.Owner, commandId, DispatchCommandState.Received, ct).ConfigureAwait(true)).ShouldBeTrue();
        await HubFrames.SendTextAsync(socket, "{\"v\":1,\"t\":\"evt\",\"commandId\":\"" + commandId + "\",\"kind\":null}", ct).ConfigureAwait(true);
        await HubFrames.SendTextAsync(socket, "{\"v\":1,\"t\":\"ack\",\"commandId\":\"" + commandId + "\",\"state\":\"accepted\"}", ct).ConfigureAwait(true);
        (await CommandStates.WaitForAsync(store, DispatchTestClient.Owner, commandId, DispatchCommandState.Accepted, ct).ConfigureAwait(true)).ShouldBeTrue();
    }

    /// <summary>A locally timed-out approval retains its reason in the command record.</summary>
    [Fact]
    public async Task A_device_approval_timeout_retains_its_reason()
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(5));
        var ct = bound.Token;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(socket, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");
        using var owner = DispatchTestClient.As(host);
        var accepted = await DispatchTestClient.SendAsync(owner, HttpMethod.Post, "/dispatch/devices/" + device.DeviceId + "/commands", DispatchTestClient.CommandJson(), ct).ConfigureAwait(true);
        var commandId = HubTestSockets.Field(accepted.Body, "commandId");
        _ = await HubTestSockets.ReceiveJsonAsync(socket, ct).ConfigureAwait(true);
        await HubFrames.SendTextAsync(socket, "{\"v\":1,\"t\":\"ack\",\"commandId\":\"" + commandId + "\",\"state\":\"received\"}", ct).ConfigureAwait(true);
        await HubFrames.SendTextAsync(socket, "{\"v\":1,\"t\":\"ack\",\"commandId\":\"" + commandId + "\",\"state\":\"awaiting_approval\"}", ct).ConfigureAwait(true);
        await HubFrames.SendTextAsync(socket, "{\"v\":1,\"t\":\"res\",\"commandId\":\"" + commandId + "\",\"state\":\"failed\",\"reason\":\"approval_timeout\"}", ct).ConfigureAwait(true);
        var store = host.Services.GetRequiredService<IDispatchStore>();
        (await CommandStates.WaitForAsync(store, DispatchTestClient.Owner, commandId, DispatchCommandState.Failed, ct).ConfigureAwait(true)).ShouldBeTrue();
        (await store.GetCommandAsync(DispatchTestClient.Owner, commandId, ct).ConfigureAwait(true))!.Reason.ShouldBe(DispatchFailureReason.ApprovalTimeout);
    }

    /// <summary>A device result completes a running command, and the hub never completes a command on its own.</summary>
    [Fact]
    public async Task A_device_result_completes_a_running_command_and_the_hub_never_completes_on_its_own()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(socket, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");
        using var owner = DispatchTestClient.As(host);
        var accepted = await DispatchTestClient.SendAsync(owner, HttpMethod.Post, "/dispatch/devices/" + device.DeviceId + "/commands", DispatchTestClient.CommandJson(), ct).ConfigureAwait(true);
        var commandId = HubTestSockets.Field(accepted.Body, "commandId");
        (await HubTestSockets.ReceiveJsonAsync(socket, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("cmd");
        var store = host.Services.GetRequiredService<IDispatchStore>();
        foreach (var state in new[] { "received", "accepted", "running" })
        {
            await HubFrames.SendTextAsync(socket, "{\"v\":1,\"t\":\"ack\",\"commandId\":\"" + commandId + "\",\"state\":\"" + state + "\"}", ct).ConfigureAwait(true);
        }

        (await CommandStates.WaitForAsync(store, DispatchTestClient.Owner, commandId, DispatchCommandState.Running, ct).ConfigureAwait(true)).ShouldBeTrue();
        await Task.Delay(TimeSpan.FromMilliseconds(300), ct).ConfigureAwait(true);
        (await store.GetCommandAsync(DispatchTestClient.Owner, commandId, ct).ConfigureAwait(true)).ShouldNotBeNull().State.ShouldBe(DispatchCommandState.Running);

        var secret = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DispatchOptions>>().Value.CommandSigning.KeyPem!;
        var text = secret + new string('x', 5000);
        var result = new DispatchResFrame { V = 1, T = DispatchFrameTypes.Res, CommandId = commandId, State = "completed", Text = text };
        await HubFrames.SendTextAsync(socket, JsonSerializer.Serialize(result, DispatchJsonContext.Default.DispatchResFrame), ct).ConfigureAwait(true);
        (await CommandStates.WaitForAsync(store, DispatchTestClient.Owner, commandId, DispatchCommandState.Completed, ct).ConfigureAwait(true)).ShouldBeTrue();
        var response = await DispatchTestClient.SendAsync(owner, HttpMethod.Get, "/dispatch/commands/" + commandId, null, ct).ConfigureAwait(true);
        HubTestSockets.Field(response.Body, "resultText").ShouldBe(("[REDACTED]" + new string('x', 5000))[..4096]);
    }

    /// <summary>A progress event's text is capped at 1,024 characters before it reaches the feed.</summary>
    [Fact]
    public async Task A_progress_event_text_is_capped_at_1024_characters_on_the_feed()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(socket, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");
        using var owner = DispatchTestClient.As(host);
        var accepted = await DispatchTestClient.SendAsync(owner, HttpMethod.Post, "/dispatch/devices/" + device.DeviceId + "/commands", DispatchTestClient.CommandJson(), ct).ConfigureAwait(true);
        var commandId = HubTestSockets.Field(accepted.Body, "commandId");
        (await HubTestSockets.ReceiveJsonAsync(socket, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("cmd");
        var store = host.Services.GetRequiredService<IDispatchStore>();
        foreach (var state in new[] { "received", "accepted", "running" })
        {
            await HubFrames.SendTextAsync(socket, "{\"v\":1,\"t\":\"ack\",\"commandId\":\"" + commandId + "\",\"state\":\"" + state + "\"}", ct).ConfigureAwait(true);
        }

        (await CommandStates.WaitForAsync(store, DispatchTestClient.Owner, commandId, DispatchCommandState.Running, ct).ConfigureAwait(true)).ShouldBeTrue();
        var longText = new string('p', 1500);
        await HubFrames.SendTextAsync(socket, "{\"v\":1,\"t\":\"evt\",\"commandId\":\"" + commandId + "\",\"kind\":\"progress\",\"text\":\"" + longText + "\"}", ct).ConfigureAwait(true);

        string? payload = null;
        for (var attempt = 0; attempt < 50 && payload is null; attempt++)
        {
            var feed = await store.ReadFeedAsync(DispatchTestClient.Owner, 0, ct).ConfigureAwait(true);
            payload = feed.Events.Select(entry => entry.Payload).FirstOrDefault(text => text is not null && text.Contains(commandId, StringComparison.Ordinal) && text.Contains("progress", StringComparison.Ordinal));
            if (payload is null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), ct).ConfigureAwait(true);
            }
        }

        var text = payload.ShouldNotBeNull();
        using var document = JsonDocument.Parse(text);
        var recorded = document.RootElement.GetProperty("text").GetString()!;
        recorded.Length.ShouldBeLessThanOrEqualTo(1024);
        recorded.Length.ShouldBeGreaterThan(0);
    }
}
