using System.Windows;
using NRS.Workbench.App.Services;
using NRS.Workbench.App.ViewModels;
using NRS.Workbench.Core.Interfaces;

namespace NRS.Workbench.App;

public partial class RepositoriesWindow : Window
{
    private readonly RepositoriesViewModel _viewModel;
    private readonly IGitService _git;
    private readonly IDialogService _dialogs;

    public RepositoriesWindow(SettingsService settings)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowThemeService.ApplyDarkTitleBar(this);

        _git = new GitService();
        _dialogs = new DialogService(settings);
        _viewModel = new RepositoriesViewModel(_git, settings, _dialogs);
        DataContext = _viewModel;

        Loaded += async (_, _) => await _viewModel.InitializeAsync();
    }

    private async void Commit_Click(object sender, RoutedEventArgs e)
    {
        var repository = _viewModel.SelectedRepository;
        if (repository is null || !repository.CanCommit) return;
        var window = new CommitWindow(_git, repository, _dialogs) { Owner = this };
        if (window.ShowDialog() == true) await _viewModel.RefreshAsync();
    }

    private async void AddRepository_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Selecciona la carpeta raíz de un repositorio Git",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        await _viewModel.AddRepositoryAsync(dialog.SelectedPath);
    }

    private async void ScanFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Selecciona una carpeta que contenga tus repositorios. Se escanearán hasta 3 niveles.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        await _viewModel.ScanFolderAsync(dialog.SelectedPath);
    }
}
