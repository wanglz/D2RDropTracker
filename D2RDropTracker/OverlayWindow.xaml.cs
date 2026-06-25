using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;

namespace D2RDropTracker;

public partial class OverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const double BaseWidth = 230;
    private const double BaseHeight = 320;
    private const double MinimumWidth = 200;
    private const double MinimumHeight = 260;

    private readonly ObservableCollection<string> _recentDrops = [];
    private readonly string _positionFile;
    private bool _positionLoaded;
    private bool _isLocked;
    private bool _hasCustomSize;
    private int _visibleDropCount = 5;

    public OverlayWindow()
    {
        InitializeComponent();
        _positionFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "D2RDropTracker",
            "overlay-position.txt");
        RecentDropsItems.ItemsSource = _recentDrops;
        SourceInitialized += OverlayWindow_SourceInitialized;
        LocationChanged += (_, _) => SavePosition();
    }

    public void UpdateSnapshot(
        string area,
        int currentRuns,
        int totalRuns,
        int totalDrops,
        TimeSpan elapsed,
        bool isRunning,
        IEnumerable<string> recentDrops)
    {
        AreaTextBlock.Text = string.IsNullOrWhiteSpace(area) ? "其他区域" : area;
        CurrentRunsTextBlock.Text = currentRuns.ToString();
        TotalRunsTextBlock.Text = totalRuns.ToString();
        DropCountTextBlock.Text = $"共 {totalDrops} 件";
        TimerStatusTextBlock.Text = isRunning ? "本轮时间 · 进行中" : "本轮时间 · 已暂停";
        TimerStatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(
                isRunning ? "#9EABB8" : "#FFB45E"));
        ElapsedTextBlock.Text = elapsed.TotalHours >= 1
            ? elapsed.ToString(@"hh\:mm\:ss")
            : elapsed.ToString(@"mm\:ss");

        _recentDrops.Clear();
        foreach (var item in recentDrops.Take(_visibleDropCount))
        {
            _recentDrops.Add(item);
        }

        if (_recentDrops.Count == 0)
        {
            _recentDrops.Add("暂无记录");
        }
    }

    public void ApplyAppearance(
        double opacity, double scale, int dropCount, string hotkeyText)
    {
        OverlayBorder.Opacity = Math.Clamp(opacity, 0.35, 1.0);
        var safeScale = Math.Clamp(scale, 0.75, 1.5);
        if (!_hasCustomSize)
        {
            Width = BaseWidth * safeScale;
            Height = BaseHeight * safeScale;
        }
        OverlayContent.LayoutTransform =
            new System.Windows.Media.ScaleTransform(safeScale, safeScale);
        _visibleDropCount = Math.Clamp(dropCount, 1, 10);
        HotkeyTextBlock.Text = hotkeyText;
    }

    public void SetLocked(bool isLocked)
    {
        _isLocked = isLocked;
        LockStateTextBlock.Text = isLocked ? "已锁定" : "可拖动";
        LockStateTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(
                isLocked ? "#7DDA8A" : "#9EABB8"));
        OverlayBorder.Cursor = isLocked
            ? System.Windows.Input.Cursors.Arrow
            : System.Windows.Input.Cursors.SizeAll;
        ResizeThumb.Visibility = isLocked ? Visibility.Collapsed : Visibility.Visible;

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        if (isLocked)
        {
            style |= WsExTransparent | WsExNoActivate | WsExToolWindow;
        }
        else
        {
            style &= ~WsExTransparent;
            style &= ~WsExNoActivate;
            style |= WsExToolWindow;
        }

        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style));
    }

    public void FollowGameWindow()
    {
        var workArea = SystemParameters.WorkArea;
        var overlayWidth = ActualWidth > 0 ? ActualWidth : Width;
        var overlayHeight = ActualHeight > 0 ? ActualHeight : Height;
        var gameWindow = FindGameWindow();

        if (gameWindow == IntPtr.Zero || !GetWindowRect(gameWindow, out var rect))
        {
            Left = workArea.Right - overlayWidth - 12;
            Top = workArea.Top + 12;
            return;
        }

        var dpi = GetDpiForWindow(gameWindow);
        var scale = dpi == 0 ? 1.0 : 96.0 / dpi;
        var gameLeft = rect.Left * scale;
        var gameTop = rect.Top * scale;
        var gameRight = rect.Right * scale;

        if (workArea.Right - gameRight >= overlayWidth + 16)
        {
            Left = gameRight + 8;
        }
        else if (gameLeft - workArea.Left >= overlayWidth + 16)
        {
            Left = gameLeft - overlayWidth - 8;
        }
        else
        {
            Left = workArea.Right - overlayWidth - 12;
        }

        Top = Math.Clamp(gameTop + 12, workArea.Top + 8, workArea.Bottom - overlayHeight - 8);
    }

    private void OverlayWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        style |= WsExToolWindow;
        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style));
        _positionLoaded = TryRestorePosition();
        if (!_positionLoaded)
        {
            FollowGameWindow();
        }
    }

    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isLocked || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
        SavePosition();
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_isLocked)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;
        Width = Math.Clamp(Width + e.HorizontalChange, MinimumWidth, workArea.Width);
        Height = Math.Clamp(Height + e.VerticalChange, MinimumHeight, workArea.Height);
        _hasCustomSize = true;
        SavePosition();
    }

    private bool TryRestorePosition()
    {
        if (!File.Exists(_positionFile))
        {
            return false;
        }

        var parts = File.ReadAllText(_positionFile).Split('|');
        if ((parts.Length != 2 && parts.Length != 4) ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var left) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var top))
        {
            return false;
        }

        var workArea = SystemParameters.WorkArea;
        if (parts.Length == 4 &&
            double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var width) &&
            double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
        {
            Width = Math.Clamp(width, MinimumWidth, workArea.Width);
            Height = Math.Clamp(height, MinimumHeight, workArea.Height);
            _hasCustomSize = true;
        }

        Left = Math.Clamp(left, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width));
        Top = Math.Clamp(top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height));
        return true;
    }

    private void SavePosition()
    {
        if (!_positionLoaded && !IsLoaded)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_positionFile)!);
        File.WriteAllText(_positionFile,
            $"{Left.ToString(CultureInfo.InvariantCulture)}|" +
            $"{Top.ToString(CultureInfo.InvariantCulture)}|" +
            $"{Width.ToString(CultureInfo.InvariantCulture)}|" +
            $"{Height.ToString(CultureInfo.InvariantCulture)}");
        _positionLoaded = true;
    }

    private static IntPtr FindGameWindow()
    {
        var result = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window))
            {
                return true;
            }

            var titleLength = GetWindowTextLength(window);
            if (titleLength == 0)
            {
                return true;
            }

            var title = new StringBuilder(titleLength + 1);
            GetWindowText(window, title, title.Capacity);
            if (title.ToString().Contains("Diablo II: Resurrected", StringComparison.OrdinalIgnoreCase))
            {
                result = window;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return result;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr newStyle);
}
