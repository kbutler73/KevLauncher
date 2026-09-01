using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace KevLauncher;

public partial class App : System.Windows.Application
{
    private Drawing.Icon? _appIcon;
    private Forms.NotifyIcon? _trayIcon;
    private Forms.ContextMenuStrip? _launcherMenu;
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

        _trayIcon.MouseClick += OnTrayIconMouseClick;

        // If launched with --minimized (used by startup registry), don't show the window.
        var startMinimized = e.Args is not null && e.Args.Any(a => string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase));
        if (!startMinimized)
        {
            ShowLauncher();
        }
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

        if (_mainWindow is not null && _mainWindow.RootItems.Count > 0)
        {
            AddLauncherMenuItems(menu.Items, _mainWindow.RootItems);

            menu.Items.Add(new Forms.ToolStripSeparator());
        }

        // Start with Windows toggle
        try
        {
            var startItem = new Forms.ToolStripMenuItem("Start with Windows")
            {
                Checked = StartupManager.IsEnabled(),
                CheckOnClick = true
            };

            startItem.Click += (_, _) =>
            {
                try
                {
                    StartupManager.SetEnabled(startItem.Checked);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Could not change Start with Windows setting.\n\n{ex.Message}", "KevLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
                    // revert check state
                    startItem.Checked = !startItem.Checked;
                }
            };

            menu.Items.Add(startItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
        }
        catch
        {
            // ignore failures when reading registry
        }

        menu.Items.Add("Open KevLauncher", null, (_, _) => ShowLauncher());
        menu.Items.Add("Add item...", null, (_, _) => _mainWindow?.AddItemFromDialog());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
    }

    private void OnTrayIconMouseClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            ShowLauncherMenu();
        }
    }

    private void ShowLauncherMenu()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _launcherMenu?.Dispose();
        _launcherMenu = BuildLauncherOnlyMenu();
        _launcherMenu.Show(Forms.Cursor.Position);
    }

    private Forms.ContextMenuStrip BuildLauncherOnlyMenu()
    {
        var menu = new Forms.ContextMenuStrip();

        if (_mainWindow is not null && _mainWindow.RootItems.Count > 0)
        {
            AddLauncherMenuItems(menu.Items, _mainWindow.RootItems);
        }

        if (menu.Items.Count == 0)
        {
            menu.Items.Add("No launcher items yet").Enabled = false;
        }

        return menu;
    }

    private void AddLauncherMenuItems(Forms.ToolStripItemCollection items, IEnumerable<LauncherNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsFolder)
            {
                var folder = new Forms.ToolStripMenuItem(node.Name);
                AddLauncherMenuItems(folder.DropDownItems, node.Children);

                if (folder.DropDownItems.Count == 0)
                {
                    folder.Enabled = false;
                }

                items.Add(folder);
            }
            else if (node.CanLaunch)
            {
                items.Add(node.Name, null, (_, _) => _mainWindow?.LaunchItem(node));
            }
        }
    }

    private void ShowLauncher()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.ShowLauncherTree();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        _launcherMenu?.Dispose();
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
