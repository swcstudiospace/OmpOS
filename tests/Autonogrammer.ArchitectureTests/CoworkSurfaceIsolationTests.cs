using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Cowork.Sessions;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Kanban;
using Autonogrammer.Sessions;

using Shouldly;

using Xunit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Autonogrammer.ArchitectureTests;

/// <summary>
/// The five isolation rules of ADR-0032 D2 that keep a Cowork task out of the Code list, the Kanban board, the Code registry and the API Code path
/// (AG-69-10, decision D-31). Each rule runs over the built product assemblies and has a negative control: a fixture that violates it must be
/// reported, so a rule that matches nothing cannot pass silently.
/// </summary>
public sealed class CoworkSurfaceIsolationTests
{
    private const string KanbanNamespace = "Autonogrammer.Presentation.Kanban";
    private const string AgentLoopSessionsNamespace = "Autonogrammer.Harness.AgentLoop.Sessions";
    private const string CoworkNamespace = "Autonogrammer.Cowork";
    private const string ContractsCoworkNamespace = "Autonogrammer.Contracts.Cowork";
    private const string PresentationCoworkNamespace = "Autonogrammer.Presentation.Cowork";
    private const string ApiCoworkNamespace = "Autonogrammer.Api.Cowork";
    private const string FixtureNamespace = "Autonogrammer.ArchitectureTests.CoworkFixtures";

    private const string KanbanBoardModelFullName = "Autonogrammer.Presentation.Kanban.KanbanBoardModel";
    private const string AgentHeadFullName = "Autonogrammer.Native.Agents.AgentHead";
    private const string WebWorkbenchFullName = "Autonogrammer.Web.Services.WebWorkbench";

    /// <summary>The AgentHead members that construct the Kanban board: the build path and its compiler-generated state machines and lambdas.</summary>
    private static readonly string[] AgentHeadBuildMembers = ["BuildAsync", "BuildModelsAsync"];

    /// <summary>
    /// The ISessionRepository members that may return a Session collection without a SessionSurface parameter. GetAsync returns one Session, not a
    /// collection; ListSessionIdsByStatusAsync returns identifiers for the reaper. Every other listing must name its surface.
    /// </summary>
    private static readonly string[] RepositoryExemptMembers =
    [
        nameof(ISessionRepository.GetAsync),
        nameof(ISessionRepository.ListSessionIdsByStatusAsync),
    ];

    private static readonly System.Reflection.Assembly[] ProductAssemblies = LoadProductAssemblies();

    private static readonly System.Type[] ProductTypes = [.. ProductAssemblies.SelectMany(SafeTypes)];

    private static readonly Architecture Arch = new ArchLoader().LoadAssemblies(ProductAssemblies).Build();

    private static readonly IObjectProvider<IType> CoworkTypes = Types().That().ResideInNamespaceMatching(SubtreePattern(CoworkNamespace))
        .Or().ResideInNamespaceMatching(SubtreePattern(ContractsCoworkNamespace))
        .Or().ResideInNamespaceMatching(SubtreePattern(PresentationCoworkNamespace));

    /// <summary>Rule 1: no type implements both the Cowork catalog and the Code catalog.</summary>
    [Fact]
    public void NoTypeImplementsBothCatalogInterfaces()
    {
        FindDualCatalogImplementers(ProductTypes).ShouldBeEmpty("a type that is both a Cowork and a Code catalog would let a Cowork row reach the Code list");
        ProductTypes.Count(static type => typeof(ISessionCatalog).IsAssignableFrom(type) && !type.IsInterface).ShouldBeGreaterThan(0, "the rule must see the Code catalog implementers");
        ProductTypes.Count(static type => typeof(ICoworkSessionCatalog).IsAssignableFrom(type) && !type.IsInterface).ShouldBeGreaterThan(0, "the rule must see the Cowork catalog implementers");
    }

