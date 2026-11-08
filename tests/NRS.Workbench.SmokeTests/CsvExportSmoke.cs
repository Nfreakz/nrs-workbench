using NRS.Workbench.App.Services;
using NRS.Workbench.Core.Models;
using NRS.Workbench.Core.Services;

namespace NRS.Workbench.SmokeTests;

internal static class CsvExportSmoke
{
    internal static void Run()
    {
        Assert(CsvWriter.Escape(null) == string.Empty && CsvWriter.Escape("plain") == "plain",
            "CSV leaves plain fields unquoted");
        Assert(CsvWriter.Escape("a,b") == "\"a,b\"", "CSV quotes fields containing a comma");
        Assert(CsvWriter.Escape("say \"hi\"") == "\"say \"\"hi\"\"\"", "CSV doubles embedded quotes");
        Assert(CsvWriter.Escape("line1\nline2") == "\"line1\nline2\"" && CsvWriter.Escape("a\r\nb") == "\"a\r\nb\"",
            "CSV quotes fields containing line breaks");
        Assert(CsvWriter.FormatLine(["x", null, "y,z"]) == "x,,\"y,z\"", "CSV keeps empty fields and column order");

        var started = new DateTimeOffset(2026, 11, 8, 10, 30, 0, TimeSpan.Zero);
        var record = new JobRunRecord
        {
            RunnerAlias = "alpha",
            JobName = "build, test \"release\"",
            GitHubTarget = "https://github.com/example/repo",
            StartedAt = started,
            CompletedAt = started.AddSeconds(95.4),
            Duration = TimeSpan.FromSeconds(95.4),
            Outcome = JobRunOutcome.Failed,
            SourceFile = @"C:\actions-runner\_diag\Worker_20261108-103000-utc.log"
        };
        string[] header = ["Runner", "Job", "Repositorio/Workflow", "Estado", "Inicio", "Duración (s)", "Archivo origen"];
        var csv = JobRunCsvExport.Build([record], header, outcome => outcome == JobRunOutcome.Failed ? "FALLO" : "OK");
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert(lines.Length == 2 && lines[0] == string.Join(",", header), "CSV export writes the header row first");
        Assert(lines[1].StartsWith("alpha,\"build, test \"\"release\"\"\",https://github.com/example/repo,FALLO,", StringComparison.Ordinal),
            "CSV export quotes the job name and keeps the status label");
        var startIso = started.ToLocalTime().ToString("yyyy-MM-dd'T'HH:mm:ssK", System.Globalization.CultureInfo.InvariantCulture);
        Assert(lines[1].Contains("," + startIso + ",95,", StringComparison.Ordinal),
            "CSV export writes ISO 8601 local start and rounded duration in seconds");
        Assert(lines[1].EndsWith(record.SourceFile, StringComparison.Ordinal), "CSV export ends with the source file");
        Assert(CsvWriter.Utf8WithBom.GetPreamble().Length == 3, "CSV export encoding emits a UTF-8 BOM");

        var rejected = false;
        try { JobRunCsvExport.Build([record], ["only", "two"], _ => "OK"); }
        catch (ArgumentException) { rejected = true; }
        Assert(rejected, "CSV export rejects a header with the wrong column count");

        Assert(UpdateCheckService.TryParseTag("v0.19.0", out var parsed) && parsed == new Version(0, 19, 0),
            "update check parses a v-prefixed release tag");
        Assert(UpdateCheckService.TryParseTag("0.19.1-preview.2", out var pre) && pre == new Version(0, 19, 1),
            "update check parses a prerelease tag");
        Assert(!UpdateCheckService.TryParseTag("nightly", out _) && !UpdateCheckService.TryParseTag(null, out _),
            "update check rejects non-numeric tags");
        var newer = UpdateCheckService.Evaluate("v0.19.0", "https://github.com/Nfreakz/nrs-workbench/releases/tag/v0.19.0", new Version(0, 18, 7));
        var same = UpdateCheckService.Evaluate("v0.18.7", "http://evil.example/release", new Version(0, 18, 7));
        Assert(newer is { IsNewer: true } && newer.ReleaseUrl.EndsWith("/tag/v0.19.0", StringComparison.Ordinal),
            "update check flags a newer tag and keeps the GitHub URL");
        Assert(same is { IsNewer: false } && same.ReleaseUrl == UpdateCheckService.ReleasesPageUrl,
            "update check ignores an equal tag and replaces a non-GitHub URL");
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("CSV export smoke test failed: " + name);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("PASS ");
        Console.ResetColor();
        Console.WriteLine(name);
    }
}
