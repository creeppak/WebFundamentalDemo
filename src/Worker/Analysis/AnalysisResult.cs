namespace Worker.Analysis;

public record AnalysisResult(
    string Analysis,
    string Summary,
    int InputTokens,
    int OutputTokens);