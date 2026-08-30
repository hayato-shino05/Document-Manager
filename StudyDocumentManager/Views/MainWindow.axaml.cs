using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

        // Enable drag & drop
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
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
        var hasFiles = e.Data.GetFiles()?
            .OfType<Avalonia.Platform.Storage.IStorageFile>()
            .Any() == true;

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

            var files = e.Data.GetFiles()?
                .OfType<Avalonia.Platform.Storage.IStorageFile>()
                .ToList();

            if (files == null || files.Count == 0)
            {
                vm.ShowInvalidDropStatus();
                return;
            }

            var filePaths = files
                .Select(file => file.Path.LocalPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Cast<string>()
                .ToList();

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
}
