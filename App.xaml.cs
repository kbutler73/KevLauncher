using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace KevLauncher;

public partial class App : System.Windows.Application
{
    private Drawing.Icon? _appIcon;
    private Forms.NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _mainWindow = new MainWindow();
        MainWindow = _mainWindow;

        _appIcon = LoadAppIcon();
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = _appIcon,
            Text = "KevLauncher",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };

        _trayIcon.DoubleClick += (_, _) => ShowLauncher();
        ShowLauncher();
    }

    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Opening += (_, _) => PopulateTrayMenu(menu);
        return menu;
    }

    private static Drawing.Icon LoadAppIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "KevLauncher.ico");
        return File.Exists(iconPath)
            ? new Drawing.Icon(iconPath)
            : (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
    }

    private void PopulateTrayMenu(Forms.ContextMenuStrip menu)
    {
        menu.Items.Clear();

        if (_mainWindow is not null && _mainWindow.Items.Count > 0)
        {
            foreach (var item in _mainWindow.Items.Take(10))
            {
                menu.Items.Add(item.Name, null, (_, _) => _mainWindow.LaunchItem(item));
            }

            menu.Items.Add(new Forms.ToolStripSeparator());
        }

        menu.Items.Add("Open KevLauncher", null, (_, _) => ShowLauncher());
        menu.Items.Add("Add item...", null, (_, _) => _mainWindow?.AddItemFromDialog());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
    }

    private void ShowLauncher()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        _appIcon?.Dispose();
    }

    private void ExitApplication()
    {
        if (_mainWindow is not null)
        {
            _mainWindow.IsExiting = true;
        }

        Shutdown();
    }
}
