namespace NRS.Workbench.Core.Models;

public sealed record DailyActivityPoint
{
    public DateTime Date { get; init; }
    public int TotalRuns { get; init; }
    public int SucceededRuns { get; init; }
    public int FailedRuns { get; init; }
    public int CancelledRuns { get; init; }
    public int UnknownRuns { get; init; }
    public double RelativeHeight { get; init; }

    public string DayLabel => Date.ToString("dd/MM");
    public string Tooltip => $"{Date:dd/MM/yyyy}\n{TotalRuns:N0} jobs · {SucceededRuns:N0} OK · {FailedRuns:N0} fallos · {CancelledRuns:N0} cancelados";
}
