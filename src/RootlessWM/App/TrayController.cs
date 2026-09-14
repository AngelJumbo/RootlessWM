using System.Drawing;
using System.Windows.Forms;

namespace RootlessWM.App;

internal sealed class TrayController : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _toggleItem;

    public TrayController(
        Action toggle,
        Action reload,
        Action openSettings,
        Action exit,
        Func<bool> isManagementEnabled)
    {
        ArgumentNullException.ThrowIfNull(toggle);
        ArgumentNullException.ThrowIfNull(reload);
        ArgumentNullException.ThrowIfNull(openSettings);
        ArgumentNullException.ThrowIfNull(exit);
        ArgumentNullException.ThrowIfNull(isManagementEnabled);

        _toggleItem = new ToolStripMenuItem("Disable management", null, (_, _) =>
        {
            toggle();
            SetManagementEnabled(isManagementEnabled());
        });
        var menu = new ContextMenuStrip();
        menu.Items.AddRange([
            _toggleItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Open settings", null, (_, _) => openSettings()),
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
