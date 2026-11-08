using System.Windows;

namespace NRS.Workbench.App.Services;

/// <summary>
/// Base for every NRS Workbench window: dark title bar and a size that fits the
/// current work area. Esc is left to each window's IsCancel button.
/// </summary>
public class ThemedWindow : Window
{
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowThemeService.ApplyDarkTitleBar(this);
        WindowThemeService.FitToWorkArea(this);
    }
}
