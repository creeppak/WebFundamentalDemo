namespace Worker;

public record WorkerOptions
{
    public static readonly IReadOnlyList<string> AllJobs = ["Prices", "Fundamentals", "News", "Analysis"];

    public IReadOnlyList<string> Jobs { get; init; } = AllJobs;
}
