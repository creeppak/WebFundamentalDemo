using Infrastructure.Domain;
using Riok.Mapperly.Abstractions;
using Worker.Analysis;
using Worker.MarketData;
using NewsItem = Worker.MarketData.NewsItem;

namespace Worker.Mappers;

[Mapper]
public partial class JobMapper
{
    // PriceBar ↔ Price
    [MapperIgnoreTarget(nameof(Price.Id))]
    public partial Price ToPrice(PriceBar bar);

    [MapperIgnoreTarget(nameof(Price.Id))]
    public partial void UpdatePrice(PriceBar bar, Price target);

    [MapperIgnoreSource(nameof(Price.Id))]
    public partial PriceBar ToPriceBar(Price price);

    // StockFundamentals ↔ Fundamental
    [MapperIgnoreTarget(nameof(Fundamental.Id))]
    [MapperIgnoreTarget(nameof(Fundamental.Date))]
    public partial Fundamental ToFundamental(StockFundamentals snapshot);

    [MapperIgnoreTarget(nameof(Fundamental.Id))]
    [MapperIgnoreTarget(nameof(Fundamental.Date))]
    public partial void UpdateFundamental(StockFundamentals snapshot, Fundamental target);

    [MapperIgnoreSource(nameof(Fundamental.Id))]
    [MapperIgnoreSource(nameof(Fundamental.Date))]
    public partial StockFundamentals ToStockFundamentals(Fundamental fundamental);

    // NewsItem ↔ NewsArticle
    [MapperIgnoreTarget(nameof(NewsArticle.Id))]
    public partial NewsArticle ToNewsArticle(NewsItem item);

    [MapperIgnoreSource(nameof(NewsArticle.Id))]
    public partial NewsItem ToNewsItem(NewsArticle article);

    // AnalysisResult → StockAnalysis (Ticker, Date, GeneratedAt set by caller)
    [MapperIgnoreTarget(nameof(StockAnalysis.Id))]
    [MapperIgnoreTarget(nameof(StockAnalysis.Ticker))]
    [MapperIgnoreTarget(nameof(StockAnalysis.Date))]
    [MapperIgnoreTarget(nameof(StockAnalysis.GeneratedAt))]
    public partial StockAnalysis ToStockAnalysis(AnalysisResult result);

    [MapperIgnoreTarget(nameof(StockAnalysis.Id))]
    [MapperIgnoreTarget(nameof(StockAnalysis.Ticker))]
    [MapperIgnoreTarget(nameof(StockAnalysis.Date))]
    [MapperIgnoreTarget(nameof(StockAnalysis.GeneratedAt))]
    public partial void UpdateAnalysis(AnalysisResult result, StockAnalysis target);
}