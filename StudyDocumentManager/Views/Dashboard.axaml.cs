using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using StudyDocumentManager.Models;

namespace StudyDocumentManager.Views;

public partial class Dashboard : UserControl
{
    private bool _syncing;
    private bool _initialized;

    public Dashboard()
    {
        InitializeComponent();

        var dgv = this.FindControl<DataGrid>("dgvDocuments");
        var cboSubject = this.FindControl<ComboBox>("cboSubject");
        var cboType = this.FindControl<ComboBox>("cboType");
        var cboStatus = this.FindControl<ComboBox>("cboStatus");

        // Double-click to open file
        if (dgv != null)
        {
            dgv.DoubleTapped += (s, e) =>
            {
                if (DataContext is DashboardModel vm && vm.SelectedDocument != null)
                    vm.OpenFileCommand.Execute(null);
            };

            dgv.SelectionChanged += (s, e) =>
            {
                if (_syncing) return;
                if (DataContext is DashboardModel vm)
                    vm.SelectedDocument = dgv.SelectedItem as Core.Entities.StudyDocument;
            };
        }

        if (cboSubject != null)
        {
            cboSubject.SelectionChanged += (s, e) =>
            {
                if (_syncing) return;
                if (DataContext is DashboardModel vm && cboSubject.SelectedItem is string sel)
                    vm.SelectedSubject = sel;
            };
        }

        if (cboType != null)
        {
            cboType.SelectionChanged += (s, e) =>
            {
                if (_syncing) return;
                if (DataContext is DashboardModel vm && cboType.SelectedItem is string sel)
                    vm.SelectedType = sel;
            };
        }

        if (cboStatus != null)
        {
            cboStatus.SelectionChanged += (s, e) =>
            {
                if (_syncing) return;
                if (DataContext is DashboardModel vm && cboStatus.SelectedItem is StatusOption option)
                    vm.SelectedStatus = option.Value;
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
                    InitializeData(dgv, cboSubject, cboType, cboStatus);
                });
            };
            timer.Start();
        };
    }

    private void InitializeData(DataGrid? dgv, ComboBox? cboSubject, ComboBox? cboType, ComboBox? cboStatus)
    {
        if (DataContext is not DashboardModel vm) return;

        // Subscribe to ViewModel property changes
        vm.PropertyChanged += (sender, args) => OnVmPropertyChanged(sender, args, dgv, cboSubject, cboType, cboStatus);

        // Initialize data (populates vm.Documents, vm.Subjects, vm.Types)
        vm.Initialize();

        // Sync UI controls
        SyncAll(vm, dgv, cboSubject, cboType, cboStatus);
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs args,
        DataGrid? dgv, ComboBox? cboSubject, ComboBox? cboType, ComboBox? cboStatus)
    {
        if (sender is not DashboardModel vm) return;
        if (_syncing) return;

        _syncing = true;
        try
        {
            switch (args.PropertyName)
            {
                case nameof(DashboardModel.Documents):
                    if (dgv != null)
                    {
                        dgv.ItemsSource = vm.Documents;
                        dgv.SelectedItem = vm.SelectedDocument;
                    }
                    break;
                case nameof(DashboardModel.SelectedDocument):
                    if (dgv != null) dgv.SelectedItem = vm.SelectedDocument;
                    break;
                case nameof(DashboardModel.Subjects):
                    SyncComboBox(cboSubject, vm.Subjects, vm.SelectedSubject);
                    break;
                case nameof(DashboardModel.Types):
                    SyncComboBox(cboType, vm.Types, vm.SelectedType);
                    break;
                case nameof(DashboardModel.SelectedSubject):
                    if (cboSubject != null) cboSubject.SelectedItem = vm.SelectedSubject;
                    break;
                case nameof(DashboardModel.SelectedType):
                    if (cboType != null) cboType.SelectedItem = vm.SelectedType;
                    break;
                case nameof(DashboardModel.StatusOptions):
                    SyncStatusComboBox(cboStatus, vm.StatusOptions, vm.SelectedStatus);
                    break;
                case nameof(DashboardModel.SelectedStatus):
                    if (cboStatus != null)
                        cboStatus.SelectedItem = FindStatusOption(vm.StatusOptions, vm.SelectedStatus);
                    break;
            }
        }
        finally { _syncing = false; }
    }

    private void SyncAll(DashboardModel vm, DataGrid? dgv, ComboBox? cboSubject, ComboBox? cboType, ComboBox? cboStatus)
    {
        _syncing = true;
        try
        {
            if (dgv != null) dgv.ItemsSource = vm.Documents;
            SyncComboBox(cboSubject, vm.Subjects, vm.SelectedSubject);
            SyncComboBox(cboType, vm.Types, vm.SelectedType);
            SyncStatusComboBox(cboStatus, vm.StatusOptions, vm.SelectedStatus);
        }
        finally { _syncing = false; }
    }

    private static void SyncComboBox(ComboBox? cbo, System.Collections.IEnumerable items, object? selectedItem)
    {
        if (cbo == null) return;
        cbo.ItemsSource = items;
        cbo.SelectedItem = selectedItem;
    }

    private static void SyncStatusComboBox(ComboBox? cbo, List<StatusOption> options, string selectedValue)
    {
        if (cbo == null) return;
        cbo.ItemsSource = options;
        cbo.SelectedItem = FindStatusOption(options, selectedValue);
    }

    private static StatusOption? FindStatusOption(List<StatusOption> options, string value)
        => options.FirstOrDefault(o => o.Value == value);
}
