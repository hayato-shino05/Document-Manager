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

        // Auto-detect type using shared helper
        string detectedType = StudyDocumentManager.Services.FileTypeDetector.Detect(
            Path.GetExtension(filePath));

        // Load dropdowns from lookup tables
        var subjects = DatabaseHelper.GetAllSubjects();
        var types = DatabaseHelper.GetAllTypes();

        // Seed default values if DB is empty
        if (subjects.Count == 0)
            subjects = new List<string> { "Công việc", "Cá nhân", "Học tập", "Dự án", "Tài chính", "Hợp đồng", "Tham khảo", "Khác" };
        if (types.Count == 0)
            types = new List<string> { "PDF", "Word", "Excel", "PowerPoint", "Tài liệu", "Báo cáo", "Hướng dẫn", "Biểu mẫu", "Hình ảnh", "Video", "Audio", "Nén", "Khác" };

        // Ensure detected type is in dropdown list
        if (!types.Contains(detectedType))
            types.Add(detectedType);

        cboMonHoc.ItemsSource = subjects;
        cboLoai.ItemsSource = types;

        // Set detected type in dropdown
        cboLoai.SelectedItem = detectedType;
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
