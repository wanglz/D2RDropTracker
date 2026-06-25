using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using D2RDropTracker.Data;
using D2RDropTracker.Models;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using WpfMessageBox = System.Windows.MessageBox;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace D2RDropTracker;

public partial class MainWindow : Window
{
    private const int HotkeyCompleteRun = 9001;
    private const int HotkeyAddDrop = 9002;
    private const int HotkeyToggleOverlay = 9003;
    private const int HotkeyScreenshot = 9004;
    private const int WmHotkey = 0x0312;

    private readonly DatabaseService _database = new();
    private readonly SettingsService _settingsService = new();
    private readonly ItemCatalogService _itemCatalog = new();
    private readonly ScreenshotService _screenshotService = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly ObservableCollection<DropRecord> _recentDrops = [];
    private readonly ObservableCollection<DropStatistic> _dropStats = [];
    private RunSession _activeRun = null!;
    private OverlayWindow? _overlayWindow;
    private int _completedRuns;
    private int _currentRunCount;
    private int _totalDrops;
    private bool _isRunning = true;
    private TimeSpan _elapsedAtResume = TimeSpan.Zero;
    private DateTime? _timerResumedAt;
    private AppSettings _settings = new();
    private Forms.NotifyIcon? _trayIcon;
    private bool _isExiting;
    private DeletedDropRecord? _lastDeletedDrop;
    private IntPtr _windowHandle;

