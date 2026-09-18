namespace RootlessWM.App;

public sealed record StyledText(IReadOnlyList<IReadOnlyList<StyledSpan>> Lines)
{
    public static readonly StyledText Empty = new([[]]);

    public StyledText(IReadOnlyList<StyledSpan> spans)
        : this((IReadOnlyList<IReadOnlyList<StyledSpan>>)[spans])
    {
    }

    public static StyledText Plain(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Empty;
        }

        var lines = text.Split('\n')
            .Select(line => (IReadOnlyList<StyledSpan>)(string.IsNullOrEmpty(line) ? [] : [new StyledSpan(line)]))
            .ToList();
        return new StyledText(lines);
    }

    // Compatibility accessor for callers that only ever dealt with single-line text.
    public IReadOnlyList<StyledSpan> Spans => Lines.Count > 0 ? Lines[0] : [];

    public string PlainText => string.Join("\n", Lines.Select(line => string.Concat(line.Select(span => span.Text))));

    public bool IsEmpty => Lines.Count == 0 || Lines.All(line => line.Count == 0 || line.All(span => string.IsNullOrEmpty(span.Text)));
}
