using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.Interfaces;

/// <summary>
/// 個人業務向けメタデータリポジトリのインターフェース。
/// </summary>
public interface IOfficeMetadataRepository
{
    OfficeDocumentMetadata? GetByDocumentId(int documentId);
    bool Save(OfficeDocumentMetadata metadata);
    bool DeleteByDocumentId(int documentId);
    IReadOnlyList<OfficeReminderItem> GetUpcomingReminders(DateTime asOfDate, int defaultDueSoonDays = 7);
}
