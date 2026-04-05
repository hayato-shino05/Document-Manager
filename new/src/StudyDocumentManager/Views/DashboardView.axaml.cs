using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using StudyDocumentManager.ViewModels;

namespace StudyDocumentManager.Views;

public partial class DashboardView : UserControl
{
    private bool _syncing;
    private bool _initialized;

    public DashboardView()
    {
        InitializeComponent();

        var dgv = this.FindControl<DataGrid>("dgvDocuments");
        var cboSubject = this.FindControl<ComboBox>("cboSubject");
        var cboType = this.FindControl<ComboBox>("cboType");

        // Double-click to open file
        if (dgv != null)
        {
            dgv.DoubleTapped += (s, e) =>
            {
                if (DataContext is DashboardViewModel vm && vm.SelectedDocument != null)
                    vm.OpenFileCommand.Execute(null);
            };

            dgv.SelectionChanged += (s, e) =>
            {
                if (_syncing) return;
                if (DataContext is DashboardViewModel vm)
                    vm.SelectedDocument = dgv.SelectedItem as Core.Entities.StudyDocument;
            };
        }

        if (cboSubject != null)
        {
            cboSubject.SelectionChanged += (s, e) =>
            {
                if (_syncing) return;
                if (DataContext is DashboardViewModel vm && cboSubject.SelectedItem is string sel)
                    vm.SelectedSubject = sel;
            };
        }

        if (cboType != null)
        {
            cboType.SelectionChanged += (s, e) =>
            {
                if (_syncing) return;
                if (DataContext is DashboardViewModel vm && cboType.SelectedItem is string sel)
                    vm.SelectedType = sel;
            };
        }

        // Use AttachedToVisualTree — fires AFTER the control is added to visual tree
        // but BEFORE layout. We then post with Loaded priority to run after layout.
        AttachedToVisualTree += (s, e) =>
        {
            if (_initialized) return;
            _initialized = true;

            // Use a timer to absolutely guarantee we run AFTER initial layout
            var timer = new System.Timers.Timer(100);
            timer.AutoReset = false;
            timer.Elapsed += (_, _) =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    InitializeData(dgv, cboSubject, cboType);
                });
            };
            timer.Start();
        };
    }

    private void InitializeData(DataGrid? dgv, ComboBox? cboSubject, ComboBox? cboType)
    {
        if (DataContext is not DashboardViewModel vm) return;

        // Subscribe to ViewModel property changes
        vm.PropertyChanged += (sender, args) => OnVmPropertyChanged(sender, args, dgv, cboSubject, cboType);

        // Initialize data (populates vm.Documents, vm.Subjects, vm.Types)
        vm.Initialize();

        // Sync UI controls
        SyncAll(vm, dgv, cboSubject, cboType);
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs args,
        DataGrid? dgv, ComboBox? cboSubject, ComboBox? cboType)
    {
        if (sender is not DashboardViewModel vm) return;
        if (_syncing) return;

        _syncing = true;
        try
        {
            switch (args.PropertyName)
            {
                case nameof(DashboardViewModel.Documents):
                    if (dgv != null) dgv.ItemsSource = vm.Documents;
                    break;
                case nameof(DashboardViewModel.Subjects):
                    SyncComboBox(cboSubject, vm.Subjects, vm.SelectedSubject);
                    break;
                case nameof(DashboardViewModel.Types):
                    SyncComboBox(cboType, vm.Types, vm.SelectedType);
                    break;
                case nameof(DashboardViewModel.SelectedSubject):
                    if (cboSubject != null) cboSubject.SelectedItem = vm.SelectedSubject;
                    break;
                case nameof(DashboardViewModel.SelectedType):
                    if (cboType != null) cboType.SelectedItem = vm.SelectedType;
                    break;
            }
        }
        finally { _syncing = false; }
    }

    private void SyncAll(DashboardViewModel vm, DataGrid? dgv, ComboBox? cboSubject, ComboBox? cboType)
    {
        _syncing = true;
        try
        {
            if (dgv != null) dgv.ItemsSource = vm.Documents;
            SyncComboBox(cboSubject, vm.Subjects, vm.SelectedSubject);
            SyncComboBox(cboType, vm.Types, vm.SelectedType);
        }
        finally { _syncing = false; }
    }

    private static void SyncComboBox(ComboBox? cbo, System.Collections.IEnumerable items, object? selectedItem)
    {
        if (cbo == null) return;
        cbo.ItemsSource = items;
        cbo.SelectedItem = selectedItem;
    }
}
