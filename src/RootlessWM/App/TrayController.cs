using System.Drawing;
using System.Windows.Forms;
using RootlessWM.Domain;

namespace RootlessWM.App;

internal sealed class TrayController : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _toggleItem;

    public TrayController(
        Action toggle,
        Action reload,
        Action exit,
        Action<TilingCommand> executeCommand,
        Func<bool> isManagementEnabled)
    {
        ArgumentNullException.ThrowIfNull(toggle);
        ArgumentNullException.ThrowIfNull(reload);
        ArgumentNullException.ThrowIfNull(exit);
        ArgumentNullException.ThrowIfNull(executeCommand);
        ArgumentNullException.ThrowIfNull(isManagementEnabled);

        _toggleItem = new ToolStripMenuItem("Disable management", null, (_, _) =>
        {
            toggle();
            SetManagementEnabled(isManagementEnabled());
        });
        var windowMenu = new ToolStripMenuItem("Window");
        windowMenu.DropDownItems.AddRange([
            CreateCommandItem("Promote to master", TilingCommand.PromoteToMaster, executeCommand),
            new ToolStripSeparator(),
            CreateCommandItem("Focus next", TilingCommand.FocusNext, executeCommand),
            CreateCommandItem("Focus previous", TilingCommand.FocusPrevious, executeCommand),
            CreateCommandItem("Focus next monitor", TilingCommand.FocusNextMonitor, executeCommand),
            CreateCommandItem("Focus previous monitor", TilingCommand.FocusPreviousMonitor, executeCommand),
            CreateCommandItem("Move to next monitor", TilingCommand.MoveToNextMonitor, executeCommand),
            CreateCommandItem("Move to previous monitor", TilingCommand.MoveToPreviousMonitor, executeCommand),
            new ToolStripSeparator(),
            CreateCommandItem("Next workspace", TilingCommand.NextWorkspace, executeCommand),
            CreateCommandItem("Previous workspace", TilingCommand.PreviousWorkspace, executeCommand),
            CreateCommandItem("Workspace 1", TilingCommand.SelectWorkspace1, executeCommand),
            CreateCommandItem("Workspace 2", TilingCommand.SelectWorkspace2, executeCommand),
            CreateCommandItem("Workspace 3", TilingCommand.SelectWorkspace3, executeCommand),
            CreateCommandItem("Workspace 4", TilingCommand.SelectWorkspace4, executeCommand),
            CreateCommandItem("Workspace 5", TilingCommand.SelectWorkspace5, executeCommand),
            CreateCommandItem("Workspace 6", TilingCommand.SelectWorkspace6, executeCommand),
            CreateCommandItem("Workspace 7", TilingCommand.SelectWorkspace7, executeCommand),
            CreateCommandItem("Workspace 8", TilingCommand.SelectWorkspace8, executeCommand),
            CreateCommandItem("Workspace 9", TilingCommand.SelectWorkspace9, executeCommand),
            CreateCommandItem("Move to next workspace", TilingCommand.MoveToNextWorkspace, executeCommand),
            CreateCommandItem("Move to previous workspace", TilingCommand.MoveToPreviousWorkspace, executeCommand),
            CreateCommandItem("Move to workspace 1", TilingCommand.MoveToWorkspace1, executeCommand),
            CreateCommandItem("Move to workspace 2", TilingCommand.MoveToWorkspace2, executeCommand),
            CreateCommandItem("Move to workspace 3", TilingCommand.MoveToWorkspace3, executeCommand),
            CreateCommandItem("Move to workspace 4", TilingCommand.MoveToWorkspace4, executeCommand),
            CreateCommandItem("Move to workspace 5", TilingCommand.MoveToWorkspace5, executeCommand),
            CreateCommandItem("Move to workspace 6", TilingCommand.MoveToWorkspace6, executeCommand),
            CreateCommandItem("Move to workspace 7", TilingCommand.MoveToWorkspace7, executeCommand),
            CreateCommandItem("Move to workspace 8", TilingCommand.MoveToWorkspace8, executeCommand),
            CreateCommandItem("Move to workspace 9", TilingCommand.MoveToWorkspace9, executeCommand),
            CreateCommandItem("Cycle layout", TilingCommand.CycleLayout, executeCommand),
            CreateCommandItem("Decrease master ratio", TilingCommand.DecreaseMasterRatio, executeCommand),
            CreateCommandItem("Increase master ratio", TilingCommand.IncreaseMasterRatio, executeCommand),
            CreateCommandItem("Decrease master count", TilingCommand.DecreaseMasterCount, executeCommand),
            CreateCommandItem("Increase master count", TilingCommand.IncreaseMasterCount, executeCommand),
            CreateCommandItem("Decrease outer gap", TilingCommand.DecreaseOuterGap, executeCommand),
            CreateCommandItem("Increase outer gap", TilingCommand.IncreaseOuterGap, executeCommand),
            CreateCommandItem("Decrease inner gap", TilingCommand.DecreaseInnerGap, executeCommand),
            CreateCommandItem("Increase inner gap", TilingCommand.IncreaseInnerGap, executeCommand),
            new ToolStripSeparator(),
            CreateCommandItem("Swap with next", TilingCommand.SwapWithNext, executeCommand),
            CreateCommandItem("Swap with previous", TilingCommand.SwapWithPrevious, executeCommand),
            new ToolStripSeparator(),
            CreateCommandItem("Toggle floating", TilingCommand.ToggleFloating, executeCommand),
            CreateCommandItem("Close", TilingCommand.Close, executeCommand)
        ]);
        var menu = new ContextMenuStrip();
        menu.Items.AddRange([
            _toggleItem,
            windowMenu,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Reload settings", null, (_, _) => reload()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Exit", null, (_, _) => exit())
        ]);
        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "RootlessWM",
            ContextMenuStrip = menu,
            Visible = true
        };
    }

    private static ToolStripMenuItem CreateCommandItem(
        string text,
        TilingCommand command,
        Action<TilingCommand> executeCommand)
    {
        return new ToolStripMenuItem(text, null, (_, _) => executeCommand(command));
    }

    public void SetManagementEnabled(bool isEnabled)
    {
        _toggleItem.Text = isEnabled ? "Disable management" : "Enable management";
    }

    public void SetStatus(string status)
    {
        ArgumentNullException.ThrowIfNull(status);
        _icon.Text = status.Length <= 63 ? status : status[..63];
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }
}
