namespace Infrastructure.Domain;

public class JobRun
{
    public int Id { get; set; }
    public string JobName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public JobRunStatus Status { get; set; }
    public int TickersSucceeded { get; set; }
    public int TickersFailed { get; set; }
    public string? ErrorMessage { get; set; }
    public long TotalInputTokens { get; set; }
    public long TotalOutputTokens { get; set; }
}