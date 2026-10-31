namespace RootlessWM.App;

internal interface IScrollWidgetProvider : IWidgetProvider
{
    void Scroll(int delta);
}
