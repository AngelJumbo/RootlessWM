namespace RootlessWM.Domain;

public readonly record struct WindowBounds(int Left, int Top, int Width, int Height)
{
    public bool IsUsable => Width > 0 && Height > 0;
}
