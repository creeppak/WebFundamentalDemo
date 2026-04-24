using Worker.MarketData;

namespace Worker.Analysis;

public record AnalysisInput(
    string Ticker,
    IReadOnlyList<PriceBar> RecentPrices,
    StockFundamentals? Fundamentals,
    IReadOnlyList<NewsItem> News,
    string? PreviousAnalysisSummary);
