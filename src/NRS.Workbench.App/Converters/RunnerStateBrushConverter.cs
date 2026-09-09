using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Converters;

public sealed class RunnerStateBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var state = value is RunnerState s ? s : RunnerState.Unknown;
        return state switch
        {
            RunnerState.Ready => new SolidColorBrush(Color.FromRgb(24, 105, 62)),
            RunnerState.Busy => new SolidColorBrush(Color.FromRgb(112, 83, 18)),
            RunnerState.Stopped => new SolidColorBrush(Color.FromRgb(109, 35, 44)),
            RunnerState.Starting or RunnerState.Stopping => new SolidColorBrush(Color.FromRgb(28, 76, 125)),
            RunnerState.Error => new SolidColorBrush(Color.FromRgb(126, 42, 35)),
            RunnerState.Unregistered => new SolidColorBrush(Color.FromRgb(74, 79, 88)),
            _ => new SolidColorBrush(Color.FromRgb(56, 65, 78))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
