using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Forms = System.Windows.Forms;

namespace D2RDropTracker.Data;

public sealed class ScreenshotService
{
    private readonly string _directory = Path.Combine(
        AppDataPaths.GetPath(),
        "截图");

    public string CapturePrimaryScreen() => CaptureRegion(null);

    public string CaptureRegion(Rectangle? region)
    {
        Directory.CreateDirectory(_directory);
        var bounds = Forms.Screen.PrimaryScreen?.Bounds
            ?? throw new InvalidOperationException("无法读取主显示器尺寸。");
        var path = Path.Combine(_directory, $"drop-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        var captureBounds = region is null
            ? bounds
            : Rectangle.Intersect(bounds, region.Value);
        if (captureBounds.Width <= 0 || captureBounds.Height <= 0)
        {
            throw new InvalidOperationException("截图区域无效。");
        }

        using var bitmap = new Bitmap(captureBounds.Width, captureBounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(captureBounds.Location, Point.Empty, captureBounds.Size);
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }

    public string CropCapturedImage(string sourcePath, Rectangle screenRegion)
    {
        Directory.CreateDirectory(_directory);
        var bounds = Forms.Screen.PrimaryScreen?.Bounds
            ?? throw new InvalidOperationException("无法读取主显示器尺寸。");
        using var source = new Bitmap(sourcePath);
        var imageBounds = new Rectangle(0, 0, source.Width, source.Height);
        var relativeRegion = new Rectangle(
            screenRegion.X - bounds.X,
            screenRegion.Y - bounds.Y,
            screenRegion.Width,
            screenRegion.Height);
        var cropRegion = Rectangle.Intersect(imageBounds, relativeRegion);
        if (cropRegion.Width <= 0 || cropRegion.Height <= 0)
        {
            throw new InvalidOperationException("截图区域无效。");
        }

        var path = Path.Combine(_directory, $"drop-crop-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        using var cropped = new Bitmap(cropRegion.Width, cropRegion.Height);
        using var graphics = Graphics.FromImage(cropped);
        graphics.DrawImage(
            source,
            new Rectangle(0, 0, cropRegion.Width, cropRegion.Height),
            cropRegion,
            GraphicsUnit.Pixel);
        cropped.Save(path, ImageFormat.Png);
        return path;
    }

    public string GetDirectory() => _directory;
}
