using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace KevLauncher;

public partial class StartMenuWindow : Window
{
    private readonly MainWindow _main;
    private System.Windows.Point _tileDragStart;
    private System.Windows.Controls.Button? _draggedTile;
    private System.Windows.Controls.Button? _dropTargetTile;
    private DropAdorner? _tileInsertionAdorner;
    private System.Windows.Documents.AdornerLayer? _tileInsertionLayer;
    private bool _insertBeforeTile;
    private bool _isDraggingTile;

    public ObservableCollection<LauncherNode> Folders { get; } = new();

    public LauncherNode? SelectedFolder { get; set; }

    public StartMenuWindow(MainWindow main)
    {
        InitializeComponent();
        _main = main;

        // populate folders from main's RootItems
        // include a synthetic 'Root' folder that contains root-level items (non-folder)
        var rootNode = new LauncherNode { Id = "__root", Name = "Root", IsFolder = true };
        foreach (var item in _main.RootItems.Where(i => !i.IsFolder))
        {
            rootNode.Children.Add(item);
        }

        if (rootNode.Children.Count > 0)
        {
            Folders.Add(rootNode);
        }

        foreach (var item in _main.RootItems.Where(i => i.IsFolder))
        {
            Folders.Add(item);
        }

        DataContext = this;

        // if there are folders, select first
        if (Folders.Count > 0)
        {
            SelectedFolder = Folders[0];
        }

        Loaded += StartMenuWindow_Loaded;
        Deactivated += (_, _) =>
        {
            // When the Start Menu loses focus (click elsewhere or taskbar), minimize so it stays in taskbar
            // BUT if we have an owned dialog open (Edit dialog), don't minimize — allow editing.
            try
            {
                // if any owned window is visible, skip minimizing
                if (System.Windows.Application.Current is { } app)
                {
                    foreach (Window w in app.Windows)
                    {
                        if (w.Owner == this && w.IsVisible)
                        {
                            return;
                        }
                    }
                }

                if (this.WindowState == WindowState.Normal)
                {
                    this.WindowState = WindowState.Minimized;
                }
            }
            catch
            {
                this.Hide();
            }
        };
    }

    private void StartMenuWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        RefreshIcons();

        // respond to selection changes
        FoldersList.SelectionChanged += (_, _) =>
        {
            SelectedFolder = FoldersList.SelectedItem as LauncherNode;
            RefreshIcons();
        };

