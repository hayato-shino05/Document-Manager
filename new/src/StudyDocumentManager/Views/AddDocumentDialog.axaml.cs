using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Views;

public partial class AddDocumentDialog : Window
{
    public StudyDocument? Result { get; private set; }

    private readonly string _filePath;

    public AddDocumentDialog()
    {
        InitializeComponent();
        _filePath = string.Empty;
    }

    public AddDocumentDialog(string filePath) : this()
    {
        _filePath = filePath;

        // Pre-fill from file
        txtFilePath.Text = filePath;
        txtTen.Text = Path.GetFileNameWithoutExtension(filePath);

        // Auto-detect type
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        string detectedType = ext switch
        {
            ".pdf" => "Tài liệu",
            ".doc" or ".docx" => "Tài liệu",
            ".ppt" or ".pptx" => "Tài liệu",
            ".xls" or ".xlsx" => "Tài liệu",
            ".txt" => "Tài liệu",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "Hình ảnh",
            ".mp4" or ".avi" or ".mkv" or ".mov" => "Video",
            ".mp3" or ".wav" or ".flac" => "Audio",
            ".zip" or ".rar" or ".7z" => "Nén",
            _ => ext.TrimStart('.').ToUpperInvariant()
        };

        // Load dropdowns from lookup tables
        var subjects = DatabaseHelper.GetAllSubjects();
        var types = DatabaseHelper.GetAllTypes();

        // Seed default values if DB is empty
        if (subjects.Count == 0)
            subjects = new List<string> { "Công việc", "Cá nhân", "Học tập", "Dự án", "Tài chính", "Hợp đồng", "Tham khảo", "Khác" };
        if (types.Count == 0)
            types = new List<string> { "Tài liệu", "Báo cáo", "Hướng dẫn", "Biểu mẫu", "Hình ảnh", "Video", "Audio", "Nén", "Khác" };

        cboMonHoc.ItemsSource = subjects;
        cboLoai.ItemsSource = types;

        // Set detected type in dropdown
        if (types.Contains(detectedType))
            cboLoai.SelectedItem = detectedType;
        else
            cboLoai.SelectedItem = types.FirstOrDefault();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var name = txtTen?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            txtTen?.Focus();
            return;
        }

        double? fileSize = null;
        try
        {
            if (File.Exists(_filePath))
                fileSize = new FileInfo(_filePath).Length / (1024.0 * 1024.0);
        }
        catch { /* ignore */ }

        Result = new StudyDocument
        {
            Ten = name,
            MonHoc = (cboMonHoc?.SelectedItem as string ?? "").Trim(),
            Loai = (cboLoai?.SelectedItem as string ?? "").Trim(),
            DuongDan = _filePath,
            GhiChu = txtGhiChu?.Text?.Trim() ?? "",
            TacGia = txtTacGia?.Text?.Trim() ?? "",
            Tags = txtTags?.Text?.Trim() ?? "",
            QuanTrong = chkQuanTrong?.IsChecked == true,
            Deadline = dpDeadline?.SelectedDate?.DateTime,
            KichThuoc = fileSize
        };

        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close(false);
    }
}
