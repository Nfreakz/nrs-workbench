using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Services;

public static class RunnerListOrganizer
{
    public static string NormalizeMode(string? mode) => mode switch
    {
        "name" or "state" or "target" or "memory" => mode,
        _ => "manual"
    };

    public static IReadOnlyList<RunnerInfo> Arrange(
        IEnumerable<RunnerInfo> runners, string? mode, IEnumerable<string>? manualOrder, string? search)
    {
        var order = (manualOrder ?? []).Select((path, index) => (path, index))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.path))
            .GroupBy(entry => entry.path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().index, StringComparer.OrdinalIgnoreCase);

        var query = search?.Trim();
        var visible = string.IsNullOrEmpty(query) ? runners : runners.Where(runner =>
            runner.Alias.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            runner.AgentName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            runner.GitHubTarget.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            runner.Labels.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            runner.FolderPath.Contains(query, StringComparison.OrdinalIgnoreCase));

        IOrderedEnumerable<RunnerInfo> sorted = NormalizeMode(mode) switch
        {
            "name" => visible.OrderBy(runner => runner.Alias, StringComparer.OrdinalIgnoreCase),
            "state" => visible.OrderBy(runner => StatePriority(runner.State))
                .ThenBy(runner => runner.Alias, StringComparer.OrdinalIgnoreCase),
            "target" => visible.OrderBy(runner => runner.GitHubTarget, StringComparer.OrdinalIgnoreCase)
                .ThenBy(runner => runner.Alias, StringComparer.OrdinalIgnoreCase),
            "memory" => visible.OrderByDescending(runner => runner.RamBytes)
                .ThenBy(runner => runner.Alias, StringComparer.OrdinalIgnoreCase),
            _ => visible.OrderBy(runner => order.TryGetValue(runner.FolderPath, out var index) ? index : int.MaxValue)
                .ThenBy(runner => runner.Alias, StringComparer.OrdinalIgnoreCase)
        };
        return sorted.ThenBy(runner => runner.FolderPath, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static IReadOnlyList<string> Move(IReadOnlyList<RunnerInfo> runners, string selectedPath, int direction)
    {
        var paths = runners.Select(runner => runner.FolderPath).ToList();
        var index = paths.FindIndex(path => string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase));
        var target = index + direction;
        if (index < 0 || target < 0 || target >= paths.Count) return paths;
        (paths[index], paths[target]) = (paths[target], paths[index]);
        return paths;
    }

    private static int StatePriority(RunnerState state) => state switch
    {
        RunnerState.Busy => 0,
        RunnerState.Error or RunnerState.Unregistered => 1,
        RunnerState.Starting or RunnerState.Stopping => 2,
        RunnerState.Stopped => 3,
        RunnerState.Ready => 4,
        _ => 5
    };
}
