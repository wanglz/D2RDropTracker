namespace D2RDropTracker.Models;

public sealed class DropStatistic
{
    public string ItemName { get; init; } = "";
    public string Category { get; init; } = "";
    public int Count { get; init; }
    public double PerHundredRuns { get; init; }
}
