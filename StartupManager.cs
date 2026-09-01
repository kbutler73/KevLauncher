using Microsoft.Win32;
using System.IO;

namespace KevLauncher;

public static class StartupManager
{
    private const string AppName = "KevLauncher";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(AppName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, true);

        if (enabled)
        {
            key.SetValue(AppName, $"\"{GetExecutablePath()}\" --minimized");
        }
        else
        {
            key.DeleteValue(AppName, false);
        }
    }

    private static string GetExecutablePath()
    {
        var appExe = Path.Combine(AppContext.BaseDirectory, "KevLauncher.exe");
        if (File.Exists(appExe))
        {
            return appExe;
        }

        return Environment.ProcessPath ?? appExe;
    }
}
