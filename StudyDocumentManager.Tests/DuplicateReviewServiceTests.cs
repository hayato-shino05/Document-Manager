using StudyDocumentManager.Core.DTOs;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Repositories;
using StudyDocumentManager.Data.Services;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class DuplicateReviewServiceTests : DatabaseTestBase
{
    private readonly DuplicateReviewService _service;
    private readonly DocumentRepository _repo;

    public DuplicateReviewServiceTests()
    {
        _service = new DuplicateReviewService(Db);
        _repo = new DocumentRepository(Db);
    }

    [Fact]
    public void DetectDuplicates_ExactFilePath_GroupsWithExactPathReason()
    {
        var doc1 = new StudyDocument { Name = "Document Alpha", FilePath = @"C:\docs\report.pdf", Type = "PDF" };
        var doc2 = new StudyDocument { Name = "Document Beta", FilePath = @"C:\docs\report.pdf", Type = "PDF" };
        var doc3 = new StudyDocument { Name = "Different", FilePath = @"C:\docs\other.pdf", Type = "PDF" };

        var groups = _service.DetectDuplicates([doc1, doc2, doc3]);

        var group = Assert.Single(groups);
        Assert.Equal(DuplicateMatchReason.ExactPath, group.Reason);
        Assert.Equal(2, group.Candidates.Count);
        Assert.Contains(group.Candidates, d => d.Name == "Document Alpha");
        Assert.Contains(group.Candidates, d => d.Name == "Document Beta");
    }

    [Fact]
    public void DetectDuplicates_ExactName_GroupsWithExactNameReason()
    {
        var doc1 = new StudyDocument { Name = "Machine Learning", FilePath = @"C:\docs\ml1.pdf" };
        var doc2 = new StudyDocument { Name = "machine learning", FilePath = @"C:\docs\ml2.pdf" };

        var groups = _service.DetectDuplicates([doc1, doc2]);

        var group = Assert.Single(groups);
        Assert.Equal(DuplicateMatchReason.ExactName, group.Reason);
        Assert.Equal(2, group.Candidates.Count);
    }

    [Fact]
    public void DetectDuplicates_IgnoresSoftDeletedDocuments()
    {
        var doc1 = new StudyDocument { Name = "Active Doc", FilePath = @"C:\docs\doc.pdf" };
        var doc2 = new StudyDocument { Name = "Active Doc", FilePath = @"C:\docs\doc.pdf", IsDeleted = true };

        var groups = _service.DetectDuplicates([doc1, doc2]);

        Assert.Empty(groups);
    }

    [Fact]
    public void BuildMergePreview_CalculatesAccurateCounts()
    {
        // 代表レコード
        var survivor = new StudyDocument { Name = "Master Record", Tags = "ai, study" };
        _repo.Add(survivor);
        survivor = _repo.GetAll().Single(d => d.Name == "Master Record");

        // 統合対象
        var duplicate = new StudyDocument { Name = "Duplicate Record", Tags = "study, exam, 2026" };
        _repo.Add(duplicate);
        duplicate = _repo.GetAll().Single(d => d.Name == "Duplicate Record");

        // メモ追加
        Db.SavePersonalNote(new PersonalNote(0, duplicate.Id, "general", "Note from duplicate", false));

        // コレクション追加
        int colId = Db.CreateCollection("Preview Col");
        Db.AddDocumentToCollection(colId, duplicate.Id);

        // プレビュー生成
        var preview = _service.BuildMergePreview(survivor.Id, [duplicate.Id]);

        Assert.Equal(survivor.Id, preview.Survivor.Id);
        Assert.Single(preview.DocumentsToMerge);
        Assert.Equal(1, preview.TransferredNotesCount);
        Assert.Equal(1, preview.TransferredCollectionsCount);
        Assert.Contains("ai", preview.MergedTags);
        Assert.Contains("exam", preview.MergedTags);
        Assert.Contains("2026", preview.MergedTags);
        Assert.True(preview.WillSoftDeleteDuplicates);
    }

    [Fact]
    public async Task MergeDuplicateAsync_UserSelectsSurvivor_MergesIntoSelectedSurvivor()
    {
        var doc1 = new StudyDocument { Name = "Doc A" };
        var doc2 = new StudyDocument { Name = "Doc B" };
        _repo.Add(doc1);
        _repo.Add(doc2);
        var docs = _repo.GetAll();
        int id1 = docs.Single(d => d.Name == "Doc A").Id;
        int id2 = docs.Single(d => d.Name == "Doc B").Id;

        // doc2 を代表レコードとして選択する FakeDialogService
        var fakeDialog = new FakeReviewDialogService(selectedSurvivorId: id2);
        var loc = new LocalizationService();
        var undoRepo = new DocumentRepository(Db);
        var undo = new UndoService();

        var model = new DuplicateDetectionModel(
            _repo, fakeDialog, loc, null, undoRepo, undo, _service);

        var group = new DuplicateGroup
        {
            GroupName = "Test Group",
            MatchInfo = "Test Match",
            Documents = new(docs)
        };

        await model.MergeDuplicateCommand.ExecuteAsync(group);

        // doc2 が残り、doc1 が削除済みになっていることを検証
        var remaining = _repo.GetAll();
        Assert.Single(remaining);
        Assert.Equal(id2, remaining[0].Id);

        var deleted = _repo.GetDeletedDocuments();
        Assert.Contains(deleted, d => d.Id == id1);
    }

    [Fact]
    public async Task MergeDuplicateAsync_WhenCancelled_DoesNotMerge()
    {
        var doc1 = new StudyDocument { Name = "Doc Cancel 1" };
        var doc2 = new StudyDocument { Name = "Doc Cancel 2" };
        _repo.Add(doc1);
        _repo.Add(doc2);
        var docs = _repo.GetAll().Where(d => d.Name.StartsWith("Doc Cancel")).ToList();

        var fakeDialog = new FakeReviewDialogService(selectedSurvivorId: null); // キャンセル
        var loc = new LocalizationService();

        var model = new DuplicateDetectionModel(
            _repo, fakeDialog, loc, null, null, null, _service);

        var group = new DuplicateGroup
        {
            GroupName = "Cancel Group",
            MatchInfo = "Test Match",
            Documents = new(docs)
        };

        await model.MergeDuplicateCommand.ExecuteAsync(group);

        // 両方とも active のまま残る
        var activeDocs = _repo.GetAll().Where(d => d.Name.StartsWith("Doc Cancel")).ToList();
        Assert.Equal(2, activeDocs.Count);
    }

    private sealed class FakeReviewDialogService : IDialogService, ICustomDialogService
    {
        private readonly int? _selectedSurvivorId;

        public FakeReviewDialogService(int? selectedSurvivorId)
        {
            _selectedSurvivorId = selectedSurvivorId;
        }

        public Task<int?> ShowDuplicateMergeReviewAsync(string groupName, string matchReason, IReadOnlyList<StudyDocument> candidates)
            => Task.FromResult(_selectedSurvivorId);

        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(true);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
        public Task<string?> ShowChangeCategoryAsync(string documentName, IList<string> existingCategories, string currentCategory) => Task.FromResult<string?>(null);
        public Task<int> ShowSelectCollectionAsync(string documentName, IList<(int Id, string Name, int DocCount)> collections) => Task.FromResult(0);
        public Task<List<StudyDocument>?> ShowDocumentPickerAsync(string collectionName, IEnumerable<StudyDocument> allDocuments, IEnumerable<int> alreadyInCollection) => Task.FromResult<List<StudyDocument>?>(null);
        public Task<AddDocumentDraft?> ShowAddDocumentAsync(string filePath, IList<string> subjects, IList<string> types) => Task.FromResult<AddDocumentDraft?>(null);
    }
}
