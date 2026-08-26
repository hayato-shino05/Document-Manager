using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StudyDocumentManager.Core.Entities;

/// <summary>
/// A user-selected folder the application watches for new documents.
/// The watcher never moves or deletes the source files; it only records
/// discovered files as Import Inbox entries for later confirmation.
/// </summary>
public sealed class WatchedFolder : INotifyPropertyChanged
{
    public int Id { get; set; }
    public string FolderPath { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool IncludeSubdirectories { get; set; }
    public DateTime? LastScanAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Transient runtime state (not persisted). Observable so the UI updates
    // when the model changes status/error after the item is already bound.
    private WatcherStatus _watcherStatus = WatcherStatus.Unknown;
    public WatcherStatus WatcherStatus
    {
        get => _watcherStatus;
        set
        {
            if (_watcherStatus == value) return;
            _watcherStatus = value;
            OnPropertyChanged();
        }
    }

    private string? _watcherError;
    public string? WatcherError
    {
        get => _watcherError;
        set
        {
            if (_watcherError == value) return;
            _watcherError = value;
            OnPropertyChanged();
        }
    }

    // Key of the localized error (not the rendered string) so the displayed
    // text can be recomputed when the UI language changes.
    private string? _watcherErrorKey;
    public string? WatcherErrorKey
    {
        get => _watcherErrorKey;
        set
        {
            if (_watcherErrorKey == value) return;
            _watcherErrorKey = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
