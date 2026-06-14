using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using D2RDropTracker.Data;
using D2RDropTracker.Models;

namespace D2RDropTracker;

public partial class BackupWindow : Window
{
    private readonly DatabaseService _database;
    private readonly ObservableCollection<BackupInfo> _backups = [];

    public bool RestoreCompleted { get; private set; }

    public BackupWindow(DatabaseService database)
    {
        InitializeComponent();
        _database = database;
        BackupGrid.ItemsSource = _backups;
        Loaded += (_, _) => LoadBackups();
    }

    private void CreateBackupButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = _database.CreateManualBackup();
            LoadBackups();
            StatusTextBlock.Text = $"备份已创建：{System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            LogService.Write("手动备份失败", ex);
            System.Windows.MessageBox.Show(
                ex.Message, "备份失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (BackupGrid.SelectedItem is not BackupInfo selected)
        {
            System.Windows.MessageBox.Show("请先选择一个备份。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (System.Windows.MessageBox.Show(
                $"确定恢复“{selected.FileName}”吗？\n当前数据库会先自动备份，恢复后程序将退出。",
                "确认恢复", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _database.RestoreBackup(selected.FilePath);
            RestoreCompleted = true;
            System.Windows.MessageBox.Show("恢复成功。请重新启动程序加载恢复的数据。",
                "恢复完成", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            LogService.Write("恢复数据库失败", ex);
            System.Windows.MessageBox.Show(
                $"恢复失败：{ex.Message}\n日志：{LogService.CurrentLogPath}",
                "恢复失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = _database.GetBackupDirectory(),
            UseShellExecute = true
        });
    }

    private void LoadBackups()
    {
        _backups.Clear();
        foreach (var backup in _database.GetBackups())
        {
            _backups.Add(backup);
        }
        StatusTextBlock.Text = $"共 {_backups.Count} 份备份";
    }
}
