namespace D2RDropTracker.Models;

public sealed class RunHistoryItem
{
    public long Id { get; init; }
    public int RunNumber { get; init; }
    public string Character { get; init; } = "";
    public string Area { get; init; } = "";
    public string Difficulty { get; init; } = "";
    public DateTime StartedAt { get; init; }
    public DateTime EndedAt { get; init; }
    public int DurationSeconds { get; init; }
    public int DropCount { get; init; }
    public int PlayerCount { get; init; } = 1;
    public int MagicFind { get; init; }
    public string Tags { get; init; } = "";
    public string Notes { get; init; } = "";
    public string StartedAtDisplay => StartedAt.ToString("yyyy-MM-dd HH:mm:ss");
    public string EndedAtDisplay => EndedAt.ToString("yyyy-MM-dd HH:mm:ss");
    public string DurationDisplay => TimeSpan.FromSeconds(DurationSeconds).ToString(
        DurationSeconds >= 3600 ? @"hh\:mm\:ss" : @"mm\:ss");
}
