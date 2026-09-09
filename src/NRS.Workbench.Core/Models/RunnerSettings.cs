namespace NRS.Workbench.Core.Models;

public sealed class RunnerSettings
{
    public List<string> RunnerRoots { get; set; } = [];
    public string FolderPattern { get; set; } = "actions-runner*";
    public int RefreshIntervalSeconds { get; set; } = 5;
    public bool ConfirmStopBusy { get; set; } = true;
    public bool KeepInTray { get; set; } = true;
    public bool NotificationsEnabled { get; set; } = true;
    public bool NotifyJobStarted { get; set; } = true;
    public bool NotifyJobCompleted { get; set; } = true;
    public bool NotifyRunnerIssues { get; set; } = true;
    public bool NotifyOnlyWhenHidden { get; set; } = true;
    public List<string> RepositoryPaths { get; set; } = [];
}
