namespace Worker.MarketData;

public record PriceBar(
    string Ticker,
    DateOnly Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);
