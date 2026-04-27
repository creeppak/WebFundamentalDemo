namespace Shared.Stocks;

public record PricePointDto(DateOnly Date, decimal Close, long Volume);