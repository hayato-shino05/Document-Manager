using System.ComponentModel;
using Avalonia.Controls;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Views;

public sealed class DuplicateReviewItemViewModel : INotifyPropertyChanged
{
    private bool _isSelectedSurvivor;

    public DuplicateReviewItemViewModel(StudyDocument document, bool isDefaultSurvivor)
    {
        Document = document;
        _isSelectedSurvivor = isDefaultSurvivor;
        RadioLabel = $"ID: {document.Id} - {document.Name}";
        AutomationIdRadio = $"DuplicateReview_Radio_{document.Id}";
        FilePathDisplay = string.IsNullOrWhiteSpace(document.FilePath) ? "(未設定)" : document.FilePath;
        
        var sizeStr = document.FileSize.HasValue
            ? $"{document.FileSize.Value:F1} MB"
            : "(未設定)";

        string dateStr;
        try
        {
            if (!string.IsNullOrWhiteSpace(document.FilePath) && System.IO.File.Exists(document.FilePath))
            {
                var writeTime = System.IO.File.GetLastWriteTime(document.FilePath);
                dateStr = $"更新日: {writeTime:yyyy-MM-dd HH:mm}";
            }
            else
            {
                dateStr = $"登録日: {document.CreatedAt:yyyy-MM-dd HH:mm}";
            }
        }
        catch
        {
            dateStr = $"登録日: {document.CreatedAt:yyyy-MM-dd HH:mm}";
        }
        SizeAndDateDisplay = $"サイズ: {sizeStr} | {dateStr}";

        var subject = string.IsNullOrWhiteSpace(document.Subject) ? "(未設定)" : document.Subject;
        var type = string.IsNullOrWhiteSpace(document.Type) ? "(未設定)" : document.Type;
        CategoryAndTypeDisplay = $"{subject} / {type}";

        TagsDisplay = string.IsNullOrWhiteSpace(document.Tags)
            ? "(なし)"
            : document.Tags;

        StatusDisplay = string.IsNullOrWhiteSpace(document.Status) ? "unread" : document.Status;
    }

    public StudyDocument Document { get; }
    public string RadioLabel { get; }
    public string AutomationIdRadio { get; }
    public string FilePathDisplay { get; }
    public string SizeAndDateDisplay { get; }
    public string CategoryAndTypeDisplay { get; }
    public string TagsDisplay { get; }
    public string StatusDisplay { get; }

    public bool IsSelectedSurvivor
    {
        get => _isSelectedSurvivor;
        set
        {
            if (_isSelectedSurvivor != value)
            {
                _isSelectedSurvivor = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelectedSurvivor)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class DuplicateMergeReviewDialog : Window
{
    public int? SelectedSurvivorId { get; private set; }

    private readonly IReadOnlyList<DuplicateReviewItemViewModel> _viewModels = [];
    private readonly ILocalizationService? _loc;

    public DuplicateMergeReviewDialog()
    {
        InitializeComponent();
    }

    public DuplicateMergeReviewDialog(
        string groupName,
        string matchReason,
        IReadOnlyList<StudyDocument> candidates,
        ILocalizationService? loc = null)
    {
        InitializeComponent();
        _loc = loc;

        var items = new List<DuplicateReviewItemViewModel>();
        for (int i = 0; i < candidates.Count; i++)
        {
            items.Add(new DuplicateReviewItemViewModel(candidates[i], isDefaultSurvivor: i == 0));
        }
        _viewModels = items;

        var titleBlock = this.FindControl<TextBlock>("TitleText");
        if (titleBlock != null)
        {
            titleBlock.Text = _loc != null
                ? $"{_loc["DuplicateDetection_Title"]} - {groupName}"
                : $"重複統合レビュー - {groupName}";
        }

        var reasonBlock = this.FindControl<TextBlock>("MatchReasonText");
        if (reasonBlock != null)
        {
            reasonBlock.Text = $"判定理由: {matchReason}";
        }

        var instructionBlock = this.FindControl<TextBlock>("InstructionText");
        if (instructionBlock != null)
        {
            instructionBlock.Text = "残す文書（代表レコード）を1つ選択してください。統合対象のタグ・コレクション・個人メモ・関連文書は代表レコードへ引き継がれ、元文書は安全にゴミ箱へ移動します。";
        }

        var reversibilityBlock = this.FindControl<TextBlock>("ReversibilityNote");
        if (reversibilityBlock != null)
        {
            reversibilityBlock.Text = "※ 統合された文書は自動削除されず、ゴミ箱（Recycle Bin）または Undo から復元可能です。";
        }

        var mergeBtn = this.FindControl<Button>("MergeButton");
        if (mergeBtn != null)
        {
            mergeBtn.Content = _loc != null ? _loc["Duplicate_Merge"] : "統合を実行";
            mergeBtn.Click += (_, _) =>
            {
                var selected = _viewModels.FirstOrDefault(vm => vm.IsSelectedSurvivor) ?? _viewModels.FirstOrDefault();
                SelectedSurvivorId = selected?.Document.Id;
                Close();
            };
        }

        var cancelBtn = this.FindControl<Button>("CancelButton");
        if (cancelBtn != null)
        {
            cancelBtn.Content = _loc != null ? _loc["Action_Cancel"] : "キャンセル";
            cancelBtn.Click += (_, _) =>
            {
                SelectedSurvivorId = null;
                Close();
            };
        }

        var itemsControl = this.FindControl<ItemsControl>("CandidatesList");
        if (itemsControl != null)
        {
            itemsControl.ItemsSource = _viewModels;
        }

        Opened += (_, _) => cancelBtn?.Focus();
    }
}
