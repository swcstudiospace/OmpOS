using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;

using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Executor;
using Autonogrammer.Harness.ComputerUse.Fakes;

using Shouldly;

using Xunit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Autonogrammer.ArchitectureTests;

/// <summary>Compiled backend-call boundaries plus project and package isolation for Computer Use.</summary>
public sealed class ComputerUseBoundaryTests
{
    private static readonly System.Reflection.Assembly[] ProductAssemblies =
        [.. LoadProductAssemblies(), typeof(Process).Assembly];

    private static readonly Architecture Arch = new ArchLoader().LoadAssemblies(ProductAssemblies).Build();

    private static readonly IObjectProvider<MethodMember> BackendInjection = MethodMembers().That()
        .AreDeclaredIn(Types().That().AreAssignableTo(typeof(IComputerUseBackend)))
        .And().HaveNameStartingWith(nameof(IComputerUseBackend.InjectAsync) + "(");

    /// <summary>The only types that may reference a computer-use backend, by full name. Nested types of a listed type are included. The computer-use composition, options, vision pipeline, tool provider and head result types are listed because they are the computer-use composition and tooling layer that already holds the backend; the satellite list omitted them, and four of them exist on the satellite line.</summary>
    private const string BackendAllowListPattern =
        @"^Autonogrammer\.(Harness\.ComputerUse\.(Abstractions\.IComputerUseBackend|Executor\.ComputerActionExecutor|Policy\.KillSwitchCoordinator|ComputerUseComposition|ComputerUseCompositionOptions|ComputerUseToolsOptions|Frames\.VisionPipeline|Tools\.ComputerToolProvider|NullComputerUseBackend|Fakes\.FakeComputerUseBackend|X11\.X11Backend|Windows\.WindowsBackend|MacOS\.MacOSBackend)|Native\.Composition\.ComputerUse\.(ComputerUseHeadComposition|ComputerStatusAdapter|ComputerUseHeadResult))([+.].+)?$";

    /// <summary>The executor and every type nested under it, including its compiler-generated async state machines. Nested full names carry '+' or '.', as the allow-list above assumes.</summary>
    private const string ExecutorFamilyPattern = @"^Autonogrammer\.Harness\.ComputerUse\.Executor\.ComputerActionExecutor([+.].+)?$";

    [Fact]
    public void InjectAsyncIsReferencedByTheExecutorOnly()
    {
        // ArchUnitNET rule for ordinary types. It reports no family callers for the executor, so it covers the non-family side only;
        // the reflection scan below is the check for the family.
        ForbidInjection(Types().That().DoNotHaveFullNameMatching(ExecutorFamilyPattern)).Check(Arch);

        // Reflection over compiled IL. InjectSequenceAsync's body is compiled into a nested async state machine, so the family's
        // call lives in that state machine's MoveNext. Every declared member of every product type is decoded and its call-site
        // tokens resolved, which reads that body directly and does not depend on type naming.
        List<string> outsiderCallers = [];
        List<MethodBase> familyCallers = [];
        foreach (System.Type type in ProductAssemblies.SelectMany(static assembly => assembly.GetTypes()))
        {
            foreach (MethodBase member in DeclaredMembers(type).Where(CallsBackendInjection))
            {
                if (IsWithinExecutorFamily(type))
                {
                    familyCallers.Add(member);
                }
                else
                {
                    outsiderCallers.Add($"{type.FullName}::{member.Name}");
                }
            }
        }

        outsiderCallers.ShouldBeEmpty("only the executor family may call InjectAsync");
        familyCallers.ShouldNotBeEmpty("the executor family must call InjectAsync; it is the only permitted caller");
        familyCallers.Any(static member => member.DeclaringType is { } declaring && typeof(IAsyncStateMachine).IsAssignableFrom(declaring))
            .ShouldBeTrue("the InjectAsync call must sit in InjectSequenceAsync's compiled state machine, the body that awaits it");
    }

    [Fact]
    public void BackendDependenciesStayWithinHarnessAndNativeComposition()
    {
        Types().That().DoNotResideInNamespaceMatching(@"^Autonogrammer\.Harness\.ComputerUse(\..*)?$")
            .And().DoNotResideInNamespace("Autonogrammer.Native.Composition.ComputerUse")
            .Should().NotDependOnAny(Types().That().AreAssignableTo(typeof(IComputerUseBackend)))
            .Because("tools may observe through the harness, but heads and views must not acquire a backend outside desktop composition")
            .Check(Arch);
    }

    [Fact]
    public void BackendReferencesStayOnTheAllowList()
    {
        Types().That().DoNotHaveFullNameMatching(BackendAllowListPattern)
            .Should().NotDependOnAny(Types().That().AreAssignableTo(typeof(IComputerUseBackend)))
            .Because("only the listed backend, executor, policy, composition and status types may reference a computer-use backend")
            .Check(Arch);
    }

