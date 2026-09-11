namespace RootlessWM.App;

public sealed record StyledText(IReadOnlyList<StyledSpan> Spans)
{
    public static readonly StyledText Empty = new([]);

    public static StyledText Plain(string text)
        => string.IsNullOrEmpty(text) ? Empty : new([new StyledSpan(text)]);

    public string PlainText => string.Concat(Spans.Select(span => span.Text));

    public bool IsEmpty => Spans.Count == 0 || Spans.All(span => string.IsNullOrEmpty(span.Text));
}
