using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace StockPilotPrintAgent;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mainWindow = new MainWindow();

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "StockPilot Print Agent"
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Åbn StockPilot Print Agent", null, (_, _) => ShowMainWindow());
        menu.Items.Add("Synkroniser printere nu", null, async (_, _) => await _mainWindow.ForceSyncPrintersAsync());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Afslut", null, (_, _) => ExitApplication());

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();

        var startMinimized = e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase)
                             || _mainWindow.Config.StartMinimized;

        if (!startMinimized)
        {
            _mainWindow.Show();
        }
        else
        {
            _trayIcon.ShowBalloonTip(2000, "StockPilot Print Agent", "Kører i baggrunden i systembakken.", Forms.ToolTipIcon.Info);
        }

        _mainWindow.StartBackgroundPolling();
    }

    public void ShowMainWindow()
    {
        if (_mainWindow == null) return;
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    public async void ExitApplication()
    {
        if (_mainWindow != null)
        {
            await _mainWindow.StopAndDisposeAsync();
        }
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        Shutdown();
    }
}