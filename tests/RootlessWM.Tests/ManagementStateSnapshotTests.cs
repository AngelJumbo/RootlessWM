using RootlessWM.Protocol;
using Xunit;

namespace RootlessWM.Tests;

public sealed class ManagementStateSnapshotTests
{
    [Fact]
    public void WithExpression_BumpsRevisionWithoutMutatingOriginal()
    {
        var original = BuildSnapshot(StateRevision.None);

        var bumped = original with { Revision = original.Revision.Next() };

        Assert.Equal(0, original.Revision.Value);
        Assert.Equal(1, bumped.Revision.Value);
    }

    [Fact]
    public void StructurallyIdenticalSnapshots_AreEqual()
    {
        var first = BuildSnapshot(StateRevision.None);
        var second = BuildSnapshot(StateRevision.None);

        Assert.Equal(first with { ManagedWindows = Array.Empty<ManagedWindowSnapshot>() }, second with { ManagedWindows = Array.Empty<ManagedWindowSnapshot>() });
        Assert.Equal(first.ManagedWindows, second.ManagedWindows);
    }

    private static ManagementStateSnapshot BuildSnapshot(StateRevision revision) => new(
        revision,
        true,
        0,
        9,
        0.55,
        8,
        4,
        1,
        new[] { new ManagedWindowSnapshot(1, 0, false, false, new WindowBoundsSnapshot(0, 0, 800, 600)) });
}
