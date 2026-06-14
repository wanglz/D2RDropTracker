namespace D2RDropTracker.Models;

public sealed class RunFilter
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string Character { get; init; } = "";
    public string Area { get; init; } = "";
    public string Difficulty { get; init; } = "";
    public string Tags { get; init; } = "";
}
