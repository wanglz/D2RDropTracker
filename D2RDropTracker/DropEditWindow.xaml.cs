using System.IO;
using System.Windows;
using System.Windows.Controls;
using D2RDropTracker.Models;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace D2RDropTracker;

public partial class DropEditWindow : Window
{
    private readonly string _defaultScreenshotDirectory;

    public string ItemNameValue => ItemNameTextBox.Text.Trim();
    public string CategoryValue => SelectedText(CategoryComboBox);
    public string QualityValue => SelectedText(QualityComboBox);
    public string TradeTypeValue => SelectedText(TradeTypeComboBox);
    public string TradeRunesValue => TradeRunesTextBox.Text.Trim();
    public string TradeMoneyValue => TradeMoneyTextBox.Text.Trim();
    public string ScreenshotPathValue => ScreenshotPathTextBox.Text.Trim();
    public long RunIdValue => RunComboBox.SelectedValue is long value ? value : 0;

    public DropEditWindow(
        DropRecord record,
        IEnumerable<RunChoice> runs,
        string defaultScreenshotDirectory = "")
    {
        InitializeComponent();
        _defaultScreenshotDirectory = defaultScreenshotDirectory;
        ItemNameTextBox.Text = record.ItemName;
        SelectComboItem(CategoryComboBox, record.Category);
        SelectComboItem(QualityComboBox, record.Quality);
        SelectComboItem(TradeTypeComboBox,
            string.IsNullOrWhiteSpace(record.TradeType) ? "未处理" : record.TradeType);
        TradeRunesTextBox.Text = record.TradeRunes;
        TradeMoneyTextBox.Text = record.TradeMoney;
        ScreenshotPathTextBox.Text = record.ScreenshotPath;
        RunComboBox.ItemsSource = runs;
        RunComboBox.SelectedValue = record.RunId;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ItemNameValue))
        {
            System.Windows.MessageBox.Show("物品名称不能为空。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void BrowseScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WpfOpenFileDialog
        {
            Title = "选择关联图片",
            Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|所有文件 (*.*)|*.*",
            InitialDirectory = ResolveInitialDirectory()
        };
        if (dialog.ShowDialog(this) == true)
        {
            ScreenshotPathTextBox.Text = dialog.FileName;
        }
    }

    private void ClearScreenshotButton_Click(object sender, RoutedEventArgs e) =>
        ScreenshotPathTextBox.Clear();

    private string ResolveInitialDirectory()
    {
        var currentPath = ScreenshotPathTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            var directory = Path.GetDirectoryName(currentPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                return directory;
            }
        }

        if (!string.IsNullOrWhiteSpace(_defaultScreenshotDirectory))
        {
            Directory.CreateDirectory(_defaultScreenshotDirectory);
            return _defaultScreenshotDirectory;
        }

        Directory.CreateDirectory(AppContext.BaseDirectory);
        return AppContext.BaseDirectory;
    }

    private static string SelectedText(System.Windows.Controls.ComboBox comboBox) =>
        (comboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "其他";

    private static void SelectComboItem(System.Windows.Controls.ComboBox comboBox, string value)
    {
        foreach (var item in comboBox.Items.OfType<System.Windows.Controls.ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
        if (!string.IsNullOrWhiteSpace(value))
        {
            var legacyItem = new System.Windows.Controls.ComboBoxItem { Content = value };
            comboBox.Items.Add(legacyItem);
            comboBox.SelectedItem = legacyItem;
            return;
        }
        comboBox.SelectedIndex = 0;
    }
}
