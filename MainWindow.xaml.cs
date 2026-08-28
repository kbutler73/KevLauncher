using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace KevLauncher;

public partial class MainWindow : Window
{
    private readonly LauncherStore _store = new();

    public ObservableCollection<LauncherItem> Items { get; } = [];

    public ICollectionView LauncherItemsView { get; }

    public bool IsExiting { get; set; }

    public MainWindow()
    {
        InitializeComponent();

        LauncherItemsView = CollectionViewSource.GetDefaultView(Items);
        LauncherItemsView.Filter = FilterLauncherItems;
        DataContext = this;

        foreach (var item in _store.Load())
        {
            Items.Add(item);
        }

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

    public void LaunchItem(LauncherItem item)
    {
        Launch(item);
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        AddItemFromDialog();
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
        var existing = Items.Select(item => item.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths.Where(path => File.Exists(path) || Directory.Exists(path)))
        {
            if (!existing.Add(path))
            {
                continue;
            }

            Items.Add(new LauncherItem
            {
                Name = Path.GetFileNameWithoutExtension(path),
                Path = path
            });
        }

        Save();
    }

    private bool FilterLauncherItems(object item)
    {
        if (item is not LauncherItem launcherItem)
        {
            return false;
        }

        var query = SearchBox?.Text?.Trim();
        return string.IsNullOrWhiteSpace(query)
            || launcherItem.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || launcherItem.Path.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void OnSearchTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        LauncherItemsView.Refresh();
        UpdateEmptyState();
    }

    private void OnRunClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is LauncherItem item)
        {
            Launch(item);
        }
    }

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LauncherList.SelectedItem is LauncherItem item)
        {
            Launch(item);
        }
    }

    private void OnLauncherListKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && LauncherList.SelectedItem is LauncherItem item)
        {
            Launch(item);
        }
        else if (e.Key == Key.Delete && LauncherList.SelectedItem is LauncherItem selected)
        {
            Items.Remove(selected);
            Save();
        }
    }

    private void Launch(LauncherItem item)
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
        if ((sender as FrameworkElement)?.Tag is not LauncherItem item)
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
        if ((sender as FrameworkElement)?.Tag is LauncherItem item)
        {
            Items.Remove(item);
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
        _store.Save(Items);
        LauncherItemsView.Refresh();
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = Items.Count == 0 || LauncherItemsView.IsEmpty
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
