using System.Diagnostics;
using NRS.Workbench.App.Services;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.SmokeTests;

internal static class Program
{
    private static int _passed;
    private static int _failed;

    public static async Task<int> Main()
    {
        Console.WriteLine("NRS Workbench public-readiness smoke tests");
        Console.WriteLine("=====================================================");

        if (!GitAvailable())
        {
            Console.Error.WriteLine("Git is not available in PATH.");
            return 2;
        }

        var root = Path.Combine(Path.GetTempPath(), "nrs-workbench-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await RunGitAsync(root, "init", "--bare", Path.Combine(root, "remote.git"));
            var work = Path.Combine(root, "work");
            var other = Path.Combine(root, "other");
            await RunGitAsync(root, "clone", Path.Combine(root, "remote.git"), work);
            await ConfigureIdentityAsync(work, "Smoke One", "smoke1@example.invalid");

            await File.WriteAllTextAsync(Path.Combine(work, "base.txt"), "base\n");
            await RunGitAsync(work, "add", "base.txt");
            await RunGitAsync(work, "commit", "-m", "initial");
            await RunGitAsync(work, "push", "-u", "origin", "HEAD");

            var service = new GitService();
            var clean = await service.InspectAsync(work);
            Check("clean repository inspection", clean.State == GitRepositoryState.Clean, clean.StateLabel);

            await File.WriteAllTextAsync(Path.Combine(work, "test-a.txt"), "A\n");
            await File.WriteAllTextAsync(Path.Combine(work, "test-b.txt"), "B\n");
            var dirty = await service.InspectAsync(work);
            var changes = await service.GetChangesAsync(dirty);
            Check("two untracked files detected", changes.Count(x => x.IsUntracked) == 2, string.Join(", ", changes.Select(x => x.DisplayPath)));

            var beforeEmpty = await CaptureStatusAsync(work);
            var emptyBlocked = false;
            try
            {
                await service.CommitAsync(dirty, "   ", new[] { changes.Single(x => x.Path == "test-a.txt") });
            }
            catch (InvalidOperationException)
            {
                emptyBlocked = true;
            }
            var afterEmpty = await CaptureStatusAsync(work);
            Check("empty commit message blocked before index mutation", emptyBlocked && beforeEmpty == afterEmpty, afterEmpty);

            await service.CommitAsync(dirty, "test: selective commit", new[] { changes.Single(x => x.Path == "test-a.txt") });
            var afterPartial = await service.InspectAsync(work);
            var afterPartialChanges = await service.GetChangesAsync(afterPartial);
            Check("partial commit leaves unselected file", afterPartialChanges.Any(x => x.Path == "test-b.txt" && x.IsUntracked));
            var names = (await RunGitAsync(work, "show", "--name-only", "--format=", "HEAD")).StdOut
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Check("partial commit contains only selected path", names.Length == 1 && names[0] == "test-a.txt", string.Join(", ", names));

            await RunGitAsync(work, "add", "test-b.txt");
            await File.WriteAllTextAsync(Path.Combine(work, "test-c.txt"), "C\n");
            var stagedState = await service.InspectAsync(work);
            var stagedChanges = await service.GetChangesAsync(stagedState);
            var blockedOutside = false;
            try
            {
                await service.CommitAsync(stagedState, "test: must block", new[] { stagedChanges.Single(x => x.Path == "test-c.txt") });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("staged", StringComparison.OrdinalIgnoreCase))
            {
                blockedOutside = true;
            }
            Check("pre-staged file outside selection is blocked", blockedOutside);
            await RunGitAsync(work, "reset");

            var unusual = new[] { "space name.txt", "-leading-dash.txt", "áéí_日本語.txt" };
            foreach (var name in unusual) await File.WriteAllTextAsync(Path.Combine(work, name), name + "\n");
            var unusualState = await service.InspectAsync(work);
            var unusualChanges = await service.GetChangesAsync(unusualState);
            var selectedUnusual = unusualChanges.Where(x => unusual.Contains(x.Path, StringComparer.Ordinal)).ToList();
            await service.CommitAsync(unusualState, "test: unusual filenames", selectedUnusual);
            var tree = (await RunGitBytesAsync(work, "ls-tree", "-r", "--name-only", "-z", "HEAD"))
                .Split((byte)0)
                .Select(x => System.Text.Encoding.UTF8.GetString(x.Array!, x.Offset, x.Count))
                .ToList();
            Check("spaces, leading dash and Unicode paths commit safely", unusual.All(tree.Contains));

            await File.WriteAllTextAsync(Path.Combine(work, "old.txt"), "old\n");
            await RunGitAsync(work, "add", "old.txt");
            await RunGitAsync(work, "commit", "-m", "add old");
            await RunGitAsync(work, "mv", "old.txt", "new.txt");
            var renameState = await service.InspectAsync(work);
            var renameChanges = await service.GetChangesAsync(renameState);
            var rename = renameChanges.SingleOrDefault(x => x.Path == "new.txt");
            Check("rename parser preserves old and new path", rename is not null && rename.OriginalPath == "old.txt", rename?.DisplayPath ?? "missing");
            if (rename is not null)
            {
                await service.CommitAsync(renameState, "test: rename", new[] { rename });
                var renameTree = (await RunGitBytesAsync(work, "ls-tree", "-r", "--name-only", "-z", "HEAD"))
                    .Split((byte)0)
                    .Select(x => System.Text.Encoding.UTF8.GetString(x.Array!, x.Offset, x.Count))
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList();
                Check("selected staged rename commits correctly", renameTree.Contains("new.txt") && !renameTree.Contains("old.txt"));
            }

            await File.WriteAllTextAsync(Path.Combine(work, "old-modified.txt"), "base\n");
            await RunGitAsync(work, "add", "old-modified.txt");
            await RunGitAsync(work, "commit", "-m", "add old modified");
            await RunGitAsync(work, "mv", "old-modified.txt", "new-modified.txt");
            await File.AppendAllTextAsync(Path.Combine(work, "new-modified.txt"), "working-tree edit\n");
            var stagedRenameWithEditState = await service.InspectAsync(work);
            var stagedRenameWithEdit = (await service.GetChangesAsync(stagedRenameWithEditState))
                .SingleOrDefault(x => x.Path == "new-modified.txt");
            Check("staged rename with working-tree edit is detected",
                stagedRenameWithEdit is not null && stagedRenameWithEdit.IsStaged && stagedRenameWithEdit.HasUnstagedChanges,
                stagedRenameWithEdit?.StatusCode ?? "missing");
            if (stagedRenameWithEdit is not null)
            {
                await service.CommitAsync(stagedRenameWithEditState, "test: staged rename plus edit", new[] { stagedRenameWithEdit });
                var committedText = await File.ReadAllTextAsync(Path.Combine(work, "new-modified.txt"));
                var oldExistsInTree = (await RunGitAsync(work, "ls-tree", "-r", "--name-only", "HEAD")).StdOut
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Contains("old-modified.txt", StringComparer.Ordinal);
                Check("staged rename plus edit commits current content",
                    committedText.Contains("working-tree edit", StringComparison.Ordinal) && !oldExistsInTree);
            }

            await File.WriteAllTextAsync(Path.Combine(work, "delete-me.txt"), "delete\n");
            await RunGitAsync(work, "add", "delete-me.txt");
            await RunGitAsync(work, "commit", "-m", "add delete");
            File.Delete(Path.Combine(work, "delete-me.txt"));
            var deletionState = await service.InspectAsync(work);
            var deletion = (await service.GetChangesAsync(deletionState)).Single(x => x.Path == "delete-me.txt");
            await service.CommitAsync(deletionState, "test: delete", new[] { deletion });
            Check("selected deletion commits correctly", !File.Exists(Path.Combine(work, "delete-me.txt")));

            await StageAndCommitRemainingAsync(work);
            var ahead = await service.InspectAsync(work);
            Check("ahead state detected", ahead.Ahead > 0, $"ahead={ahead.Ahead}");
            await service.PushAsync(ahead);
            var pushed = await service.InspectAsync(work);
            Check("push clears ahead", pushed.Ahead == 0, $"ahead={pushed.Ahead}");

            await RunGitAsync(root, "clone", Path.Combine(root, "remote.git"), other);
            await ConfigureIdentityAsync(other, "Smoke Two", "smoke2@example.invalid");
            await File.WriteAllTextAsync(Path.Combine(other, "remote.txt"), "remote\n");
            await RunGitAsync(other, "add", "remote.txt");
            await RunGitAsync(other, "commit", "-m", "remote advance");
            await RunGitAsync(other, "push");
            await service.FetchAsync(await service.InspectAsync(work));
            var behind = await service.InspectAsync(work);
            Check("behind state detected after fetch", behind.Behind == 1 && behind.Ahead == 0, $"ahead={behind.Ahead} behind={behind.Behind}");
            await service.PullFastForwardAsync(behind);
            var pulled = await service.InspectAsync(work);
            Check("safe fast-forward pull returns clean sync", pulled.Ahead == 0 && pulled.Behind == 0);

            await File.WriteAllTextAsync(Path.Combine(work, "local-diverge.txt"), "local\n");
            await RunGitAsync(work, "add", "local-diverge.txt");
            await RunGitAsync(work, "commit", "-m", "local diverge");
            await File.WriteAllTextAsync(Path.Combine(other, "remote-diverge.txt"), "remote\n");
            await RunGitAsync(other, "add", "remote-diverge.txt");
            await RunGitAsync(other, "commit", "-m", "remote diverge");
            await RunGitAsync(other, "push");
            await service.FetchAsync(await service.InspectAsync(work));
            var diverged = await service.InspectAsync(work);
            Check("divergence detected", diverged.State == GitRepositoryState.Diverged && diverged.Ahead > 0 && diverged.Behind > 0,
                $"state={diverged.StateLabel} ahead={diverged.Ahead} behind={diverged.Behind}");
            var pullBlocked = await ThrowsAsync(() => service.PullFastForwardAsync(diverged));
            var pushBlocked = await ThrowsAsync(() => service.PushAsync(diverged));
            Check("divergent pull blocked by service", pullBlocked);
            Check("remote-ahead push blocked by service", pushBlocked);

            var credentialRepo = Path.Combine(root, "credential-remote");
            await RunGitAsync(root, "clone", Path.Combine(root, "remote.git"), credentialRepo);
            await RunGitAsync(credentialRepo, "remote", "set-url", "origin", "https://user:super-secret-token@github.com/example/private-repo.git");
            var sanitized = await service.InspectAsync(credentialRepo);
            Check("remote credentials are redacted from display", !sanitized.RemoteUrl.Contains("super-secret-token", StringComparison.Ordinal) &&
                !sanitized.RemoteUrl.Contains("user@", StringComparison.OrdinalIgnoreCase), sanitized.RemoteUrl);
            Check("credential-bearing GitHub remote still yields safe browser URL",
                sanitized.GitHubUrl == "https://github.com/example/private-repo", sanitized.GitHubUrl);

            var syntheticToken = "ghp_" + new string('A', 30);
            var redactedMessage = SensitiveDataRedactor.Redact(
                "fatal https://alice:password123@github.com/example/repo.git token " + syntheticToken);
            Check("diagnostic redactor removes URL credentials and GitHub tokens",
                !redactedMessage.Contains("password123", StringComparison.Ordinal) &&
                !redactedMessage.Contains(syntheticToken, StringComparison.Ordinal) &&
                redactedMessage.Contains("https://***@github.com", StringComparison.OrdinalIgnoreCase), redactedMessage);
        }
        catch (Exception ex)
        {
            Fail("unexpected smoke-test exception", ex.ToString());
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }

        Console.WriteLine();
        Console.WriteLine($"Passed: {_passed}  Failed: {_failed}");
        return _failed == 0 ? 0 : 1;
    }

