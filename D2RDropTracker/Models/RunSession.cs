namespace D2RDropTracker.Models;

public sealed class RunSession
{
    public long Id { get; init; }
    public string Character { get; init; } = "";
    public string Area { get; init; } = "";
    public string Difficulty { get; init; } = "";
    public DateTime StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }
    public int? DurationSeconds { get; init; }
    public bool IsCompleted { get; init; }
    public int TimerElapsedSeconds { get; init; }
    public DateTime? TimerResumedAt { get; init; }
    public bool IsTimerRunning { get; init; }
    public int PlayerCount { get; init; } = 1;
    public int MagicFind { get; init; }
    public string Tags { get; init; } = "";
    public string Notes { get; init; } = "";
}
