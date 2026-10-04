using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using NRS.Workbench.App.Services;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.SmokeTests;

internal static class PortableWorkspaceSmoke
{
    internal static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "nrs-portable-workspace-" + Guid.NewGuid().ToString("N"));
        var secondRoot = Path.Combine(Path.GetTempPath(), "nrs-portable-workspace-moved-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "Workbench", "Data");
        var runnerRoot = Path.Combine(root, "Runners");
        var runner = Path.Combine(runnerRoot, "runner-one");
        var repo = Path.Combine(root, "Repos", "repo-one");
        Directory.CreateDirectory(runner);
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(runner, "run.cmd"), "@echo off");
        File.WriteAllText(Path.Combine(runner, ".runner"),
            "{\"agentId\":42,\"agentName\":\"portable-one\",\"gitHubUrl\":\"https://github.com/acme/project\",\"workFolder\":\"_work\",\"poolName\":\"Default\"}");

        try
        {
            var encoded = PortablePathCodec.Encode(runner, root);
            Assert(PortablePathCodec.IsPortableToken(encoded) && encoded.Contains("Runners", StringComparison.Ordinal),
                "portable path codec stores same-volume paths without the drive/root prefix");
            var movedPath = PortablePathCodec.Decode(encoded, secondRoot);
            Assert(movedPath == Path.GetFullPath(Path.Combine(secondRoot, "Runners", "runner-one")),
                "portable path codec resolves the same relative runner after a volume-root change");

            var settings = new RunnerSettings
            {
                RunnerRoots = [runnerRoot],
                RepositoryPaths = [repo],
                RunnerDisplayOrder = [runner],
                RunnerQueueIncludedPaths = [runner],
                RunnerQueueUseAllRunners = false
            };
            var settingsService = new SettingsService(data, root);
            settingsService.Save(settings);

            var rawSettings = File.ReadAllText(settingsService.SettingsPath);
            Assert(rawSettings.Contains("@portable/", StringComparison.Ordinal) &&
                   !rawSettings.Contains(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase),
                "portable internal settings store same-volume paths as portable tokens");

            var loaded = settingsService.Load();
            Assert(loaded.RunnerRoots.Single() == Path.GetFullPath(runnerRoot) &&
                   loaded.RepositoryPaths.Single() == Path.GetFullPath(repo) &&
                   loaded.RunnerDisplayOrder.Single() == Path.GetFullPath(runner),
                "portable internal settings resolve tokens back to absolute runtime paths");

            var firstSession = new RuntimeSessionTracker(data, machineFingerprint: "HASH-A", portableMode: true);
            Assert(firstSession.BeginSession(preview: false) is null,
                "portable runtime session starts without a previous marker");
            var secondMachineSession = new RuntimeSessionTracker(data, machineFingerprint: "HASH-B", portableMode: true);
            Assert(secondMachineSession.BeginSession(preview: false) is null,
                "portable runtime session ignores an unclean marker from another machine");
            secondMachineSession.EndSession();

            var queue = new FileRunnerQueueStateStore(data, root);
            queue.Save([runner]);
            var journalPath = Path.Combine(data, "runner-queue-state.json");
            var journalRaw = File.ReadAllText(journalPath);
            Assert(journalRaw.Contains("@portable/", StringComparison.Ordinal) &&
                   !journalRaw.Contains(Environment.MachineName, StringComparison.OrdinalIgnoreCase) &&
                   queue.Load().Single() == Path.GetFullPath(runner),
                "portable queue journal stores relative paths and a non-readable machine fingerprint");

            var manifest = new PortableWorkspaceManifestStore(data, root);
            var registration = PortableRunnerMetadataReader.Read(runner)
                               ?? throw new InvalidOperationException("Synthetic runner metadata was not parsed.");
            manifest.RecordPending(runner, registration, ["gpu", "build"], hasDefaultLabels: true);
            var pending = manifest.FindPending(runner);
            Assert(pending is not null &&
                   pending.CustomLabels.SequenceEqual(["gpu", "build"]) &&
                   pending.HasDefaultLabels &&
                   pending.Registration.AgentName == "portable-one",
                "portable manifest retains non-secret metadata needed to retry an interrupted migration");
            manifest.RecordPrepared(runner, "https://github.com/acme/project", "portable-one");
            Assert(manifest.FindPending(runner) is null,
                "marking a runner prepared clears its pending migration record");
            var manifestRaw = File.ReadAllText(Path.Combine(data, "portable-workspace.json"));
            Assert(manifestRaw.Contains("@portable/", StringComparison.Ordinal) &&
                   !manifestRaw.Contains(Environment.MachineName, StringComparison.OrdinalIgnoreCase) &&
                   manifest.FindPrepared(runner) is not null,
                "portable manifest records prepared runners without the raw machine name");

            Assert(
                PortableRunnerPreparationService.IsSafeWorkFolder(runner, "_work") &&
                PortableRunnerPreparationService.IsSafeWorkFolder(runner, Path.Combine("_work", "nested")) &&
                !PortableRunnerPreparationService.IsSafeWorkFolder(runner, @"C:\outside") &&
                !PortableRunnerPreparationService.IsSafeWorkFolder(runner, @"..\outside"),
                "portable runner preparation accepts only work folders contained by the runner directory");

            var duplicateRegistration = new PortableRunnerRegistration(
                1, "duplicate-name", "https://github.com/acme/project", "_work", "Default", false, false);
            var duplicateA = new PortableRunnerCandidate
            {
                FolderPath = Path.Combine(root, "Runners", "duplicate-a"),
                DisplayName = "duplicate-name",
                GitHubUrl = duplicateRegistration.GitHubUrl,
                StateLabel = "Needs preparation",
                Registration = duplicateRegistration,
                CanPrepare = true
            };
            var duplicateB = new PortableRunnerCandidate
            {
                FolderPath = Path.Combine(root, "Runners", "duplicate-b"),
                DisplayName = "duplicate-name",
                GitHubUrl = duplicateRegistration.GitHubUrl,
                StateLabel = "Needs preparation",
                Registration = duplicateRegistration with { AgentId = 2 },
                CanPrepare = true
            };
            var duplicateBlocked = false;
            try { PortableRunnerPreparationService.ValidateSelection([duplicateA, duplicateB]); }
            catch (InvalidOperationException) { duplicateBlocked = true; }
            Assert(duplicateBlocked,
                "portable bulk preparation blocks duplicate target/name identities before touching local runner configuration");

            var handler = new FakeGitHubHandler();
            var client = new GitHubRunnerRegistrationClient(new HttpClient(handler));
            const string sessionPat = "session-pat-never-write";
            var remote = await client.GetRunnerAsync("https://github.com/acme/project", 42, sessionPat);
            var token = await client.CreateRegistrationTokenAsync("https://github.com/acme/project", sessionPat);

            Assert(remote.CustomLabels.SequenceEqual(["gpu", "build"]) && remote.HasDefaultLabels,
                "GitHub runner metadata preserves custom labels and detects default labels");
            Assert(token.Token == "temporary-registration-token" &&
                   handler.Calls.Count == 2 &&
                   handler.Calls.All(x => x.Authorization == "Bearer " + sessionPat) &&
                   handler.Calls[0].Url.EndsWith("/repos/acme/project/actions/runners/42", StringComparison.Ordinal) &&
                   handler.Calls[1].Url.EndsWith("/repos/acme/project/actions/runners/registration-token", StringComparison.Ordinal),
                "GitHub client uses the session PAT only for authenticated metadata/token requests");

            var diskText = string.Join("\n",
                Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Select(path =>
                    {
                        try { return File.ReadAllText(path); } catch { return string.Empty; }
                    }));
            Assert(!diskText.Contains(sessionPat, StringComparison.Ordinal) &&
                   !diskText.Contains("temporary-registration-token", StringComparison.Ordinal),
                "portable workspace files never persist GitHub PATs or registration tokens");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
            try { Directory.Delete(secondRoot, true); } catch { }
        }
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Portable workspace smoke test failed: " + name);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("PASS ");
        Console.ResetColor();
        Console.WriteLine(name);
    }

    private sealed class FakeGitHubHandler : HttpMessageHandler
    {
        public List<(string Url, string Authorization)> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add((
                request.RequestUri?.ToString() ?? string.Empty,
                request.Headers.Authorization?.ToString() ?? string.Empty));

            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"id\":42,\"name\":\"portable-one\",\"labels\":[{\"name\":\"self-hosted\",\"type\":\"read-only\"},{\"name\":\"Windows\",\"type\":\"read-only\"},{\"name\":\"X64\",\"type\":\"read-only\"},{\"name\":\"gpu\",\"type\":\"custom\"},{\"name\":\"build\",\"type\":\"custom\"}]}")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    "{\"token\":\"temporary-registration-token\",\"expires_at\":\"2026-10-04T15:00:00Z\"}")
            });
        }
    }
}
