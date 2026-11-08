using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NRS.Workbench.App.Converters;

public sealed class VersionAccentConverter : IMultiValueConverter
{
    private static readonly Brush NormalBrush = Frozen(0x9B, 0xA3, 0xAE);
    private static readonly Brush MismatchBrush = Frozen(0xC5, 0x9A, 0x4A);

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var version = values.Length > 0 ? values[0]?.ToString()?.Trim() : null;
        var dominant = values.Length > 1 ? values[1]?.ToString()?.Trim() : null;

        if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(dominant))
            return NormalBrush;

        return string.Equals(version, dominant, StringComparison.OrdinalIgnoreCase)
            ? NormalBrush
            : MismatchBrush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
