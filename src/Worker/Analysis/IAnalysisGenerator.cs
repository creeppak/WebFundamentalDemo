namespace Worker.Analysis;

public interface IAnalysisGenerator
{
    // Returns null on any failure — caller must keep the previous analysis.
    Task<AnalysisResult?> GenerateAsync(AnalysisInput input, CancellationToken ct);
}