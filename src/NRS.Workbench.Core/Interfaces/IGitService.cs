using NRS.Workbench.Core.Models;

namespace NRS.Workbench.Core.Interfaces;

public interface IGitService
{
    Task<string> GetVersionAsync();
    Task<GitRepositoryInfo> InspectAsync(string repositoryPath);
    Task<IReadOnlyList<string>> FindRepositoriesAsync(string rootPath, int maxDepth = 3);
    Task<IReadOnlyList<GitFileChange>> GetChangesAsync(GitRepositoryInfo repository);
    Task<string> GetDiffAsync(GitRepositoryInfo repository, GitFileChange change);
    Task CommitAsync(GitRepositoryInfo repository, string message, IReadOnlyList<GitFileChange> selectedChanges);
    Task FetchAsync(GitRepositoryInfo repository);
    Task PullFastForwardAsync(GitRepositoryInfo repository);
    Task PushAsync(GitRepositoryInfo repository);
}
