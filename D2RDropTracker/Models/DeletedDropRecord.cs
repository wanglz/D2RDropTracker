namespace D2RDropTracker.Models;

public sealed class DeletedDropRecord
{
    public long Id { get; init; }
    public long OriginalDropId { get; init; }
    public long RunId { get; init; }
    public string ItemName { get; init; } = "";
    public string Category { get; init; } = "";
    public string Quality { get; init; } = "";
    public DateTime DroppedAt { get; init; }
    public DateTime DeletedAt { get; init; }
    public int RunNumber { get; init; }
    public string DeletedAtDisplay => DeletedAt.ToString("yyyy-MM-dd HH:mm:ss");
}
