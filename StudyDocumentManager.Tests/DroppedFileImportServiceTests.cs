using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public class DroppedFileImportServiceTests
{
    [Fact]
    public void BuildDocumentFromPath_MapsNameTypeAndSize()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sdm_drop_{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(tempFile, new byte[2048]);

        try
        {
            var service = new DroppedFileImportService(new FakeDocumentRepository());

            var document = service.BuildDocumentFromPath(tempFile);

            Assert.Equal(Path.GetFileNameWithoutExtension(tempFile), document.Ten);
            Assert.Equal(tempFile, document.DuongDan);
            Assert.Equal("PDF", document.Loai);
            Assert.NotNull(document.KichThuoc);
            Assert.True(document.KichThuoc > 0);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    private sealed class FakeDocumentRepository : IDocument
    {
        public List<StudyDocument> GetAll() => throw new NotImplementedException();
        public StudyDocument? GetById(int id) => throw new NotImplementedException();
        public List<StudyDocument> Search(string keyword) => throw new NotImplementedException();
        public List<StudyDocument> Filter(string subject, string type) => throw new NotImplementedException();
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => throw new NotImplementedException();
        public bool Add(StudyDocument document) => throw new NotImplementedException();
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
