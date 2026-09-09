using System.Drawing;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _icon;
    private readonly System.Windows.Forms.ContextMenuStrip _menu = new();
    private readonly Action _show;
    private readonly Action _refresh;
    private readonly Action _startAll;
    private readonly Action _stopAll;
    private readonly Action _about;
    private readonly Action _exit;
    private Icon? _ownedIcon;

    public TrayIconService(Action show, Action refresh, Action startAll, Action stopAll, Action about, Action exit)
    {
        _show = show;
        _refresh = refresh;
        _startAll = startAll;
        _stopAll = stopAll;
        _about = about;
        _exit = exit;

        _ownedIcon = LoadApplicationIcon();
        _icon = new System.Windows.Forms.NotifyIcon
        {
            Icon = _ownedIcon,
            Text = "NRS Workbench",
            Visible = false,
            ContextMenuStrip = _menu
        };
        _icon.DoubleClick += (_, _) => _show();
        RebuildMenu([]);
    }

    public void SetVisible(bool visible) => _icon.Visible = visible;

    public void Update(IReadOnlyList<RunnerInfo> runners)
    {
        var busy = runners.Count(x => x.State == RunnerState.Busy);
        var text = $"Workspace · {runners.Count} runners · {busy} busy";
        _icon.Text = text.Length <= 63 ? text : "NRS Workbench";
        RebuildMenu(runners);
    }


    public void ShowNotification(string title, string message, System.Windows.Forms.ToolTipIcon icon = System.Windows.Forms.ToolTipIcon.Info)
    {
        if (!_icon.Visible) return;
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = message.Length <= 250 ? message : message[..247] + "...";
        _icon.BalloonTipIcon = icon;
        _icon.ShowBalloonTip(5000);
    }

    private void RebuildMenu(IReadOnlyList<RunnerInfo> runners)
    {
        _menu.Items.Clear();
        _menu.Items.Add(new System.Windows.Forms.ToolStripMenuItem("NRS Workbench") { Enabled = false });

        if (runners.Count > 0)
        {
            var ready = runners.Count(x => x.State == RunnerState.Ready);
            var busy = runners.Count(x => x.State == RunnerState.Busy);
            var stopped = runners.Count(x => x.State == RunnerState.Stopped);
            _menu.Items.Add(new System.Windows.Forms.ToolStripMenuItem($"{runners.Count} runners · {ready} ready · {busy} busy · {stopped} stopped") { Enabled = false });
            _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            foreach (var runner in runners.OrderBy(x => x.Alias, StringComparer.OrdinalIgnoreCase))
            {
                var item = new System.Windows.Forms.ToolStripMenuItem($"{StateGlyph(runner.State)}  {runner.Alias}   {StateLabel(runner.State)}")
                {
                    Enabled = false
                };
                _menu.Items.Add(item);
            }
        }
        else
        {
            _menu.Items.Add(new System.Windows.Forms.ToolStripMenuItem("Sin runners detectados") { Enabled = false });
        }

        _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        _menu.Items.Add("Abrir NRS Workbench", null, (_, _) => _show());
        _menu.Items.Add("Refrescar", null, (_, _) => _refresh());
        _menu.Items.Add("Iniciar todos", null, (_, _) => _startAll());
        _menu.Items.Add("Parar todos", null, (_, _) => _stopAll());
        _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        _menu.Items.Add("Acerca de · Neo RS", null, (_, _) => _about());
        _menu.Items.Add("Salir", null, (_, _) => _exit());
    }

    private static string StateGlyph(RunnerState state) => state switch
    {
        RunnerState.Ready => "●",
        RunnerState.Busy => "●",
        RunnerState.Stopped => "●",
        RunnerState.Error => "!",
        _ => "○"
    };

    private static string StateLabel(RunnerState state) => state switch
    {
        RunnerState.Ready => "READY",
        RunnerState.Busy => "BUSY",
        RunnerState.Stopped => "STOPPED",
        RunnerState.Starting => "STARTING",
        RunnerState.Stopping => "STOPPING",
        RunnerState.Error => "ERROR",
        RunnerState.Unregistered => "UNREGISTERED",
        _ => "UNKNOWN"
    };

    private static Icon LoadApplicationIcon()
    {
        try
        {
            var resource = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/NRSWorkbench.ico", UriKind.Absolute));
            if (resource?.Stream is not null)
            {
                using var stream = resource.Stream;
                using var icon = new Icon(stream);
                return (Icon)icon.Clone();
            }
        }
        catch
        {
            // Fall back to a system icon only if the embedded application icon cannot be loaded.
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _ownedIcon?.Dispose();
        _ownedIcon = null;
    }
}
