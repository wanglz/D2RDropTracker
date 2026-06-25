namespace D2RDropTracker.Models;

public sealed class AppSettings
{
    public string CompleteRunKey { get; set; } = "F8";
    public string CompleteRunModifiers { get; set; } = "无";
    public string AddDropKey { get; set; } = "F9";
    public string AddDropModifiers { get; set; } = "无";
    public string ToggleOverlayKey { get; set; } = "F10";
    public string ToggleOverlayModifiers { get; set; } = "无";
    public string ScreenshotKey { get; set; } = "F11";
    public string ScreenshotModifiers { get; set; } = "无";
    public bool OverlayLocked { get; set; }
    public double OverlayOpacity { get; set; } = 0.98;
    public double OverlayScale { get; set; } = 1.0;
    public int OverlayDropCount { get; set; } = 5;
    public int BackupRetentionDays { get; set; } = 30;
    public int CurrentRunCount { get; set; }
}
