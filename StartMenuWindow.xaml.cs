using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace KevLauncher;

public partial class StartMenuWindow : Window
{
    private readonly MainWindow _main;

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
            try
            {
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
                iconElement = new System.Windows.Controls.Image
                {
                    Width = 48,
                    Height = 48,
                    Source = (new PathToIconConverter()).Convert(child.Path, typeof(System.Windows.Media.ImageSource), null, System.Globalization.CultureInfo.CurrentCulture) as System.Windows.Media.ImageSource
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
                Width = 48,
                Height = 48,
                Child = iconElement,
                Background = System.Windows.Media.Brushes.Transparent,
                CornerRadius = new System.Windows.CornerRadius(8),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Padding = new Thickness(6)
            };

            stack.Children.Add(iconContainer);
            stack.Children.Add(txt);
            btn.Content = stack;
            btn.Click += IconButton_Click;

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

                var scale = new System.Windows.Media.ScaleTransform(1.0, 1.0);
                iconElement.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                iconElement.RenderTransform = scale;

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
