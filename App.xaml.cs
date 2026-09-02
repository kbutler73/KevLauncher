using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
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
    private StartMenuWindow? _startMenuWindow;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _mainWindow = new MainWindow();

        // Apply theme based on Windows setting
        try
        {
            ApplySystemTheme();
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += (_, _) => ApplySystemTheme();
        }
        catch
        {
            // ignore theme errors
        }

        // Initialize tray icon and startup window
        _appIcon = LoadAppIcon();
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = _appIcon,
            Text = "KevLauncher",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };

        _trayIcon.MouseClick += OnTrayIconMouseClick;

        // Create StartMenuWindow as the application's main window and start minimized so only the taskbar icon appears
        var startupWin = new StartMenuWindow(_mainWindow);
        _startMenuWindow = startupWin;
        MainWindow = startupWin;
        // Position the window centered at the bottom of the primary work area before showing
        try
        {
            var wa = SystemParameters.WorkArea;
            startupWin.WindowStartupLocation = WindowStartupLocation.Manual;
            var left = wa.Left + (wa.Width - startupWin.Width) / 2.0;
            var top = wa.Bottom - startupWin.Height - 8; // small gap above taskbar
            if (left < wa.Left) left = wa.Left;
            if (left + startupWin.Width > wa.Right) left = wa.Right - startupWin.Width;
            if (top < wa.Top) top = wa.Top;
            startupWin.Left = left;
            startupWin.Top = top;
        }
        catch
        {
            // ignore positioning errors
        }

        // Show and immediately minimize so the taskbar icon is visible but the window is not intrusive
        startupWin.Show();
        startupWin.WindowState = WindowState.Minimized;
    }

    // Previously used a custom activation handler to toggle the window; now rely on OS default behavior.

    // P/Invoke to get taskbar position
    private enum ABEdge : uint
    {
        ABE_LEFT = 0,
        ABE_TOP = 1,
        ABE_RIGHT = 2,
        ABE_BOTTOM = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public ABEdge uEdge;
        public RECT rc;
        public int lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern UInt32 SHAppBarMessage(UInt32 dwMessage, ref APPBARDATA pData);

    private const UInt32 ABM_GETTASKBARPOS = 5;

    private RECT GetTaskbarRect()
    {
        var data = new APPBARDATA();
        data.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
        var res = SHAppBarMessage(ABM_GETTASKBARPOS, ref data);
        return data.rc;
    }

    private ABEdge GetTaskbarEdge(RECT rc)
    {
        // Use screen work area vs rc to infer edge
        var wa = SystemParameters.WorkArea;
        // if taskbar height is small and docked at bottom/top
        if (rc.left <= wa.Left && rc.right >= wa.Right)
        {
            // horizontal bar
            if (rc.top > wa.Top) return ABEdge.ABE_BOTTOM;
            return ABEdge.ABE_TOP;
        }

        // vertical
        if (rc.top <= wa.Top && rc.bottom >= wa.Bottom)
        {
            if (rc.left < wa.Left) return ABEdge.ABE_LEFT;
            return ABEdge.ABE_RIGHT;
        }

        // default
        return ABEdge.ABE_BOTTOM;
    }

    private void ApplySystemTheme()
    {
        try
        {
            var personalize = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = personalize?.GetValue("AppsUseLightTheme");
            var isLight = true;
            if (val is int ival)
            {
                isLight = ival != 0;
            }

            ApplyTheme(isLight);
        }
        catch
        {
            // default to light
            ApplyTheme(true);
        }
    }

    private void ApplyTheme(bool isLight)
    {
        // remove existing theme dictionaries
        var existing = Resources.MergedDictionaries.Where(d => d.Source != null && (d.Source.OriginalString.Contains("Themes/Light.xaml") || d.Source.OriginalString.Contains("Themes/Dark.xaml"))).ToList();
        foreach (var d in existing) Resources.MergedDictionaries.Remove(d);

        var themePath = isLight ? "Themes/Light.xaml" : "Themes/Dark.xaml";
        try
        {
            var rd = new ResourceDictionary { Source = new System.Uri(themePath, System.UriKind.Relative) };
            Resources.MergedDictionaries.Add(rd);
        }
        catch
        {
            // ignore load failures
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
        menu.Items.Add("Open Start Menu", null, (_, _) => ShowStartMenu());
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

    public void ShowStartMenu()
    {
        if (_mainWindow is null)
            return;

        // Reuse existing StartMenuWindow if already open
        if (_startMenuWindow is not null)
        {
            try
            {
                if (_startMenuWindow.WindowState == WindowState.Minimized)
                {
                    _startMenuWindow.WindowState = WindowState.Normal;
                    _startMenuWindow.Activate();
                    return;
                }

                if (_startMenuWindow.IsVisible)
                {
                    _startMenuWindow.Activate();
                    return;
                }
            }
            catch
            {
                // ignore and recreate below
            }
        }

        var win = new StartMenuWindow(_mainWindow);
        _startMenuWindow = win;
        win.Closed += (_, _) => _startMenuWindow = null;

        // Make the StartMenuWindow the application's MainWindow so it shows in the taskbar and receives activation
        MainWindow = win;

        // Let the window size to its content, measure first, then position centered at bottom of primary work area
        try
        {
            win.SizeToContent = SizeToContent.WidthAndHeight;
            // measure against work area to get DesiredSize
            var wa = SystemParameters.WorkArea;
            win.Measure(new System.Windows.Size(wa.Width, wa.Height));
            var desired = win.DesiredSize;

            win.WindowStartupLocation = WindowStartupLocation.Manual;
            var left = wa.Left + (wa.Width - desired.Width) / 2.0;
            var top = wa.Bottom - desired.Height - 8; // small gap above taskbar
            if (left < wa.Left) left = wa.Left;
            if (left + desired.Width > wa.Right) left = wa.Right - desired.Width;
            if (top < wa.Top) top = wa.Top;

            win.Left = left;
            win.Top = top;
        }
        catch
        {
            // fallback to default positioning
        }

        win.Show();
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
