using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using StudyDocumentManager.Models;

namespace StudyDocumentManager.Views;

public partial class MainWindow : Window
{
    private readonly Func<OnboardingModel>? _onboardingModelFactory;

    public MainWindow() : this(null)
    {
    }

    public MainWindow(Func<OnboardingModel>? onboardingModelFactory)
    {
        _onboardingModelFactory = onboardingModelFactory;
        InitializeComponent();
        Opened += OnOpened;
        Closed += OnClosed;

        // Enable drag & drop with tunnel and bubble strategies across platforms (Windows & Linux)
        AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Bubble | RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DragOverEvent, OnDragOver, RoutingStrategies.Bubble | RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DragEnterEvent, OnDragOver, RoutingStrategies.Bubble | RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(this, true);
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowModel model || _onboardingModelFactory is null)
            return;

        model.HelpRequested += OnHelpRequested;
        var onboarding = _onboardingModelFactory();
        if (onboarding.ShouldShow)
            await ShowOnboardingAsync(onboarding);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowModel model)
            model.HelpRequested -= OnHelpRequested;
    }

    private async void OnHelpRequested(object? sender, EventArgs e)
    {
        if (_onboardingModelFactory is not null)
            await ShowOnboardingAsync(_onboardingModelFactory());
    }

    private async Task ShowOnboardingAsync(OnboardingModel model)
    {
        var dialog = new OnboardingDialog(model);
        await dialog.ShowDialog(this);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (DataContext is MainWindowModel { CurrentView: AddEditModel addEdit }
            && e.KeyModifiers == KeyModifiers.Control
            && e.Key == Key.S)
        {
            addEdit.SaveCommand.Execute(null);
            e.Handled = true;
            return;
        }

        var vm = DataContext as MainWindowModel;
        if (vm?.CurrentView is DashboardModel dashboard)
        {
            if (e.KeyModifiers == KeyModifiers.Control)
            {
                switch (e.Key)
                {
                    case Key.N:
                        dashboard.AddDocumentCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.F:
                        dashboard.ToggleAdvancedFilterCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.E:
                        dashboard.ExportCsvCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.O:
                        dashboard.OpenFileCommand.Execute(null);
                        e.Handled = true;
                        return;
                }
            }
            else if (e.KeyModifiers == KeyModifiers.None)
            {
                switch (e.Key)
                {
                    case Key.F5:
                        dashboard.RefreshCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.Delete:
                        dashboard.DeleteDocumentCommand.Execute(null);
                        e.Handled = true;
                        return;
                }
            }
        }

        base.OnKeyDown(e);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var hasFiles = e.Data.Contains(DataFormats.Files)
            || e.Data.Contains(DataFormats.FileNames)
            || e.Data.Contains("text/uri-list")
            || GetFilePathsFromDataObject(e.Data).Count > 0;

        if (DataContext is not MainWindowModel vm || !hasFiles)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.DragEffects = vm.CanAcceptDroppedFiles
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        try
        {
            if (DataContext is not MainWindowModel vm)
                return;

            var filePaths = GetFilePathsFromDataObject(e.Data);

            if (filePaths.Count == 0)
            {
                vm.ShowInvalidDropStatus();
                return;
            }

            await vm.HandleDroppedFilesAsync(filePaths);
        }
        catch (IOException)
        {
            if (DataContext is MainWindowModel vm)
                vm.ShowInvalidDropStatus();
        }
        catch (UnauthorizedAccessException)
        {
            if (DataContext is MainWindowModel vm)
                vm.ShowInvalidDropStatus();
        }
        catch
        {
            if (DataContext is MainWindowModel vm)
                vm.ShowInvalidDropStatus();
        }
        finally
        {
            e.Handled = true;
        }
    }

    internal static List<string> GetFilePathsFromDataObject(IDataObject? data)
    {
        if (data == null)
            return [];

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Storage items (IStorageItem / IStorageFile)
        try
        {
            var files = data.GetFiles();
            if (files != null)
            {
                foreach (var file in files)
                {
                    if (file == null) continue;
                    var localPath = file.TryGetLocalPath();
                    if (string.IsNullOrEmpty(localPath) && file.Path != null)
                    {
                        var rawPath = file.Path.IsAbsoluteUri ? file.Path.LocalPath : file.Path.ToString();
                        if (rawPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase) &&
                            Uri.TryCreate(rawPath, UriKind.Absolute, out var uri))
                        {
                            localPath = Uri.UnescapeDataString(uri.LocalPath);
                        }
                        else
                        {
                            localPath = rawPath;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(localPath))
                    {
                        localPath = Uri.UnescapeDataString(localPath);
                        result.Add(Path.GetFullPath(localPath));
                    }
                }
            }
        }
        catch { }

        // 2. FileNames / String paths (Standard Linux / Windows XDnD formats)
        try
        {
#pragma warning disable CS0618
            var fileNames = data.GetFileNames();
#pragma warning restore CS0618
            if (fileNames != null)
            {
                foreach (var fileName in fileNames)
                {
                    if (string.IsNullOrWhiteSpace(fileName)) continue;
                    var path = fileName.Trim();
                    if (path.StartsWith("file://", StringComparison.OrdinalIgnoreCase) &&
                        Uri.TryCreate(path, UriKind.Absolute, out var uri))
                    {
                        path = Uri.UnescapeDataString(uri.LocalPath);
                    }
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        path = Uri.UnescapeDataString(path);
                        result.Add(Path.GetFullPath(path));
                    }
                }
            }
        }
        catch { }

        // 3. text/uri-list MIME type (Standard Linux GNOME / KDE / XFCE file manager drag source)
        try
        {
            if (data.Contains("text/uri-list"))
            {
                var uriListData = data.Get("text/uri-list");
                var uriListText = uriListData switch
                {
                    string s => s,
                    byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
                    IEnumerable<string> lines => string.Join("\n", lines),
                    _ => uriListData?.ToString()
                };

                if (!string.IsNullOrWhiteSpace(uriListText))
                {
                    var lines = uriListText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                    foreach (var rawLine in lines)
                    {
                        var line = rawLine.Trim();
                        if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line))
                            continue;

                        if (line.StartsWith("file://", StringComparison.OrdinalIgnoreCase) &&
                            Uri.TryCreate(line, UriKind.Absolute, out var uri))
                        {
                            var localPath = Uri.UnescapeDataString(uri.LocalPath);
                            result.Add(Path.GetFullPath(localPath));
                        }
                        else if (Path.IsPathFullyQualified(line) || File.Exists(line) || Directory.Exists(line))
                        {
                            result.Add(Path.GetFullPath(line));
                        }
                    }
                }
            }
        }
        catch { }

        // 4. Text / Plain URI fallback
        try
        {
            var text = data.GetText();
            if (!string.IsNullOrWhiteSpace(text) && text.Contains("file://", StringComparison.OrdinalIgnoreCase))
            {
                var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (line.StartsWith("file://", StringComparison.OrdinalIgnoreCase) &&
                        Uri.TryCreate(line, UriKind.Absolute, out var uri))
                    {
                        var localPath = Uri.UnescapeDataString(uri.LocalPath);
                        result.Add(Path.GetFullPath(localPath));
                    }
                }
            }
        }
        catch { }

        return result.ToList();
    }
}
