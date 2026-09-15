namespace RootlessWM.Protocol;

public readonly record struct RequestId(Guid Value)
{
    public static RequestId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}
