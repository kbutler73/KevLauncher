using System.Collections.ObjectModel;
using System.IO;

namespace KevLauncher;

public sealed class LauncherNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public bool IsFolder { get; set; }

    public ObservableCollection<LauncherNode> Children { get; set; } = [];

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
}
