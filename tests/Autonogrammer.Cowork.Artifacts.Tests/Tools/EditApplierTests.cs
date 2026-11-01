using Autonogrammer.Cowork.Artifacts.Tools;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Artifacts.Tests.Tools;

/// <summary>Exact-once edits refuse a miss or a double match and write nothing.</summary>
public sealed class EditApplierTests
{
    /// <summary>Verifies a unique match is applied and a double match from double-match.txt is refused.</summary>
    [Fact]
    public void DoubleMatchFixtureIsRefusedAndUniqueMatchApplies()
    {
        var needle = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "hostile", "tools", "double-match.txt")).Trim();
        needle.ShouldBe("repeat-me");
        var doubled = needle + " " + needle;
        EditApplier.TryApply(doubled, [new EditApplier.Edit(needle, "once")], out var unchanged, out var error).ShouldBeFalse();
        error.ShouldBe("edit_ambiguous");
        unchanged.ShouldBe(doubled);
        EditApplier.TryApply("keep " + needle + " tail", [new EditApplier.Edit(needle, "done")], out var updated, out var none).ShouldBeTrue();
        none.ShouldBeNull();
        updated.ShouldBe("keep done tail");
    }
}
