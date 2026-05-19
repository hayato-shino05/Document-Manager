using StudyDocumentManager.Core.DTOs;
using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Views;

public partial class AddDocumentDialog : Window
{
    public AddDocumentDraft? Result { get; private set; }

    private readonly string _filePath;

    public AddDocumentDialog()
    {
        InitializeComponent();
        _filePath = string.Empty;
    }

    /// <summary>
    /// Má»Ÿ dialog import 1 file.
    /// subjects vÃ  types Ä‘Æ°á»£c chuáº©n bá»‹ bá»Ÿi caller (MainWindow qua IDocumentRepository).
    /// </summary>
    public AddDocumentDialog(string filePath, IList<string> subjects, IList<string> types) : this()
    {
        _filePath = filePath;

        // Äiá»n sáºµn tá»« file
        txtFilePath.Text = filePath;
        txtTen.Text = Path.GetFileNameWithoutExtension(filePath);

        // Tá»± nháº­n diá»‡n loáº¡i file
        string detectedType = StudyDocumentManager.Core.Services.FileTypeDetector.Detect(
            Path.GetExtension(filePath));

        // Chuyá»ƒn sang List Ä‘á»ƒ cÃ³ thá»ƒ thÃªm náº¿u cáº§n
        var typeList = types.ToList();

        // Äáº£m báº£o loáº¡i tá»± nháº­n diá»‡n cÃ³ trong danh sÃ¡ch
        if (!typeList.Contains(detectedType))
            typeList.Add(detectedType);

        cboMonHoc.ItemsSource = subjects;
        cboLoai.ItemsSource = typeList;

        // Chá»n sáºµn loáº¡i tá»± nháº­n diá»‡n
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

        Result = new AddDocumentDraft
        {
            Ten = name,
            MonHoc = (cboMonHoc?.SelectedItem as string ?? "").Trim(),
            Loai = (cboLoai?.SelectedItem as string ?? "").Trim(),
            DuongDan = _filePath,
            GhiChu = txtGhiChu?.Text?.Trim() ?? "",
            TacGia = txtTacGia?.Text?.Trim() ?? "",
            Tags = txtTags?.Text?.Trim() ?? "",
            QuanTrong = chkQuanTrong?.IsChecked == true,
            Deadline = dpDeadline?.SelectedDate?.DateTime
        };

        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close(false);
    }
}