    private static bool GitAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch { return false; }
    }

    private static async Task ConfigureIdentityAsync(string path, string name, string email)
    {
        await RunGitAsync(path, "config", "user.name", name);
        await RunGitAsync(path, "config", "user.email", email);
    }

    private static async Task StageAndCommitRemainingAsync(string path)
    {
        await RunGitAsync(path, "add", "-A");
        var check = await RunGitAsync(path, new[] { "diff", "--cached", "--quiet" }, allowFailure: true);
        if (check.ExitCode != 0) await RunGitAsync(path, "commit", "-m", "smoke cleanup");
    }

    private static async Task<string> CaptureStatusAsync(string path)
    {
        var result = await RunGitAsync(path, "status", "--porcelain=v1", "--untracked-files=all");
        return result.StdOut;
    }

    private static async Task<bool> ThrowsAsync(Func<Task> action)
    {
        try { await action(); return false; }
        catch (InvalidOperationException) { return true; }
    }

    private static async Task<GitResult> RunGitAsync(string cwd, params string[] args) =>
        await RunGitAsync(cwd, args, allowFailure: false);

    private static async Task<GitResult> RunGitAsync(string cwd, string[] args, bool allowFailure)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start git.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var result = new GitResult(process.ExitCode, await stdout, await stderr);
        if (!allowFailure && result.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed ({result.ExitCode}): {result.StdErr}");
        return result;
    }

    private static async Task<byte[]> RunGitBytesAsync(string cwd, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start git.");
        using var memory = new MemoryStream();
        var copy = process.StandardOutput.BaseStream.CopyToAsync(memory);
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await copy;
        if (process.ExitCode != 0) throw new InvalidOperationException(await stderr);
        return memory.ToArray();
    }

    private static IEnumerable<ArraySegment<byte>> Split(this byte[] source, byte separator)
    {
        var start = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] != separator) continue;
            if (i > start) yield return new ArraySegment<byte>(source, start, i - start);
            start = i + 1;
        }
        if (start < source.Length) yield return new ArraySegment<byte>(source, start, source.Length - start);
    }

    private static void Check(string name, bool condition, string? detail = null)
    {
        if (condition)
        {
            _passed++;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("PASS ");
        }
        else
        {
            _failed++;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("FAIL ");
        }
        Console.ResetColor();
        Console.WriteLine(name + (string.IsNullOrWhiteSpace(detail) ? string.Empty : $" [{detail}]"));
    }

    private static void Fail(string name, string detail) => Check(name, false, detail);

    private sealed record GitResult(int ExitCode, string StdOut, string StdErr);
}
