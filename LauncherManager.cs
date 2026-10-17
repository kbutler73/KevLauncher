using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace KevLauncher;

public sealed class LauncherManager
{
    private readonly LauncherStore _store = new();

    public ObservableCollection<LauncherNode> RootItems { get; } = [];

    public LauncherManager()
    {
        foreach (var item in _store.Load())
        {
            EnsureNodeShape(item);
            RootItems.Add(item);
        }
    }

    public void AddItemFromDialog(Window? owner = null)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Add to KevLauncher",
            CheckFileExists = true,
            Filter = "Launchable items|*.exe;*.lnk;*.url;*.bat;*.cmd;*.ps1;*.msi|All files|*.*"
        };

        if (dialog.ShowDialog(owner) != true || ContainsLaunchPath(RootItems, dialog.FileName))
        {
            return;
        }

        var node = LauncherNode.CreateLaunchItem(dialog.FileName);
        RootItems.Add(node);

        var editDialog = new EditItemWindow(node.Name, node.Path, node.Parameters);
        if (owner is not null)
        {
            editDialog.Owner = owner;
        }

        if (editDialog.ShowDialog() == true)
        {
            node.Name = editDialog.ItemName;
            node.Path = editDialog.ItemPath;
            node.Parameters = editDialog.ItemParameters;
        }

        Save();
    }

    public void LaunchItem(LauncherNode item, Window? owner = null)
    {
        if (!item.CanLaunch)
        {
            return;
        }

        try
        {
            if (IsFolderPath(item.Path))
            {
                OpenInExplorer(item.Path);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = item.Path,
                Arguments = item.Parameters ?? string.Empty,
                UseShellExecute = true,
                WorkingDirectory = Directory.Exists(item.Path)
                    ? item.Path
                    : Path.GetDirectoryName(item.Path)
            });
        }
        catch (Exception ex)
        {
            if (owner is null)
            {
                System.Windows.MessageBox.Show($"Could not launch {item.Name}.\n\n{ex.Message}", "KevLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                System.Windows.MessageBox.Show(owner, $"Could not launch {item.Name}.\n\n{ex.Message}", "KevLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public void Save() => _store.Save(RootItems);

    private static bool IsFolderPath(string path) =>
        Directory.Exists(path) || IsNetworkFolderPath(path);

    private static bool IsNetworkFolderPath(string path) =>
        IsNetworkLocation(path) && (!Path.HasExtension(path) || Directory.Exists(path));

    private static bool IsNetworkLocation(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return true;
        }

        try
        {
            var root = Path.GetPathRoot(path);
            return !string.IsNullOrWhiteSpace(root)
                && new DriveInfo(root).DriveType == DriveType.Network;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void OpenInExplorer(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{path.Replace("\"", "\\\"")}\"",
            UseShellExecute = true
        });
    }

    private static bool ContainsLaunchPath(IEnumerable<LauncherNode> nodes, string path)
    {
        return Flatten(nodes).Any(item => item.CanLaunch && string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<LauncherNode> Flatten(IEnumerable<LauncherNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }

    private static void EnsureNodeShape(LauncherNode node)
    {
        if (string.IsNullOrWhiteSpace(node.Id))
        {
            node.Id = Guid.NewGuid().ToString("N");
        }

        node.Children ??= [];
        foreach (var child in node.Children)
        {
            EnsureNodeShape(child);
        }
    }
}