    [Fact]
    public void CompiledSecondInjectCallersAreRejected()
    {
        var architecture = new ArchLoader().LoadAssemblies(
            typeof(ComputerUseBoundaryTests).Assembly,
            typeof(ComputerActionExecutor).Assembly,
            typeof(IComputerUseBackend).Assembly).Build();

        foreach (System.Type caller in new[] { typeof(InterfaceInjector), typeof(ConcreteInjector) })
        {
            var rule = ForbidInjection(Types().That().Are(caller));
            rule.HasNoViolations(architecture).ShouldBeFalse();
            Should.Throw<FailedArchRuleException>(() => rule.Check(architecture));
        }
    }

    [Fact]
    public void SyntheticIsolatedHeadAndPresentationEdgesAreFlagged()
    {
        foreach (string head in new[]
        {
            "Autonogrammer.Web", "Autonogrammer.UI", "Autonogrammer.Api",
            "Autonogrammer.AgentWorker", "Autonogrammer.Android",
        })
        {
            ComputerUseBoundaryChecker.FindForbiddenProjectEdges(
                [(head, "Autonogrammer.Harness.ComputerUse")]).ShouldNotBeEmpty();
            ComputerUseBoundaryChecker.FindForbiddenProjectEdges(
                [(head, "Autonogrammer.Harness.ComputerUse.Abstractions")]).ShouldNotBeEmpty();
        }

        ComputerUseBoundaryChecker.FindForbiddenProjectEdges(
            [("Autonogrammer.Presentation", "Autonogrammer.Harness.ComputerUse.X11")]).ShouldNotBeEmpty();
        ComputerUseBoundaryChecker.FindForbiddenProjectEdges(
            [("Autonogrammer.Presentation", "Autonogrammer.Harness.ComputerUse.Abstractions")]).ShouldBeEmpty();
    }

    [Fact]
    public void AbstractionsHasNoProcessDependencyAndNoPackageReference()
    {
        string root = ProjectGraphChecker.FindRepoRoot();
        string project = Path.Combine(root, "src", "Autonogrammer.Harness.ComputerUse.Abstractions");
        var document = XDocument.Load(Path.Combine(project, "Autonogrammer.Harness.ComputerUse.Abstractions.csproj"));
        document.Descendants().Where(static element => string.Equals(element.Name.LocalName, "PackageReference", StringComparison.Ordinal)).ShouldBeEmpty();
        Types().That().ResideInAssembly(typeof(IComputerUseBackend).Assembly)
            .Should().NotDependOnAny(typeof(Process)).Check(Arch);
    }

    [Fact]
    public void ComputerUseCoreAndAbstractionsDoNotReferenceAnOsBackend()
    {
        string root = ProjectGraphChecker.FindRepoRoot();
        ProjectGraphChecker.ReadEdges(root)
            .Where(static edge => edge.From is "Autonogrammer.Harness.ComputerUse" or "Autonogrammer.Harness.ComputerUse.Abstractions")
            .Where(static edge => edge.To is "Autonogrammer.Harness.ComputerUse.X11"
                or "Autonogrammer.Harness.ComputerUse.Windows" or "Autonogrammer.Harness.ComputerUse.MacOS")
            .ShouldBeEmpty();
    }

    [Fact]
    public void WebUiApiWorkerAndAndroidDoNotReferenceComputerUseProjects()
    {
        string root = ProjectGraphChecker.FindRepoRoot();
        List<string> violations = ComputerUseBoundaryChecker.FindForbiddenProjectEdges(ProjectGraphChecker.ReadEdges(root));
        violations.ShouldBeEmpty();
    }

    private static TypesShouldConjunctionWithDescription ForbidInjection(IObjectProvider<IType> callers) =>
        Types().That().Are(callers).Should().NotCallAny(BackendInjection)
            .Because("only ComputerActionExecutor may inject after the permission gate and single-use ticket");

    private const BindingFlags DeclaredMembersFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
        | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>The opcode table keyed by the 16-bit opcode value (one-byte opcodes, or 0xFE-prefixed two-byte opcodes).</summary>
    private static readonly Dictionary<ushort, System.Reflection.Emit.OpCode> IlOpCodes =
        typeof(System.Reflection.Emit.OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.FieldType == typeof(System.Reflection.Emit.OpCode))
            .Select(static field => (System.Reflection.Emit.OpCode)field.GetValue(null)!)
            .ToDictionary(static opCode => unchecked((ushort)opCode.Value));

