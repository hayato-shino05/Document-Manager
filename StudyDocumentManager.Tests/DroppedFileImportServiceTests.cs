using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public class DroppedFileImportServiceTests
{
    [Fact]
    public void AvailableCategories_UseCatalogWhenDocumentsHaveNoAssignedCategory()
    {
        var service = new DroppedFileImportService(
            new FakeDocumentRepository(),
            new FakeCategoryRepository());

        Assert.Equal(["Math", "Finance"], service.GetAvailableSubjects([]));
        Assert.Equal(["Word"], service.GetAvailableTypes([]));
    }

    [Fact]
    public void BuildDocumentFromPath_MapsNameTypeAndSize()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sdm_drop_{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(tempFile, new byte[2048]);

        try
        {
            var service = new DroppedFileImportService(new FakeDocumentRepository());

            var document = service.BuildDocumentFromPath(tempFile);

            Assert.Equal(Path.GetFileNameWithoutExtension(tempFile), document.Name);
            Assert.Equal(tempFile, document.FilePath);
            Assert.Equal("PDF", document.Type);
            Assert.NotNull(document.FileSize);
            Assert.True(document.FileSize > 0);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    private sealed class FakeCategoryRepository : ICategoryRepository
    {
        public List<string> GetAllSubjects() => ["Math", "Finance"];
        public List<string> GetAllTypes() => ["Word"];
        public List<(string Name, int Count)> GetSubjectsWithCount() => [];
        public List<(string Name, int Count)> GetTypesWithCount() => [];
        public bool AddSubject(string name) => false;
        public bool AddType(string name) => false;
        public bool UpdateSubjectName(string oldName, string newName) => false;
        public bool UpdateTypeName(string oldName, string newName) => false;
        public bool DeleteDocumentsBySubject(string subjectName) => false;
        public bool DeleteDocumentsByType(string typeName) => false;
        public int GetTotalDocumentCount() => 0;
    }

    private sealed class FakeDocumentRepository : IDocumentRepository
    {
        public List<StudyDocument> GetAll() => throw new NotImplementedException();
        public StudyDocument? GetById(int id) => throw new NotImplementedException();
        public List<StudyDocument> Search(string keyword) => throw new NotImplementedException();
        public List<StudyDocument> Filter(string subject, string type) => throw new NotImplementedException();
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => throw new NotImplementedException();
        public bool Add(StudyDocument document) => throw new NotImplementedException();
        public bool AddWithCatalogs(StudyDocument document) => throw new NotImplementedException();
        public bool Update(StudyDocument document) => throw new NotImplementedException();
        public bool Delete(int id) => throw new NotImplementedException();
        public List<string> GetDistinctSubjects() => throw new NotImplementedException();
        public List<string> GetDistinctTypes() => throw new NotImplementedException();
        public List<string> GetDistinctTags() => throw new NotImplementedException();
        public List<StudyDocument> GetUpcomingDeadlines(int days) => throw new NotImplementedException();
        public List<StudyDocument> GetOverdueDocuments() => throw new NotImplementedException();
        public void EnsureSubjectExists(string subject) => throw new NotImplementedException();
        public void EnsureTypeExists(string type) => throw new NotImplementedException();
    }
}
