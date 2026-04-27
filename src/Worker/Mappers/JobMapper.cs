using Infrastructure.Domain;
using Riok.Mapperly.Abstractions;
using Worker.MarketData;
using NewsItem = Worker.MarketData.NewsItem;

namespace Worker.Mappers;

[Mapper]
public partial class JobMapper
{
    [MapperIgnoreTarget(nameof(Price.Id))]
    public partial Price ToPrice(PriceBar bar);

    [MapperIgnoreTarget(nameof(Price.Id))]
    public partial void UpdatePrice(PriceBar bar, Price target);

    [MapperIgnoreTarget(nameof(Fundamental.Id))]
    [MapperIgnoreTarget(nameof(Fundamental.Date))]
    public partial Fundamental ToFundamental(StockFundamentals snapshot);

    [MapperIgnoreTarget(nameof(Fundamental.Id))]
    [MapperIgnoreTarget(nameof(Fundamental.Date))]
    public partial void UpdateFundamental(StockFundamentals snapshot, Fundamental target);

    [MapperIgnoreTarget(nameof(NewsArticle.Id))]
    public partial NewsArticle ToNewsArticle(NewsItem item);
}