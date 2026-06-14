using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using D2RDropTracker.Models;

namespace D2RDropTracker;

public partial class RunEditWindow : Window
{
    public string CharacterValue => CharacterTextBox.Text.Trim();
    public string AreaValue => AreaTextBox.Text.Trim();
    public string DifficultyValue =>
        (DifficultyComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "地狱";
    public DateTime StartedAtValue { get; private set; }
    public DateTime EndedAtValue { get; private set; }
    public int PlayerCountValue =>
        int.TryParse(PlayerCountComboBox.SelectedItem?.ToString(), out var value) ? value : 1;
    public int MagicFindValue =>
        int.TryParse(MagicFindTextBox.Text, out var value) ? Math.Max(0, value) : 0;
    public string TagsValue => TagsTextBox.Text.Trim();
    public string NotesValue => NotesTextBox.Text.Trim();

    public RunEditWindow(RunHistoryItem run)
    {
        InitializeComponent();
        CharacterTextBox.Text = run.Character;
        AreaTextBox.Text = run.Area;
        SelectDifficulty(run.Difficulty);
        StartedAtTextBox.Text = run.StartedAt.ToString("yyyy-MM-dd HH:mm:ss");
        EndedAtTextBox.Text = run.EndedAt.ToString("yyyy-MM-dd HH:mm:ss");
        PlayerCountComboBox.ItemsSource = Enumerable.Range(1, 8);
        PlayerCountComboBox.SelectedItem = run.PlayerCount;
        MagicFindTextBox.Text = run.MagicFind.ToString();
        TagsTextBox.Text = run.Tags;
        NotesTextBox.Text = run.Notes;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        const string format = "yyyy-MM-dd HH:mm:ss";
        if (!DateTime.TryParseExact(StartedAtTextBox.Text.Trim(), format,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var startedAt) ||
            !DateTime.TryParseExact(EndedAtTextBox.Text.Trim(), format,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var endedAt))
        {
            System.Windows.MessageBox.Show("请输入正确的日期时间格式。", "格式错误",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (endedAt < startedAt)
        {
            System.Windows.MessageBox.Show("结束时间不能早于开始时间。", "时间错误",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        StartedAtValue = startedAt;
        EndedAtValue = endedAt;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void SelectDifficulty(string value)
    {
        foreach (var item in DifficultyComboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                DifficultyComboBox.SelectedItem = item;
                return;
            }
        }
        DifficultyComboBox.SelectedIndex = 2;
    }
}
