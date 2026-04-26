using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class DuplicateDetectionModel : ModelBase
{
    private readonly IDocument _repository;
    private readonly IDialogService _dialogService;

    [ObservableProperty] private ObservableCollection<DuplicateGroup> _duplicateGroups = new();
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private int _totalGroups;

    public DuplicateDetectionModel(IDocument repository, IDialogService dialogService)
    {
        _repository = repository;
        _dialogService = dialogService;
    }

    [RelayCommand]
    private async Task ScanDuplicatesAsync()
    {
        IsScanning = true;
        DuplicateGroups.Clear();

        var docs = _repository.GetAll();

        // Group by name (case-insensitive)
        var groups = docs
            .GroupBy(d => d.Ten.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in groups)
        {
            DuplicateGroups.Add(new DuplicateGroup
            {
                GroupName = group.First().Ten,
                Documents = new ObservableCollection<StudyDocument>(group.ToList()),
                Count = group.Count()
            });
        }

        TotalGroups = DuplicateGroups.Count;
        IsScanning = false;

        if (TotalGroups == 0)
        {
            await _dialogService.ShowMessageAsync("Kết quả", "Không tìm thấy tài liệu trùng lặp! ✅");
        }
    }

    [RelayCommand]
    private async Task DeleteDuplicateAsync(StudyDocument? doc)
    {
        if (doc == null) return;

        var confirmed = await _dialogService.ShowConfirmAsync("Xác nhận",
            $"Xóa tài liệu '{doc.Ten}' (ID: {doc.Id})?",
            "Xoá", isDanger: true);
        if (!confirmed) return;

        _repository.Delete(doc.Id);

        // Refresh scan
        await ScanDuplicatesAsync();
    }
}

public class DuplicateGroup
{
    public string GroupName { get; set; } = string.Empty;
    public ObservableCollection<StudyDocument> Documents { get; set; } = new();
    public int Count { get; set; }
}
