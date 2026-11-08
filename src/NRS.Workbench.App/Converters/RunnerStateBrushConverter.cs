using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Converters;

public sealed class RunnerStateBrushConverter : IValueConverter
{
    private static readonly Brush ReadyBrush = Frozen(24, 105, 62);
    private static readonly Brush BusyBrush = Frozen(112, 83, 18);
    private static readonly Brush StoppedBrush = Frozen(109, 35, 44);
    private static readonly Brush TransitionBrush = Frozen(28, 76, 125);
    private static readonly Brush ErrorBrush = Frozen(126, 42, 35);
    private static readonly Brush UnregisteredBrush = Frozen(74, 79, 88);
    private static readonly Brush UnknownBrush = Frozen(56, 65, 78);

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var state = value is RunnerState s ? s : RunnerState.Unknown;
        return state switch
        {
            RunnerState.Ready => ReadyBrush,
            RunnerState.Busy => BusyBrush,
            RunnerState.Stopped => StoppedBrush,
            RunnerState.Starting or RunnerState.Stopping => TransitionBrush,
            RunnerState.Error => ErrorBrush,
            RunnerState.Unregistered => UnregisteredBrush,
            _ => UnknownBrush
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
