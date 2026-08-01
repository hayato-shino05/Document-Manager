using System.Collections;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Interfaces;

using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class CategoryManagementModel : ModelBase
{
    private readonly IDocumentRepository _repository;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _loc;

    [ObservableProperty] private ObservableCollection<CategoryItem> _subjects = new();
    [ObservableProperty] private ObservableCollection<CategoryItem> _types = new();

    [ObservableProperty] private CategoryItem? _selectedSubject;
    [ObservableProperty] private CategoryItem? _selectedType;

    [ObservableProperty] private IList _selectedSubjects = new List<CategoryItem>();
    [ObservableProperty] private IList _selectedTypes = new List<CategoryItem>();

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private int _totalDocumentCount;

    public string StatusText => string.Format(_loc["Status_CategorySummary"], TotalDocumentCount, Subjects.Count, Types.Count);

    public CategoryManagementModel(IDocumentRepository repository, ICategoryRepository categoryRepo, IDialogService dialogService, ILocalizationService loc)
    {
        _repository = repository;
        _categoryRepo = categoryRepo;
        _dialogService = dialogService;
        _loc = loc;
        LoadData();
    }

    private void LoadData()
    {
        var subjectsData = _categoryRepo.GetSubjectsWithCount();
        Subjects = new ObservableCollection<CategoryItem>(
            subjectsData.Select(s => new CategoryItem(s.Name, s.Count)));

        var typesData = _categoryRepo.GetTypesWithCount();
        Types = new ObservableCollection<CategoryItem>(
            typesData.Select(t => new CategoryItem(t.Name, t.Count)));

        TotalDocumentCount = _categoryRepo.GetTotalDocumentCount();
        OnPropertyChanged(nameof(StatusText));
    }

    [RelayCommand]
    private async Task RenameSubjectAsync()
    {
        if (SelectedSubject == null) return;

        var subject = SelectedSubject;
        var newName = await _dialogService.ShowInputAsync(_loc["Category_RenameSubjectTitle"], _loc["Category_NewNameLabel"], subject.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == subject.Name) return;

        try
        {
            if (!_categoryRepo.UpdateSubjectName(subject.Name, newName.Trim()))
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
                return;
            }

            LoadData();
            await _dialogService.ShowMessageAsync(_loc["Dialog_Success"],
                string.Format(_loc["Category_RenameSubjectDone"], newName.Trim()));
        }
        catch
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    [RelayCommand]
    private async Task RenameTypeAsync()
    {
        if (SelectedType == null) return;

        var type = SelectedType;
        var newName = await _dialogService.ShowInputAsync(_loc["Category_RenameTypeTitle"], _loc["Category_NewNameLabel"], type.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == type.Name) return;

        try
        {
            if (!_categoryRepo.UpdateTypeName(type.Name, newName.Trim()))
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
                return;
            }

            LoadData();
            await _dialogService.ShowMessageAsync(_loc["Dialog_Success"],
                string.Format(_loc["Category_RenameTypeDone"], newName.Trim()));
        }
        catch
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    [RelayCommand]
    private async Task DeleteSubjectAsync()
    {
        var targets = SelectedSubjects.Cast<CategoryItem>().ToList();
        if (targets.Count == 0)
        {
            if (SelectedSubject == null) return;
            targets = [SelectedSubject];
        }

        int totalDocs = targets.Sum(t => t.Count);
        string namesStr = targets.Count == 1
            ? $"'{targets[0].Name}'"
            : string.Format(_loc["Category_SelectedCount"], targets.Count);

        string confirmMsg = totalDocs == 0
            ? string.Format(_loc["Category_DeleteConfirmMsg"], namesStr)
            : string.Format(_loc["Category_DeleteWithDocsMsg"], namesStr, totalDocs);

        bool confirm = await _dialogService.ShowConfirmAsync(_loc["Category_ConfirmDeleteSubject"], confirmMsg,
            _loc["Action_Delete"], isDanger: true);
        if (!confirm) return;

        try
        {
            foreach (var item in targets)
            {
                if (!_categoryRepo.DeleteDocumentsBySubject(item.Name))
                {
                    await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
                    return;
                }
            }

            LoadData();
            SelectedSubjects = new List<CategoryItem>();

            string doneMsg = targets.Count == 1
                ? string.Format(_loc["Category_DeletedSubject"], targets[0].Name)
                : string.Format(_loc["Category_DeletedSubjects"], targets.Count);
            await _dialogService.ShowMessageAsync(_loc["Dialog_Success"], doneMsg);
        }
        catch
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    [RelayCommand]
    private async Task DeleteTypeAsync()
    {
        var targets = SelectedTypes.Cast<CategoryItem>().ToList();
        if (targets.Count == 0)
        {
            if (SelectedType == null) return;
            targets = [SelectedType];
        }

        int totalDocs = targets.Sum(t => t.Count);
        string namesStr = targets.Count == 1
            ? $"'{targets[0].Name}'"
            : string.Format(_loc["Category_SelectedCount"], targets.Count);

        string confirmMsg = totalDocs == 0
            ? string.Format(_loc["Category_DeleteConfirmMsg"], namesStr)
            : string.Format(_loc["Category_DeleteWithDocsMsg"], namesStr, totalDocs);

        bool confirm = await _dialogService.ShowConfirmAsync(_loc["Category_ConfirmDeleteType"], confirmMsg,
            _loc["Action_Delete"], isDanger: true);
        if (!confirm) return;

        try
        {
            foreach (var item in targets)
            {
                if (!_categoryRepo.DeleteDocumentsByType(item.Name))
                {
                    await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
                    return;
                }
            }

            LoadData();
            SelectedTypes = new List<CategoryItem>();

            string doneMsg = targets.Count == 1
                ? string.Format(_loc["Category_DeletedType"], targets[0].Name)
                : string.Format(_loc["Category_DeletedTypes"], targets.Count);
            await _dialogService.ShowMessageAsync(_loc["Dialog_Success"], doneMsg);
        }
        catch
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    [RelayCommand]
    private async Task AddSubjectAsync()
    {
        var name = await _dialogService.ShowInputAsync(_loc["Category_AddSubjectTitle"], _loc["Category_AddSubjectLabel"], "");
        if (string.IsNullOrWhiteSpace(name)) return;

        var trimmed = name.Trim();
        if (Subjects.Any(s => s.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            await _dialogService.ShowMessageAsync(_loc["Dialog_Error"],
                string.Format(_loc["Category_AlreadyExists"], trimmed));
            return;
        }

        try
        {
            if (!_categoryRepo.AddSubject(trimmed))
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
                return;
            }

            LoadData();
            await _dialogService.ShowMessageAsync(_loc["Dialog_Success"],
                string.Format(_loc["Category_AddedSubject"], trimmed));
        }
        catch
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    [RelayCommand]
    private async Task AddTypeAsync()
    {
        var name = await _dialogService.ShowInputAsync(_loc["Category_AddTypeTitle"], _loc["Category_AddTypeLabel"], "");
        if (string.IsNullOrWhiteSpace(name)) return;

        var trimmed = name.Trim();
        if (Types.Any(t => t.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            await _dialogService.ShowMessageAsync(_loc["Dialog_Error"],
                string.Format(_loc["Category_AlreadyExists"], trimmed));
            return;
        }

        try
        {
            if (!_categoryRepo.AddType(trimmed))
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
                return;
            }

            LoadData();
            await _dialogService.ShowMessageAsync(_loc["Dialog_Success"],
                string.Format(_loc["Category_AddedType"], trimmed));
        }
        catch
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadData();
    }
}

/// <summary>
/// Category item with name and document count
/// </summary>
public class CategoryItem
{
    public string Name { get; set; }
    public int Count { get; set; }
    public string Display => $"{Name} ({Count})";

    public CategoryItem(string name, int count)
    {
        Name = name;
        Count = count;
    }
}
