namespace Shared.Portfolio;

public record TransactionDto(
    Guid Id,
    string TransactionType,
    string? Ticker,
    decimal Price,
    decimal Quantity,
    DateTime CreatedAt);