    /// <summary>Negative control for rule 1: a fixture that implements both catalogs is reported.</summary>
    [Fact]
    public void DualCatalogFixtureIsReported()
    {
        FindDualCatalogImplementers([typeof(DualCatalogFixture)]).ShouldBe([typeof(DualCatalogFixture).FullName!]);
    }

    /// <summary>Rule 2: the Kanban presentation and the Harness agent-loop sessions never reference a Cowork type.</summary>
    [Fact]
    public void KanbanAndAgentLoopSessionsNeverReferenceCowork()
    {
        ViolatesCoworkReference(Arch, KanbanNamespace).ShouldBeFalse("Presentation.Kanban must not reference Cowork types");
        ViolatesCoworkReference(Arch, AgentLoopSessionsNamespace).ShouldBeFalse("Harness.AgentLoop.Sessions must not reference Cowork types");
        ProductTypes.Any(type => InNamespace(type, KanbanNamespace)).ShouldBeTrue("the rule must select the Kanban types it guards");
        ProductTypes.Any(type => InNamespace(type, AgentLoopSessionsNamespace)).ShouldBeTrue("the rule must select the agent-loop session types it guards");
    }

    /// <summary>Negative control for rule 2: a Kanban-namespace type that names a Cowork registry is reported.</summary>
    [Fact]
    public void KanbanConsumerOfCoworkRegistryIsReported()
    {
        var fixtureArchitecture = new ArchLoader().LoadAssemblies(typeof(CoworkSurfaceIsolationTests).Assembly, typeof(CoworkSessionRegistry).Assembly).Build();
        ViolatesCoworkReference(fixtureArchitecture, FixtureNamespace).ShouldBeTrue("the fixture in the fixture namespace references CoworkSessionRegistry and must violate the rule");
    }

    /// <summary>Rule 3: KanbanBoardModel is constructed only by the AgentHead build path and the WebWorkbench constructor.</summary>
    [Fact]
    public void KanbanBoardModelIsConstructedOnlyInTheHeadBuildPaths()
    {
        var sites = KanbanConstructionSites(ProductTypes).ToList();
        sites.Where(static site => !IsAllowedKanbanSite(site)).Select(static site => site.DeclaringType?.FullName + "::" + site.Name).ShouldBeEmpty(
            "KanbanBoardModel may be constructed only by AgentHead.Build (BuildAsync/BuildModelsAsync) and the WebWorkbench constructor");
        sites.Any(IsAgentHeadSite).ShouldBeTrue("the AgentHead build path must be found");
        sites.Any(static site => string.Equals(site.DeclaringType?.FullName, WebWorkbenchFullName, StringComparison.Ordinal)).ShouldBeTrue("the WebWorkbench constructor must be found");
    }

    /// <summary>Negative control for rule 3: a construction outside the build paths is reported.</summary>
    [Fact]
    public void KanbanConstructionOutsideTheBuildPathIsReported()
    {
        var sites = KanbanConstructionSites([typeof(KanbanConstructorFixture)]).ToList();
        sites.ShouldNotBeEmpty("the fixture constructs KanbanBoardModel and must be found by the scanner");
        sites.Where(static site => !IsAllowedKanbanSite(site)).ShouldNotBeEmpty("a fixture is not an allowed construction site");
    }

    /// <summary>Rule 4: no ISessionRepository listing returns Session without a SessionSurface parameter, except the named exemptions.</summary>
    [Fact]
    public void RepositoryListingsNameTheSurface()
    {
        FindUnfilteredSessionListings(typeof(ISessionRepository)).ShouldBeEmpty("every Session listing on the repository must take a SessionSurface");
        typeof(ISessionRepository).GetMethod(nameof(ISessionRepository.ListByOwnerAsync)).ShouldNotBeNull("the surface-filtered listing must exist");
        foreach (var exempt in RepositoryExemptMembers)
        {
            typeof(ISessionRepository).GetMethod(exempt).ShouldNotBeNull("an exemption must name a member the repository declares");
        }
    }

