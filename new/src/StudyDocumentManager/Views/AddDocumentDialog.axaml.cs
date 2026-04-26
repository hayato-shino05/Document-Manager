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
    /// Mở dialog import 1 file.
    /// subjects và types được chuẩn bị bởi caller (MainWindow qua IDocumentRepository).
    /// </summary>
    public AddDocumentDialog(string filePath, IList<string> subjects, IList<string> types) : this()
    {
        _filePath = filePath;

        // Điền sẵn từ file
        txtFilePath.Text = filePath;
        txtTen.Text = Path.GetFileNameWithoutExtension(filePath);

        // Tự nhận diện loại file
        string detectedType = StudyDocumentManager.Services.FileTypeDetector.Detect(
            Path.GetExtension(filePath));

        // Chuyển sang List để có thể thêm nếu cần
        var typeList = types.ToList();

        // Đảm bảo loại tự nhận diện có trong danh sách
        if (!typeList.Contains(detectedType))
            typeList.Add(detectedType);

        cboMonHoc.ItemsSource = subjects;
        cboLoai.ItemsSource = typeList;

        // Chọn sẵn loại tự nhận diện
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
