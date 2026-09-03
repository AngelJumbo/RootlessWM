namespace RootlessWM.Domain;

public interface IWindowCommander
{
    bool Focus(nint handle);

    bool Close(nint handle);

    bool Show(nint handle);

    bool Hide(nint handle);
}
