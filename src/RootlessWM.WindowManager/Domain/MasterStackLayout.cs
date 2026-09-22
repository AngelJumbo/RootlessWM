namespace RootlessWM.Domain;

public sealed class MasterStackLayout
{
    private static readonly IReadOnlyDictionary<MasterStackLayoutMode, ILayout> Layouts =
        new ILayout[]
        {
            new MasterLeftLayout(),
            new MasterTopLayout(),
            new MonocleLayout(),
            new FloatingLayout(),
            new GridLayout(),
            new FibonacciLayout(),
            new DwindleLayout(),
            new CenteredMasterLayout()
        }.ToDictionary(layout => layout.Mode);

    public IReadOnlyList<WindowPlacement> Calculate(
        WindowBounds workArea,
        IReadOnlyList<nint> orderedHandles,
        MasterStackLayoutOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(orderedHandles);
        if (!workArea.IsUsable)
        {
            throw new ArgumentOutOfRangeException(nameof(workArea), "The work area must have positive dimensions.");
        }

        if (orderedHandles.Count == 0)
        {
            return [];
        }

        var layoutOptions = options ?? MasterStackLayoutOptions.Default;
        layoutOptions.Validate();

        if (!Layouts.TryGetValue(layoutOptions.Mode, out var layout))
        {
            throw new ArgumentOutOfRangeException(nameof(options), layoutOptions.Mode, "The layout mode is not supported.");
        }

        var outerGap = Math.Min(layoutOptions.OuterGap, Math.Min(workArea.Width / 2, workArea.Height / 2));
        var tileHeight = workArea.Height - (outerGap * 2);
        var tileWidth = workArea.Width - (outerGap * 2);
        var left = workArea.Left + outerGap;
        var top = workArea.Top + outerGap;

        return layout.Calculate(left, top, tileWidth, tileHeight, orderedHandles, layoutOptions);
    }
}
