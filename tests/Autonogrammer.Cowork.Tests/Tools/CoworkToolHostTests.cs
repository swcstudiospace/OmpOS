using System.Text.Json;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Cowork.Tools;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Tools;

/// <summary>The eight-step host, caps, taint and descriptor boundaries.</summary>
public sealed class CoworkToolHostTests
{
    [Fact]
    public async Task StepOrder_PreviewPrecedesGatePrecedesBrokerPrecedesInvokePrecedesCapsPrecedesAudit()
    {
        var order = new List<string>();
        var provider = new ScriptedProvider
        {
            OnPreview = () => order.Add("preview"),
            OnInvoke = () =>
            {
                order.Add("invoke");
                return CoworkToolResult.Text(new string('x', CoworkResultLimiter.MaxTextChars + 1));
            },
        };
        var gate = new ScriptedGate(_ =>
        {
            order.Add("gate");
            return new CoworkPolicyDecision(CoworkPolicyOutcome.Ask, false, null);
        });
        var broker = new ScriptedBroker(_ =>
        {
            order.Add("broker");
            return new CoworkApprovalDecision(CoworkApprovalOutcome.Allowed);
        });
        var audit = new ScriptedAudit(_ => order.Add("audit"));
        var host = Host(provider, gate, broker, audit);
        var lease = host.BeginSession(Session());
        lease.SetTurnOrigin(CoworkTurnOrigin.Local);
        var result = await host.InvokeAsync("s", "artifact_read", Object("{\"n\":1}"), TestContext.Current.CancellationToken);
        order.ShouldBe(["preview", "gate", "broker", "invoke", "audit"]);
        result.IsError.ShouldBeTrue();
        Text(result).ShouldBe(CoworkResultLimiter.CappedMessage);
        result.Content.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task UnofferedTool_IsRefusedBeforePreview()
    {
        var previewed = false;
        var provider = new ScriptedProvider { OnPreview = () => previewed = true };
        var host = Host(provider, new ScriptedGate(_ => Allow()), new ScriptedBroker(_ => Allowed()), new ScriptedAudit(_ => { }));
        _ = host.BeginSession(Session());
        var result = await host.InvokeAsync("s", "missing_tool", Object("{}"), TestContext.Current.CancellationToken);
        previewed.ShouldBeFalse();
        Text(result).ShouldBe(CoworkToolMessages.NotOffered);
    }

    [Fact]
    public async Task ApprovalTimeout_IsADenialTheModelReadsAsNotApproved()
    {
        var invoked = false;
        var provider = new ScriptedProvider { OnInvoke = () => { invoked = true; return CoworkToolResult.Text("ran"); } };
        var broker = new ScriptedBroker(async request =>
        {
            _ = request;
            await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
            return new CoworkApprovalDecision(CoworkApprovalOutcome.Allowed);
        });
        var host = Host(
            provider,
            new ScriptedGate(_ => new CoworkPolicyDecision(CoworkPolicyOutcome.Ask, false, null)),
            broker,
            new ScriptedAudit(_ => { }),
            new CoworkToolHostOptions { ApprovalTimeout = TimeSpan.FromMilliseconds(30) });
        _ = host.BeginSession(Session());
        var result = await host.InvokeAsync("s", "artifact_read", Object("{}"), TestContext.Current.CancellationToken);
        invoked.ShouldBeFalse();
        Text(result).ShouldBe(CoworkToolMessages.NotApproved);
    }

    [Fact]
    public async Task TaintedSession_AsksForActuatingCallDespiteStoredGrantAndSkip()
    {
        var asked = 0;
        var grants = new MemoryGrants();
        await grants.GrantAsync(new CoworkGrantKey("artifact.write", "cowork"), TestContext.Current.CancellationToken);
        var provider = new ScriptedProvider
        {
            Effect = CoworkToolEffect.Actuating,
            Approval = CoworkApprovalRequirement.ByPolicy,
            Grant = new CoworkGrantKey("artifact.write", "cowork"),
            OnInvoke = () => CoworkToolResult.Text("page", isUntrusted: true),
        };
        var host = Host(
            provider,
            new CoworkPolicyGate(grants, () => CoworkPermissionMode.Skip),
            new ScriptedBroker(_ => { asked++; return new CoworkApprovalDecision(CoworkApprovalOutcome.Allowed); }),
            new ScriptedAudit(_ => { }),
            grants: grants);
        var lease = host.BeginSession(Session());
        var first = await host.InvokeAsync("s", "artifact_read", Object("{}"), TestContext.Current.CancellationToken);
        first.IsError.ShouldBeFalse();
        asked.ShouldBe(0);
        lease.IsTainted.ShouldBeTrue();
        _ = await host.InvokeAsync("s", "artifact_read", Object("{}"), TestContext.Current.CancellationToken);
        asked.ShouldBe(1);
        lease.IsTainted.ShouldBeTrue();
    }

    [Fact]
    public async Task ModelOriginArgument_IsIgnored()
    {
        CoworkTurnOrigin? seen = null;
        var auditOrigin = CoworkTurnOrigin.Dispatch;
        var provider = new ScriptedProvider
        {
            OnInvokeWith = invocation =>
            {
                seen = invocation.Origin;
                return CoworkToolResult.Text("ok");
            },
        };
        var host = Host(provider, new ScriptedGate(_ => Allow()), new ScriptedBroker(_ => Allowed()), new ScriptedAudit(entry => auditOrigin = entry.Origin));
        var lease = host.BeginSession(Session());
        lease.SetTurnOrigin(CoworkTurnOrigin.Local);
        _ = await host.InvokeAsync("s", "artifact_read", Object("{\"origin\":\"Dispatch\"}"), TestContext.Current.CancellationToken);
        seen.ShouldBe(CoworkTurnOrigin.Local);
        auditOrigin.ShouldBe(CoworkTurnOrigin.Local);
    }

    [Fact]
    public async Task DispatchOrigin_MustBeLocalDenial()
    {
        var invoked = false;
        var provider = new ScriptedProvider
        {
            Approval = CoworkApprovalRequirement.EveryCallOnExecutingDevice,
            OnInvoke = () => { invoked = true; return CoworkToolResult.Text("ran"); },
        };
        var host = Host(
            provider,
            new CoworkPolicyGate(new MemoryGrants(), () => CoworkPermissionMode.Skip),
            new ScriptedBroker(_ => new CoworkApprovalDecision(CoworkApprovalOutcome.Allowed)),
            new ScriptedAudit(_ => { }));
        var lease = host.BeginSession(Session());
        lease.SetTurnOrigin(CoworkTurnOrigin.Dispatch);
        var result = await host.InvokeAsync("s", "artifact_read", Object("{}"), TestContext.Current.CancellationToken);
        invoked.ShouldBeFalse();
        Text(result).ShouldBe(CoworkToolMessages.NotApproved);
    }

    [Fact]
    public async Task OneCallAtATime_SecondWaitsUntilFirstReleases()
    {
        var entered = 0;
        var hold = 1;
        var provider = new ScriptedProvider
        {
            OnPreviewAsync = async () =>
            {
                var count = Interlocked.Increment(ref entered);
                while (count == 1 && Volatile.Read(ref hold) == 1)
                {
                    await Task.Delay(10, TestContext.Current.CancellationToken).ConfigureAwait(false);
                }
            },
        };
        var host = Host(provider, new ScriptedGate(_ => Allow()), new ScriptedBroker(_ => Allowed()), new ScriptedAudit(_ => { }));
        _ = host.BeginSession(Session());
        var first = host.InvokeAsync("s", "artifact_read", Object("{}"), TestContext.Current.CancellationToken);
        var ready = await WaitUntilAsync(() => Volatile.Read(ref entered) == 1);
        ready.ShouldBeTrue();
        var second = host.InvokeAsync("s", "artifact_read", Object("{}"), TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Volatile.Read(ref entered).ShouldBe(1);
        Volatile.Write(ref hold, 0);
        _ = await first.ConfigureAwait(true);
        _ = await second.ConfigureAwait(true);
        Volatile.Read(ref entered).ShouldBe(2);
    }

    [Theory]
    [InlineData(40, true)]
    [InlineData(41, false)]
    public void DescriptorNameLength_Boundary(int length, bool valid)
    {
        var name = "ab_" + new string('a', length - 3);
        name.Length.ShouldBe(length);
        CoworkDescriptorValidator.TryValidate(Descriptor(name, "ab", "ok", "{}", CoworkToolEffect.ReadOnly, CoworkApprovalRequirement.ByPolicy), out _).ShouldBe(valid);
    }

    [Fact]
    public void DescriptorBoundaries_RefuseTheNamedCases()
    {
        CoworkDescriptorValidator.TryValidate(Descriptor("ab_tool", "ab", new string('x', 1024), "{}", CoworkToolEffect.ReadOnly, CoworkApprovalRequirement.ByPolicy), out _).ShouldBeTrue();
        CoworkDescriptorValidator.TryValidate(Descriptor("ab_tool", "ab", new string('x', 1025), "{}", CoworkToolEffect.ReadOnly, CoworkApprovalRequirement.ByPolicy), out _).ShouldBeFalse();
        CoworkDescriptorValidator.TryValidate(Descriptor("ab__c", "ab", "ok", "{}", CoworkToolEffect.ReadOnly, CoworkApprovalRequirement.ByPolicy), out _).ShouldBeFalse();
        CoworkDescriptorValidator.TryValidate(Descriptor("cowork_tool", "cowork", "ok", "{}", CoworkToolEffect.ReadOnly, CoworkApprovalRequirement.ByPolicy), out _).ShouldBeFalse();
        CoworkDescriptorValidator.TryValidate(Descriptor("ab_tool", "ab", "ok", "[]", CoworkToolEffect.ReadOnly, CoworkApprovalRequirement.ByPolicy), out _).ShouldBeFalse();
        CoworkDescriptorValidator.TryValidate(Descriptor("ab_tool", "ab", "ok", "{}", CoworkToolEffect.Destructive, CoworkApprovalRequirement.ByProviderGate), out _).ShouldBeFalse();
    }

    [Fact]
    public void ResultCaps_FailInsteadOfTruncating()
    {
        var exact = CoworkResultLimiter.Apply(CoworkToolResult.Text(new string('a', CoworkResultLimiter.MaxTextChars)));
        exact.IsError.ShouldBeFalse();
        Text(exact).Length.ShouldBe(CoworkResultLimiter.MaxTextChars);
        CoworkResultLimiter.Apply(CoworkToolResult.Text(new string('a', CoworkResultLimiter.MaxTextChars + 1))).IsError.ShouldBeTrue();
        var images = new CoworkContentBlock[]
        {
            new CoworkImageBlock(new byte[1], "image/png", "a", true),
            new CoworkImageBlock(new byte[1], "image/jpeg", "b", true),
            new CoworkImageBlock(new byte[1], "image/webp", "c", true),
        };
        CoworkResultLimiter.Apply(new CoworkToolResult(images, false)).IsError.ShouldBeTrue();
        CoworkResultLimiter.Apply(new CoworkToolResult([new CoworkImageBlock(new byte[CoworkResultLimiter.MaxImageBytes + 1], "image/png", "big", true)], false)).IsError.ShouldBeTrue();
        CoworkResultLimiter.Apply(new CoworkToolResult([new CoworkImageBlock(new byte[1], "image/gif", "no", true)], false)).IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task ProviderException_IsFixedTextWithoutArguments()
    {
        var provider = new ScriptedProvider { OnInvoke = () => throw new InvalidOperationException("secret-argument") };
        var host = Host(provider, new ScriptedGate(_ => Allow()), new ScriptedBroker(_ => Allowed()), new ScriptedAudit(_ => { }));
        _ = host.BeginSession(Session());
        var result = await host.InvokeAsync("s", "artifact_read", Object("{\"secret\":\"value\"}"), TestContext.Current.CancellationToken);
        Text(result).ShouldBe(CoworkToolMessages.ProviderFailed);
        Text(result).ShouldNotContain("secret");
    }

    private static CoworkToolHost Host(
        ScriptedProvider provider,
        ICoworkPolicyGate gate,
        ICoworkApprovalBroker broker,
        ICoworkToolAudit audit,
        CoworkToolHostOptions? options = null,
        ICoworkGrantStore? grants = null) =>
        new([provider], gate, broker, grants ?? new MemoryGrants(), audit, options);

    private static CoworkSessionContext Session() => new()
    {
        SessionId = "s",
        WorkingDirectory = "/",
        TrustedFolders = [],
        ModelReadsImages = false,
    };

    private static CoworkPolicyDecision Allow() => new(CoworkPolicyOutcome.Allow, false, null);

    private static CoworkApprovalDecision Allowed() => new(CoworkApprovalOutcome.Allowed);

    private static JsonElement Object(string json) => JsonDocument.Parse(json).RootElement;

    private static string Text(CoworkToolResult result) => ((CoworkTextBlock)result.Content[0]).Text;

    private static CoworkToolDescriptor Descriptor(string name, string provider, string description, string schema, CoworkToolEffect effect, CoworkApprovalRequirement approval) =>
        new()
        {
            Name = name,
            ProviderId = provider,
            Description = description,
            InputSchema = JsonDocument.Parse(schema).RootElement,
            Effect = effect,
            Approval = approval,
        };

    private static async Task<bool> WaitUntilAsync(Func<bool> ready)
    {
        for (var i = 0; i < 50; i++)
        {
            if (ready())
            {
                return true;
            }

            await Task.Delay(20).ConfigureAwait(false);
        }

        return ready();
    }

    private sealed class ScriptedProvider : ICoworkToolProvider
    {
        public CoworkToolEffect Effect { get; init; } = CoworkToolEffect.ReadOnly;

        public CoworkApprovalRequirement Approval { get; init; } = CoworkApprovalRequirement.ByPolicy;

        public CoworkGrantKey? Grant { get; init; }

        public Action? OnPreview { get; init; }

        public Func<Task>? OnPreviewAsync { get; init; }

        public Func<CoworkToolResult>? OnInvoke { get; init; }

        public Func<CoworkToolInvocation, CoworkToolResult>? OnInvokeWith { get; init; }

        public string ProviderId => "artifact";

        public IReadOnlyList<CoworkToolDescriptor> Describe() =>
        [
            new CoworkToolDescriptor
            {
                Name = "artifact_read",
                ProviderId = ProviderId,
                Description = "Read",
                InputSchema = JsonDocument.Parse("{}").RootElement,
                Effect = Effect,
                Approval = Approval,
            },
        ];

        public ValueTask<CoworkProviderState> GetStateAsync(CoworkSessionContext session, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new CoworkProviderState(CoworkProviderAvailability.Available, null));

        public async ValueTask<CoworkCallPreview> PreviewAsync(CoworkSessionContext session, CoworkToolInvocation invocation, CancellationToken cancellationToken)
        {
            OnPreview?.Invoke();
            if (OnPreviewAsync is not null)
            {
                await OnPreviewAsync().ConfigureAwait(false);
            }

            return new CoworkCallPreview("read", "target", [], Grant);
        }

        public ValueTask<CoworkToolResult> InvokeAsync(CoworkSessionContext session, CoworkToolInvocation invocation, CancellationToken cancellationToken)
        {
            if (OnInvokeWith is not null)
            {
                return ValueTask.FromResult(OnInvokeWith(invocation));
            }

            return ValueTask.FromResult(OnInvoke?.Invoke() ?? CoworkToolResult.Text("ok"));
        }

        public ValueTask EndSessionAsync(string sessionId, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class ScriptedGate(Func<CoworkPolicyRequest, CoworkPolicyDecision> decide) : ICoworkPolicyGate
    {
        public ValueTask<CoworkPolicyDecision> DecideAsync(CoworkPolicyRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(decide(request));
    }

    private sealed class ScriptedBroker(Func<CoworkApprovalRequest, CoworkApprovalDecision> decide) : ICoworkApprovalBroker
    {
        public ScriptedBroker(Func<CoworkApprovalRequest, Task<CoworkApprovalDecision>> decide)
            : this(_ => new CoworkApprovalDecision(CoworkApprovalOutcome.Denied))
        {
            _async = decide;
        }

        private readonly Func<CoworkApprovalRequest, Task<CoworkApprovalDecision>>? _async;

        public async ValueTask<CoworkApprovalDecision> RequestAsync(CoworkApprovalRequest request, CancellationToken cancellationToken)
        {
            if (_async is not null)
            {
                var pending = _async(request);
                var finished = await Task.WhenAny(pending, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);
                if (finished != pending)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                return await pending.ConfigureAwait(false);
            }

            return decide(request);
        }
    }

    private sealed class ScriptedAudit(Action<CoworkToolAuditEntry> record) : ICoworkToolAudit
    {
        public ValueTask RecordAsync(CoworkToolAuditEntry entry, CancellationToken cancellationToken)
        {
            record(entry);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MemoryGrants : ICoworkGrantStore
    {
        private readonly List<CoworkGrantKey> _keys = [];

        public ValueTask<bool> IsGrantedAsync(CoworkGrantKey key, CancellationToken cancellationToken) =>
            ValueTask.FromResult(_keys.Contains(key));

        public ValueTask GrantAsync(CoworkGrantKey key, CancellationToken cancellationToken)
        {
            _keys.Add(key);
            return ValueTask.CompletedTask;
        }

        public ValueTask RevokeAsync(CoworkGrantKey key, CancellationToken cancellationToken)
        {
            _ = _keys.Remove(key);
            return ValueTask.CompletedTask;
        }
    }
}
