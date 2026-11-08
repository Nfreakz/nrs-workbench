using System.Text.Json;
using System.Text.RegularExpressions;

namespace NRS.Workbench.SmokeTests;

/// <summary>
/// Verifies that every localizable text used by the desktop layer has an English and a Catalan entry.
/// <c>{local:Tr Source='…'}</c> and <c>UiLanguage.Text("…")</c> use the Spanish text itself as the key,
/// and the 2-argument <c>UiLanguage.Choose(es, en)</c> looks the Catalan text up by the Spanish literal.
/// The source tree is located from the test binary by walking up to the solution file.
/// </summary>
internal static class LocalizationSmoke
{
    private static readonly Regex TrKey = new(@"\{local:Tr\s+Source='([^']*)'\}", RegexOptions.Compiled);
    private static readonly Regex TextKey = new(@"UiLanguage\.Text\(\s*""((?:[^""\\]|\\.)*)""\s*\)", RegexOptions.Compiled);
    private static readonly Regex TwoArgChoose = new(
        @"UiLanguage\.Choose\(\s*""((?:[^""\\]|\\.)*)""\s*,\s*""(?:[^""\\]|\\.)*""\s*\)",
        RegexOptions.Compiled);

    internal static void Run()
    {
        var root = FindRepositoryRoot();
        var appDirectory = Path.Combine(root, "src", "NRS.Workbench.App");
        var english = LoadResource(Path.Combine(appDirectory, "Localization", "en.json"));
        var catalan = LoadResource(Path.Combine(appDirectory, "Localization", "ca.json"));

        var xamlKeys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(appDirectory, "*.xaml", SearchOption.AllDirectories))
            foreach (Match match in TrKey.Matches(File.ReadAllText(file)))
                xamlKeys.Add(match.Groups[1].Value);
        Assert(xamlKeys.Count > 0, "localization smoke finds {local:Tr} keys in XAML");

        var textKeys = new SortedSet<string>(StringComparer.Ordinal);
        var chooseKeys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(appDirectory, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            foreach (Match match in TextKey.Matches(source)) textKeys.Add(Regex.Unescape(match.Groups[1].Value));
            foreach (Match match in TwoArgChoose.Matches(source)) chooseKeys.Add(Regex.Unescape(match.Groups[1].Value));
        }

        var keys = xamlKeys.Concat(textKeys).ToList();
        AssertComplete(Missing(keys, english), "en.json contains every {local:Tr}/UiLanguage.Text key");
        AssertComplete(Missing(keys, catalan), "ca.json contains every {local:Tr}/UiLanguage.Text key");
        AssertComplete(Missing(chooseKeys, catalan), "ca.json contains every Spanish literal passed to the 2-argument UiLanguage.Choose");
    }

    private static void AssertComplete(List<string> missing, string name) =>
        Assert(missing.Count == 0, name + Describe(missing));

    private static List<string> Missing(IEnumerable<string> keys, IReadOnlyDictionary<string, string> resource) =>
        keys.Where(key => !resource.ContainsKey(key)).Distinct(StringComparer.Ordinal).ToList();

    private static string Describe(List<string> missing) =>
        missing.Count == 0 ? string.Empty : " [missing: " + string.Join(" | ", missing.Select(x => x.Length > 60 ? x[..60] + "…" : x)) + "]";

    private static IReadOnlyDictionary<string, string> LoadResource(string path)
    {
        Assert(File.Exists(path), $"language resource exists: {path}");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
               ?? throw new InvalidOperationException($"Language resource is invalid: {path}");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NRSWorkbench.sln"))) return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate NRSWorkbench.sln above " + AppContext.BaseDirectory);
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Localization smoke test failed: " + name);
        Console.ForegroundColor = ConsoleColor.Green; Console.Write("PASS "); Console.ResetColor(); Console.WriteLine(name);
    }
}
