using System.IO;
using System.Text.Json;
using D2RDropTracker.Models;

namespace D2RDropTracker.Data;

public sealed class ItemCatalogService
{
    private readonly string _builtInPath;
    private readonly string _customPath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ItemCatalogService()
    {
        _builtInPath = Path.Combine(AppContext.BaseDirectory, "Data", "items.json");
        var userDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "D2RDropTracker");
        Directory.CreateDirectory(userDirectory);
        _customPath = Path.Combine(userDirectory, "custom-items.json");
        Reload();
    }

    public IReadOnlyList<ItemCatalogEntry> Items { get; private set; } = [];

    public void Reload()
    {
        var items = LoadFile(_builtInPath);
        items.AddRange(LoadFile(_customPath));
        Items = items
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .OrderBy(item => item.Name)
            .ToList();
    }

    public ItemCatalogEntry? Find(string name)
    {
        var value = name.Trim();
        return Items.FirstOrDefault(item =>
            string.Equals(item.Name, value, StringComparison.OrdinalIgnoreCase) ||
            item.Aliases.Any(alias =>
                string.Equals(alias, value, StringComparison.OrdinalIgnoreCase)));
    }

    public List<ItemCatalogEntry> GetCustomItems() => LoadFile(_customPath);

    public void SaveCustomItems(IEnumerable<ItemCatalogEntry> items)
    {
        File.WriteAllText(
            _customPath,
            JsonSerializer.Serialize(items.ToList(), _jsonOptions));
        Reload();
    }

    private List<ItemCatalogEntry> LoadFile(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<List<ItemCatalogEntry>>(
                    File.ReadAllText(path), _jsonOptions) ?? []
                : [];
        }
        catch (Exception ex)
        {
            LogService.Write($"读取物品词库失败：{path}", ex);
            return [];
        }
    }
}
