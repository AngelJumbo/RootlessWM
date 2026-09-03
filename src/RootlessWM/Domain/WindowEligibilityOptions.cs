namespace RootlessWM.Domain;

public sealed class WindowEligibilityOptions
{
    public ISet<string> ExcludedExecutableNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public ISet<string> ExcludedWindowClasses { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
