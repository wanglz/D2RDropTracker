using System.Windows;
using D2RDropTracker.Data;
using D2RDropTracker.Models;

namespace D2RDropTracker;

public partial class PreferencesWindow : Window
{
    private readonly DatabaseService _database;
    private readonly ItemCatalogService _catalog;

    public AppSettings ResultSettings { get; private set; }

    public PreferencesWindow(
        AppSettings settings, DatabaseService database, ItemCatalogService catalog)
    {
        InitializeComponent();
        _database = database;
        _catalog = catalog;
        ResultSettings = settings;

        var modifiers = new[] { "无", "Ctrl", "Alt", "Shift", "Ctrl+Alt", "Ctrl+Shift", "Alt+Shift", "Ctrl+Alt+Shift" };
        var keys = Enumerable.Range(1, 12).Select(number => $"F{number}").ToArray();
        CompleteModifierComboBox.ItemsSource = modifiers;
        DropModifierComboBox.ItemsSource = modifiers;
        OverlayModifierComboBox.ItemsSource = modifiers;
        ScreenshotModifierComboBox.ItemsSource = modifiers;
        CompleteKeyComboBox.ItemsSource = keys;
        DropKeyComboBox.ItemsSource = keys;
        OverlayKeyComboBox.ItemsSource = keys;
        ScreenshotKeyComboBox.ItemsSource = keys;
        DropCountComboBox.ItemsSource = Enumerable.Range(1, 10).ToArray();

        CompleteModifierComboBox.SelectedItem = settings.CompleteRunModifiers;
        DropModifierComboBox.SelectedItem = settings.AddDropModifiers;
        OverlayModifierComboBox.SelectedItem = settings.ToggleOverlayModifiers;
        ScreenshotModifierComboBox.SelectedItem = settings.ScreenshotModifiers;
        CompleteKeyComboBox.SelectedItem = settings.CompleteRunKey;
        DropKeyComboBox.SelectedItem = settings.AddDropKey;
        OverlayKeyComboBox.SelectedItem = settings.ToggleOverlayKey;
        ScreenshotKeyComboBox.SelectedItem = settings.ScreenshotKey;
        OpacitySlider.Value = settings.OverlayOpacity;
        ScaleSlider.Value = settings.OverlayScale;
        DropCountComboBox.SelectedItem = settings.OverlayDropCount;
        RetentionDaysTextBox.Text = settings.BackupRetentionDays.ToString();

        OpacitySlider.ValueChanged += (_, _) =>
            OpacityValueTextBlock.Text = $"{OpacitySlider.Value:P0}";
        ScaleSlider.ValueChanged += (_, _) =>
            ScaleValueTextBlock.Text = $"{ScaleSlider.Value:P0}";
        OpacityValueTextBlock.Text = $"{OpacitySlider.Value:P0}";
        ScaleValueTextBlock.Text = $"{ScaleSlider.Value:P0}";
    }

    private void CheckDatabaseButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = _database.CheckIntegrity();
            StatusTextBlock.Text = result == "ok"
                ? "数据库完整性检查通过"
                : $"数据库检查结果：{result}";
        }
        catch (Exception ex)
        {
            LogService.Write("数据库完整性检查失败", ex);
            StatusTextBlock.Text = $"检查失败：{ex.Message}";
        }
    }

    private void CatalogButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new CatalogWindow(_catalog) { Owner = this };
        window.ShowDialog();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(RetentionDaysTextBox.Text, out var retentionDays) || retentionDays < 1)
        {
            System.Windows.MessageBox.Show("备份保留天数必须是大于 0 的整数。", "设置错误",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var shortcuts = new[]
        {
            Shortcut(CompleteModifierComboBox.SelectedItem, CompleteKeyComboBox.SelectedItem),
            Shortcut(DropModifierComboBox.SelectedItem, DropKeyComboBox.SelectedItem),
            Shortcut(OverlayModifierComboBox.SelectedItem, OverlayKeyComboBox.SelectedItem),
            Shortcut(ScreenshotModifierComboBox.SelectedItem, ScreenshotKeyComboBox.SelectedItem)
        };
        if (shortcuts.Distinct(StringComparer.OrdinalIgnoreCase).Count() != shortcuts.Length)
        {
            System.Windows.MessageBox.Show("四个功能不能使用相同的组合快捷键。", "快捷键冲突",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ResultSettings.CompleteRunModifiers = CompleteModifierComboBox.SelectedItem?.ToString() ?? "无";
        ResultSettings.AddDropModifiers = DropModifierComboBox.SelectedItem?.ToString() ?? "无";
        ResultSettings.ToggleOverlayModifiers = OverlayModifierComboBox.SelectedItem?.ToString() ?? "无";
        ResultSettings.ScreenshotModifiers = ScreenshotModifierComboBox.SelectedItem?.ToString() ?? "无";
        ResultSettings.CompleteRunKey = CompleteKeyComboBox.SelectedItem?.ToString() ?? "F8";
        ResultSettings.AddDropKey = DropKeyComboBox.SelectedItem?.ToString() ?? "F9";
        ResultSettings.ToggleOverlayKey = OverlayKeyComboBox.SelectedItem?.ToString() ?? "F10";
        ResultSettings.ScreenshotKey = ScreenshotKeyComboBox.SelectedItem?.ToString() ?? "F11";
        ResultSettings.OverlayOpacity = OpacitySlider.Value;
        ResultSettings.OverlayScale = ScaleSlider.Value;
        ResultSettings.OverlayDropCount = (int)(DropCountComboBox.SelectedItem ?? 5);
        ResultSettings.BackupRetentionDays = retentionDays;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private static string Shortcut(object? modifier, object? key) =>
        $"{modifier ?? "无"}+{key ?? "F8"}";
}
