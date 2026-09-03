namespace RootlessWM.Domain;

public interface IManagedWindowStateStore
{
    IReadOnlyList<ManagedWindowState> Load();

    void Save(IReadOnlyList<ManagedWindowState> windows);

    void Clear();
}
