using RootlessWM.App;
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

    [Fact]
    public void JsonWorkspaceStateStore_RoundTripsStateAtomically()
    {
        var path = Path.Combine(Path.GetTempPath(), $"RootlessWM-{Guid.NewGuid():N}", "workspaces.json");
        try
        {
            var store = new JsonWorkspaceStateStore(path);
            var expected = new WorkspaceStateData(new Dictionary<long, int> { [42] = 2 });

            store.Save(expected);

            var actual = store.Load();
            Assert.NotNull(actual);
            Assert.Equal(expected.Assignments, actual.Assignments);
        }
        finally
        {
            var directory = Path.GetDirectoryName(path);
            if (directory is not null && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
