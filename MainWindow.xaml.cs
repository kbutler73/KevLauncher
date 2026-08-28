using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualBasic;

namespace KevLauncher;

public partial class MainWindow : Window
{
    private readonly LauncherStore _store = new();
    private LauncherNode? _selectedNode;

    public ObservableCollection<LauncherNode> RootItems { get; } = [];

    public ObservableCollection<LauncherNode> VisibleRootItems { get; } = [];

    public bool IsExiting { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        foreach (var item in _store.Load())
        {
            EnsureNodeShape(item);
            RootItems.Add(item);
        }

        RebuildVisibleTree();
        UpdateEmptyState();
    }

    public void AddItemFromDialog()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Add to KevLauncher",
            CheckFileExists = true,
            Filter = "Launchable items|*.exe;*.lnk;*.url;*.bat;*.cmd;*.ps1;*.msi|All files|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            AddPaths([dialog.FileName]);
        }
    }

    public void LaunchItem(LauncherNode item)
    {
        if (item.CanLaunch)
        {
            Launch(item);
        }
    }

    public IEnumerable<LauncherNode> GetLaunchItems()
    {
        return Flatten(RootItems).Where(item => item.CanLaunch);
    }

    public void ShowLauncherTree()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        LauncherTree.Focus();
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        AddItemFromDialog();
    }

    private void OnAddFolderClick(object sender, RoutedEventArgs e)
    {
        var name = Interaction.InputBox("Folder name:", "New KevLauncher Folder", "New Folder").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        AddNode(LauncherNode.CreateFolder(name));
        Save();
    }

    private void OnDragEnter(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] paths)
        {
            AddPaths(paths);
        }
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(path => File.Exists(path) || Directory.Exists(path)))
        {
            if (ContainsLaunchPath(RootItems, path))
            {
                continue;
            }

            AddNode(LauncherNode.CreateLaunchItem(path));
        }

        Save();
    }

    private void AddNode(LauncherNode node)
    {
        var selected = GetOriginalSelectedNode();
        if (selected?.IsFolder == true)
        {
            selected.Children.Add(node);
            return;
        }

        var parent = selected is null ? null : FindParent(RootItems, selected.Id);
        if (parent?.IsFolder == true)
        {
            parent.Children.Add(node);
        }
        else
        {
            RootItems.Add(node);
        }
    }

    private void OnSearchTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        RebuildVisibleTree();
        UpdateEmptyState();
    }

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        _selectedNode = e.NewValue as LauncherNode;
    }

    private void OnRunClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is LauncherNode item)
        {
            LaunchItem(item);
        }
    }

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LauncherTree.SelectedItem is LauncherNode item)
        {
            LaunchItem(item);
        }
    }

    private void OnLauncherTreeKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && LauncherTree.SelectedItem is LauncherNode item)
        {
            LaunchItem(item);
        }
        else if (e.Key == Key.Delete && LauncherTree.SelectedItem is LauncherNode selected)
        {
            RemoveNode(selected.Id);
            Save();
        }
    }

    private void Launch(LauncherNode item)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = item.Path,
                UseShellExecute = true,
                WorkingDirectory = Directory.Exists(item.Path)
                    ? item.Path
                    : Path.GetDirectoryName(item.Path)
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"Could not launch {item.Name}.\n\n{ex.Message}", "KevLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not LauncherNode item)
        {
            return;
        }

        var target = Directory.Exists(item.Path) ? item.Path : Path.GetDirectoryName(item.Path);
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        });
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is LauncherNode item)
        {
            RemoveNode(item.Id);
            Save();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (IsExiting)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void Save()
    {
        _store.Save(RootItems);
        RebuildVisibleTree();
        UpdateEmptyState();
    }

    private void RebuildVisibleTree()
    {
        VisibleRootItems.Clear();

        var query = SearchBox?.Text?.Trim();
        foreach (var item in RootItems)
        {
            var visible = string.IsNullOrWhiteSpace(query)
                ? item
                : CloneMatchingTree(item, query);

            if (visible is not null)
            {
                VisibleRootItems.Add(visible);
            }
        }
    }

    private static LauncherNode? CloneMatchingTree(LauncherNode node, string query)
    {
        var children = node.Children
            .Select(child => CloneMatchingTree(child, query))
            .Where(child => child is not null)
            .Cast<LauncherNode>()
            .ToList();

        var isMatch = node.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || node.Path.Contains(query, StringComparison.OrdinalIgnoreCase);

        if (!isMatch && children.Count == 0)
        {
            return null;
        }

        var clone = new LauncherNode
        {
            Id = node.Id,
            Name = node.Name,
            Path = node.Path,
            IsFolder = node.IsFolder
        };

        var visibleChildren = isMatch ? node.Children.AsEnumerable() : children;
        foreach (var child in visibleChildren)
        {
            clone.Children.Add(isMatch ? CloneTree(child) : child);
        }

        return clone;
    }

    private static LauncherNode CloneTree(LauncherNode node)
    {
        var clone = new LauncherNode
        {
            Id = node.Id,
            Name = node.Name,
            Path = node.Path,
            IsFolder = node.IsFolder
        };

        foreach (var child in node.Children)
        {
            clone.Children.Add(CloneTree(child));
        }

        return clone;
    }

    private LauncherNode? GetOriginalSelectedNode()
    {
        return _selectedNode is null ? null : FindNode(RootItems, _selectedNode.Id);
    }

    private static LauncherNode? FindNode(IEnumerable<LauncherNode> nodes, string id)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id)
            {
                return node;
            }

            var child = FindNode(node.Children, id);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }

    private static LauncherNode? FindParent(IEnumerable<LauncherNode> nodes, string childId)
    {
        foreach (var node in nodes)
        {
            if (node.Children.Any(child => child.Id == childId))
            {
                return node;
            }

            var parent = FindParent(node.Children, childId);
            if (parent is not null)
            {
                return parent;
            }
        }

        return null;
    }

    private bool RemoveNode(string id)
    {
        var root = RootItems.FirstOrDefault(item => item.Id == id);
        if (root is not null)
        {
            RootItems.Remove(root);
            return true;
        }

        foreach (var node in Flatten(RootItems).Where(node => node.IsFolder))
        {
            var child = node.Children.FirstOrDefault(item => item.Id == id);
            if (child is not null)
            {
                node.Children.Remove(child);
                return true;
            }
        }

        return false;
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

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = RootItems.Count == 0 || VisibleRootItems.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
