using RootlessWM.Platform.Win32;

namespace RootlessWM.App;

/// <summary>Maps widget string keys to provider instances.</summary>
internal sealed class WorkspaceBarWidgetRegistry
{
    private readonly IReadOnlyDictionary<string, IWidgetProvider> _providers;

    public WorkspaceBarWidgetRegistry(IEnumerable<IWidgetProvider> providers)
    {
        _providers = providers.ToDictionary(
            provider => provider.Key,
            provider => provider,
            StringComparer.OrdinalIgnoreCase);
    }

    public static WorkspaceBarWidgetRegistry CreateDefault()
    {
        var metricsSampler = new SystemMetricsSampler();
        return new WorkspaceBarWidgetRegistry(
        [
            new CpuWidgetProvider(metricsSampler),
            new MemoryWidgetProvider(metricsSampler),
            new ClockWidgetProvider(),
            new DateWidgetProvider(),
            new UptimeWidgetProvider(),
            new BatteryWidgetProvider(new BatteryMetricsSampler()),
            new DiskWidgetProvider(),
            new NetworkWidgetProvider(new NetworkMetricsSampler())
        ]);
    }

    public bool TryGet(string key, out IWidgetProvider provider)
        => _providers.TryGetValue(key, out provider!);

    /// <summary>Creates a provider for a configured widget, honoring per-widget options.</summary>
    public IWidgetProvider? Create(string key, WorkspaceBarWidgetOptions options)
    {
        if (string.Equals(key, TextWidgetProvider.Key, StringComparison.OrdinalIgnoreCase))
        {
            return new TextWidgetProvider(options.Text);
        }

        if (string.Equals(key, CommandWidgetProvider.Key, StringComparison.OrdinalIgnoreCase))
        {
            return new CommandWidgetProvider(options.Command, options.IntervalMilliseconds);
        }

        return TryGet(key, out var provider) ? provider : null;
    }
}