    public MainWindow()
    {
        InitializeComponent();
        RecentDropsGrid.ItemsSource = _recentDrops;
        DropStatsGrid.ItemsSource = _dropStats;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        _timer.Tick += (_, _) => RefreshTimer();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _database.Initialize();
            _database.CreateDailyBackup();
            _settings = _settingsService.Load();
            _currentRunCount = Math.Max(0, _settings.CurrentRunCount);
            _database.CleanupOldBackups(_settings.BackupRetentionDays);
            InitializeSettingsControls();
            ReloadCatalogSuggestions();
            _activeRun = _database.GetOrCreateActiveRun(
                CharacterTextBox.Text, SelectedText(AreaComboBox), SelectedText(DifficultyComboBox));
            _elapsedAtResume = TimeSpan.FromSeconds(_activeRun.TimerElapsedSeconds);
            _timerResumedAt = _activeRun.IsTimerRunning
                ? _activeRun.TimerResumedAt ?? _activeRun.StartedAt
                : null;
            _isRunning = _activeRun.IsTimerRunning;

            CharacterTextBox.Text = _activeRun.Character;
            SelectComboItem(AreaComboBox, _activeRun.Area);
            SelectComboItem(DifficultyComboBox, _activeRun.Difficulty);
            StartPauseButton.Content = _isRunning ? "暂停" : "继续";
            LoadDashboard();

            _overlayWindow = new OverlayWindow();
            _overlayWindow.Show();
            _overlayWindow.SetLocked(_settings.OverlayLocked);
            ApplyOverlayAppearance();
            UpdateOverlay();

            _windowHandle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(_windowHandle)?.AddHook(WindowMessageHook);
            var hotkeysOk = RegisterConfiguredHotkeys();
            InitializeTrayIcon();
            StatusTextBlock.Text = hotkeysOk
                ? "数据已加载，全局快捷键已启用"
                : "数据已加载；快捷键被其他程序占用，可使用界面按钮";

            _timer.Start();
        }
        catch (Exception ex)
        {
            LogService.Write("主窗口初始化失败", ex);
            WpfMessageBox.Show($"程序初始化失败：{ex.Message}", "D2R 掉落统计器",
                MessageBoxButton.OK, MessageBoxImage.Error);
            _isExiting = true;
            Close();
        }
    }

    private void CompleteRunButton_Click(object sender, RoutedEventArgs e) => CompleteCurrentRun();

    private void CompleteCurrentRun()
    {
        var now = DateTime.Now;
        var elapsed = GetCurrentElapsed();
        _database.CompleteRun(_activeRun.Id, now, elapsed, CharacterTextBox.Text.Trim(),
            SelectedText(AreaComboBox), SelectedText(DifficultyComboBox),
            ParseInt(SelectedText(PlayerCountComboBox), 1),
            ParseInt(MagicFindTextBox.Text, 0),
            TagsTextBox.Text,
            NotesTextBox.Text);

        _activeRun = _database.CreateRun(CharacterTextBox.Text.Trim(),
            SelectedText(AreaComboBox), SelectedText(DifficultyComboBox), now);
        _elapsedAtResume = TimeSpan.Zero;
        _timerResumedAt = now;
        _isRunning = true;
        _currentRunCount++;
        _settings.CurrentRunCount = _currentRunCount;
        _settingsService.Save(_settings);
        NotesTextBox.Clear();
        StartPauseButton.Content = "暂停";
        StatusTextBlock.Text =
            $"本次已刷 {_currentRunCount} 轮；历史总完成 {_database.GetCompletedRunCount()} 场，用时 {FormatDuration(elapsed)}";
        LoadDashboard();
        RefreshTimer();
    }

    private void StartPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            _elapsedAtResume = GetCurrentElapsed();
            _timerResumedAt = null;
            _isRunning = false;
            StartPauseButton.Content = "继续";
            _database.UpdateTimerState(
                _activeRun.Id, (int)_elapsedAtResume.TotalSeconds, null, false);
            StatusTextBlock.Text = $"计时已暂停：{FormatDuration(_elapsedAtResume)}";
        }
        else
        {
            _timerResumedAt = DateTime.Now;
            _isRunning = true;
            StartPauseButton.Content = "暂停";
            _database.UpdateTimerState(
                _activeRun.Id, (int)_elapsedAtResume.TotalSeconds, _timerResumedAt, true);
            StatusTextBlock.Text = "计时已继续";
        }

        RefreshTimer();
    }

    private void ResetRunButton_Click(object sender, RoutedEventArgs e)
    {
        var now = DateTime.Now;
        _elapsedAtResume = TimeSpan.Zero;
        _timerResumedAt = _isRunning ? now : null;
        _activeRun = new RunSession
        {
            Id = _activeRun.Id,
            Character = _activeRun.Character,
            Area = _activeRun.Area,
            Difficulty = _activeRun.Difficulty,
            StartedAt = now,
            TimerElapsedSeconds = 0,
            TimerResumedAt = _timerResumedAt,
            IsTimerRunning = _isRunning
        };
        _database.ResetRunStart(_activeRun.Id, now, _isRunning);
        StatusTextBlock.Text = _isRunning ? "本轮计时已重置并重新开始" : "本轮计时已重置，当前仍为暂停状态";
        RefreshTimer();
    }

    private void ResetCurrentRunsButton_Click(object sender, RoutedEventArgs e)
    {
        _currentRunCount = 0;
        _settings.CurrentRunCount = 0;
        _settingsService.Save(_settings);
        CurrentRunsTextBlock.Text = "0";
        StatusTextBlock.Text = "当前轮数已重置，历史总完成次数不受影响";
        UpdateOverlay();
    }

    private void OverlayLockCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings is null)
        {
            return;
        }

        _settings.OverlayLocked = OverlayLockCheckBox.IsChecked == true;
        _overlayWindow?.SetLocked(_settings.OverlayLocked);
        _settingsService.Save(_settings);
        StatusTextBlock.Text = _settings.OverlayLocked
            ? "小窗已锁定，鼠标操作会穿透"
            : "小窗已解锁，可以拖动";
    }

    private void ApplyHotkeysButton_Click(object sender, RoutedEventArgs e)
    {
        var completeKey = CompleteHotkeyComboBox.SelectedItem?.ToString() ?? "F8";
        var dropKey = DropHotkeyComboBox.SelectedItem?.ToString() ?? "F9";
        var completeShortcut = FormatShortcut(_settings.CompleteRunModifiers, completeKey);
        var dropShortcut = FormatShortcut(_settings.AddDropModifiers, dropKey);
        var overlayShortcut = FormatShortcut(
            _settings.ToggleOverlayModifiers, _settings.ToggleOverlayKey);
        var screenshotShortcut = FormatShortcut(
            _settings.ScreenshotModifiers, _settings.ScreenshotKey);
        if (new[] { completeShortcut, dropShortcut, overlayShortcut, screenshotShortcut }
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4)
        {
            WpfMessageBox.Show("四个功能不能使用相同的组合快捷键。", "快捷键冲突",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.CompleteRunKey = completeKey;
        _settings.AddDropKey = dropKey;
        _settingsService.Save(_settings);
        var success = RegisterConfiguredHotkeys();
        UpdateHotkeyLabels();
        StatusTextBlock.Text = success ? "快捷键已更新" : "快捷键注册失败，可能已被其他程序占用";
    }

    private void PreferencesButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new PreferencesWindow(_settings, _database, _itemCatalog) { Owner = this };
        if (window.ShowDialog() != true)
        {
            return;
        }

        _settings = window.ResultSettings;
        _itemCatalog.Reload();
        ReloadCatalogSuggestions();
        _settingsService.Save(_settings);
        _database.CleanupOldBackups(_settings.BackupRetentionDays);
        InitializeSettingsControls();
        ApplyOverlayAppearance();
        var success = RegisterConfiguredHotkeys();
        StatusTextBlock.Text = success
            ? "偏好设置已保存"
            : "设置已保存，但有快捷键被其他程序占用";
    }

    private void UndoLastRunButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_database.TryUndoLastCompletedRun(_activeRun.Id, out var restoredRun) || restoredRun is null)
        {
            WpfMessageBox.Show("无法撤销。请确认已有完成场次，并且当前新场还没有记录掉落。",
                "撤销失败", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _activeRun = restoredRun;
        if (_currentRunCount > 0)
        {
            _currentRunCount--;
            _settings.CurrentRunCount = _currentRunCount;
            _settingsService.Save(_settings);
        }
        CharacterTextBox.Text = _activeRun.Character;
        SelectComboItem(AreaComboBox, _activeRun.Area);
        SelectComboItem(DifficultyComboBox, _activeRun.Difficulty);
        _elapsedAtResume = TimeSpan.FromSeconds(_activeRun.TimerElapsedSeconds);
        _timerResumedAt = _activeRun.TimerResumedAt;
        _isRunning = _activeRun.IsTimerRunning;
        StartPauseButton.Content = "暂停";
        LoadDashboard();
        RefreshTimer();
        StatusTextBlock.Text = "已撤销上一场完成记录，本轮计时重新开始";
    }

    private void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        var historyWindow = new HistoryWindow(_database) { Owner = this };
        historyWindow.DataChanged += (_, _) => LoadDashboard();
        historyWindow.ShowDialog();
        LoadDashboard();
    }

    private void BackupButton_Click(object sender, RoutedEventArgs e)
    {
        var backupWindow = new BackupWindow(_database) { Owner = this };
        backupWindow.ShowDialog();
        if (backupWindow.RestoreCompleted)
        {
            _isExiting = true;
            Close();
            System.Windows.Application.Current.Shutdown();
        }
    }

    private void ScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        CaptureScreenshot();
    }

    private void CaptureScreenshot()
    {
        try
        {
            var path = _screenshotService.CapturePrimaryScreen();
            StatusTextBlock.Text = $"截图已保存，待人工识别：{path}";
        }
        catch (Exception ex)
        {
            LogService.Write("截图失败", ex);
            WpfMessageBox.Show(ex.Message, "截图失败",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddDropButton_Click(object sender, RoutedEventArgs e) => AddDrop();

    private void AddDrop()
    {
        var itemName = ItemNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(itemName))
        {
            StatusTextBlock.Text = "请输入物品名称";
            ItemNameTextBox.Focus();
            return;
        }

        _database.AddDrop(_activeRun.Id, itemName, SelectedText(CategoryComboBox),
            SelectedText(QualityComboBox), DateTime.Now);
        ItemNameTextBox.Text = "";
        ItemNameTextBox.Focus();
        StatusTextBlock.Text = $"已记录掉落：{itemName}";
        LoadDashboard();
    }

    private void DeleteDropButton_Click(object sender, RoutedEventArgs e)
    {
        if (RecentDropsGrid.SelectedItem is not DropRecord selected)
        {
            StatusTextBlock.Text = "请先选择要删除的掉落";
            return;
        }

        if (WpfMessageBox.Show($"确定删除“{selected.ItemName}”吗？", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        _database.DeleteDrop(selected.Id);
        _lastDeletedDrop = _database.GetDeletedDrops().FirstOrDefault();
        UndoDeleteDropButton.IsEnabled = true;
        StatusTextBlock.Text = $"已删除：{selected.ItemName}";
        LoadDashboard();
    }

    private void UndoDeleteDropButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lastDeletedDrop is null)
        {
            return;
        }

        if (!_database.RestoreDeletedDrop(_lastDeletedDrop.Id))
        {
            StatusTextBlock.Text = "恢复失败：原场次可能已被删除";
            return;
        }
        StatusTextBlock.Text = $"已恢复掉落：{_lastDeletedDrop.ItemName}";
        _lastDeletedDrop = null;
        UndoDeleteDropButton.IsEnabled = false;
        LoadDashboard();
    }

    private void RecycleBinButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new RecycleBinWindow(_database) { Owner = this };
        window.DataChanged += (_, _) => LoadDashboard();
        window.ShowDialog();
        _lastDeletedDrop = _database.GetDeletedDrops().FirstOrDefault();
        UndoDeleteDropButton.IsEnabled = _lastDeletedDrop is not null;
        LoadDashboard();
    }

    private void EditDropButton_Click(object sender, RoutedEventArgs e) => EditSelectedDrop();

    private void RecentDropsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) =>
        EditSelectedDrop();

    private void EditSelectedDrop()
    {
        if (RecentDropsGrid.SelectedItem is not DropRecord selected)
        {
            StatusTextBlock.Text = "请先选择要编辑的掉落";
            return;
        }

        var editor = new DropEditWindow(selected, _database.GetRunChoices()) { Owner = this };
        if (editor.ShowDialog() != true)
        {
            return;
        }

        _database.UpdateDrop(
            selected.Id,
            editor.RunIdValue,
            editor.ItemNameValue,
            editor.CategoryValue,
            editor.QualityValue);
        StatusTextBlock.Text = $"已更新掉落：{editor.ItemNameValue}";
        LoadDashboard();
    }

    private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WpfSaveFileDialog
        {
            Title = "导出掉落记录",
            Filter = "CSV 文件 (*.csv)|*.csv",
            FileName = $"D2R掉落记录_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var records = _database.GetAllDrops();
        var csv = new StringBuilder();
        csv.AppendLine("掉落时间,物品名称,分类,掉落者,场次,角色,区域,难度,场次开始时间,场次结束时间");
        foreach (var record in records)
        {
            csv.AppendLine(string.Join(",",
                Csv(record.DroppedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                Csv(record.ItemName), Csv(record.Category), Csv(record.Quality),
                record.RunNumber, Csv(record.Character), Csv(record.Area), Csv(record.Difficulty),
                Csv(record.RunStartedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                Csv(record.RunEndedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "")));
        }

        File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
        StatusTextBlock.Text = $"已导出 {records.Count} 条记录：{dialog.FileName}";
    }

    private void ItemNameTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddDrop();
            e.Handled = true;
        }
    }

    private void ItemNameTextBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var selectedName = ItemNameTextBox.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(selectedName))
        {
            return;
        }

        var item = _itemCatalog.Find(selectedName);
        if (item is null)
        {
            return;
        }
        SelectComboItem(CategoryComboBox, item.Category);
        ItemNameTextBox.Text = item.Name;
    }

    private void LoadDashboard()
    {
        var summary = _database.GetSummary();
        _completedRuns = summary.TotalRuns;
        _totalDrops = summary.TotalDrops;
        TotalRunsTextBlock.Text = summary.TotalRuns.ToString();
        CurrentRunsTextBlock.Text = _currentRunCount.ToString();
        TotalDropsTextBlock.Text = summary.TotalDrops.ToString();
        AverageTimeTextBlock.Text = summary.TotalRuns == 0
            ? "--:--"
            : FormatDuration(TimeSpan.FromSeconds(summary.AverageSeconds));

        ReplaceContents(_recentDrops, _database.GetRecentDrops(100));
        ReplaceContents(_dropStats, _database.GetDropStatistics(summary.TotalRuns));
        UpdateOverlay();
    }

    private void RefreshTimer()
    {
        TimerTextBlock.Text = FormatDuration(GetCurrentElapsed());
        CurrentRunStartTextBlock.Text = $"开始：{_activeRun.StartedAt:MM-dd HH:mm:ss}";
        UpdateOverlay();
    }

    private void UpdateOverlay()
    {
        _overlayWindow?.UpdateSnapshot(
            SelectedText(AreaComboBox),
            _currentRunCount,
            _completedRuns,
            _totalDrops,
            GetCurrentElapsed(),
            _isRunning,
            _recentDrops.Select(drop => $"{drop.RunNumber} {drop.ItemName}  [{drop.Category}]"));
    }

    private TimeSpan GetCurrentElapsed() =>
        _isRunning && _timerResumedAt is not null
            ? _elapsedAtResume + (DateTime.Now - _timerResumedAt.Value)
            : _elapsedAtResume;

    private IntPtr WindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey)
        {
            return IntPtr.Zero;
        }

        switch (wParam.ToInt32())
        {
            case HotkeyCompleteRun:
                CompleteCurrentRun();
                break;
            case HotkeyAddDrop:
                Activate();
                WindowState = WindowState.Normal;
                ItemNameTextBox.Focus();
                break;
            case HotkeyToggleOverlay:
                ToggleOverlay();
                break;
            case HotkeyScreenshot:
                CaptureScreenshot();
                break;
        }

        handled = true;
        return IntPtr.Zero;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            Hide();
            _trayIcon?.ShowBalloonTip(1500, "D2R 掉落统计器",
                "程序仍在托盘运行，快捷键和监控小窗保持可用。", Forms.ToolTipIcon.Info);
            return;
        }

        _timer.Stop();
        _overlayWindow?.Close();
        _trayIcon?.Dispose();
        if (_windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, HotkeyCompleteRun);
            UnregisterHotKey(_windowHandle, HotkeyAddDrop);
            UnregisterHotKey(_windowHandle, HotkeyToggleOverlay);
            UnregisterHotKey(_windowHandle, HotkeyScreenshot);
        }
    }

    private void InitializeSettingsControls()
    {
        var functionKeys = Enumerable.Range(1, 12).Select(number => $"F{number}").ToArray();
        CompleteHotkeyComboBox.ItemsSource = functionKeys;
        DropHotkeyComboBox.ItemsSource = functionKeys;
        CompleteHotkeyComboBox.SelectedItem = functionKeys.Contains(_settings.CompleteRunKey)
            ? _settings.CompleteRunKey : "F8";
        DropHotkeyComboBox.SelectedItem = functionKeys.Contains(_settings.AddDropKey)
            ? _settings.AddDropKey : "F9";
        OverlayLockCheckBox.IsChecked = _settings.OverlayLocked;
        UpdateHotkeyLabels();
    }

    private bool RegisterConfiguredHotkeys()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return false;
        }

        UnregisterHotKey(_windowHandle, HotkeyCompleteRun);
        UnregisterHotKey(_windowHandle, HotkeyAddDrop);
        UnregisterHotKey(_windowHandle, HotkeyToggleOverlay);
        UnregisterHotKey(_windowHandle, HotkeyScreenshot);
        var completeOk = RegisterHotKey(
            _windowHandle, HotkeyCompleteRun,
            ToModifierFlags(_settings.CompleteRunModifiers),
            ToVirtualKey(_settings.CompleteRunKey));
        var dropOk = RegisterHotKey(
            _windowHandle, HotkeyAddDrop,
            ToModifierFlags(_settings.AddDropModifiers),
            ToVirtualKey(_settings.AddDropKey));
        var overlayOk = RegisterHotKey(
            _windowHandle, HotkeyToggleOverlay,
            ToModifierFlags(_settings.ToggleOverlayModifiers),
            ToVirtualKey(_settings.ToggleOverlayKey));
        var screenshotOk = RegisterHotKey(
            _windowHandle, HotkeyScreenshot,
            ToModifierFlags(_settings.ScreenshotModifiers),
            ToVirtualKey(_settings.ScreenshotKey));
        return completeOk && dropOk && overlayOk && screenshotOk;
    }

    private void UpdateHotkeyLabels()
    {
        CompleteRunButton.Content = $"完成本轮  {FormatShortcut(_settings.CompleteRunModifiers, _settings.CompleteRunKey)}";
        HotkeyHintTextBlock.Text =
            $"{FormatShortcut(_settings.CompleteRunModifiers, _settings.CompleteRunKey)} 完成本轮  ·  " +
            $"{FormatShortcut(_settings.AddDropModifiers, _settings.AddDropKey)} 记录掉落  ·  " +
            $"{FormatShortcut(_settings.ScreenshotModifiers, _settings.ScreenshotKey)} 截图";
    }

    private void InitializeTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示主窗口", null, (_, _) => Dispatcher.Invoke(ShowMainWindow));
        menu.Items.Add("显示/隐藏监控小窗", null, (_, _) => Dispatcher.Invoke(ToggleOverlay));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Application,
            Text = "D2R 掉落统计器",
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMainWindow);
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ToggleOverlay()
    {
        if (_overlayWindow is null)
        {
            return;
        }

        if (_overlayWindow.IsVisible)
        {
            _overlayWindow.Hide();
        }
        else
        {
            _overlayWindow.Show();
            _overlayWindow.SetLocked(_settings.OverlayLocked);
            ApplyOverlayAppearance();
        }
    }

    private void ApplyOverlayAppearance()
    {
        _overlayWindow?.ApplyAppearance(
            _settings.OverlayOpacity,
            _settings.OverlayScale,
            _settings.OverlayDropCount,
            $"{FormatShortcut(_settings.CompleteRunModifiers, _settings.CompleteRunKey)} 完成  |  " +
            $"{FormatShortcut(_settings.AddDropModifiers, _settings.AddDropKey)} 掉落  |  " +
            $"{FormatShortcut(_settings.ScreenshotModifiers, _settings.ScreenshotKey)} 截图");
    }

    private void ExitApplication()
    {
        var elapsed = GetCurrentElapsed();
        _database.UpdateTimerState(_activeRun.Id, (int)elapsed.TotalSeconds, null, false);
        _isExiting = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private static uint ToVirtualKey(string keyName)
    {
        return Enum.TryParse<Key>(keyName, true, out var key)
            ? (uint)KeyInterop.VirtualKeyFromKey(key)
            : (uint)KeyInterop.VirtualKeyFromKey(Key.F8);
    }

    private static uint ToModifierFlags(string modifiers)
    {
        uint flags = 0;
        if (modifiers.Contains("Alt", StringComparison.OrdinalIgnoreCase))
        {
            flags |= 0x0001;
        }
        if (modifiers.Contains("Ctrl", StringComparison.OrdinalIgnoreCase))
        {
            flags |= 0x0002;
        }
        if (modifiers.Contains("Shift", StringComparison.OrdinalIgnoreCase))
        {
            flags |= 0x0004;
        }
        return flags;
    }

    private static string FormatShortcut(string modifiers, string key) =>
        string.Equals(modifiers, "无", StringComparison.OrdinalIgnoreCase)
            ? key
            : $"{modifiers}+{key}";

    private static string SelectedText(System.Windows.Controls.ComboBox comboBox) =>
        (comboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "";

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
    }

    private static string FormatDuration(TimeSpan duration) =>
        duration.TotalHours >= 1 ? duration.ToString(@"hh\:mm\:ss") : duration.ToString(@"mm\:ss");

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static int ParseInt(string value, int fallback) =>
        int.TryParse(value, out var result) ? result : fallback;

    private static void ReplaceContents<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private void ReloadCatalogSuggestions()
    {
        ItemNameTextBox.ItemsSource = _itemCatalog.Items
            .SelectMany(item => new[] { item.Name }.Concat(item.Aliases))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value)
            .ToArray();
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
