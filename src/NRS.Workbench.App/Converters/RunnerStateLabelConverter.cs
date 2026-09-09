using System.Globalization;
using System.Windows.Data;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Converters;

public sealed class RunnerStateLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is RunnerState state ? state.ToString().ToUpperInvariant() : "UNKNOWN";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
