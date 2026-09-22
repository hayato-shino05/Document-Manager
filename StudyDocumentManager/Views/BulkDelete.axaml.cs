using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using StudyDocumentManager.Models;

namespace StudyDocumentManager.Views;

public partial class BulkDelete : UserControl
{
    private bool _syncing;

    public BulkDelete()
    {
        InitializeComponent();

        // Defer data population until after layout pass completes
        Loaded += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (DataContext is BulkDeleteModel vm)
                {
                    vm.Initialize();
                    SyncComboBoxes(vm);
                    SyncDataGrid(vm);

                    // Watch for property changes to keep UI in sync
                    vm.PropertyChanged += OnViewModelPropertyChanged;

                    // Wire ComboBox selection events
                    var cboSubject = this.FindControl<ComboBox>("cboSubject")!;
                    var cboType = this.FindControl<ComboBox>("cboType")!;
                    var cboNewSubject = this.FindControl<ComboBox>("cboNewSubject")!;

                    cboSubject.SelectionChanged += (_, _) =>
                    {
                        if (_syncing || cboSubject.SelectedItem is not string s) return;
                        vm.SelectedSubject = s;
                    };

                    cboType.SelectionChanged += (_, _) =>
                    {
                        if (_syncing || cboType.SelectedItem is not string t) return;
                        vm.SelectedType = t;
                    };

                    cboNewSubject.SelectionChanged += (_, _) =>
                    {
                        if (_syncing || cboNewSubject.SelectedItem is not string ns) return;
                        vm.NewSubjectValue = ns;
                    };
                }
            }, DispatcherPriority.Background);
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not BulkDeleteModel vm) return;

        switch (e.PropertyName)
        {
            case nameof(vm.Subjects):
            case nameof(vm.Types):
            case nameof(vm.AvailableSubjects):
                SyncComboBoxes(vm);
                break;
            case nameof(vm.Documents):
                SyncDataGrid(vm);
                break;
        }
    }

    private void SyncComboBoxes(BulkDeleteModel vm)
    {
        _syncing = true;
        try
        {
            var cboSubject = this.FindControl<ComboBox>("cboSubject")!;
            var cboType = this.FindControl<ComboBox>("cboType")!;
            var cboNewSubject = this.FindControl<ComboBox>("cboNewSubject")!;

            cboSubject.ItemsSource = vm.Subjects;
            cboSubject.SelectedItem = vm.SelectedSubject;

            cboType.ItemsSource = vm.Types;
            cboType.SelectedItem = vm.SelectedType;

            cboNewSubject.ItemsSource = vm.AvailableSubjects;
            cboNewSubject.SelectedItem = vm.NewSubjectValue;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SyncDataGrid(BulkDeleteModel vm)
    {
        var dg = this.FindControl<DataGrid>("dgDocuments")!;
        dg.ItemsSource = vm.Documents;
    }
}