    private static bool IsWithinExecutorFamily(System.Type type)
    {
        for (System.Type? current = type; current is not null; current = current.DeclaringType)
        {
            if (current == typeof(ComputerActionExecutor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Every method, instance constructor and static constructor declared on the type, including compiler-generated ones such as async state machines.</summary>
    private static IEnumerable<MethodBase> DeclaredMembers(System.Type type)
    {
        foreach (MethodInfo method in type.GetMethods(DeclaredMembersFlags))
        {
            yield return method;
        }

        foreach (ConstructorInfo constructor in type.GetConstructors(DeclaredMembersFlags & ~BindingFlags.Static))
        {
            yield return constructor;
        }

        if (type.TypeInitializer is { } initializer)
        {
            yield return initializer;
        }
    }

    /// <summary>
    /// True when the member's compiled IL contains a call, callvirt, ldftn or ldvirtftn whose resolved target is
    /// <see cref="IComputerUseBackend.InjectAsync"/>. Each instruction is decoded by its operand size, so operand bytes are
    /// never misread as opcodes.
    /// </summary>
    private static bool CallsBackendInjection(MethodBase method)
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

            System.Reflection.Emit.OpCode opCode = IlOpCodes[code];
            if (IsMethodReference(opCode)
                && IsBackendInjection(method.Module.ResolveMethod(BitConverter.ToInt32(il, offset), typeArguments, methodArguments)
                    ?? throw new InvalidOperationException($"Unresolvable call target in {method}.")))
            {
                return true;
            }

            offset += OperandSize(opCode, il, offset);
        }

        return false;
    }

    private static bool IsMethodReference(System.Reflection.Emit.OpCode opCode) =>
        opCode.OperandType == System.Reflection.Emit.OperandType.InlineMethod
        && (opCode == System.Reflection.Emit.OpCodes.Call || opCode == System.Reflection.Emit.OpCodes.Callvirt
            || opCode == System.Reflection.Emit.OpCodes.Ldftn || opCode == System.Reflection.Emit.OpCodes.Ldvirtftn);

    private static bool IsBackendInjection(MethodBase target)
    {
        if (!string.Equals(target.Name, nameof(IComputerUseBackend.InjectAsync), StringComparison.Ordinal)
            || target.DeclaringType is not { } declaring
            || !typeof(IComputerUseBackend).IsAssignableFrom(declaring))
        {
            return false;
        }

        ParameterInfo[] parameters = target.GetParameters();
        return parameters.Length == 2
            && parameters[0].ParameterType == typeof(InputPrimitive)
            && parameters[1].ParameterType == typeof(CancellationToken);
    }

    /// <summary>Operand byte count of an instruction, as defined by ECMA-335 Partition III.</summary>
    private static int OperandSize(System.Reflection.Emit.OpCode opCode, byte[] il, int offset) => opCode.OperandType switch
    {
        System.Reflection.Emit.OperandType.InlineNone => 0,
        System.Reflection.Emit.OperandType.ShortInlineBrTarget or System.Reflection.Emit.OperandType.ShortInlineI or System.Reflection.Emit.OperandType.ShortInlineVar => 1,
        System.Reflection.Emit.OperandType.InlineVar => 2,
        System.Reflection.Emit.OperandType.InlineI8 or System.Reflection.Emit.OperandType.InlineR => 8,
        System.Reflection.Emit.OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, offset)),
        _ => 4,
    };

    private static IEnumerable<System.Reflection.Assembly> LoadProductAssemblies()
    {
        string root = ProjectGraphChecker.FindRepoRoot();
        foreach (string name in ProjectGraphChecker.ReadProjectNames(root))
        {
            string path = Path.Combine(AppContext.BaseDirectory, name + ".dll");
            if (!File.Exists(path))
            {
                // The Android head targets net10.0-android and is covered by the project graph in this suite.
                if (string.Equals(name, "Autonogrammer.Android", StringComparison.Ordinal))
                {
                    continue;
                }

                throw new FileNotFoundException($"Expected built assembly for {name} at {path}.");
            }

            yield return System.Reflection.Assembly.LoadFrom(path);
        }
    }

    // These compile actual interface and concrete dispatch edges; neither method is executed.
    private static class InterfaceInjector
    {
        public static ValueTask InjectAsync(IComputerUseBackend backend, InputPrimitive primitive, CancellationToken cancellationToken) =>
            backend.InjectAsync(primitive, cancellationToken);
    }

    private static class ConcreteInjector
    {
        public static ValueTask InjectAsync(FakeComputerUseBackend backend, InputPrimitive primitive, CancellationToken cancellationToken) =>
            backend.InjectAsync(primitive, cancellationToken);
    }

    /// <summary>Pure checkers so a synthetic violation can be asserted without editing the tree.</summary>
    private static class ComputerUseBoundaryChecker
    {
        private static readonly string[] IsolatedHeads =
        [
            "Autonogrammer.Web",
            "Autonogrammer.UI",
            "Autonogrammer.Api",
            "Autonogrammer.AgentWorker",
            "Autonogrammer.Android",
        ];



        public static List<string> FindForbiddenProjectEdges(IEnumerable<(string From, string To)> edges)
        {
            List<string> violations = [];
            foreach ((string from, string to) in edges)
            {
                if (!to.StartsWith("Autonogrammer.Harness.ComputerUse", StringComparison.Ordinal))
                {
                    continue;
                }

                if (IsolatedHeads.Contains(from, StringComparer.Ordinal))
                {
                    violations.Add(from + " -> " + to);
                }

                if (string.Equals(from, "Autonogrammer.Presentation", StringComparison.Ordinal)
                    && !string.Equals(to, "Autonogrammer.Harness.ComputerUse.Abstractions", StringComparison.Ordinal))
                {
                    violations.Add(from + " -> " + to);
                }
            }

            return violations;
        }
    }
}
