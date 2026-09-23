using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using NRS.Workbench.App.Services;

namespace NRS.Workbench.App.Converters;

public sealed class LocalizedTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string source || UiLanguage.Current != "en") return value ?? string.Empty;
        var match = Regex.Match(source, @"^(\d+) cambios?$", RegexOptions.CultureInvariant);
        return match.Success ? $"{match.Groups[1].Value} changes" : UiLanguage.Text(source);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}
