using System.Windows;
using System.Windows.Controls;
using D2RDropTracker.Models;

namespace D2RDropTracker;

public partial class DropEditWindow : Window
{
    public string ItemNameValue => ItemNameTextBox.Text.Trim();
    public string CategoryValue => SelectedText(CategoryComboBox);
    public string QualityValue => SelectedText(QualityComboBox);
    public long RunIdValue => RunComboBox.SelectedValue is long value ? value : 0;

    public DropEditWindow(DropRecord record, IEnumerable<RunChoice> runs)
    {
        InitializeComponent();
        ItemNameTextBox.Text = record.ItemName;
        SelectComboItem(CategoryComboBox, record.Category);
        SelectComboItem(QualityComboBox, record.Quality);
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
