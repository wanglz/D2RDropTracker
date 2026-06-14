namespace D2RDropTracker.Models;

public sealed class DashboardSummary
{
    public int TotalRuns { get; init; }
    public int TotalDrops { get; init; }
    public double AverageSeconds { get; init; }
}
