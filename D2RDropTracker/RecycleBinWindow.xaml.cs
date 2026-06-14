using System.Collections.ObjectModel;
using System.Windows;
using D2RDropTracker.Data;
using D2RDropTracker.Models;

namespace D2RDropTracker;

public partial class RecycleBinWindow : Window
{
    private readonly DatabaseService _database;
    private readonly ObservableCollection<DeletedDropRecord> _items = [];

    public event EventHandler? DataChanged;

    public RecycleBinWindow(DatabaseService database)
    {
        InitializeComponent();
        _database = database;
        DeletedDropsGrid.ItemsSource = _items;
        Loaded += (_, _) => Reload();
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (DeletedDropsGrid.SelectedItem is not DeletedDropRecord selected)
        {
            return;
        }
        if (!_database.RestoreDeletedDrop(selected.Id))
        {
            System.Windows.MessageBox.Show("原场次已经不存在，无法恢复该掉落。", "恢复失败",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Reload();
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (DeletedDropsGrid.SelectedItem is not DeletedDropRecord selected)
        {
            return;
        }
        if (System.Windows.MessageBox.Show($"永久删除“{selected.ItemName}”吗？", "确认",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        _database.PermanentlyDeleteDeletedDrop(selected.Id);
        Reload();
    }

    private void EmptyButton_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show("确定清空全部回收站记录吗？此操作无法撤销。", "确认清空",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        _database.EmptyDeletedDrops();
        Reload();
    }

    private void Reload()
    {
        _items.Clear();
        foreach (var item in _database.GetDeletedDrops())
        {
            _items.Add(item);
        }
    }
}
