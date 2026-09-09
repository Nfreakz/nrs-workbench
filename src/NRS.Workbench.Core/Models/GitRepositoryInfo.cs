namespace NRS.Workbench.Core.Models;

public sealed class GitRepositoryInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string RemoteUrl { get; set; } = string.Empty;
    public string GitHubUrl { get; set; } = string.Empty;
    public string Upstream { get; set; } = string.Empty;
    public int Ahead { get; set; }
    public int Behind { get; set; }
    public int ModifiedCount { get; set; }
    public int AddedCount { get; set; }
    public int DeletedCount { get; set; }
    public int UntrackedCount { get; set; }
    public int ConflictCount { get; set; }
    public string LastCommitSha { get; set; } = string.Empty;
    public string LastCommitSubject { get; set; } = string.Empty;
    public DateTimeOffset? LastCommitDate { get; set; }
    public GitRepositoryState State { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public int ChangeCount => ModifiedCount + AddedCount + DeletedCount + UntrackedCount + ConflictCount;
    public bool IsDirty => ChangeCount > 0;
    public bool HasRemote => !string.IsNullOrWhiteSpace(RemoteUrl);
    public bool HasUpstream => !string.IsNullOrWhiteSpace(Upstream);

    public string StateLabel => State switch
    {
        GitRepositoryState.Clean => "CLEAN",
        GitRepositoryState.Changes => "CAMBIOS",
        GitRepositoryState.Ahead => "AHEAD",
        GitRepositoryState.Behind => "BEHIND",
        GitRepositoryState.Diverged => "DIVERGED",
        GitRepositoryState.Conflict => "CONFLICTO",
        GitRepositoryState.NoRemote => "SIN REMOTE",
        _ => "ERROR"
    };

    public string ChangeSummary => ChangeCount == 0
        ? "Sin cambios"
        : $"{ChangeCount} cambio{(ChangeCount == 1 ? string.Empty : "s")}";

    public string AheadDisplay => Ahead > 0 ? $"↑ {Ahead}" : "—";
    public string BehindDisplay => Behind > 0 ? $"↓ {Behind}" : "—";
    public string LastCommitDisplay => string.IsNullOrWhiteSpace(LastCommitSubject)
        ? "—"
        : $"{LastCommitSha} · {LastCommitSubject}";
    public string LastCommitWhenDisplay => LastCommitDate is null ? "—" : LastCommitDate.Value.LocalDateTime.ToString("dd/MM/yyyy HH:mm");

    public bool CanPull => HasUpstream && Behind > 0 && Ahead == 0 && !IsDirty && ConflictCount == 0;
    public bool CanPush => HasUpstream && Ahead > 0 && Behind == 0 && ConflictCount == 0;
    public bool CanFetch => HasRemote && State != GitRepositoryState.Error;
    public bool CanCommit => IsDirty && ConflictCount == 0 && State != GitRepositoryState.Error;
}
