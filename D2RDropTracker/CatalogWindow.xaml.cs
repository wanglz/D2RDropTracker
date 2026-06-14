using System.Collections.ObjectModel;
using System.Windows;
using D2RDropTracker.Data;
using D2RDropTracker.Models;

namespace D2RDropTracker;

public partial class CatalogWindow : Window
{
    private readonly ItemCatalogService _catalog;
    private readonly ObservableCollection<CatalogRow> _rows = [];

    public CatalogWindow(ItemCatalogService catalog)
    {
        InitializeComponent();
        _catalog = catalog;
        foreach (var item in catalog.GetCustomItems())
        {
            _rows.Add(new CatalogRow
            {
                Name = item.Name,
                Category = item.Category,
                Quality = item.Quality,
                AliasesText = string.Join(" | ", item.Aliases)
            });
        }
        CatalogGrid.ItemsSource = _rows;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        CatalogGrid.CommitEdit();
        _catalog.SaveCustomItems(_rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Name))
            .Select(row => new ItemCatalogEntry
            {
                Name = row.Name.Trim(),
                Category = string.IsNullOrWhiteSpace(row.Category) ? "其他" : row.Category.Trim(),
                Quality = string.IsNullOrWhiteSpace(row.Quality) ? "普通" : row.Quality.Trim(),
                Aliases = row.AliasesText.Split('|', StringSplitOptions.RemoveEmptyEntries)
                    .Select(alias => alias.Trim())
                    .Where(alias => alias.Length > 0)
                    .ToList()
            }));
        DialogResult = true;
    }

    public sealed class CatalogRow
    {
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Quality { get; set; } = "";
        public string AliasesText { get; set; } = "";
    }
}
