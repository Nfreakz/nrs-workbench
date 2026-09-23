using System.Text.Json;
using System.Windows.Markup;

namespace NRS.Workbench.App.Services;

public static class UiLanguage
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> English = new(() =>
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("/NRSWorkbench;component/Localization/en.json", UriKind.Relative));
        if (resource is null) throw new InvalidOperationException("English language resource is missing.");
        using var stream = resource.Stream;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException("English language resource is invalid.");
    });

    public static string Current { get; private set; } = "es";

    public static void Select(string? language) => Current = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "es";

    public static string Text(string source) => Current == "en" && English.Value.TryGetValue(source, out var translated)
        ? translated : source;

    public static string Choose(string spanish, string english) => Current == "en" ? english : spanish;
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public string Source { get; set; } = string.Empty;
    public override object ProvideValue(IServiceProvider serviceProvider) => UiLanguage.Text(Source);
}
