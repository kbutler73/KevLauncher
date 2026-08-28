using System.IO;
using System.Text.Json;

namespace KevLauncher;

public sealed class LauncherStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public LauncherStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _filePath = Path.Combine(appData, "KevLauncher", "launcher-items.json");
    }

    public IReadOnlyList<LauncherNode> Load()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var nodes = JsonSerializer.Deserialize<List<LauncherNode>>(json, SerializerOptions);
            if (nodes is not null)
            {
                return nodes;
            }

            var legacyItems = JsonSerializer.Deserialize<List<LegacyLauncherItem>>(json, SerializerOptions) ?? [];
            return legacyItems
                .Where(item => !string.IsNullOrWhiteSpace(item.Path))
                .Select(item => new LauncherNode
                {
                    Name = item.Name,
                    Path = item.Path,
                    IsFolder = false
                })
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public void Save(IEnumerable<LauncherNode> items)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_filePath, JsonSerializer.Serialize(items, SerializerOptions));
    }

    private sealed class LegacyLauncherItem
    {
        public string Name { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;
    }
}
