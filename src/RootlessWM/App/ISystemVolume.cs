namespace RootlessWM.App;

internal readonly record struct SystemVolumeState(float Scalar, bool Muted);

internal interface ISystemVolume
{
    SystemVolumeState Sample();

    void Adjust(float delta);

    void ToggleMute();
}
