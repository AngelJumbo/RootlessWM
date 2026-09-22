using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using RootlessWM.App;
using RootlessWM.Domain;

namespace RootlessWM.Platform.Win32;

internal sealed class GlobalHotkeySource : IDisposable
{
    private static readonly (int Identifier, uint Modifiers, uint VirtualKey, TilingCommand Command)[] Bindings =
    [
        (1, NativeMethods.ModAlt, NativeMethods.VkM, TilingCommand.PromoteToMaster),
        (2, NativeMethods.ModAlt, NativeMethods.VkJ, TilingCommand.FocusNext),
        (3, NativeMethods.ModAlt, NativeMethods.VkK, TilingCommand.FocusPrevious),
        (4, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkL, TilingCommand.FocusNextMonitor),
        (5, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkH, TilingCommand.FocusPreviousMonitor),
        (6, NativeMethods.ModAlt | NativeMethods.ModControl, NativeMethods.VkL, TilingCommand.MoveToNextMonitor),
        (7, NativeMethods.ModAlt | NativeMethods.ModControl, NativeMethods.VkH, TilingCommand.MoveToPreviousMonitor),
        (8, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkJ, TilingCommand.SwapWithNext),
        (9, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkK, TilingCommand.SwapWithPrevious),
        (10, NativeMethods.ModAlt, NativeMethods.VkF, TilingCommand.ToggleFloating),
        (11, NativeMethods.ModAlt, NativeMethods.VkQ, TilingCommand.Close),
        (12, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkR, TilingCommand.EnableManagement),
        (13, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkEscape, TilingCommand.DisableManagement),
        (14, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkN, TilingCommand.NextWorkspace),
        (15, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkP, TilingCommand.PreviousWorkspace),
        (16, NativeMethods.ModAlt | NativeMethods.ModControl, NativeMethods.VkN, TilingCommand.MoveToNextWorkspace),
        (17, NativeMethods.ModAlt | NativeMethods.ModControl, NativeMethods.VkP, TilingCommand.MoveToPreviousWorkspace),
        (18, NativeMethods.ModAlt, NativeMethods.VkSpace, TilingCommand.CycleLayout),
        (19, NativeMethods.ModAlt, NativeMethods.VkH, TilingCommand.DecreaseMasterRatio),
        (20, NativeMethods.ModAlt, NativeMethods.VkL, TilingCommand.IncreaseMasterRatio),
        (39, NativeMethods.ModAlt, NativeMethods.VkI, TilingCommand.IncreaseMasterCount),
        (40, NativeMethods.ModAlt, NativeMethods.VkD, TilingCommand.DecreaseMasterCount),
        (41, NativeMethods.ModAlt, NativeMethods.VkO, TilingCommand.IncreaseOuterGap),
        (42, NativeMethods.ModAlt, NativeMethods.VkU, TilingCommand.DecreaseOuterGap),
        (43, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkO, TilingCommand.IncreaseInnerGap),
        (44, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkU, TilingCommand.DecreaseInnerGap),
        (45, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkF, TilingCommand.MaximizeWindow),
        (46, NativeMethods.ModAlt, NativeMethods.VkE, TilingCommand.ToggleExplorer),
        (47, NativeMethods.ModAlt, NativeMethods.VkP, TilingCommand.OpenRunner),
        (48, NativeMethods.ModAlt, NativeMethods.VkB, TilingCommand.ToggleStatusBar),
        (49, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkSpace, TilingCommand.CycleLayoutPrevious),
        (50, 0, 0, TilingCommand.SelectLayoutMasterLeft),
        (51, 0, 0, TilingCommand.SelectLayoutMasterTop),
        (52, 0, 0, TilingCommand.SelectLayoutMonocle),
        (53, 0, 0, TilingCommand.SelectLayoutFloating),
        (54, 0, 0, TilingCommand.SelectLayoutGrid),
        (55, 0, 0, TilingCommand.SelectLayoutFibonacci),
        (56, 0, 0, TilingCommand.SelectLayoutDwindle),
        (57, 0, 0, TilingCommand.SelectLayoutCenteredMaster),
        (21, NativeMethods.ModAlt, NativeMethods.Vk1, TilingCommand.SelectWorkspace1),
        (22, NativeMethods.ModAlt, NativeMethods.Vk2, TilingCommand.SelectWorkspace2),
        (23, NativeMethods.ModAlt, NativeMethods.Vk3, TilingCommand.SelectWorkspace3),
        (24, NativeMethods.ModAlt, NativeMethods.Vk4, TilingCommand.SelectWorkspace4),
        (25, NativeMethods.ModAlt, NativeMethods.Vk5, TilingCommand.SelectWorkspace5),
        (26, NativeMethods.ModAlt, NativeMethods.Vk6, TilingCommand.SelectWorkspace6),
        (27, NativeMethods.ModAlt, NativeMethods.Vk7, TilingCommand.SelectWorkspace7),
        (28, NativeMethods.ModAlt, NativeMethods.Vk8, TilingCommand.SelectWorkspace8),
        (29, NativeMethods.ModAlt, NativeMethods.Vk9, TilingCommand.SelectWorkspace9),
        (30, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.Vk1, TilingCommand.MoveToWorkspace1),
        (31, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.Vk2, TilingCommand.MoveToWorkspace2),
        (32, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.Vk3, TilingCommand.MoveToWorkspace3),
        (33, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.Vk4, TilingCommand.MoveToWorkspace4),
        (34, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.Vk5, TilingCommand.MoveToWorkspace5),
        (35, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.Vk6, TilingCommand.MoveToWorkspace6),
        (36, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.Vk7, TilingCommand.MoveToWorkspace7),
        (37, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.Vk8, TilingCommand.MoveToWorkspace8),
        (38, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.Vk9, TilingCommand.MoveToWorkspace9)
    ];

