using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Fakes;
using Autonogrammer.Harness.ComputerUse.Policy;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Policy;

internal sealed class GateFixture
{
    public required ComputerActionGate Gate { get; init; }

    public required FakeComputerUseBackend Backend { get; init; }

    public required GrantStore Grants { get; init; }

    public required KillSwitchCoordinator KillSwitch { get; init; }

    public required ScriptPrompt Prompt { get; init; }

    public required ScriptAudit Audit { get; init; }

    public required RateLimiter Limiter { get; init; }

    public required ComputerUseOptions Options { get; init; }

    public required ManualTimeProvider Time { get; init; }

    public required ComputerActionRequest Request { get; set; }

    public static async Task<GateFixture> CreateAsync(bool enabled = true, bool signedIn = true, bool cowork = true, bool hasApprover = true)
    {
        ManualTimeProvider time = new(DateTimeOffset.Parse("2026-10-30T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        FakeComputerUseBackend backend = new();
        backend.SetWindows([Indicator()]);
        MemorySettings settings = new();
        AppCatalogue catalogue = await AppCatalogue.LoadAsync(settings, TestContext.Current.CancellationToken).ConfigureAwait(true);
        GrantStore grants = new(time);
        ComputerUseOptions options = new() { Enabled = enabled };
        RateLimiter limiter = new(time, options.RatePerMinute, options.RateBurst, options.StepLimit, options.StepContinueGrant, options.FailurePause);
        ScriptPrompt prompt = new(time) { HasLocalApprover = hasApprover };
        KillSwitchCoordinator kill = new(new ConfirmingRegistrar(true), backend, new CountingAudit());
        ScriptAudit audit = new();
        ComputerActionGate gate = new(options, new Access(signedIn, cowork), backend, grants, catalogue, limiter, kill, prompt, audit, time);
        return new GateFixture
        {
            Gate = gate,
            Backend = backend,
            Grants = grants,
            KillSwitch = kill,
            Prompt = prompt,
            Audit = audit,
            Limiter = limiter,
            Options = options,
            Time = time,
            Request = RequestFor("code"),
        };
    }

    public static async Task<GateFixture> ReadyAsync(ComputerGrantTier tier, bool hasApprover = true)
    {
        GateFixture fixture = await CreateAsync(hasApprover: hasApprover).ConfigureAwait(true);
        fixture.Gate.OptIn("session");
        fixture.Gate.NoteFrame("session", "frame-1");
        await fixture.KillSwitch.TryArmAsync("Ctrl+Alt+Shift+Escape", TestContext.Current.CancellationToken).ConfigureAwait(true);
        fixture.Backend.SetWindows([Window("target", "code", "editor", false), Indicator()]);
        fixture.Grants.TryCreateFromUser("session", "code", tier, ComputerActionOrigin.Local, null, false, out _);
        fixture.Bind(ComputerActionClass.Click);
        return fixture;
    }

    public void BindChord(string chord)
    {
        Bind(ComputerActionClass.Chord);
        Gate.BindHostAction(Request.ActionHash, new ComputerHostAction { ActionClass = ComputerActionClass.Chord, Chord = chord });
    }

    public void Bind(ComputerActionClass actionClass) =>
        Gate.BindHostAction(Request.ActionHash, new ComputerHostAction { ActionClass = actionClass });

    public async Task MarkSeenAsync() => await Gate.AuthorizeAsync(Request, TestContext.Current.CancellationToken).ConfigureAwait(true);

    public void ExhaustRate()
    {
        for (int i = 0; i < Options.RateBurst; i++)
        {
            Limiter.TryConsume();
        }
    }


    public async Task<ComputerGateDenied> DenyAsync()
    {
        ComputerGateDecision decision = await Gate.AuthorizeAsync(Request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        return decision.ShouldBeOfType<ComputerGateDenied>();
    }

    public static WindowInfo Indicator() => new()
    {
        Id = "indicator",
        Pid = 1,
        AppIdentity = AppCatalogue.IndicatorIdentity,
        Title = "indicator",
        Bounds = new PixelRect { X = 0, Y = 0, Width = 10, Height = 10 },
        Minimized = false,
        Focused = false,
        IsOwnProcess = true,
    };

    public static WindowInfo Window(string id, string identity, string title, bool own, bool minimizedIndicator = false)
    {
        if (minimizedIndicator)
        {
            return new WindowInfo
            {
                Id = "indicator",
                Pid = 1,
                AppIdentity = AppCatalogue.IndicatorIdentity,
                Title = "indicator",
                Bounds = new PixelRect { X = 0, Y = 0, Width = 10, Height = 10 },
                Minimized = true,
                Focused = false,
                IsOwnProcess = true,
            };
        }

        return new WindowInfo
        {
            Id = id,
            Pid = 2,
            AppIdentity = identity,
            Title = title,
            Bounds = new PixelRect { X = 0, Y = 0, Width = 100, Height = 100 },
            Minimized = false,
            Focused = true,
            IsOwnProcess = own,
        };
    }

    private static ComputerActionRequest RequestFor(string identity) => new()
    {
        SessionId = "session",
        ToolName = "computer_click",
        FrameId = "frame-1",
        Origin = ComputerActionOrigin.Local,
        ActionHash = new byte[] { 1, 2, 3, 4 },
        Target = new ComputerObservedTarget { WindowId = "target", AppIdentity = identity },
        ScaledCoordinates = new ModelPoint { X = 1, Y = 1 },
    };

    private sealed class Access(bool signedIn, bool cowork) : IComputerUseAccessFacts
    {
        public bool IsSignedIn { get; } = signedIn;

        public bool IsCoworkSession(string sessionId) => cowork;
    }

    private sealed class ConfirmingRegistrar(bool confirm) : IGlobalHotkeyRegistrar
    {
        public ValueTask<bool> RegisterAsync(string chord, CancellationToken cancellationToken) => ValueTask.FromResult(confirm);

        public ValueTask UnregisterAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
