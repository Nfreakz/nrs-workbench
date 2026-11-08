namespace NRS.Workbench.Core.Models;

/// <summary>
/// Language hook for the fixed labels Core models expose. Core cannot see the
/// App's UiLanguage, so the App replaces <see cref="Choose"/> at startup.
/// Only fixed labels go through here; user and log data never do.
/// </summary>
public static class DisplayText
{
    public static Func<string, string, string, string> Choose { get; set; } = (spanish, _, _) => spanish;
}
