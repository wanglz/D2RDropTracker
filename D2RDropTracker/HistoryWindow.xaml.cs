using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using D2RDropTracker.Data;
using D2RDropTracker.Models;
using Microsoft.Win32;
using WpfMessageBox = System.Windows.MessageBox;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace D2RDropTracker;

public partial class HistoryWindow : Window
{
    private readonly DatabaseService _database;
    private readonly ObservableCollection<RunHistoryItem> _runs = [];
    private readonly ObservableCollection<DropStatistic> _dropStats = [];

    public event EventHandler? DataChanged;

    public HistoryWindow(DatabaseService database)
    {
        InitializeComponent();
        _database = database;
        HistoryGrid.ItemsSource = _runs;
        FilteredDropStatsGrid.ItemsSource = _dropStats;
        Loaded += (_, _) => LoadHistory();
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e) => LoadHistory();

    private void ClearFilterButton_Click(object sender, RoutedEventArgs e)
    {
        StartDatePicker.SelectedDate = null;
        EndDatePicker.SelectedDate = null;
        CharacterFilterTextBox.Clear();
        AreaFilterTextBox.Clear();
        DifficultyFilterTextBox.Clear();
        TagsFilterTextBox.Clear();
        LoadHistory();
    }

    private void DeleteRunButton_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryGrid.SelectedItem is not RunHistoryItem selected)
        {
            WpfMessageBox.Show("请先选择要删除的场次。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (WpfMessageBox.Show(
                $"确定删除第 {selected.RunNumber} 场吗？该场关联的 {selected.DropCount} 条掉落也会删除。",
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        if (_database.DeleteCompletedRun(selected.Id))
        {
            LoadHistory();
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void EditRunButton_Click(object sender, RoutedEventArgs e) => EditSelectedRun();

    private void HistoryGrid_MouseDoubleClick(
        object sender, System.Windows.Input.MouseButtonEventArgs e) => EditSelectedRun();

    private void EditSelectedRun()
    {
        if (HistoryGrid.SelectedItem is not RunHistoryItem selected)
        {
            return;
        }
        var editor = new RunEditWindow(selected) { Owner = this };
        if (editor.ShowDialog() != true)
        {
            return;
        }
        _database.UpdateCompletedRun(
            selected.Id,
            editor.CharacterValue,
            editor.AreaValue,
            editor.DifficultyValue,
            editor.StartedAtValue,
            editor.EndedAtValue,
            editor.PlayerCountValue,
            editor.MagicFindValue,
            editor.TagsValue,
            editor.NotesValue);
        LoadHistory();
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WpfSaveFileDialog
        {
            Title = "导出场次历史",
            Filter = "CSV 文件 (*.csv)|*.csv",
            FileName = $"D2R场次历史_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var csv = new StringBuilder();
        csv.AppendLine("场次,角色,区域,难度,开始时间,结束时间,有效耗时秒,掉落数");
        foreach (var run in _runs)
        {
            csv.AppendLine(string.Join(",",
                run.RunNumber, Csv(run.Character), Csv(run.Area), Csv(run.Difficulty),
                Csv(run.StartedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                Csv(run.EndedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                run.DurationSeconds, run.DropCount));
        }
        File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
    }

    private void LoadHistory()
    {
        var filter = new RunFilter
        {
            StartDate = StartDatePicker.SelectedDate,
            EndDate = EndDatePicker.SelectedDate,
            Character = CharacterFilterTextBox.Text,
            Area = AreaFilterTextBox.Text,
            Difficulty = DifficultyFilterTextBox.Text,
            Tags = TagsFilterTextBox.Text
        };
        var values = _database.GetRunHistory(filter);
        var dropStatistics = _database.GetFilteredDropStatistics(filter, values.Count);
        var filteredDrops = _database.GetFilteredDrops(filter);
        _runs.Clear();
        foreach (var value in values)
        {
            _runs.Add(value);
        }
        _dropStats.Clear();
        foreach (var statistic in dropStatistics)
        {
            _dropStats.Add(statistic);
        }

        FilteredRunsTextBlock.Text = values.Count.ToString();
        FilteredDropsTextBlock.Text = filteredDrops.Count.ToString();
        FilteredAverageTextBlock.Text = values.Count == 0
            ? "--:--" : FormatSeconds(values.Average(run => run.DurationSeconds));
        FilteredFastestTextBlock.Text = values.Count == 0
            ? "--:--" : FormatSeconds(values.Min(run => run.DurationSeconds));
    }

    private static string FormatSeconds(double seconds)
    {
        var duration = TimeSpan.FromSeconds(seconds);
        return duration.TotalHours >= 1
            ? duration.ToString(@"hh\:mm\:ss")
            : duration.ToString(@"mm\:ss");
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private RunFilter CurrentFilter() => new()
    {
        StartDate = StartDatePicker.SelectedDate,
        EndDate = EndDatePicker.SelectedDate,
        Character = CharacterFilterTextBox.Text,
        Area = AreaFilterTextBox.Text,
        Difficulty = DifficultyFilterTextBox.Text,
        Tags = TagsFilterTextBox.Text
    };

    private void ChartsButton_Click(object sender, RoutedEventArgs e)
    {
        new ChartsWindow(_database.GetChartData(CurrentFilter())) { Owner = this }.ShowDialog();
    }
}
