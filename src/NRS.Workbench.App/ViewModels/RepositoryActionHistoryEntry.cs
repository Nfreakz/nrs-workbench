namespace NRS.Workbench.App.ViewModels;

public sealed record RepositoryActionHistoryEntry(
    DateTimeOffset Timestamp,
    string RepositoryName,
    string Action,
    bool Success,
    string Detail)
{
    public string TimeText => Timestamp.LocalDateTime.ToString("HH:mm:ss");
    public string ResultText => Success ? "OK" : "ERROR";
}