    private readonly HashSet<int> _registeredIdentifiers = [];
    private readonly Dictionary<int, LaunchHotkeySettings> _launchBindings = [];
    private readonly List<string> _unavailableLaunchHotkeys = [];
    private bool _disposed;

    public IReadOnlyList<string> UnavailableLaunchHotkeys => _unavailableLaunchHotkeys;

    public IReadOnlyList<TilingCommand> Start(
        IReadOnlyDictionary<string, string>? overrides,
        IReadOnlyList<LaunchHotkeySettings>? launchHotkeys = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var unavailableCommands = new List<TilingCommand>();
        foreach (var binding in Bindings)
        {
            var configuredBinding = GetConfiguredBinding(binding, overrides);
            if (configuredBinding is null)
            {
                // No default binding and no user override: this is an optional hotkey
                // (e.g. a direct-layout select command) that is simply not registered.
                continue;
            }

            if (!NativeMethods.RegisterHotKey(nint.Zero, binding.Identifier, configuredBinding.Value.Modifiers, configuredBinding.Value.VirtualKey))
            {
                unavailableCommands.Add(binding.Command);
                continue;
            }

            _registeredIdentifiers.Add(binding.Identifier);
        }

        if (launchHotkeys is not null)
        {
            var nextIdentifier = 1000;
            foreach (var launch in launchHotkeys)
            {
                if (!TryParseBinding(launch.Hotkey, out var modifiers, out var virtualKey))
                {
                    _unavailableLaunchHotkeys.Add($"{launch.Hotkey} (invalid binding)");
                    continue;
                }

                var identifier = nextIdentifier++;
                if (!NativeMethods.RegisterHotKey(nint.Zero, identifier, modifiers, virtualKey))
                {
                    _unavailableLaunchHotkeys.Add($"{launch.Hotkey} (Win32 error {Marshal.GetLastWin32Error()})");
                    continue;
                }

                _registeredIdentifiers.Add(identifier);
                _launchBindings[identifier] = launch;
            }
        }

        return unavailableCommands;
    }

    private static (uint Modifiers, uint VirtualKey)? GetConfiguredBinding(
        (int Identifier, uint Modifiers, uint VirtualKey, TilingCommand Command) defaultBinding,
        IReadOnlyDictionary<string, string>? overrides)
    {
        if (overrides is not null
            && overrides.TryGetValue(defaultBinding.Command.ToString(), out var value)
            && TryParseBinding(value, out var modifiers, out var virtualKey))
        {
            return (modifiers, virtualKey);
        }

        // Modifiers == 0 marks an optional hotkey (e.g. a direct-layout select command) with no
        // default binding: it stays unregistered unless the user configures an override above.
        return defaultBinding.Modifiers == 0 ? null : (defaultBinding.Modifiers, defaultBinding.VirtualKey);
    }

    private static bool TryParseBinding(string value, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        var parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        foreach (var modifier in parts[..^1])
        {
            var normalized = modifier.ToUpperInvariant();
            uint modifierValue = normalized switch
            {
                "ALT" => NativeMethods.ModAlt,
                "CTRL" or "CONTROL" => NativeMethods.ModControl,
                "SHIFT" => NativeMethods.ModShift,
                "SUPER" or "WIN" => NativeMethods.ModWin,
                _ => 0
            };

            if (modifierValue == 0)
            {
                return false;
            }

            modifiers |= modifierValue;
        }

        var key = parts[^1];
        if (key.Length == 1 && (char.IsLetter(key[0]) || char.IsDigit(key[0])))
        {
            virtualKey = (uint)char.ToUpperInvariant(key[0]);
            return modifiers != 0;
        }

        if (string.Equals(key, "Enter", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "Return", StringComparison.OrdinalIgnoreCase))
        {
            virtualKey = NativeMethods.VkEnter;
            return modifiers != 0;
        }

        if (string.Equals(key, "Escape", StringComparison.OrdinalIgnoreCase))
        {
            virtualKey = NativeMethods.VkEscape;
            return modifiers != 0;
        }

        if (string.Equals(key, "Space", StringComparison.OrdinalIgnoreCase))
        {
            virtualKey = NativeMethods.VkSpace;
            return modifiers != 0;
        }

        return false;
    }

    public bool TryGetCommand(uint messageId, nuint hotkeyIdentifier, out TilingCommand command)
    {
        if (messageId == NativeMethods.WmHotkey)
        {
            var binding = Bindings.FirstOrDefault(binding => binding.Identifier == (int)hotkeyIdentifier);
            if (binding != default)
            {
                command = binding.Command;
                return true;
            }
        }

        command = default;
        return false;
    }

    public bool TryGetLaunch(uint messageId, nuint hotkeyIdentifier, [NotNullWhen(true)] out LaunchHotkeySettings? launch)
    {
        if (messageId == NativeMethods.WmHotkey && _launchBindings.TryGetValue((int)hotkeyIdentifier, out launch))
        {
            return true;
        }

        launch = null;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var identifier in _registeredIdentifiers)
        {
            _ = NativeMethods.UnregisterHotKey(nint.Zero, identifier);
        }

        _registeredIdentifiers.Clear();
        _launchBindings.Clear();
        _unavailableLaunchHotkeys.Clear();
        _disposed = true;
    }
}
