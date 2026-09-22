namespace RootlessWM.Domain;

public sealed class LayoutState
{
    private readonly Dictionary<(int Workspace, nint Monitor), MasterStackLayoutOptions> _options = [];

    private const double MinMasterRatio = 0.05;
    private const double MaxMasterRatio = 0.95;
    private const int MinMasterCount = 1;
    private const int MaxMasterCount = MasterStackLayoutOptions.MaxMasterCount;
    private const int MinGap = 0;
    private const int MaxGap = MasterStackLayoutOptions.MaxGap;

    public MasterStackLayoutOptions Get(
        int workspace,
        nint monitor,
        MasterStackLayoutOptions fallback)
    {
        return _options.TryGetValue((workspace, monitor), out var options)
            ? options
            : fallback;
    }

    public MasterStackLayoutOptions Cycle(
        int workspace,
        nint monitor,
        MasterStackLayoutOptions fallback,
        IReadOnlyList<MasterStackLayoutMode> enabledOrder)
    {
        return Advance(workspace, monitor, fallback, enabledOrder, direction: 1);
    }

    public MasterStackLayoutOptions CyclePrevious(
        int workspace,
        nint monitor,
        MasterStackLayoutOptions fallback,
        IReadOnlyList<MasterStackLayoutMode> enabledOrder)
    {
        return Advance(workspace, monitor, fallback, enabledOrder, direction: -1);
    }

    public MasterStackLayoutOptions SetMode(
        int workspace,
        nint monitor,
        MasterStackLayoutOptions fallback,
        MasterStackLayoutMode mode)
    {
        var options = Get(workspace, monitor, fallback) with { Mode = mode };
        _options[(workspace, monitor)] = options;
        return options;
    }

    private MasterStackLayoutOptions Advance(
        int workspace,
        nint monitor,
        MasterStackLayoutOptions fallback,
        IReadOnlyList<MasterStackLayoutMode> enabledOrder,
        int direction)
    {
        var current = Get(workspace, monitor, fallback);
        if (enabledOrder.Count == 0)
        {
            return current;
        }

        var index = -1;
        for (var i = 0; i < enabledOrder.Count; i++)
        {
            if (enabledOrder[i] == current.Mode)
            {
                index = i;
                break;
            }
        }

        MasterStackLayoutMode nextMode;
        if (index < 0)
        {
            // The current mode is not part of the configured cycle list (e.g. it was set via a
            // direct-select hotkey for a layout that isn't in layouts.enabled): resume from the
            // natural start of the list in the requested direction instead of mutating the list.
            nextMode = direction > 0 ? enabledOrder[0] : enabledOrder[^1];
        }
        else
        {
            var nextIndex = ((index + direction) % enabledOrder.Count + enabledOrder.Count) % enabledOrder.Count;
            nextMode = enabledOrder[nextIndex];
        }

        var options = current with { Mode = nextMode };
        _options[(workspace, monitor)] = options;
        return options;
    }

    public MasterStackLayoutOptions AdjustMasterRatio(
        int workspace,
        nint monitor,
        MasterStackLayoutOptions fallback,
        double delta)
    {
        var current = Get(workspace, monitor, fallback);
        var nextRatio = Math.Clamp(current.MasterRatio + delta, MinMasterRatio, MaxMasterRatio);
        var options = current with { MasterRatio = nextRatio };
        _options[(workspace, monitor)] = options;
        return options;
    }

    public MasterStackLayoutOptions AdjustMasterCount(
        int workspace,
        nint monitor,
        MasterStackLayoutOptions fallback,
        int delta)
    {
        var current = Get(workspace, monitor, fallback);
        var nextCount = Math.Clamp(current.MasterCount + delta, MinMasterCount, MaxMasterCount);
        var options = current with { MasterCount = nextCount };
        _options[(workspace, monitor)] = options;
        return options;
    }

    public MasterStackLayoutOptions AdjustOuterGap(
        int workspace,
        nint monitor,
        MasterStackLayoutOptions fallback,
        int delta)
    {
        var current = Get(workspace, monitor, fallback);
        var nextGap = Math.Clamp(current.OuterGap + delta, MinGap, MaxGap);
        var options = current with { OuterGap = nextGap };
        _options[(workspace, monitor)] = options;
        return options;
    }

    public MasterStackLayoutOptions AdjustInnerGap(
        int workspace,
        nint monitor,
        MasterStackLayoutOptions fallback,
        int delta)
    {
        var current = Get(workspace, monitor, fallback);
        var nextGap = Math.Clamp(current.InnerGap + delta, MinGap, MaxGap);
        var options = current with { InnerGap = nextGap };
        _options[(workspace, monitor)] = options;
        return options;
    }
}