    /// <summary>Negative control for rule 4: an unfiltered Session listing is reported.</summary>
    [Fact]
    public void UnfilteredRepositoryListingFixtureIsReported()
    {
        FindUnfilteredSessionListings(typeof(IUnfilteredRepositoryFixture)).ShouldBe([nameof(IUnfilteredRepositoryFixture.ListAllAsync)]);
    }

    /// <summary>Rule 5: the Api.Cowork module uses neither the Code session registry nor the Code catalog projector, by type name.</summary>
    [Fact]
    public void ApiCoworkUsesNeitherSessionRegistryNorCatalogProjector()
    {
        ViolatesApiCoworkRule(Arch, ApiCoworkNamespace).ShouldBeFalse("Api.Cowork must not depend on SessionRegistry or CatalogProjector");
        ProductTypes.Any(type => InNamespace(type, ApiCoworkNamespace)).ShouldBeTrue("the rule must select the Api.Cowork types it guards");
    }

    /// <summary>Negative control for rule 5: a consumer that names a type called SessionRegistry is reported.</summary>
    [Fact]
    public void ApiCoworkConsumerOfSessionRegistryIsReported()
    {
        var fixtureArchitecture = new ArchLoader().LoadAssemblies(typeof(CoworkSurfaceIsolationTests).Assembly).Build();
        ViolatesApiCoworkRule(fixtureArchitecture, FixtureNamespace).ShouldBeTrue("the fixture consumer depends on a type named SessionRegistry and must violate the rule");
    }

    /// <summary>A pattern that matches a namespace and every namespace below it, and no longer name that shares its prefix.</summary>
    private static string SubtreePattern(string ns) => "^" + Regex.Escape(ns) + @"(\..+)?$";

    private static bool ViolatesCoworkReference(Architecture architecture, string consumerNamespace)
    {
        var rule = Types().That().ResideInNamespaceMatching(SubtreePattern(consumerNamespace))
            .Should().NotDependOnAny(CoworkTypes)
            .Because("a Cowork type must not reach the Code list or the Kanban board");
        return !rule.HasNoViolations(architecture);
    }

    private static bool ViolatesApiCoworkRule(Architecture architecture, string consumerNamespace)
    {
        var rule = Types().That().ResideInNamespaceMatching(SubtreePattern(consumerNamespace))
            .Should().NotDependOnAny(Types().That().HaveName("SessionRegistry").Or().HaveName("CatalogProjector"))
            .Because("the Cowork API routes read only the Cowork tables and never the Code session registry or projection");
        return !rule.HasNoViolations(architecture);
    }

    /// <summary>Returns the names of types that implement both catalog interfaces. The input is any set of types, so a fixture can be checked.</summary>
    private static List<string> FindDualCatalogImplementers(IEnumerable<System.Type> types) =>
        types.Where(static type => typeof(ICoworkSessionCatalog).IsAssignableFrom(type) && typeof(ISessionCatalog).IsAssignableFrom(type))
            .Select(static type => type.FullName!)
            .ToList();

    /// <summary>
    /// Returns the names of repository members that return a Session collection or stream without a SessionSurface parameter, excluding the named
    /// exemptions. A single Session is not a listing.
    /// </summary>
    private static List<string> FindUnfilteredSessionListings(System.Type repository) =>
        repository.GetMethods()
            .Where(method => !RepositoryExemptMembers.Contains(method.Name, StringComparer.Ordinal))
            .Where(method => ReturnsSessionListing(method.ReturnType))
            .Where(method => !method.GetParameters().Any(static parameter => parameter.ParameterType == typeof(SessionSurface)))
            .Select(static method => method.Name)
            .ToList();

