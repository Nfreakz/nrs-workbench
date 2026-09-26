using System.Diagnostics;
using System.Globalization;
using System.Text;
using NRS.Workbench.Core.Interfaces;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Services;

public sealed class GitService : IGitService
{
    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", ".gradle", ".idea", ".vs", "packages", "vendor", ".next", "dist", "build"
    };

    public async Task<string> GetVersionAsync()
    {
        var result = await RunAsync(Environment.CurrentDirectory, ["--version"], allowFailure: true);
        if (result.ExitCode != 0) return UiLanguage.Choose("Git no disponible", "Git unavailable");
        return string.IsNullOrWhiteSpace(result.StdOut) ? UiLanguage.Choose("Git detectado", "Git detected") : result.StdOut.Trim();
    }

    public async Task<GitRepositoryInfo> InspectAsync(string repositoryPath)
    {
        var path = Path.GetFullPath(repositoryPath);
        var info = new GitRepositoryInfo { Path = path, Name = new DirectoryInfo(path).Name };

        if (!Directory.Exists(path))
        {
            info.State = GitRepositoryState.Error;
            info.ErrorMessage = UiLanguage.Choose("La carpeta no existe.", "The folder does not exist.");
            return info;
        }

        var top = await RunAsync(path, ["rev-parse", "--show-toplevel"], allowFailure: true);
        if (top.ExitCode != 0)
        {
            info.State = GitRepositoryState.Error;
            info.ErrorMessage = UiLanguage.Choose("La carpeta no es un repositorio Git válido.", "The folder is not a valid Git repository.");
            return info;
        }

        var canonicalPath = top.StdOut.Trim();
        if (!string.IsNullOrWhiteSpace(canonicalPath))
        {
            info.Path = Path.GetFullPath(canonicalPath);
            info.Name = new DirectoryInfo(info.Path).Name;
        }

        var status = await RunAsync(info.Path, ["status", "--porcelain=v2", "--branch"], allowFailure: false);
        ParseStatus(status.StdOut, info);

        var remote = await RunAsync(info.Path, ["remote", "get-url", "origin"], allowFailure: true);
        if (remote.ExitCode == 0)
        {
            var rawRemote = remote.StdOut.Trim();
            info.RemoteUrl = SanitizeRemoteForDisplay(rawRemote);
            info.GitHubUrl = ToBrowserGitHubUrl(rawRemote);
        }

        var commit = await RunAsync(info.Path, ["log", "-1", "--format=%h%x1f%s%x1f%cI"], allowFailure: true);
        if (commit.ExitCode == 0 && !string.IsNullOrWhiteSpace(commit.StdOut))
        {
            var parts = commit.StdOut.Trim().Split('\u001f');
            if (parts.Length > 0) info.LastCommitSha = parts[0];
            if (parts.Length > 1) info.LastCommitSubject = parts[1];
            if (parts.Length > 2 && DateTimeOffset.TryParse(parts[2], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when))
                info.LastCommitDate = when;
        }

        info.State = DetermineState(info);
        return info;
    }

    public Task<IReadOnlyList<string>> FindRepositoriesAsync(string rootPath, int maxDepth = 3) =>
        Task.Run<IReadOnlyList<string>>(() => FindRepositories(rootPath, maxDepth));


    public async Task<IReadOnlyList<GitFileChange>> GetChangesAsync(GitRepositoryInfo repository)
    {
        EnsureRepositoryUsable(repository);
        var result = await RunAsync(repository.Path, ["status", "--porcelain=v1", "-z", "--untracked-files=all"], allowFailure: false);
        return ParseFileChanges(result.StdOut);
    }

    public async Task<string> GetDiffAsync(GitRepositoryInfo repository, GitFileChange change)
    {
        EnsureRepositoryUsable(repository);
        if (change.IsUntracked)
            return await ReadUntrackedPreviewAsync(repository.Path, change.Path);

        var sections = new List<string>();
        if (change.IsStaged)
        {
            var staged = await RunAsync(repository.Path, ["diff", "--cached", "--no-ext-diff", "--no-textconv", "--", change.Path], allowFailure: true);
            if (!string.IsNullOrWhiteSpace(staged.StdOut))
                sections.Add("=== STAGED ===\r\n" + staged.StdOut.TrimEnd());
        }

        if (change.HasUnstagedChanges)
        {
            var working = await RunAsync(repository.Path, ["diff", "--no-ext-diff", "--no-textconv", "--", change.Path], allowFailure: true);
            if (!string.IsNullOrWhiteSpace(working.StdOut))
                sections.Add("=== WORKING TREE ===\r\n" + working.StdOut.TrimEnd());
        }

        if (sections.Count == 0 && !string.IsNullOrWhiteSpace(change.OriginalPath))
        {
            var rename = await RunAsync(repository.Path, ["diff", "HEAD", "--no-ext-diff", "--no-textconv", "--", change.OriginalPath, change.Path], allowFailure: true);
            if (!string.IsNullOrWhiteSpace(rename.StdOut)) sections.Add(rename.StdOut.TrimEnd());
        }

        return sections.Count == 0 ? UiLanguage.Choose("No hay diff textual disponible para este archivo.", "No text diff is available for this file.") : string.Join("\r\n\r\n", sections);
    }

    public async Task CommitAsync(GitRepositoryInfo repository, string message, IReadOnlyList<GitFileChange> selectedChanges)
    {
        EnsureRepositoryUsable(repository);
        var commitMessage = (message ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(commitMessage)) throw new InvalidOperationException(UiLanguage.Choose("Escribe un mensaje de commit.", "Enter a commit message."));
        if (repository.ConflictCount > 0) throw new InvalidOperationException(UiLanguage.Choose("Commit bloqueado: el repositorio contiene conflictos sin resolver.", "Commit blocked: the repository has unresolved conflicts."));
        if (selectedChanges.Count == 0) throw new InvalidOperationException(UiLanguage.Choose("Selecciona al menos un archivo para el commit.", "Select at least one file to commit."));

        var currentChanges = await GetChangesAsync(repository);
        var selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var change in selectedChanges)
        {
            if (!string.IsNullOrWhiteSpace(change.Path)) selectedPaths.Add(change.Path);
            if (!string.IsNullOrWhiteSpace(change.OriginalPath)) selectedPaths.Add(change.OriginalPath);
        }

        var stagedOutsideSelection = currentChanges
            .Where(x => x.IsStaged && !selectedPaths.Contains(x.Path) && (string.IsNullOrWhiteSpace(x.OriginalPath) || !selectedPaths.Contains(x.OriginalPath)))
            .Select(x => x.DisplayPath)
            .ToList();
        if (stagedOutsideSelection.Count > 0)
        {
            var preview = string.Join("\r\n", stagedOutsideSelection.Take(8).Select(x => "• " + x));
            if (stagedOutsideSelection.Count > 8) preview += UiLanguage.Choose($"\r\n• … y {stagedOutsideSelection.Count - 8} más", $"\r\n• … and {stagedOutsideSelection.Count - 8} more", $"\r\n• … i {stagedOutsideSelection.Count - 8} més");
            throw new InvalidOperationException(UiLanguage.Choose("Hay cambios ya staged que no están seleccionados. Para evitar incluirlos por accidente, el commit se ha bloqueado.\r\n\r\n", "Other staged changes were not selected. The commit is blocked to avoid including them by accident.\r\n\r\n") + preview);
        }

        // Stage only paths that still need staging. A fully staged rename produced by
        // `git mv` already removed the original path from the index, so trying to run
        // `git add -A -- old.txt new.txt` can fail with "pathspec ... did not match".
        // For staged+unstaged files we stage only the current path; for an unstaged
        // rename we also include the original path so Git can record the deletion.
        var pathsToStage = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var change in selectedChanges)
        {
            if (change.IsStaged && !change.HasUnstagedChanges)
                continue;

            if (!string.IsNullOrWhiteSpace(change.Path))
                pathsToStage.Add(change.Path);

            if (!change.IsStaged && !string.IsNullOrWhiteSpace(change.OriginalPath))
                pathsToStage.Add(change.OriginalPath);
        }

        var paths = pathsToStage.ToList();
        const int batchSize = 80;
        for (var offset = 0; offset < paths.Count; offset += batchSize)
        {
            var batch = paths.Skip(offset).Take(batchSize).ToList();
            var args = new List<string> { "add", "-A", "--" };
            args.AddRange(batch);
            await RunAsync(repository.Path, args, allowFailure: false);
        }

        var stagedCheck = await RunAsync(repository.Path, ["diff", "--cached", "--quiet"], allowFailure: true);
        if (stagedCheck.ExitCode == 0) throw new InvalidOperationException(UiLanguage.Choose("Los archivos seleccionados no producen ningún cambio para commitear.", "The selected files have no changes to commit."));

        await RunAsync(repository.Path, ["commit", "-m", commitMessage], allowFailure: false);
    }

    public async Task FetchAsync(GitRepositoryInfo repository)
    {
        EnsureRepositoryUsable(repository);
        if (!repository.HasRemote) throw new InvalidOperationException(UiLanguage.Choose("Este repositorio no tiene remote 'origin'.", "This repository has no 'origin' remote."));
        await RunAsync(repository.Path, ["fetch", "--prune"], allowFailure: false);
    }

    public async Task PullFastForwardAsync(GitRepositoryInfo repository)
    {
        EnsureRepositoryUsable(repository);
        var current = await InspectAsync(repository.Path);
        EnsureRepositoryUsable(current);
        EnsureBranchUnchanged(repository, current);
        if (!current.HasUpstream)
            throw new InvalidOperationException(UiLanguage.Choose(
                "La rama actual no tiene upstream configurado.",
                "The current branch has no configured upstream.",
                "La branca actual no té upstream configurat."));
        if (current.IsDirty)
            throw new InvalidOperationException(UiLanguage.Choose(
                "Pull bloqueado: el repositorio ha cambiado y ahora contiene cambios locales sin commit.",
                "Pull blocked: the repository changed and now contains uncommitted local changes.",
                "Pull bloquejat: el repositori ha canviat i ara conté canvis locals sense commit."));
        if (current.Ahead > 0 && current.Behind > 0)
            throw new InvalidOperationException(UiLanguage.Choose(
                "Pull automático bloqueado: la rama ha divergido. Resuelve la estrategia manualmente.",
                "Automatic pull blocked: the branch has diverged. Choose a strategy manually.",
                "Pull automàtic bloquejat: la branca ha divergit. Resol l'estratègia manualment."));
        await RunAsync(current.Path, ["pull", "--ff-only"], allowFailure: false);
    }

    public async Task PushAsync(GitRepositoryInfo repository)
    {
        EnsureRepositoryUsable(repository);
        var current = await InspectAsync(repository.Path);
        EnsureRepositoryUsable(current);
        EnsureBranchUnchanged(repository, current);
        if (!current.HasUpstream)
            throw new InvalidOperationException(UiLanguage.Choose(
                "La rama actual no tiene upstream configurado.",
                "The current branch has no configured upstream.",
                "La branca actual no té upstream configurat."));
        if (current.Behind > 0)
            throw new InvalidOperationException(UiLanguage.Choose(
                "Push bloqueado: el remoto contiene commits que todavía no tienes localmente.",
                "Push blocked: the remote has commits missing locally.",
                "Push bloquejat: el remot conté commits que encara no tens localment."));
        if (current.Ahead <= 0)
            throw new InvalidOperationException(UiLanguage.Choose(
                "No hay commits locales pendientes de push.",
                "There are no local commits to push.",
                "No hi ha commits locals pendents de push."));
        await RunAsync(current.Path, ["push"], allowFailure: false);
    }

    private static void EnsureBranchUnchanged(GitRepositoryInfo displayed, GitRepositoryInfo current)
    {
        if (!string.Equals(displayed.Branch, current.Branch, StringComparison.Ordinal))
            throw new InvalidOperationException(UiLanguage.Choose(
                "La rama actual ha cambiado desde la última actualización. Refresca el repositorio antes de continuar.",
                "The current branch changed since the last refresh. Refresh the repository before continuing.",
                "La branca actual ha canviat des de l'última actualització. Actualitza el repositori abans de continuar."));
    }

    public async Task<IReadOnlyList<string>> GetLocalBranchesAsync(GitRepositoryInfo repository)
    {
        EnsureRepositoryUsable(repository);
        var result = await RunAsync(repository.Path, ["branch", "--list", "--format=%(refname:short)"], allowFailure: false);
        return result.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .OrderBy(branch => branch, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task SwitchLocalBranchAsync(GitRepositoryInfo repository, string branch)
    {
        EnsureRepositoryUsable(repository);
        if (string.IsNullOrWhiteSpace(branch)) throw new InvalidOperationException(UiLanguage.Choose("Selecciona una rama local.", "Select a local branch."));

        // The displayed snapshot may be stale. Reinspect immediately before any Git write.
        var current = await InspectAsync(repository.Path);
        if (current.State == GitRepositoryState.Error || current.IsDirty || current.ConflictCount > 0)
            throw new InvalidOperationException(UiLanguage.Choose("Cambio de rama bloqueado: el repositorio tiene cambios locales o conflictos.", "Branch switch blocked: the repository has local changes or conflicts."));
        if (!string.Equals(current.Branch, repository.Branch, StringComparison.Ordinal))
            throw new InvalidOperationException(UiLanguage.Choose("La rama actual ha cambiado. Actualiza el repositorio y vuelve a intentarlo.", "The current branch has changed. Refresh the repository and try again."));
        if (string.Equals(branch, current.Branch, StringComparison.Ordinal)) return;

        var branches = await GetLocalBranchesAsync(current);
        if (!branches.Contains(branch, StringComparer.Ordinal))
            throw new InvalidOperationException(UiLanguage.Choose("La rama seleccionada ya no existe como rama local.", "The selected local branch no longer exists."));

        // An exact validated ref prevents a branch name from being interpreted as a Git option.
        await RunAsync(current.Path, ["switch", "--", branch], allowFailure: false);
    }


    private static IReadOnlyList<GitFileChange> ParseFileChanges(string text)
    {
        var result = new List<GitFileChange>();
        var records = text.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < records.Length; i++)
        {
            var record = records[i];
            if (record.Length < 3) continue;
            var x = record[0];
            var y = record[1];
            var path = record.Length > 3 ? record[3..] : string.Empty;
            if (string.IsNullOrWhiteSpace(path)) continue;

            var untracked = x == '?' && y == '?';
            var ignored = x == '!' && y == '!';
            if (ignored) continue;

            var original = string.Empty;
            if ((x is 'R' or 'C' || y is 'R' or 'C') && i + 1 < records.Length)
                original = records[++i];

            result.Add(new GitFileChange
            {
                Path = path,
                OriginalPath = original,
                IndexStatus = untracked ? '?' : x,
                WorkTreeStatus = untracked ? '?' : y,
                IsUntracked = untracked,
                IsConflict = IsConflictStatus(x, y),
                IsSelected = !IsConflictStatus(x, y)
            });
        }
        return result.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsConflictStatus(char x, char y)
    {
        var code = new string([x, y]);
        return code is "DD" or "AU" or "UD" or "UA" or "DU" or "AA" or "UU" || x == 'U' || y == 'U';
    }

    private static async Task<string> ReadUntrackedPreviewAsync(string repositoryPath, string relativePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(repositoryPath, relativePath));
            var root = Path.GetFullPath(repositoryPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
                return UiLanguage.Choose("Archivo nuevo no disponible para vista previa.", "New file unavailable for preview.");

            var attributes = File.GetAttributes(fullPath);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                return UiLanguage.Choose("Vista previa omitida para enlaces o reparse points por seguridad.", "Preview skipped for links or reparse points for safety.");

            var info = new FileInfo(fullPath);
            if (info.Length > 512 * 1024) return UiLanguage.Choose($"Archivo nuevo de {info.Length / 1024:N0} KB. Vista previa omitida por tamaño.", $"New file of {info.Length / 1024:N0} KB. Preview skipped due to size.", $"Fitxer nou de {info.Length / 1024:N0} KB. Vista prèvia omesa per la mida.");

            await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            var text = await reader.ReadToEndAsync();
            if (text.IndexOf('\0') >= 0) return UiLanguage.Choose("Archivo nuevo binario. No hay diff textual disponible.", "New binary file. No text diff is available.");
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var preview = string.Join("\r\n", lines.Take(800).Select(line => "+ " + line));
            if (lines.Length > 800) preview += UiLanguage.Choose($"\r\n… vista previa truncada ({lines.Length - 800} líneas más)", $"\r\n… preview truncated ({lines.Length - 800} more lines)", $"\r\n… vista prèvia truncada ({lines.Length - 800} línies més)");
            return UiLanguage.Choose($"=== ARCHIVO NUEVO ===\r\n+++ {relativePath}\r\n\r\n{preview}", $"=== NEW FILE ===\r\n+++ {relativePath}\r\n\r\n{preview}", $"=== FITXER NOU ===\r\n+++ {relativePath}\r\n\r\n{preview}");
        }
        catch (Exception ex)
        {
            return UiLanguage.Choose("No se pudo generar la vista previa: ", "Could not generate preview: ") + ex.Message;
        }
    }

    private static void EnsureRepositoryUsable(GitRepositoryInfo repository)
    {
        if (repository.State == GitRepositoryState.Error)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(repository.ErrorMessage) ? UiLanguage.Choose("Repositorio no disponible.", "Repository unavailable.") : repository.ErrorMessage);
    }

    private static GitRepositoryState DetermineState(GitRepositoryInfo info)
    {
        if (!string.IsNullOrWhiteSpace(info.ErrorMessage)) return GitRepositoryState.Error;
        if (info.ConflictCount > 0) return GitRepositoryState.Conflict;
        if (info.Ahead > 0 && info.Behind > 0) return GitRepositoryState.Diverged;
        if (info.Behind > 0) return GitRepositoryState.Behind;
        if (info.Ahead > 0) return GitRepositoryState.Ahead;
        if (info.IsDirty) return GitRepositoryState.Changes;
        if (!info.HasRemote) return GitRepositoryState.NoRemote;
        return GitRepositoryState.Clean;
    }

    private static void ParseStatus(string text, GitRepositoryInfo info)
    {
        foreach (var raw in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("# branch.head ", StringComparison.Ordinal))
            {
                info.Branch = line[14..].Trim();
                if (info.Branch == "(detached)") info.Branch = "DETACHED";
                continue;
            }
            if (line.StartsWith("# branch.upstream ", StringComparison.Ordinal))
            {
                info.Upstream = line[18..].Trim();
                continue;
            }
            if (line.StartsWith("# branch.ab ", StringComparison.Ordinal))
            {
                var parts = line[12..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (part.StartsWith('+') && int.TryParse(part[1..], out var ahead)) info.Ahead = ahead;
                    if (part.StartsWith('-') && int.TryParse(part[1..], out var behind)) info.Behind = behind;
                }
                continue;
            }
            if (line.StartsWith("? ", StringComparison.Ordinal))
            {
                info.UntrackedCount++;
                continue;
            }
            if (line.StartsWith("u ", StringComparison.Ordinal))
            {
                info.ConflictCount++;
                continue;
            }
            if (line.StartsWith("1 ", StringComparison.Ordinal) || line.StartsWith("2 ", StringComparison.Ordinal))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                CountXY(parts[1], info);
            }
        }
    }

    private static void CountXY(string xy, GitRepositoryInfo info)
    {
        if (xy.Length < 2) return;
        var chars = new[] { xy[0], xy[1] }.Where(c => c != '.').Distinct().ToArray();
        foreach (var c in chars)
        {
            if (c == 'D') info.DeletedCount++;
            else if (c == 'A') info.AddedCount++;
            else if (c is 'U') info.ConflictCount++;
            else info.ModifiedCount++;
        }
    }

    private static string SanitizeRemoteForDisplay(string remote)
    {
        if (string.IsNullOrWhiteSpace(remote)) return string.Empty;
        var value = remote.Trim();

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https" or "ssh")
        {
            var builder = new UriBuilder(uri)
            {
                UserName = string.Empty,
                Password = string.Empty
            };
            return builder.Uri.ToString().TrimEnd('/');
        }

        var at = value.IndexOf('@');
        var colon = value.IndexOf(':', at + 1);
        if (at > 0 && colon > at)
            return "***@" + value[(at + 1)..];

        return value;
    }

    private static string ToBrowserGitHubUrl(string remote)
    {
        if (string.IsNullOrWhiteSpace(remote)) return string.Empty;
        var value = remote.Trim();

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            var path = uri.AbsolutePath.TrimStart('/');
            if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) path = path[..^4];
            return string.IsNullOrWhiteSpace(path) ? string.Empty : "https://github.com/" + path;
        }

        var marker = "github.com:";
        var markerIndex = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            var path = value[(markerIndex + marker.Length)..].TrimStart('/');
            if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) path = path[..^4];
            return string.IsNullOrWhiteSpace(path) ? string.Empty : "https://github.com/" + path;
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> FindRepositories(string rootPath, int maxDepth)
    {
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath)) return [];

        void Visit(string path, int depth)
        {
            if (depth > maxDepth) return;
            try
            {
                if (Directory.Exists(Path.Combine(path, ".git")) || File.Exists(Path.Combine(path, ".git")))
                {
                    results.Add(Path.GetFullPath(path));
                    return;
                }

                foreach (var child in Directory.EnumerateDirectories(path))
                {
                    var name = Path.GetFileName(child);
                    if (IgnoredDirectoryNames.Contains(name)) continue;
                    try
                    {
                        var attributes = File.GetAttributes(child);
                        if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                    }
                    catch
                    {
                        continue;
                    }
                    Visit(child, depth + 1);
                }
            }
            catch
            {
                // Selected roots can contain protected folders. Skip inaccessible branches.
            }
        }

        Visit(Path.GetFullPath(rootPath), 0);
        return results.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task<GitCommandResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, bool allowFailure)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var result = new GitCommandResult(process.ExitCode, await stdoutTask, await stderrTask);
            if (!allowFailure && result.ExitCode != 0)
            {
                var message = string.IsNullOrWhiteSpace(result.StdErr) ? result.StdOut : result.StdErr;
                var safeMessage = SensitiveDataRedactor.Redact(message.Trim());
                throw new InvalidOperationException(UiLanguage.Choose($"Git devolvió código {result.ExitCode}: {safeMessage}", $"Git exited with code {result.ExitCode}: {safeMessage}", $"Git ha retornat el codi {result.ExitCode}: {safeMessage}"));
            }
            return result;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException(UiLanguage.Choose("No se encuentra Git en PATH. Instala Git for Windows o añade git.exe al PATH.", "Git was not found in PATH. Install Git for Windows or add git.exe to PATH."), ex);
        }
    }

    private sealed record GitCommandResult(int ExitCode, string StdOut, string StdErr);
}
