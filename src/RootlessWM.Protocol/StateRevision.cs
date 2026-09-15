namespace RootlessWM.Protocol;

public readonly record struct StateRevision(long Value) : IComparable<StateRevision>
{
    public static readonly StateRevision None = new(0);

    public StateRevision Next() => new(Value + 1);

    public int CompareTo(StateRevision other) => Value.CompareTo(other.Value);

    public static bool operator >(StateRevision left, StateRevision right) => left.Value > right.Value;

    public static bool operator <(StateRevision left, StateRevision right) => left.Value < right.Value;

    public static bool operator >=(StateRevision left, StateRevision right) => left.Value >= right.Value;

    public static bool operator <=(StateRevision left, StateRevision right) => left.Value <= right.Value;
}
