using System.Data;
using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Services;

/// <summary>
/// 重複文書の検出および統合プレビュー生成サービス実装。
/// </summary>
public class DuplicateReviewService : IDuplicateReviewService
{
    private readonly DatabaseHelper _db;

    public DuplicateReviewService(DatabaseHelper db)
    {
        _db = db;
    }

    /// <summary>
    /// 文書一覧から重複候補グループを検出する。
    /// </summary>
    public IReadOnlyList<DuplicateReviewGroup> DetectDuplicates(IReadOnlyList<StudyDocument> documents)
    {
        var activeDocs = documents.Where(d => !d.IsDeleted).ToList();
        var groups = new List<DuplicateReviewGroup>();
        var handledIds = new HashSet<int>();

        // 1. ファイルパス完全一致（最も信頼度の高い重複判定）
        var pathGroups = activeDocs
            .Where(d => !string.IsNullOrWhiteSpace(d.FilePath))
            .GroupBy(d => d.FilePath.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var g in pathGroups)
        {
            var candidates = g.ToList();
            groups.Add(new DuplicateReviewGroup(
                GroupKey: $"path:{g.Key}",
                Reason: DuplicateMatchReason.ExactPath,
                MatchDescription: $"ファイルパス完全一致 ({candidates.Count} 件)",
                Candidates: candidates));
            foreach (var doc in candidates)
                handledIds.Add(doc.Id);
        }

        // 2. 文書名完全一致（大文字小文字を区別しない）
        var nameGroups = activeDocs
            .Where(d => !handledIds.Contains(d.Id) && !string.IsNullOrWhiteSpace(d.Name))
            .GroupBy(d => d.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var g in nameGroups)
        {
            var candidates = g.ToList();
            groups.Add(new DuplicateReviewGroup(
                GroupKey: $"name:{g.Key}",
                Reason: DuplicateMatchReason.ExactName,
                MatchDescription: $"文書名完全一致 ({candidates.Count} 件)",
                Candidates: candidates));
            foreach (var doc in candidates)
                handledIds.Add(doc.Id);
        }

        // 3. 文書名および文書タイプ一致
        var nameTypeGroups = activeDocs
            .Where(d => !handledIds.Contains(d.Id) && !string.IsNullOrWhiteSpace(d.Name) && !string.IsNullOrWhiteSpace(d.Type))
            .GroupBy(d => $"{d.Name.Trim()}|||{d.Type.Trim()}", StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var g in nameTypeGroups)
        {
            var candidates = g.ToList();
            groups.Add(new DuplicateReviewGroup(
                GroupKey: $"nametype:{g.Key}",
                Reason: DuplicateMatchReason.SameNameAndType,
                MatchDescription: $"文書名および文書タイプ一致 ({candidates.Count} 件)",
                Candidates: candidates));
            foreach (var doc in candidates)
                handledIds.Add(doc.Id);
        }

        return groups;
    }

    /// <summary>
    /// 代表文書と統合対象文書から影響範囲プレビューを生成する。
    /// </summary>
    public DuplicateMergePreview BuildMergePreview(int survivorId, IReadOnlyList<int> duplicateIds)
    {
        var allDocs = _db.GetAllDocuments();
        var survivor = allDocs.FirstOrDefault(d => d.Id == survivorId)
            ?? throw new ArgumentException($"Survivor document {survivorId} not found.");

        var duplicates = duplicateIds
            .Where(id => id != survivorId)
            .Distinct()
            .Select(id => allDocs.FirstOrDefault(d => d.Id == id))
            .Where(d => d != null)
            .Cast<StudyDocument>()
            .ToList();

        int transferredNotesCount = 0;
        int transferredCollectionsCount = 0;
        int transferredRelationsCount = 0;

        using (var connection = new SqliteConnection(_db.ConnectionString))
        {
            connection.Open();

            // Notes count
            foreach (var dup in duplicates)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM personal_notes WHERE document_id = @docId AND (is_deleted IS NULL OR is_deleted = 0)";
                cmd.Parameters.AddWithValue("@docId", dup.Id);
                transferredNotesCount += Convert.ToInt32(cmd.ExecuteScalar());
            }

            // Collections count
            var survivorColIds = new HashSet<int>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT collection_id FROM collection_items WHERE document_id = @survivorId";
                cmd.Parameters.AddWithValue("@survivorId", survivor.Id);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    survivorColIds.Add(reader.GetInt32(0));
            }

            foreach (var dup in duplicates)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT collection_id FROM collection_items WHERE document_id = @dupId";
                cmd.Parameters.AddWithValue("@dupId", dup.Id);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int colId = reader.GetInt32(0);
                    if (!survivorColIds.Contains(colId))
                    {
                        transferredCollectionsCount++;
                        survivorColIds.Add(colId);
                    }
                }
            }

            // Relations count
            var survivorRelIds = new HashSet<int>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT CASE WHEN doc_id_1 = @id THEN doc_id_2 ELSE doc_id_1 END FROM document_relations WHERE doc_id_1 = @id OR doc_id_2 = @id";
                cmd.Parameters.AddWithValue("@id", survivor.Id);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    survivorRelIds.Add(reader.GetInt32(0));
            }

            foreach (var dup in duplicates)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT CASE WHEN doc_id_1 = @id THEN doc_id_2 ELSE doc_id_1 END FROM document_relations WHERE doc_id_1 = @id OR doc_id_2 = @id";
                cmd.Parameters.AddWithValue("@id", dup.Id);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int relId = reader.GetInt32(0);
                    if (relId != survivor.Id && !survivorRelIds.Contains(relId))
                    {
                        transferredRelationsCount++;
                        survivorRelIds.Add(relId);
                    }
                }
            }
        }

        // Merged tags calculation
        var survivorTags = (survivor.Tags ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var mergedTags = new HashSet<string>(survivorTags, StringComparer.OrdinalIgnoreCase);
        foreach (var dup in duplicates)
        {
            var dupTags = (dup.Tags ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var t in dupTags)
                mergedTags.Add(t);
        }

        return new DuplicateMergePreview(
            Survivor: survivor,
            DocumentsToMerge: duplicates,
            TransferredNotesCount: transferredNotesCount,
            MergedTags: mergedTags.OrderBy(t => t).ToList(),
            TransferredCollectionsCount: transferredCollectionsCount,
            TransferredRelationsCount: transferredRelationsCount,
            WillSoftDeleteDuplicates: true);
    }
}
