using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

namespace KevLauncher;

public sealed class LauncherNode : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = string.Empty;
    private string _path = string.Empty;
    private string _parameters = string.Empty;

    public string Id { get => _id; set { _id = value; OnPropertyChanged(nameof(Id)); } }

    public string Name { get => _name; set { _name = value; OnPropertyChanged(nameof(Name)); } }

    public string Path { get => _path; set { _path = value; OnPropertyChanged(nameof(Path)); } }

    public string Parameters { get => _parameters; set { _parameters = value; OnPropertyChanged(nameof(Parameters)); } }

    public bool IsFolder { get; set; }

    private bool _isExpanded;
    public bool IsExpanded { get => _isExpanded; set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); } }
    // previously had inline edit support; kept minimal model without inline state

    public ObservableCollection<LauncherNode> Children { get; set; } = new ObservableCollection<LauncherNode>();

    public bool CanLaunch => !IsFolder && !string.IsNullOrWhiteSpace(Path);

    public static LauncherNode CreateFolder(string name)
    {
        return new LauncherNode
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            IsFolder = true
        };
    }

    public static LauncherNode CreateLaunchItem(string path)
    {
        return new LauncherNode
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = System.IO.Path.GetFileNameWithoutExtension(path),
            Path = path,
            IsFolder = false
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
