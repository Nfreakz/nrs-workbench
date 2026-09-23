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

    private async void SwitchBranch_Click(object sender, RoutedEventArgs e)
    {
        var repository = _viewModel.SelectedRepository;
        if (repository is null || !repository.CanSwitchBranch) return;
        try
        {
            var branches = (await _git.GetLocalBranchesAsync(repository))
                .Where(branch => !string.Equals(branch, repository.Branch, StringComparison.Ordinal)).ToList();
            if (branches.Count == 0)
            {
                MessageBox.Show(this, UiLanguage.Choose("No hay otras ramas locales en este repositorio.", "There are no other local branches in this repository."), UiLanguage.Choose("Cambiar rama", "Switch branch"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var picker = new System.Windows.Controls.ComboBox
            {
                ItemsSource = branches,
                SelectedIndex = 0,
                MinWidth = 310,
                Margin = new Thickness(0, 12, 0, 14)
            };
            var window = new Window
            {
                Owner = this,
                Title = UiLanguage.Choose("Cambiar rama local", "Switch local branch"),
                Width = 470,
                Height = 210,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = (System.Windows.Media.Brush)FindResource("BgBrush"),
                Foreground = (System.Windows.Media.Brush)FindResource("TextBrush")
            };
            var content = new System.Windows.Controls.StackPanel { Margin = new Thickness(24) };
            content.Children.Add(new System.Windows.Controls.TextBlock { Text = $"{repository.Name}  ·  {repository.Branch}", FontWeight = FontWeights.SemiBold });
            content.Children.Add(picker);
            var confirm = new System.Windows.Controls.Button { Content = UiLanguage.Choose("Revisar cambio", "Review switch"), Width = 130, HorizontalAlignment = HorizontalAlignment.Right };
            confirm.Click += (_, _) => window.DialogResult = true;
            content.Children.Add(confirm);
            window.Content = content;
            window.SourceInitialized += (_, _) => WindowThemeService.ApplyDarkTitleBar(window);
            if (window.ShowDialog() != true || picker.SelectedItem is not string target) return;

            if (!_dialogs.Confirm(UiLanguage.Choose($"Repositorio: {repository.Name}\nRama actual: {repository.Branch}\nNueva rama local: {target}\n\nNo se descargará ni fusionará nada. ¿Cambiar de rama?", $"Repository: {repository.Name}\nCurrent branch: {repository.Branch}\nNew local branch: {target}\n\nNothing will be downloaded or merged. Switch branch?"), UiLanguage.Choose("Confirmar cambio de rama", "Confirm branch switch"))) return;
            await _git.SwitchLocalBranchAsync(repository, target);
            await _viewModel.RefreshAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not switch local branch", ex);
            _dialogs.ShowError(ex.Message, UiLanguage.Choose("Cambiar rama", "Switch branch"));
            await _viewModel.RefreshAsync();
        }
    }

    private async void AddRepository_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = UiLanguage.Choose("Selecciona la carpeta raíz de un repositorio Git", "Select the root folder of a Git repository"),
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
            Description = UiLanguage.Choose("Selecciona una carpeta que contenga tus repositorios. Se escanearán hasta 3 niveles.", "Select a folder containing your repositories. Up to three levels will be scanned."),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        await _viewModel.ScanFolderAsync(dialog.SelectedPath);
    }
}
