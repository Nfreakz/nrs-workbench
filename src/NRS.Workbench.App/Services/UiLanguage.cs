using System.Text.Json;
using System.Windows.Markup;

namespace NRS.Workbench.App.Services;

public static class UiLanguage
{
    private static IReadOnlyDictionary<string, string> LoadResource(string language)
    {
        var resource = System.Windows.Application.GetResourceStream(
            new Uri($"/NRSWorkbench;component/Localization/{language}.json", UriKind.Relative));
        if (resource is null) throw new InvalidOperationException($"{language} language resource is missing.");
        using var stream = resource.Stream;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException($"{language} language resource is invalid.");
    }

    private static readonly Lazy<IReadOnlyDictionary<string, string>> English =
        new(() => LoadResource("en"));
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Catalan =
        new(() => LoadResource("ca"));

    public static string Current { get; private set; } = "es";

    public static void Select(string? language) =>
        Current = language?.ToLowerInvariant() switch
        {
            "en" => "en",
            "ca" => "ca",
            _ => "es"
        };

    public static string Text(string source) => Current switch
    {
        "en" when English.Value.TryGetValue(source, out var english) => english,
        "ca" when Catalan.Value.TryGetValue(source, out var catalan) => catalan,
        _ => source
    };

    public static string Choose(string spanish, string english) => Current switch
    {
        "en" => english,
        "ca" => Text(spanish),
        _ => spanish
    };

    public static string Choose(string spanish, string english, string catalan) => Current switch
    {
        "en" => english,
        "ca" => catalan,
        _ => spanish
    };
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public string Source { get; set; } = string.Empty;
    public override object ProvideValue(IServiceProvider serviceProvider) => UiLanguage.Text(Source);
}
