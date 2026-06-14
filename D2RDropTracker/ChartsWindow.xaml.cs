using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using D2RDropTracker.Models;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace D2RDropTracker;

public partial class ChartsWindow : Window
{
    private readonly ChartData _data;

    public ChartsWindow(ChartData data)
    {
        InitializeComponent();
        _data = data;
        Loaded += (_, _) =>
        {
            DrawBars(DailyCanvas, _data.DailyRuns, "#1769AA");
            DrawBars(AreaCanvas, _data.AreaAverageSeconds, "#D08A24");
            DrawBars(CategoryCanvas, _data.CategoryDrops, "#4E9B63");
        };
    }

    private static void DrawBars(Canvas canvas, IReadOnlyList<ChartPoint> points, string color)
    {
        canvas.Children.Clear();
        if (points.Count == 0)
        {
            canvas.Children.Add(new TextBlock
            {
                Text = "暂无数据",
                Foreground = WpfBrushes.Gray,
                Margin = new Thickness(16)
            });
            return;
        }

        var width = Math.Max(600, canvas.ActualWidth);
        var height = canvas.Height;
        var max = points.Max(point => point.Value);
        var slot = width / points.Count;
        var barWidth = Math.Max(12, slot * 0.58);
        var brush = (WpfBrush)new BrushConverter().ConvertFromString(color)!;

        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var barHeight = max <= 0 ? 0 : point.Value / max * (height - 55);
            var left = index * slot + (slot - barWidth) / 2;
            var top = height - 30 - barHeight;
            var bar = new WpfRectangle
            {
                Width = barWidth,
                Height = barHeight,
                Fill = brush,
                RadiusX = 3,
                RadiusY = 3
            };
            Canvas.SetLeft(bar, left);
            Canvas.SetTop(bar, top);
            canvas.Children.Add(bar);

            var value = new TextBlock
            {
                Text = point.Value.ToString("F0"),
                FontSize = 11,
                Foreground = WpfBrushes.Black
            };
            Canvas.SetLeft(value, left);
            Canvas.SetTop(value, Math.Max(0, top - 18));
            canvas.Children.Add(value);

            var label = new TextBlock
            {
                Text = point.Label,
                FontSize = 10,
                Width = slot,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Canvas.SetLeft(label, index * slot);
            Canvas.SetTop(label, height - 25);
            canvas.Children.Add(label);
        }
    }
}
