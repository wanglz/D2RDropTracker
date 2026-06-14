namespace D2RDropTracker.Models;

public sealed class DropRecord
{
    public long Id { get; init; }
    public long RunId { get; init; }
    public string ItemName { get; init; } = "";
    public string Category { get; init; } = "";
    public string Quality { get; init; } = "";
    public DateTime DroppedAt { get; init; }
    public int RunNumber { get; init; }
    public string Character { get; init; } = "";
    public string Area { get; init; } = "";
    public string Difficulty { get; init; } = "";
    public DateTime RunStartedAt { get; init; }
    public DateTime? RunEndedAt { get; init; }
    public string DroppedAtDisplay => DroppedAt.ToString("MM-dd HH:mm");
}
