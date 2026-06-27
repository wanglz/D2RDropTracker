namespace D2RDropTracker.Models;

public sealed class DropRecord
{
    public long Id { get; init; }
    public long RunId { get; init; }
    public string ItemName { get; init; } = "";
    public string Category { get; init; } = "";
    public string Quality { get; init; } = "";
    public string ScreenshotPath { get; init; } = "";
    public string TradeType { get; init; } = "";
    public string TradeRunes { get; init; } = "";
    public string TradeMoney { get; init; } = "";
    public DateTime DroppedAt { get; init; }
    public int RunNumber { get; init; }
    public string Character { get; init; } = "";
    public string Area { get; init; } = "";
    public string Difficulty { get; init; } = "";
    public DateTime RunStartedAt { get; init; }
    public DateTime? RunEndedAt { get; init; }
    public string ScreenshotState => string.IsNullOrWhiteSpace(ScreenshotPath) ? "" : "有";
    public string TradeDisplay
    {
        get
        {
            if (string.Equals(TradeType, "换符文", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(TradeRunes) ? "换符文" : $"符文：{TradeRunes}";
            }

            if (string.Equals(TradeType, "卖钱", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(TradeMoney) ? "卖钱" : $"卖：{TradeMoney}";
            }

            return TradeType;
        }
    }
    public string DroppedAtDisplay => DroppedAt.ToString("MM-dd HH:mm");
}
