using StudyDocumentManager.Core;
using StudyDocumentManager.Core.DTOs;
using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using StudyDocumentManager.Core.Interfaces;
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
        Opened += (_, _) => txtTen?.Focus();
    }

    public AddDocumentDialog(string filePath, IList<string> subjects, IList<string> types) : this()
    {
        _filePath = filePath;

        txtFilePath.Text = filePath;
        txtTen.Text = Path.GetFileNameWithoutExtension(filePath);

        string detectedType = StudyDocumentManager.Core.Services.FileTypeDetector.Detect(
            Path.GetExtension(filePath));

        var typeList = types.ToList();

        if (!typeList.Contains(detectedType))
            typeList.Add(detectedType);

        cboMonHoc.ItemsSource = subjects;
        cboLoai.ItemsSource = typeList;

        cboLoai.SelectedItem = detectedType;
    }


    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var name = txtTen?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            txtNameError.Text = GetNameRequiredMessage();
            txtNameError.IsVisible = true;
            txtTen?.Focus();
            return;
        }

        txtNameError.IsVisible = false;
        txtNameError.Text = string.Empty;

        Result = new AddDocumentDraft
        {
            Name = name,
            Subject = (cboMonHoc?.SelectedItem as string ?? "").Trim(),
            Type = (cboLoai?.SelectedItem as string ?? "").Trim(),
            FilePath = _filePath,
            Notes = txtGhiChu?.Text?.Trim() ?? "",
            Author = txtTacGia?.Text?.Trim() ?? "",
            Tags = txtTags?.Text?.Trim() ?? "",
            IsImportant = chkQuanTrong?.IsChecked == true,
            Deadline = dpDeadline?.SelectedDate?.DateTime
        };

        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close(false);
    }

    private static string GetNameRequiredMessage()
    {
        return (Application.Current?.Resources["Loc"] as ILocalizationService)?["AddEdit_NameRequired"]
            ?? "AddEdit_NameRequired";
    }
}
