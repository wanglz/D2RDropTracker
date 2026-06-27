using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace D2RDropTracker;

public partial class RegionSelectionWindow : Window
{
    private readonly Rectangle _screenBounds;
    private System.Windows.Point? _startPoint;

    public Rectangle? SelectedRegion { get; private set; }

    public RegionSelectionWindow(string? backgroundImagePath = null)
    {
        InitializeComponent();
        _screenBounds = Forms.Screen.PrimaryScreen?.Bounds
            ?? throw new InvalidOperationException("无法读取主显示器尺寸。");
        Left = SystemParameters.PrimaryScreenWidth == 0 ? 0 : 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
        if (!string.IsNullOrWhiteSpace(backgroundImagePath))
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(backgroundImagePath, UriKind.Absolute);
            image.EndInit();
            SelectionCanvas.Background = new ImageBrush(image)
            {
                Stretch = Stretch.Fill
            };
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(SelectionCanvas);
        SelectionRectangle.Visibility = Visibility.Visible;
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;
        CaptureMouse();
    }

    private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_startPoint is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        UpdateSelection(e.GetPosition(SelectionCanvas));
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_startPoint is null)
        {
            return;
        }

        UpdateSelection(e.GetPosition(SelectionCanvas));
        ReleaseMouseCapture();

        var dpi = VisualTreeHelper.GetDpi(this);
        var left = Canvas.GetLeft(SelectionRectangle);
        var top = Canvas.GetTop(SelectionRectangle);
        var width = SelectionRectangle.Width;
        var height = SelectionRectangle.Height;
        if (width < 8 || height < 8)
        {
            DialogResult = false;
            return;
        }

        SelectedRegion = new Rectangle(
            _screenBounds.Left + (int)Math.Round(left * dpi.DpiScaleX),
            _screenBounds.Top + (int)Math.Round(top * dpi.DpiScaleY),
            (int)Math.Round(width * dpi.DpiScaleX),
            (int)Math.Round(height * dpi.DpiScaleY));
        DialogResult = true;
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
    }

    private void UpdateSelection(System.Windows.Point current)
    {
        var start = _startPoint!.Value;
        var left = Math.Min(start.X, current.X);
        var top = Math.Min(start.Y, current.Y);
        var width = Math.Abs(current.X - start.X);
        var height = Math.Abs(current.Y - start.Y);

        Canvas.SetLeft(SelectionRectangle, left);
        Canvas.SetTop(SelectionRectangle, top);
        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;
    }
}
