namespace RootlessWM.Domain;

internal static class LayoutMath
{
    public static WindowPlacement FullTile(nint handle, int left, int top, int width, int height)
        => new(handle, new WindowBounds(left, top, Math.Max(1, width), Math.Max(1, height)));

    public static int ClampGap(int gap, int dimension, int gapCount)
        => gapCount <= 0 ? 0 : Math.Max(0, Math.Min(gap, dimension / gapCount));

    /// <summary>
    /// Distributes <paramref name="total"/> across <paramref name="segments"/> slots, each at least
    /// 1 unit wide/tall, spreading the remainder across the leading slots so the placement is
    /// deterministic for a given (total, segments) pair regardless of window identity.
    /// </summary>
    public static int[] SplitSegments(int total, int segments, int gap)
    {
        if (segments <= 0)
        {
            return [];
        }

        if (segments == 1)
        {
            return [Math.Max(1, total)];
        }

        var clampedGap = ClampGap(gap, total, segments - 1);
        var available = Math.Max(total - (clampedGap * (segments - 1)), segments);
        var baseSize = available / segments;
        var remainder = available % segments;
        var sizes = new int[segments];
        for (var index = 0; index < segments; index++)
        {
            sizes[index] = Math.Max(1, baseSize + (index < remainder ? 1 : 0));
        }

        return sizes;
    }

    /// <summary>
    /// Lays out <paramref name="segments"/> slots starting at <paramref name="start"/>, spanning
    /// <paramref name="total"/> units, separated by (a clamped) <paramref name="gap"/>.
    /// </summary>
    public static (int Offset, int Size)[] LayoutSegments(int start, int total, int segments, int gap)
    {
        if (segments <= 0)
        {
            return [];
        }

        var clampedGap = ClampGap(gap, total, segments - 1);
        var sizes = SplitSegments(total, segments, gap);
        var result = new (int Offset, int Size)[segments];
        var offset = start;
        for (var index = 0; index < segments; index++)
        {
            result[index] = (offset, sizes[index]);
            offset += sizes[index] + clampedGap;
        }

        return result;
    }

    /// <summary>
    /// Splits <paramref name="total"/> units starting at <paramref name="start"/> into two
    /// segments sized by <paramref name="ratio"/>, separated by a clamped gap. Both segments are
    /// guaranteed to be at least 1 unit wide/tall.
    /// </summary>
    public static ((int Offset, int Size) A, (int Offset, int Size) B) SplitWeighted(int start, int total, double ratio, int gap)
    {
        var clampedGap = ClampGap(gap, total, 1);
        var available = Math.Max(total - clampedGap, 2);
        var sizeA = Math.Clamp((int)Math.Round(available * ratio, MidpointRounding.AwayFromZero), 1, available - 1);
        var sizeB = available - sizeA;
        return ((start, sizeA), (start + sizeA + clampedGap, sizeB));
    }
}
