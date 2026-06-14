namespace D2RDropTracker.Models;

public sealed class ItemCatalogEntry
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Quality { get; set; } = "";
    public List<string> Aliases { get; set; } = [];
}
