using RootlessWM.App;
using RootlessWM.Domain;

namespace RootlessWM.Platform.Win32;

internal sealed class GlobalHotkeySource : IDisposable
{
    private static readonly (int Identifier, uint Modifiers, uint VirtualKey, TilingCommand Command)[] Bindings =
    [
        (1, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkM, TilingCommand.PromoteToMaster),
        (2, NativeMethods.ModAlt, NativeMethods.VkJ, TilingCommand.FocusNext),
        (3, NativeMethods.ModAlt, NativeMethods.VkK, TilingCommand.FocusPrevious),
        (4, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkL, TilingCommand.FocusNextMonitor),
        (5, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkH, TilingCommand.FocusPreviousMonitor),
        (6, NativeMethods.ModAlt | NativeMethods.ModControl, NativeMethods.VkL, TilingCommand.MoveToNextMonitor),
        (7, NativeMethods.ModAlt | NativeMethods.ModControl, NativeMethods.VkH, TilingCommand.MoveToPreviousMonitor),
        (8, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkJ, TilingCommand.SwapWithNext),
        (9, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkK, TilingCommand.SwapWithPrevious),
        (10, NativeMethods.ModAlt, NativeMethods.VkF, TilingCommand.ToggleFloating),
        (11, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkQ, TilingCommand.Close),
        (12, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkR, TilingCommand.EnableManagement),
        (13, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkEscape, TilingCommand.DisableManagement),
        (14, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkN, TilingCommand.NextWorkspace),
        (15, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkP, TilingCommand.PreviousWorkspace),
        (16, NativeMethods.ModAlt | NativeMethods.ModControl, NativeMethods.VkN, TilingCommand.MoveToNextWorkspace),
        (17, NativeMethods.ModAlt | NativeMethods.ModControl, NativeMethods.VkP, TilingCommand.MoveToPreviousWorkspace),
        (18, NativeMethods.ModAlt, NativeMethods.VkT, TilingCommand.CycleLayout),
        (19, NativeMethods.ModAlt, NativeMethods.VkH, TilingCommand.DecreaseMasterRatio),
        (20, NativeMethods.ModAlt, NativeMethods.VkL, TilingCommand.IncreaseMasterRatio),
        (39, NativeMethods.ModAlt, NativeMethods.VkI, TilingCommand.IncreaseMasterCount),
        (40, NativeMethods.ModAlt, NativeMethods.VkD, TilingCommand.DecreaseMasterCount),
        (41, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkO, TilingCommand.IncreaseOuterGap),
        (42, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkU, TilingCommand.DecreaseOuterGap),
        (43, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkI, TilingCommand.IncreaseInnerGap),
        (44, NativeMethods.ModAlt | NativeMethods.ModShift, NativeMethods.VkD, TilingCommand.DecreaseInnerGap),
        (45, NativeMethods.ModAlt, NativeMethods.VkM, TilingCommand.MaximizeWindow),
        (46, NativeMethods.ModAlt, NativeMethods.VkE, TilingCommand.ToggleExplorer),
        (47, NativeMethods.ModAlt, NativeMethods.VkSpace, TilingCommand.OpenRunner),
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
    private bool _disposed;

    public IReadOnlyList<TilingCommand> Start(IReadOnlyDictionary<string, string>? overrides)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var unavailableCommands = new List<TilingCommand>();
        foreach (var binding in Bindings)
        {
            var configuredBinding = GetConfiguredBinding(binding, overrides);
            if (!NativeMethods.RegisterHotKey(nint.Zero, binding.Identifier, configuredBinding.Modifiers, configuredBinding.VirtualKey))
            {
                unavailableCommands.Add(binding.Command);
                continue;
            }

            _registeredIdentifiers.Add(binding.Identifier);
        }

        return unavailableCommands;
    }

    private static (uint Modifiers, uint VirtualKey) GetConfiguredBinding(
        (int Identifier, uint Modifiers, uint VirtualKey, TilingCommand Command) defaultBinding,
        IReadOnlyDictionary<string, string>? overrides)
    {
        if (overrides is not null
            && overrides.TryGetValue(defaultBinding.Command.ToString(), out var value)
            && TryParseBinding(value, out var modifiers, out var virtualKey))
        {
            return (modifiers, virtualKey);
        }

        return (defaultBinding.Modifiers, defaultBinding.VirtualKey);
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
            modifiers |= modifier.ToUpperInvariant() switch
            {
                "ALT" => NativeMethods.ModAlt,
                "CTRL" or "CONTROL" => NativeMethods.ModControl,
                "SHIFT" => NativeMethods.ModShift,
                _ => 0
            };

            if (modifier is not ("Alt" or "ALT" or "Ctrl" or "CTRL" or "Control" or "CONTROL" or "Shift" or "SHIFT"))
            {
                return false;
            }
        }

        var key = parts[^1];
        if (key.Length == 1 && (char.IsLetter(key[0]) || char.IsDigit(key[0])))
        {
            virtualKey = (uint)char.ToUpperInvariant(key[0]);
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
        _disposed = true;
    }
}
