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

    public IReadOnlyList<LauncherItem> Load()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<LauncherItem>>(json, SerializerOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Save(IEnumerable<LauncherItem> items)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_filePath, JsonSerializer.Serialize(items, SerializerOptions));
    }
}
