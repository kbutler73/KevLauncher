using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace KevLauncher;

/// <summary>
/// Best-effort integration with the tabbed File Explorer UI in Windows 11.
/// Windows has no public shell-launch API to target a new tab, so this uses the
/// same keyboard actions a user would use after bringing an Explorer window forward.
/// </summary>
internal static class ExplorerTabLauncher
{
    private const string ExplorerWindowClass = "CabinetWClass";
    private const int SwRestore = 9;

    public static async Task<bool> TryOpenInExistingTabAsync(string path)
    {
        var explorerWindow = FindExplorerWindow();
        if (explorerWindow == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (IsIconic(explorerWindow))
            {
                ShowWindow(explorerWindow, SwRestore);
            }

            if (!SetForegroundWindow(explorerWindow))
            {
                return false;
            }

            // Let Explorer process activation before sending its standard tab and
            // address-bar shortcuts. Recheck focus after the tab has been created
            // so we never type a path into another application.
            await Task.Delay(125);
            if (GetForegroundWindow() != explorerWindow)
            {
                return false;
            }

            SendKeys.SendWait("^t");
            await Task.Delay(125);
            if (GetForegroundWindow() != explorerWindow)
            {
                return false;
            }

            SendKeys.SendWait("^l");
            SendKeys.SendWait(EscapeForSendKeys(path));
            SendKeys.SendWait("{ENTER}");
            return true;
        }
        catch (ExternalException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static IntPtr FindExplorerWindow()
    {
        var foregroundWindow = GetForegroundWindow();
        if (IsExplorerWindow(foregroundWindow))
        {
            return foregroundWindow;
        }

        var explorerWindow = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            if (IsWindowVisible(window) && IsExplorerWindow(window))
            {
                explorerWindow = window;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return explorerWindow;
    }

    private static bool IsExplorerWindow(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return false;
        }

        var className = new StringBuilder(256);
        return GetClassName(window, className, className.Capacity) > 0
            && string.Equals(className.ToString(), ExplorerWindowClass, StringComparison.Ordinal);
    }

    private static string EscapeForSendKeys(string value)
    {
        var escaped = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            escaped.Append(character switch
            {
                '{' => "{{}",
                '}' => "{}}",
                '+' => "{+}",
                '^' => "{^}",
                '%' => "{%}",
                '~' => "{~}",
                '(' => "{(}",
                ')' => "{)}",
                '[' => "{[}",
                ']' => "{]}",
                _ => character.ToString()
            });
        }

        return escaped.ToString();
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder className, int maxCount);
}
