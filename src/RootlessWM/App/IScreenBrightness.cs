namespace RootlessWM.App;

internal interface IScreenBrightness
{
    int Sample();

    void Adjust(int delta);
}
