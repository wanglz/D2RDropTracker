using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace D2RDropTracker;

public partial class ScreenshotPreviewWindow : Window
{
    public ScreenshotPreviewWindow(string imagePath)
    {
        InitializeComponent();
        Title = $"掉落截图 - {Path.GetFileName(imagePath)}";
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(imagePath, UriKind.Absolute);
        image.EndInit();
        PreviewImage.Source = image;
    }
}
