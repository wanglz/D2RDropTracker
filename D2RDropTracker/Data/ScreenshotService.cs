using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Forms = System.Windows.Forms;

namespace D2RDropTracker.Data;

public sealed class ScreenshotService
{
    private readonly string _directory = Path.Combine(
        AppContext.BaseDirectory,
        "统计数据",
        "截图");

    public string CapturePrimaryScreen()
    {
        Directory.CreateDirectory(_directory);
        var bounds = Forms.Screen.PrimaryScreen?.Bounds
            ?? throw new InvalidOperationException("无法读取主显示器尺寸。");
        var path = Path.Combine(_directory, $"drop-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }

    public string GetDirectory() => _directory;
}
