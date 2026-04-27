using Infrastructure.Domain;
using Riok.Mapperly.Abstractions;
using Shared.Stocks;

namespace Api.Mappers;

[Mapper]
public partial class StockMapper
{
    [MapperIgnoreSource(nameof(Fundamental.Id))]
    [MapperIgnoreSource(nameof(Fundamental.Ticker))]
    public partial FundamentalsDto ToFundamentalsDto(Fundamental fundamental);

    [MapperIgnoreSource(nameof(StockAnalysis.Id))]
    [MapperIgnoreSource(nameof(StockAnalysis.Ticker))]
    [MapperIgnoreSource(nameof(StockAnalysis.InputTokens))]
    [MapperIgnoreSource(nameof(StockAnalysis.OutputTokens))]
    public partial AnalysisDto ToAnalysisDto(StockAnalysis analysis);

    [MapperIgnoreSource(nameof(NewsArticle.Id))]
    [MapperIgnoreSource(nameof(NewsArticle.Ticker))]
    public partial NewsArticleDto ToNewsArticleDto(NewsArticle article);

    [MapperIgnoreSource(nameof(Price.Id))]
    [MapperIgnoreSource(nameof(Price.Ticker))]
    [MapperIgnoreSource(nameof(Price.Open))]
    [MapperIgnoreSource(nameof(Price.High))]
    [MapperIgnoreSource(nameof(Price.Low))]
    public partial PricePointDto ToPricePointDto(Price price);
}
