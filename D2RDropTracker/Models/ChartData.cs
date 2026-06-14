namespace D2RDropTracker.Models;

public sealed class ChartData
{
    public List<ChartPoint> DailyRuns { get; init; } = [];
    public List<ChartPoint> AreaAverageSeconds { get; init; } = [];
    public List<ChartPoint> CategoryDrops { get; init; } = [];
}

public sealed class ChartPoint
{
    public string Label { get; init; } = "";
    public double Value { get; init; }
}
