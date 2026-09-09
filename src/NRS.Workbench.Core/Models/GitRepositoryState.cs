namespace NRS.Workbench.Core.Models;

public enum GitRepositoryState
{
    Clean,
    Changes,
    Ahead,
    Behind,
    Diverged,
    Conflict,
    NoRemote,
    Error
}
