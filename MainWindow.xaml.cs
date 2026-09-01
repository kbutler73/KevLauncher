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
    private System.Windows.Point _dragStartPoint;
    private string? _draggedNodeId;

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
        try
        {
            if (FindName("StartWithWindowsCheckbox") is System.Windows.Controls.CheckBox cb)
            {
                cb.IsChecked = StartupManager.IsEnabled();
            }
        }
        catch
        {
            // ignore - best effort
        }
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

    private void OnStartWithWindowsChecked(object sender, RoutedEventArgs e)
    {
        try
        {
            StartupManager.SetEnabled(true);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"Could not enable Start with Windows.\n\n{ex.Message}", "KevLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
            if (sender is System.Windows.Controls.CheckBox cb)
            {
                cb.IsChecked = false;
            }
        }
    }

    private void OnStartWithWindowsUnchecked(object sender, RoutedEventArgs e)
    {
        try
        {
            StartupManager.SetEnabled(false);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"Could not disable Start with Windows.\n\n{ex.Message}", "KevLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
            if (sender is System.Windows.Controls.CheckBox cb)
            {
                cb.IsChecked = true;
            }
        }
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

    private void OnRenameClick(object sender, RoutedEventArgs e)
    {
        if ((sender as System.Windows.Controls.MenuItem)?.Parent is System.Windows.Controls.ContextMenu menu
            && menu.PlacementTarget is System.Windows.FrameworkElement fe
            && fe.DataContext is LauncherNode node)
        {
            var newName = Microsoft.VisualBasic.Interaction.InputBox("Name:", "Rename", node.Name).Trim();
            if (!string.IsNullOrWhiteSpace(newName) && newName != node.Name)
            {
                node.Name = newName;
                Save();
            }
        }
    }

    private void OnTreeViewPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        if (LauncherTree.SelectedItem is LauncherNode node)
        {
            _draggedNodeId = node.Id;
        }
        else
        {
            _draggedNodeId = null;
        }
    }

    private void OnTreeViewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
            return;

        if (_draggedNodeId is null)
            return;

        var currentPos = e.GetPosition(null);
        if (Math.Abs(currentPos.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(currentPos.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            var data = new System.Windows.DataObject("KevLauncher.LauncherNode", _draggedNodeId);
            System.Windows.DragDrop.DoDragDrop(LauncherTree, data, System.Windows.DragDropEffects.Move);
        }
    }

    private void OnTreeViewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("KevLauncher.LauncherNode"))
        {
            e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = System.Windows.DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnTreeViewDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("KevLauncher.LauncherNode"))
            return;

        var sourceId = e.Data.GetData("KevLauncher.LauncherNode") as string;
        if (string.IsNullOrWhiteSpace(sourceId))
            return;

        // find target node under mouse
        var tvItem = FindTreeViewItemUnderMouse(e.GetPosition(LauncherTree));
        LauncherNode? targetNode = tvItem?.DataContext as LauncherNode;

        // prevent dropping onto self or descendant
        if (targetNode is not null && IsDescendant(sourceId, targetNode))
        {
            return;
        }

        // remove from old parent
        var sourceNode = FindNode(RootItems, sourceId);
        if (sourceNode is null)
            return;

        var oldParent = FindParent(RootItems, sourceId);
        if (oldParent is null)
        {
            RootItems.Remove(sourceNode);
        }
        else
        {
            oldParent.Children.Remove(sourceNode);
        }

        // add to target
        if (targetNode is null)
        {
            // drop to root
            RootItems.Add(sourceNode);
        }
        else if (targetNode.IsFolder)
        {
            targetNode.Children.Add(sourceNode);
        }
        else
        {
            // insert next to target in same parent
            var parent = FindParent(RootItems, targetNode.Id);
            if (parent is null)
            {
                var index = RootItems.IndexOf(targetNode);
                RootItems.Insert(index + 1, sourceNode);
            }
            else
            {
                var index = parent.Children.IndexOf(targetNode);
                parent.Children.Insert(index + 1, sourceNode);
            }
        }

        Save();
    }

    private System.Windows.Controls.TreeViewItem? FindTreeViewItemUnderMouse(System.Windows.Point position)
    {
        var element = LauncherTree.InputHitTest(position) as System.Windows.DependencyObject;
        while (element is not null && element is not System.Windows.Controls.TreeViewItem)
        {
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }

        return element as System.Windows.Controls.TreeViewItem;
    }

    private bool IsDescendant(string sourceId, LauncherNode target)
    {
        if (target.Id == sourceId)
            return true;

        foreach (var child in target.Children)
        {
            if (IsDescendant(sourceId, child))
                return true;
        }

        return false;
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
            IsFolder = node.IsFolder,
            IsExpanded = node.IsExpanded
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
            IsFolder = node.IsFolder,
            IsExpanded = node.IsExpanded
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
