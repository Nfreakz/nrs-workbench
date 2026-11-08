using System.Windows;
using System.Windows.Controls;
using NRS.Workbench.App.Services;
using NRS.Workbench.App.ViewModels;
using NRS.Workbench.Core.Interfaces;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App;

public partial class CommitWindow : Window
{
    private readonly CommitViewModel _viewModel;

    public CommitWindow(IGitService git, GitRepositoryInfo repository, IDialogService dialogs)
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            WindowThemeService.ApplyDarkTitleBar(this);
            WindowThemeService.FitToWorkArea(this);
        };
        _viewModel = new CommitViewModel(git, repository, dialogs);
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
        Closing += (_, e) =>
        {
            // Never tear the window down while git commit/push is still running.
            if (_viewModel.IsBusy) e.Cancel = true;
        };
    }

    private void CloseWithResult(bool result)
    {
        if (!IsLoaded) return;
        DialogResult = result;
        Close();
    }

    private async void Changes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await _viewModel.LoadDiffAsync();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => _viewModel.SetAllSelected(true);
    private void SelectNone_Click(object sender, RoutedEventArgs e) => _viewModel.SetAllSelected(false);
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private async void Commit_Click(object sender, RoutedEventArgs e)
    {
        if (await _viewModel.CommitAsync(false))
            CloseWithResult(true);
    }

    private async void CommitPush_Click(object sender, RoutedEventArgs e)
    {
        if (await _viewModel.CommitAsync(true))
            CloseWithResult(true);
    }
}
