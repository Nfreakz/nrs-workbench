using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Converters;

public sealed class RunnerStateAccentBrushConverter : IValueConverter
{
    private static readonly Brush ReadyBrush = Frozen(61, 220, 132);
    private static readonly Brush BusyBrush = Frozen(245, 196, 81);
    private static readonly Brush StoppedBrush = Frozen(255, 100, 112);
    private static readonly Brush TransitionBrush = Frozen(88, 166, 255);
    private static readonly Brush ErrorBrush = Frozen(255, 123, 114);
    private static readonly Brush UnregisteredBrush = Frozen(139, 148, 158);
    private static readonly Brush UnknownBrush = Frozen(142, 163, 187);

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
