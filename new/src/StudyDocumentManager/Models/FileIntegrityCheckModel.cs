using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class FileIntegrityCheckModel : ModelBase
{
    private readonly IDocumentRepository _repository;
    private readonly IDialogService _dialogService;

    [ObservableProperty] private ObservableCollection<IntegrityResult> _results = new();
    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private int _totalChecked;
    [ObservableProperty] private int _missingCount;
    [ObservableProperty] private string _statusText = "Nhấn 'Quét' để kiểm tra tính toàn vẹn file.";

    public FileIntegrityCheckModel(IDocumentRepository repository, IDialogService dialogService)
    {
        _repository = repository;
        _dialogService = dialogService;
    }

    [RelayCommand]
    private async Task CheckIntegrityAsync()
    {
        IsChecking = true;
        Results.Clear();
        TotalChecked = 0;
        MissingCount = 0;

        var docs = _repository.GetAll();
        foreach (var doc in docs)
        {
            TotalChecked++;
            if (!string.IsNullOrEmpty(doc.DuongDan) && !File.Exists(doc.DuongDan))
            {
                MissingCount++;
                Results.Add(new IntegrityResult
                {
                    Document = doc,
                    FilePath = doc.DuongDan,
                    Status = "❌ File không tồn tại"
                });
            }
        }

        IsChecking = false;
        StatusText = $"Hoàn thành! Tìm thấy {MissingCount}/{TotalChecked} file bị thiếu.";

        if (MissingCount == 0)
        {
            await _dialogService.ShowMessageAsync("Kết quả", $"Đã kiểm tra {TotalChecked} tài liệu. Tất cả file đều tồn tại! ✅");
        }
    }

    // ═══ Per-item actions (matching legacy WinForms) ═══

    /// <summary>
    /// Select a new file to replace the missing one - updates path in DB.
    /// </summary>
    [RelayCommand]
    private async Task SelectNewFileAsync(IntegrityResult? item)
    {
        if (item == null) return;

        var newPath = await _dialogService.ShowOpenFileAsync("Chọn file mới",
            "Tất cả file (*.*)|*.*|PDF (*.pdf)|*.pdf|Word (*.docx;*.doc)|*.docx;*.doc|Excel (*.xlsx)|*.xlsx");
        if (string.IsNullOrWhiteSpace(newPath)) return;

        if (DatabaseHelper.UpdateDocumentPath(item.Document.Id, newPath))
        {
            Results.Remove(item);
            MissingCount--;
            StatusText = $"File thiếu: {MissingCount}";
            await _dialogService.ShowMessageAsync("Thành công", "Đã cập nhật đường dẫn file!");
        }
    }

    /// <summary>
    /// Clear the file path but keep metadata.
    /// </summary>
    [RelayCommand]
    private async Task ClearFilePathAsync(IntegrityResult? item)
    {
        if (item == null) return;

        var confirmed = await _dialogService.ShowConfirmAsync("Xác nhận",
            "Bạn có chắc muốn xóa đường dẫn file?\nMetadata tài liệu sẽ được giữ lại (đường dẫn sẽ rỗng).");
        if (!confirmed) return;

        if (DatabaseHelper.ClearDocumentPath(item.Document.Id))
        {
            Results.Remove(item);
            MissingCount--;
            StatusText = $"File thiếu: {MissingCount}";
        }
    }

    /// <summary>
    /// Soft-delete a single document with missing file.
    /// </summary>
    [RelayCommand]
    private async Task DeleteDocumentAsync(IntegrityResult? item)
    {
        if (item == null) return;

        var confirmed = await _dialogService.ShowConfirmAsync("Xác nhận xóa",
            $"Bạn có chắc muốn xóa tài liệu:\n\"{item.Document.Ten}\"?\n\n(Có thể khôi phục từ Thùng rác)",
            "Xoá", isDanger: true);
        if (!confirmed) return;

        if (_repository.Delete(item.Document.Id))
        {
            Results.Remove(item);
            MissingCount--;
            StatusText = $"File thiếu: {MissingCount}";
        }
    }

    /// <summary>
    /// Remove ALL documents with missing files (bulk soft-delete).
    /// </summary>
    [RelayCommand]
    private async Task RemoveMissingAsync()
    {
        if (Results.Count == 0) return;

        var confirmed = await _dialogService.ShowConfirmAsync("Xác nhận",
            $"Xóa {Results.Count} tài liệu có file bị mất khỏi cơ sở dữ liệu?\n(Có thể khôi phục từ Thùng rác)",
            "Xoá tất cả", isDanger: true);
        if (!confirmed) return;

        int removed = 0;
        foreach (var result in Results.ToList())
        {
            if (_repository.Delete(result.Document.Id))
            {
                removed++;
                Results.Remove(result);
            }
        }

        MissingCount = Results.Count;
        StatusText = $"Đã di chuyển {removed} tài liệu vào Thùng rác.";
        await _dialogService.ShowMessageAsync("Hoàn tất", $"Đã di chuyển {removed} tài liệu vào Thùng rác.");
    }
}

public class IntegrityResult
{
    public StudyDocument Document { get; set; } = new();
    public string FilePath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
