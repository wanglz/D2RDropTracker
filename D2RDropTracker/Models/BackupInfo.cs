namespace D2RDropTracker.Models;

public sealed class BackupInfo
{
    public string FilePath { get; init; } = "";
    public string FileName => System.IO.Path.GetFileName(FilePath);
    public DateTime CreatedAt { get; init; }
    public long SizeBytes { get; init; }
    public string CreatedAtDisplay => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
    public string SizeDisplay => $"{SizeBytes / 1024.0:F1} KB";
}