    private static bool ReturnsSessionListing(System.Type returnType)
    {
        System.Type payload = returnType.IsGenericType && returnType.GetGenericTypeDefinition() is var definition
            && (definition == typeof(Task<>) || definition == typeof(ValueTask<>))
            ? returnType.GetGenericArguments()[0]
            : returnType;
        if (payload.IsArray)
        {
            return payload.GetElementType() == typeof(Session);
        }

        return payload.IsGenericType && payload.GetGenericArguments().Length == 1 && payload.GetGenericArguments()[0] == typeof(Session);
    }

    private static IEnumerable<MethodBase> KanbanConstructionSites(IEnumerable<System.Type> types) =>
        types.SelectMany(DeclaredMembers).Where(CallsKanbanConstructor);

    private static bool IsAllowedKanbanSite(MethodBase site)
    {
        for (System.Type? type = site.DeclaringType; type is not null; type = type.DeclaringType)
        {
            if (string.Equals(type.FullName, WebWorkbenchFullName, StringComparison.Ordinal))
            {
                return string.Equals(OwningMemberName(site), ".ctor", StringComparison.Ordinal);
            }

            if (string.Equals(type.FullName, AgentHeadFullName, StringComparison.Ordinal))
            {
                return AgentHeadBuildMembers.Contains(OwningMemberName(site), StringComparer.Ordinal);
            }
        }

        return false;
    }

    private static bool IsAgentHeadSite(MethodBase site) =>
        IsAllowedKanbanSite(site) && site.DeclaringType is not null && IsWithin(site.DeclaringType, AgentHeadFullName);

