using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Converters;

public sealed class RunnerStateAccentBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var state = value is RunnerState s ? s : RunnerState.Unknown;
        return state switch
        {
            RunnerState.Ready => new SolidColorBrush(Color.FromRgb(61, 220, 132)),
            RunnerState.Busy => new SolidColorBrush(Color.FromRgb(245, 196, 81)),
            RunnerState.Stopped => new SolidColorBrush(Color.FromRgb(255, 100, 112)),
            RunnerState.Starting or RunnerState.Stopping => new SolidColorBrush(Color.FromRgb(88, 166, 255)),
            RunnerState.Error => new SolidColorBrush(Color.FromRgb(255, 123, 114)),
            RunnerState.Unregistered => new SolidColorBrush(Color.FromRgb(139, 148, 158)),
            _ => new SolidColorBrush(Color.FromRgb(142, 163, 187))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
