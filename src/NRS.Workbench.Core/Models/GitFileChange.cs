using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NRS.Workbench.Core.Models;

public sealed class GitFileChange : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public string Path { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public char IndexStatus { get; set; } = ' ';
    public char WorkTreeStatus { get; set; } = ' ';
    public bool IsUntracked { get; set; }
    public bool IsConflict { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public bool IsStaged => !IsUntracked && IndexStatus != ' ' && IndexStatus != '?';
    public bool HasUnstagedChanges => IsUntracked || (WorkTreeStatus != ' ' && WorkTreeStatus != '?');
    public bool IsRename => IndexStatus is 'R' or 'C' || WorkTreeStatus is 'R' or 'C';

    public string DisplayPath => string.IsNullOrWhiteSpace(OriginalPath)
        ? Path
        : $"{OriginalPath} → {Path}";

    public string StatusCode => IsUntracked ? "??" : $"{Normalize(IndexStatus)}{Normalize(WorkTreeStatus)}";

    public string StatusLabel
    {
        get
        {
            if (IsConflict) return "CONFLICTO";
            if (IsUntracked) return "NUEVO";
            if (IsRename) return "RENOMBRADO";
            if (IndexStatus == 'D' || WorkTreeStatus == 'D') return "ELIMINADO";
            if (IndexStatus == 'A' || WorkTreeStatus == 'A') return "AÑADIDO";
            if (IsStaged && HasUnstagedChanges) return "STAGED + CAMBIOS";
            if (IsStaged) return "STAGED";
            return "MODIFICADO";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static char Normalize(char value) => value == ' ' ? '.' : value;
}
