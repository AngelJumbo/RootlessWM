namespace RootlessWM.App;

internal sealed class TextWidgetProvider : IWidgetProvider
{
    public const string Key = "text";

    private readonly string _text;

    public TextWidgetProvider(string text)
    {
        _text = text;
    }

    string IWidgetProvider.Key => Key;

    public string GetText() => _text;
}
