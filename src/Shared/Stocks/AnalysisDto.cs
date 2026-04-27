namespace Shared.Stocks;

public record AnalysisDto(
    DateOnly Date,
    string Analysis,
    string Summary,
    DateTime GeneratedAt);