        // ensure first item selected
        if (FoldersList.Items.Count > 0)
        {
            FoldersList.SelectedIndex = 0;
        }
    }

    private void RefreshIcons()
    {
        IconsPanel.Children.Clear();

        if (SelectedFolder is null)
        {
            return;
        }

        foreach (var child in SelectedFolder.Children.Where(c => c.CanLaunch))
        {
            var btn = new System.Windows.Controls.Button
            {
                Width = 100,
                // allow the button to grow vertically for multi-line names
                MinHeight = 72,
                Margin = new Thickness(8,6,8,6),
                Tag = child,
                // no tooltip (label visible under icon)
                VerticalContentAlignment = VerticalAlignment.Top,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4),
                Cursor = System.Windows.Input.Cursors.Hand,
                Focusable = false
            };
            try
            {
                if (this.FindResource("IconTileButton") is System.Windows.Style s)
                {
                    btn.Style = s;
                }
            }
            catch { }

            var stack = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Vertical, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };

            // icon: folder glyph for folders, image for files
            System.Windows.FrameworkElement iconElement;
            // Treat actual folder nodes or filesystem directories as folders for icon purposes
            if (child.IsFolder || (!string.IsNullOrWhiteSpace(child.Path) && System.IO.Directory.Exists(child.Path)))
            {
                // use Segoe MDL2 Assets folder glyph (vector) so it respects theme brushes
                var tb = new System.Windows.Controls.TextBlock
                {
                    Text = "\uE8B7",
                    FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                    FontSize = 28,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };
                try
                {
                    if (System.Windows.Application.Current?.Resources["AccentBrush"] is System.Windows.Media.Brush acc)
                    {
                        tb.Foreground = acc;
                    }
                }
                catch { }
                iconElement = tb;
            }
            else
            {
                // create a flexible Image that will scale to fit the tile while preserving aspect ratio
                var imageSource = (new PathToIconConverter()).Convert(child.Path, typeof(System.Windows.Media.ImageSource), null, System.Globalization.CultureInfo.CurrentCulture) as System.Windows.Media.ImageSource;
                iconElement = new System.Windows.Controls.Image
                {
                    Source = imageSource,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    StretchDirection = System.Windows.Controls.StretchDirection.Both,
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = true
                };
            }

            var txt = new System.Windows.Controls.TextBlock
            {
                Text = child.Name,
                TextAlignment = System.Windows.TextAlignment.Center,
                TextWrapping = System.Windows.TextWrapping.Wrap,
                Width = 90,
                Margin = new Thickness(0,6,0,0)
            };

            // Put the icon inside a small border so we can highlight on hover without covering the label
            var iconContainer = new System.Windows.Controls.Border
            {
                Width = 56,
                Height = 56,
                Background = System.Windows.Media.Brushes.Transparent,
                CornerRadius = new System.Windows.CornerRadius(8),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Padding = new Thickness(4),
                Child = null
            };

            // If the icon element is an Image, constrain it to fit the container. Keep Width/Height unset so Stretch + Max* control sizing.
            if (iconElement is System.Windows.Controls.Image img)
            {
                img.MaxWidth = 48;
                img.MaxHeight = 48;
                img.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                img.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                img.Stretch = System.Windows.Media.Stretch.Uniform;
                iconContainer.Child = img;
            }
            else
            {
                // center vector/text glyphs as well
                if (iconElement is FrameworkElement fe)
                {
                    fe.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                    fe.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                }
                iconContainer.Child = iconElement;
            }

            stack.Children.Add(iconContainer);
            stack.Children.Add(txt);
            btn.Content = stack;
            btn.Click += IconButton_Click;
            btn.PreviewMouseLeftButtonDown += Tile_PreviewMouseLeftButtonDown;
            btn.PreviewMouseMove += Tile_PreviewMouseMove;
            // set DataContext so context menu handlers can find the node if needed
            btn.DataContext = child;

            // attach a context menu to allow editing the tile
            try
            {
                var cm = new System.Windows.Controls.ContextMenu();
                var editItem = new System.Windows.Controls.MenuItem { Header = "Edit..." };
                editItem.Click += (_, _) =>
                {
                    try
                    {
                        var dlg = new EditItemWindow(child.Name, child.Path, child.Parameters)
                        {
                            Owner = this
                        };

                        if (dlg.ShowDialog() == true)
                        {
                            child.Name = dlg.ItemName;
                            child.Path = dlg.ItemPath;
                            child.Parameters = dlg.ItemParameters;
                            _main.Save();
                            // refresh this view so changes show immediately
                            RefreshIcons();
                        }
                    }
                    catch { }
                };

                cm.Items.Add(editItem);
                btn.ContextMenu = cm;
            }
            catch { }

            // hover visual: subtle accent background behind the icon
            try
            {
                // Derive a hover color that's a subtle contrast from the surface background
                System.Windows.Media.Brush hoverBrush = null;
                try
                {
                    var surface = System.Windows.Application.Current?.Resources["SurfaceBackground"] as System.Windows.Media.SolidColorBrush;
                    var accent = System.Windows.Application.Current?.Resources["AccentBrush"] as System.Windows.Media.SolidColorBrush;

                    if (surface is not null)
                    {
                        var baseColor = surface.Color;
                        // perceived luminance
                        var lum = 0.2126 * baseColor.R + 0.7152 * baseColor.G + 0.0722 * baseColor.B;
                        double t = lum < 128 ? 0.12 : 0.06;

                        System.Windows.Media.Color blended;
                        if (lum < 128)
                        {
                            // blend with white to lighten slightly
                            blended = System.Windows.Media.Color.FromArgb(
                                baseColor.A,
                                (byte)(baseColor.R * (1 - t) + 255 * t),
                                (byte)(baseColor.G * (1 - t) + 255 * t),
                                (byte)(baseColor.B * (1 - t) + 255 * t));
                        }
                        else
                        {
                            // blend with black to darken slightly
                            blended = System.Windows.Media.Color.FromArgb(
                                baseColor.A,
                                (byte)(baseColor.R * (1 - t)),
                                (byte)(baseColor.G * (1 - t)),
                                (byte)(baseColor.B * (1 - t)));
                        }

                        hoverBrush = new System.Windows.Media.SolidColorBrush(blended) { Opacity = 1.0 };
                    }
                    else if (accent is not null)
                    {
                        hoverBrush = new System.Windows.Media.SolidColorBrush(accent.Color) { Opacity = 0.12 };
                    }
                    else
                    {
                        hoverBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGray) { Opacity = 0.08 };
                    }
                }
                catch
                {
                    hoverBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGray) { Opacity = 0.08 };
                }

                // apply scale to the container so both icon and its outline/shadow scale together
                var scale = new System.Windows.Media.ScaleTransform(1.0, 1.0);
                iconContainer.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                iconContainer.RenderTransform = scale;

                // use border outline on hover instead of filled rectangle
                btn.MouseEnter += (_, _) =>
                {
                    try
                    {
                        // apply a subtle drop shadow on hover instead of a filled background
                        var ds = new System.Windows.Media.Effects.DropShadowEffect
                        {
                            BlurRadius = 12,
                            ShadowDepth = 0,
                            Opacity = 0.38
                        };

                        // prefer a slightly tinted shadow using AccentBrush if available
                        try
                        {
                            if (System.Windows.Application.Current?.Resources["AccentBrush"] is System.Windows.Media.SolidColorBrush acc)
                            {
                                ds.Color = acc.Color;
                                ds.Opacity = 0.28;
                            }
                            else
                            {
                                ds.Color = System.Windows.Media.Colors.Black;
                                ds.Opacity = 0.38;
                            }
                        }
                        catch
                        {
                            ds.Color = System.Windows.Media.Colors.Black;
                        }

                        iconContainer.Effect = ds;
                        scale.ScaleX = 1.06;
                        scale.ScaleY = 1.06;
                    }
                    catch { }
                };

                btn.MouseLeave += (_, _) =>
                {
                    try
                    {
                        iconContainer.Effect = null;
                        scale.ScaleX = 1.0;
                        scale.ScaleY = 1.0;
                    }
                    catch { }
                };
            }
            catch { }

            IconsPanel.Children.Add(btn);
        }
    }

    private void IconButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isDraggingTile)
        {
            return;
        }

        if (sender is System.Windows.Controls.Button b && b.Tag is LauncherNode node)
        {
            _main.LaunchItem(node);
            // minimize after launching so the window remains in the taskbar
            try
            {
                this.WindowState = WindowState.Minimized;
            }
            catch
            {
                // fallback to Hide if minimize fails
                Hide();
            }
        }
    }

    private void Tile_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _draggedTile = sender as System.Windows.Controls.Button;
        _tileDragStart = e.GetPosition(IconsPanel);
    }

    private void Tile_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_draggedTile != sender || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(IconsPanel);
        if (Math.Abs(position.X - _tileDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _tileDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (_draggedTile.Tag is not LauncherNode node)
        {
            return;
        }

        _isDraggingTile = true;
        _draggedTile.Opacity = 0.45;
        try
        {
            System.Windows.DragDrop.DoDragDrop(_draggedTile,
                new System.Windows.DataObject("KevLauncher.StartMenuTile", node),
                System.Windows.DragDropEffects.Move);
        }
        finally
        {
            if (_draggedTile is not null)
            {
                _draggedTile.Opacity = 1;
            }

            _draggedTile = null;
            ClearDropTarget();
            // WPF can raise Click immediately after a completed drag.
            Dispatcher.BeginInvoke(() => _isDraggingTile = false,
                System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void IconsPanel_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("KevLauncher.StartMenuTile"))
        {
            e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var target = FindTileAt(e.GetPosition(IconsPanel));
        if (target == _draggedTile)
        {
            target = null;
        }

        var insertBefore = target is not null && e.GetPosition(target).X < target.ActualWidth / 2;
        SetDropTarget(target, insertBefore);
        e.Effects = System.Windows.DragDropEffects.Move;
        e.Handled = true;
    }

    private void IconsPanel_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        if (!IconsPanel.IsMouseOver)
        {
            ClearDropTarget();
        }
    }

    private void IconsPanel_Drop(object sender, System.Windows.DragEventArgs e)
    {
        try
        {
            if (e.Data.GetData("KevLauncher.StartMenuTile") is not LauncherNode dragged || SelectedFolder is null)
            {
                return;
            }

            var targetTile = FindTileAt(e.GetPosition(IconsPanel));
            var target = targetTile?.Tag as LauncherNode;
            var insertBefore = targetTile is not null && e.GetPosition(targetTile).X < targetTile.ActualWidth / 2;
            MoveTile(dragged, target, insertBefore);
        }
        finally
        {
            ClearDropTarget();
            e.Handled = true;
        }
    }

    private System.Windows.Controls.Button? FindTileAt(System.Windows.Point point)
    {
        var element = IconsPanel.InputHitTest(point) as DependencyObject;
        while (element is not null && element != IconsPanel)
        {
            if (element is System.Windows.Controls.Button button && button.Parent == IconsPanel)
            {
                return button;
            }

            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    private void SetDropTarget(System.Windows.Controls.Button? tile, bool insertBefore)
    {
        if (_dropTargetTile == tile && _insertBeforeTile == insertBefore)
        {
            return;
        }

        ClearDropTarget();
        _dropTargetTile = tile;
        _insertBeforeTile = insertBefore;
        if (tile is not null)
        {
            _tileInsertionLayer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(tile);
            if (_tileInsertionLayer is not null)
            {
                _tileInsertionAdorner = new DropAdorner(tile,
                    insertBefore ? DropPosition.TileBefore : DropPosition.TileAfter);
                _tileInsertionLayer.Add(_tileInsertionAdorner);
            }
        }
    }

    private void ClearDropTarget()
    {
        if (_dropTargetTile is not null)
        {
            _dropTargetTile = null;
        }

        if (_tileInsertionAdorner is not null && _tileInsertionLayer is not null)
        {
            _tileInsertionLayer.Remove(_tileInsertionAdorner);
        }

        _tileInsertionAdorner = null;
        _tileInsertionLayer = null;
    }

    private void MoveTile(LauncherNode dragged, LauncherNode? target, bool insertBefore)
    {
        if (target == dragged)
        {
            return;
        }

        var source = SelectedFolder?.Id == "__root" ? _main.RootItems : SelectedFolder?.Children;
        if (source is null || !source.Contains(dragged))
        {
            return;
        }

        var oldPositions = IconsPanel.Children
            .OfType<System.Windows.Controls.Button>()
            .ToDictionary(tile => tile, tile => tile.TransformToAncestor(IconsPanel).Transform(new System.Windows.Point()));

        source.Remove(dragged);
        if (target is not null && source.Contains(target))
        {
            var targetIndex = source.IndexOf(target);
            source.Insert(insertBefore ? targetIndex : targetIndex + 1, dragged);
        }
        else
        {
            // Dropping on empty space places the tile at the end, like a phone launcher.
            source.Add(dragged);
        }

        // The Root entry is a display-only collection, so mirror the persisted root
        // collection back into it before rebuilding the tiles.
        if (SelectedFolder?.Id == "__root")
        {
            SelectedFolder.Children.Clear();
            foreach (var item in _main.RootItems.Where(item => !item.IsFolder))
            {
                SelectedFolder.Children.Add(item);
            }
        }

        _main.Save();
        AnimateTilesToNewOrder(oldPositions);
    }

    private void AnimateTilesToNewOrder(IReadOnlyDictionary<System.Windows.Controls.Button, System.Windows.Point> oldPositions)
    {
        if (SelectedFolder is null)
        {
            return;
        }

        var tilesByNode = IconsPanel.Children
            .OfType<System.Windows.Controls.Button>()
            .Where(tile => tile.Tag is LauncherNode)
            .ToDictionary(tile => (LauncherNode)tile.Tag, tile => tile);

        var orderedTiles = SelectedFolder.Children
            .Where(node => node.CanLaunch && tilesByNode.ContainsKey(node))
            .Select(node => tilesByNode[node])
            .ToList();

        IconsPanel.Children.Clear();
        foreach (var tile in orderedTiles)
        {
            IconsPanel.Children.Add(tile);
        }

        IconsPanel.UpdateLayout();
        foreach (var tile in orderedTiles)
        {
            if (!oldPositions.TryGetValue(tile, out var oldPosition))
            {
                continue;
            }

            var newPosition = tile.TransformToAncestor(IconsPanel).Transform(new System.Windows.Point());
            var translate = new System.Windows.Media.TranslateTransform(
                oldPosition.X - newPosition.X,
                oldPosition.Y - newPosition.Y);
            tile.RenderTransform = translate;

            var duration = new Duration(TimeSpan.FromMilliseconds(220));
            var easing = new System.Windows.Media.Animation.CubicEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
            };
            translate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0, duration) { EasingFunction = easing });
            translate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0, duration) { EasingFunction = easing });
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // prevent closing the StartMenuWindow with the X button; minimize instead so the taskbar icon remains
        e.Cancel = true;
        try
        {
            this.WindowState = WindowState.Minimized;
        }
        catch
        {
            this.Hide();
        }
    }
}
