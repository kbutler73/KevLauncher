using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Documents;
using Microsoft.VisualBasic;

namespace KevLauncher;

public partial class MainWindow : Window
{
    private readonly LauncherStore _store = new();
    private LauncherNode? _selectedNode;
    private System.Windows.Point _dragStartPoint;
    private string? _draggedNodeId;
        private bool _minimizeToTray = false; // when false, keep taskbar icon visible
        private WindowState _lastWindowState = WindowState.Normal;

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

    // Previously handled activation to toggle StartMenu; StartMenu is now the primary window.

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

    private void OnRenameClick(object sender, RoutedEventArgs e)
    {
        // kept for compatibility; redirect to edit
        OnEditClick(sender, e);
    }

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        LauncherNode? node = null;

        // MenuItem context
        if (sender is System.Windows.Controls.MenuItem mi && mi.Parent is System.Windows.Controls.ContextMenu menu && menu.PlacementTarget is System.Windows.FrameworkElement fe && fe.DataContext is LauncherNode n1)
        {
            node = n1;
        }
        // Button or other FrameworkElement using Tag
        else if (sender is System.Windows.FrameworkElement fe2 && fe2.Tag is LauncherNode n2)
        {
            node = n2;
        }
        // Fallback: DataContext
        else if (sender is System.Windows.FrameworkElement fe3 && fe3.DataContext is LauncherNode n3)
        {
            node = n3;
        }

        if (node is null)
            return;

        var dlg = new EditItemWindow(node.Name, node.Path, node.Parameters)
        {
            Owner = this
        };

        if (dlg.ShowDialog() == true)
        {
            node.Name = dlg.ItemName;
            node.Path = dlg.ItemPath;
            node.Parameters = dlg.ItemParameters;
            Save();
        }
    }

    // Inline edit handlers removed; editing now uses EditItemWindow

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

        // show visual indicator for before/inside/after
        var pos = e.GetPosition(LauncherTree);
        var tvItem = FindTreeViewItemUnderMouse(pos);

        UpdateDropAdorner(tvItem, e.GetPosition(tvItem ?? (System.Windows.IInputElement)LauncherTree));
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

        // determine placement
        var relative = tvItem is not null ? e.GetPosition(tvItem) : e.GetPosition(LauncherTree);
        var placement = DeterminePlacement(tvItem, relative);

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

        // add to target according to placement
        if (targetNode is null)
        {
            // drop to root
            RootItems.Add(sourceNode);
        }
        else if (placement == DropPosition.Inside && targetNode.IsFolder)
        {
            targetNode.Children.Add(sourceNode);
        }
        else if (placement == DropPosition.Before)
        {
            var parent = FindParent(RootItems, targetNode.Id);
            if (parent is null)
            {
                var index = RootItems.IndexOf(targetNode);
                RootItems.Insert(index, sourceNode);
            }
            else
            {
                var index = parent.Children.IndexOf(targetNode);
                parent.Children.Insert(index, sourceNode);
            }
        }
        else // After or default
        {
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

        RemoveDropAdorner();

        Save();
    }

    private System.Windows.Controls.TreeViewItem? FindTreeViewItemUnderMouse(System.Windows.Point position)
    {
        var element = LauncherTree.InputHitTest(position) as System.Windows.DependencyObject;
        while (element is not null && element is not System.Windows.Controls.TreeViewItem)
        {
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }
        if (element is null)
            return null;

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

    private DropAdorner? _currentAdorner;
    private System.Windows.Controls.TreeViewItem? _adornedItem;

    private DropPosition DeterminePlacement(System.Windows.Controls.TreeViewItem? tvItem, System.Windows.Point relative)
    {
        if (tvItem is null)
            return DropPosition.After;

        var h = tvItem.ActualHeight;
        if (h <= 0)
            return DropPosition.After;

        if (relative.Y < h * 0.33)
            return DropPosition.Before;
        if (relative.Y > h * 0.66)
            return DropPosition.After;
        return DropPosition.Inside;
    }

    private void UpdateDropAdorner(System.Windows.Controls.TreeViewItem? tvItem, System.Windows.Point relative)
    {
        var placement = DeterminePlacement(tvItem, relative);

        if (_adornedItem != tvItem || _currentAdorner is null)
        {
            RemoveDropAdorner();
        }

        if (tvItem is null)
            return;

        if (_currentAdorner is not null && _adornedItem == tvItem)
        {
            // already showing
            return;
        }

        var layer = AdornerLayer.GetAdornerLayer(tvItem);
        if (layer is null)
            return;

        _currentAdorner = new DropAdorner(tvItem, placement);
        layer.Add(_currentAdorner);
        _adornedItem = tvItem;
    }

    private void RemoveDropAdorner()
    {
        if (_currentAdorner is null || _adornedItem is null)
            return;

        var layer = AdornerLayer.GetAdornerLayer(_adornedItem);
        if (layer is not null)
        {
            layer.Remove(_currentAdorner);
        }

        _currentAdorner = null;
        _adornedItem = null;
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(path => File.Exists(path) || Directory.Exists(path)))
        {
            if (ContainsLaunchPath(RootItems, path))
            {
                continue;
            }

            var node = LauncherNode.CreateLaunchItem(path);
            AddNode(node);

            // Prompt to edit parameters / name after adding a new launch item
            try
            {
                var dlg = new EditItemWindow(node.Name, node.Path, node.Parameters)
                {
                    Owner = this
                };

                if (dlg.ShowDialog() == true)
                {
                    node.Name = dlg.ItemName;
                    node.Path = dlg.ItemPath;
                    node.Parameters = dlg.ItemParameters;
                }
            }
            catch
            {
                // best-effort
            }
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
            // launch on double-click
            LaunchItem(item);
        }
    }

    private System.Windows.Controls.TreeViewItem? FindTreeViewItemByNode(LauncherNode node)
    {
        // Walk the tree to find the TreeViewItem whose DataContext.Id matches
        return FindTreeViewItem(LauncherTree, node.Id);
    }

    private System.Windows.Controls.TreeViewItem? FindTreeViewItem(System.Windows.Controls.ItemsControl container, string id)
    {
        for (int i = 0; i < container.Items.Count; i++)
        {
            var item = container.ItemContainerGenerator.ContainerFromIndex(i) as System.Windows.Controls.TreeViewItem;
            if (item is null)
                continue;

            if (item.DataContext is LauncherNode ln && ln.Id == id)
                return item;

            var child = FindTreeViewItem(item, id);
            if (child is not null)
                return child;
        }

        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) return null;

        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
            if (child is T t)
                return t;

            var result = FindVisualChild<T>(child);
            if (result is not null)
                return result;
        }

        return null;
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
        else if (e.Key == Key.F2 && LauncherTree.SelectedItem is LauncherNode toRename)
        {
            // open edit dialog for selected node
            var dlg = new EditItemWindow(toRename.Name, toRename.Path, toRename.Parameters)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                toRename.Name = dlg.ItemName;
                toRename.Path = dlg.ItemPath;
                toRename.Parameters = dlg.ItemParameters;
                Save();
            }

            e.Handled = true;
        }
    }

    private void Launch(LauncherNode item)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = item.Path,
                Arguments = item.Parameters ?? string.Empty,
                UseShellExecute = true,
                WorkingDirectory = !string.IsNullOrWhiteSpace(item.Path) && Directory.Exists(item.Path)
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
        // track last state
        _lastWindowState = WindowState;

        if (_minimizeToTray && WindowState == WindowState.Minimized)
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