    private static bool IsWithin(System.Type type, string fullName)
    {
        for (System.Type? current = type; current is not null; current = current.DeclaringType)
        {
            if (string.Equals(current.FullName, fullName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The source member a compiled member came from. Async state machines are named &lt;Method&gt;d__N and lambdas or closures &lt;Method&gt;b__N,
    /// so the name is read from the member or from its enclosing compiler-generated type.
    /// </summary>
    private static string OwningMemberName(MethodBase member)
    {
        if (CompilerGeneratedName(member.Name) is { } fromMember)
        {
            return fromMember;
        }

        for (System.Type? type = member.DeclaringType; type is not null; type = type.DeclaringType)
        {
            if (CompilerGeneratedName(type.Name) is { } fromType)
            {
                return fromType;
            }
        }

        return member.Name;
    }

    /// <summary>The name between the angle brackets of a compiler-generated name such as &lt;Method&gt;d__3, or null for an ordinary name.</summary>
    private static string? CompilerGeneratedName(string name)
    {
        if (!name.StartsWith('<'))
        {
            return null;
        }

        var close = name.IndexOf('>', StringComparison.Ordinal);
        return close > 1 ? name[1..close] : null;
    }

    private static bool InNamespace(System.Type type, string ns) =>
        type.Namespace is { } name && (string.Equals(name, ns, StringComparison.Ordinal) || name.StartsWith(ns + ".", StringComparison.Ordinal));

    private static IEnumerable<System.Type> SafeTypes(System.Reflection.Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<System.Type>();
        }
    }

    private static System.Reflection.Assembly[] LoadProductAssemblies()
    {
        var root = ProjectGraphChecker.FindRepoRoot();
        List<System.Reflection.Assembly> assemblies = [];
        foreach (var name in ProjectGraphChecker.ReadProjectNames(root))
        {
            var path = Path.Combine(AppContext.BaseDirectory, name + ".dll");
            if (!File.Exists(path))
            {
                // The Android head targets net10.0-android and is built only in its own CI job.
                if (string.Equals(name, "Autonogrammer.Android", StringComparison.Ordinal))
                {
                    continue;
                }

                throw new FileNotFoundException($"Expected built assembly for {name} at {path}.");
            }

            assemblies.Add(System.Reflection.Assembly.LoadFrom(path));
        }

        return [.. assemblies];
    }

    private const BindingFlags DeclaredFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
        | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>The opcode table keyed by the 16-bit opcode value (one-byte opcodes, or 0xFE-prefixed two-byte opcodes).</summary>
    private static readonly Dictionary<ushort, OpCode> IlOpCodes =
        typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.FieldType == typeof(OpCode))
            .Select(static field => (OpCode)field.GetValue(null)!)
            .ToDictionary(static opCode => unchecked((ushort)opCode.Value));

    /// <summary>Every method, instance constructor and type initializer declared on the type, including compiler-generated ones.</summary>
    private static IEnumerable<MethodBase> DeclaredMembers(System.Type type)
    {
        foreach (MethodInfo method in type.GetMethods(DeclaredFlags))
        {
            yield return method;
        }

        foreach (ConstructorInfo constructor in type.GetConstructors(DeclaredFlags & ~BindingFlags.Static))
        {
            yield return constructor;
        }

        if (type.TypeInitializer is { } initializer)
        {
            yield return initializer;
        }
    }

    /// <summary>
    /// True when the member's compiled IL contains a newobj or call whose target is a KanbanBoardModel constructor. Each instruction is decoded by its
    /// operand size, so operand bytes are never misread as opcodes.
    /// </summary>
    private static bool CallsKanbanConstructor(MethodBase method)
    {
        System.Reflection.MethodBody? body = method.GetMethodBody();
        if (body is null)
        {
            return false;
        }

        byte[] il = body.GetILAsByteArray() ?? [];
        System.Type? declaring = method.DeclaringType;
        System.Type[]? typeArguments = declaring is { IsGenericType: true } ? declaring.GetGenericArguments() : null;
        System.Type[]? methodArguments = method.IsGenericMethodDefinition ? method.GetGenericArguments() : null;

        int offset = 0;
        while (offset < il.Length)
        {
            ushort code = il[offset++];
            if (code == 0xFE)
            {
                code = unchecked((ushort)(0xFE00 | il[offset++]));
            }

            OpCode opCode = IlOpCodes[code];
            if (IsConstructionOpCode(opCode)
                && IsKanbanConstructor(method.Module.ResolveMethod(BitConverter.ToInt32(il, offset), typeArguments, methodArguments)
                    ?? throw new InvalidOperationException($"Unresolvable call target in {method}.")))
            {
                return true;
            }

            offset += OperandSize(opCode, il, offset);
        }

        return false;
    }

    private static bool IsConstructionOpCode(OpCode opCode) =>
        opCode.OperandType == OperandType.InlineMethod && (opCode == OpCodes.Newobj || opCode == OpCodes.Call);

    private static bool IsKanbanConstructor(MethodBase target) =>
        target.IsConstructor && string.Equals(target.DeclaringType?.FullName, KanbanBoardModelFullName, StringComparison.Ordinal);

    /// <summary>Operand byte count of an instruction, as defined by ECMA-335 Partition III.</summary>
    private static int OperandSize(OpCode opCode, byte[] il, int offset) => opCode.OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, offset)),
        _ => 4,
    };

    /// <summary>Negative-control fixture for rule 1: implements both catalog interfaces, so the rule must report it.</summary>
    private abstract class DualCatalogFixture : ICoworkSessionCatalog, ISessionCatalog
    {
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        IReadOnlyList<CoworkSessionSummary> ICoworkSessionCatalog.Snapshot() => [];

        IReadOnlyList<SessionSummary> ISessionCatalog.Snapshot() => [];
    }

    /// <summary>Negative-control fixture for rule 4: lists every Session with no surface, so the rule must report it.</summary>
    private interface IUnfilteredRepositoryFixture
    {
        Task<IReadOnlyList<Session>> ListAllAsync(CancellationToken cancellationToken);
    }

    /// <summary>Negative-control fixture for rule 3: constructs the Kanban board outside the head build path, so the scanner must find it.</summary>
    private static class KanbanConstructorFixture
    {
        public static KanbanBoardModel Make(SessionCatalogModel catalog, TimeProvider timeProvider) => new(catalog, timeProvider);
    }
}
