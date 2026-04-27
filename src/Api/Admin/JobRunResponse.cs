namespace Api.Admin;

public record JobRunResponse(
    int Id,
    string JobName,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string Status,
    int TickersSucceeded,
    int TickersFailed,
    string? ErrorMessage,
    long TotalInputTokens,
    long TotalOutputTokens);