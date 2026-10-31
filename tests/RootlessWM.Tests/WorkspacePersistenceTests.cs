using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class WorkspacePersistenceTests
{
    [Fact]
    public void WorkspaceState_RestoreKeepsLiveAssignmentsAndDropsStaleHandles()
    {
        var state = new WorkspaceState(3);
        state.Restore(
            new WorkspaceStateData(new Dictionary<long, int>
            {
                [1] = 1,
                [2] = 2,
                [99] = 0
            }),
            [(nint)1, (nint)2]);

        _ = state.Switch((nint)100, 1);
        Assert.Equal([(nint)1], state.GetWindows((nint)100, [(nint)1, (nint)2], _ => (nint)100));
        _ = state.Switch((nint)100, 1);
        Assert.Equal([(nint)2], state.GetWindows((nint)100, [(nint)1, (nint)2], _ => (nint)100));
    }

}
