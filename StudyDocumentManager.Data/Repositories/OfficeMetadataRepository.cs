using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

/// <summary>
/// 個人業務文書メタデータおよびリマインダー永続化リポジトリ。
/// </summary>
public class OfficeMetadataRepository : IOfficeMetadataRepository
{
    private readonly DatabaseHelper _db;

    public OfficeMetadataRepository(DatabaseHelper db)
    {
        _db = db;
    }

    public OfficeDocumentMetadata? GetByDocumentId(int documentId)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, document_id, document_number, contact_name, organization_or_project, effective_date, expiry_date, confidentiality_level, reminder_enabled, reminder_days_before, created_at, updated_at FROM office_document_metadata WHERE document_id = @docId LIMIT 1";
        cmd.Parameters.AddWithValue("@docId", documentId);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
            return MapReader(reader);
        return null;
    }

    public bool Save(OfficeDocumentMetadata metadata)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO office_document_metadata (
                document_id, document_number, contact_name, organization_or_project,
                effective_date, expiry_date, confidentiality_level,
                reminder_enabled, reminder_days_before, created_at, updated_at
            ) VALUES (
                @documentId, @documentNumber, @contactName, @organizationOrProject,
                @effectiveDate, @expiryDate, @confidentialityLevel,
                @reminderEnabled, @reminderDaysBefore, @createdAt, @updatedAt
            )
            ON CONFLICT(document_id) DO UPDATE SET
                document_number = excluded.document_number,
                contact_name = excluded.contact_name,
                organization_or_project = excluded.organization_or_project,
                effective_date = excluded.effective_date,
                expiry_date = excluded.expiry_date,
                confidentiality_level = excluded.confidentiality_level,
                reminder_enabled = excluded.reminder_enabled,
                reminder_days_before = excluded.reminder_days_before,
                updated_at = datetime('now', 'localtime');
            """;
        cmd.Parameters.AddWithValue("@documentId", metadata.DocumentId);
        cmd.Parameters.AddWithValue("@documentNumber", (object?)metadata.DocumentNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@contactName", (object?)metadata.ContactName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@organizationOrProject", (object?)metadata.OrganizationOrProject ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@effectiveDate", metadata.EffectiveDate.HasValue ? metadata.EffectiveDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : DBNull.Value);
        cmd.Parameters.AddWithValue("@expiryDate", metadata.ExpiryDate.HasValue ? metadata.ExpiryDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : DBNull.Value);
        cmd.Parameters.AddWithValue("@confidentialityLevel", metadata.ConfidentialityLevel);
        cmd.Parameters.AddWithValue("@reminderEnabled", metadata.ReminderEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@reminderDaysBefore", metadata.ReminderDaysBefore);
        cmd.Parameters.AddWithValue("@createdAt", metadata.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool DeleteByDocumentId(int documentId)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM office_document_metadata WHERE document_id = @docId";
        cmd.Parameters.AddWithValue("@docId", documentId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public IReadOnlyList<OfficeReminderItem> GetUpcomingReminders(DateTime asOfDate, int defaultDueSoonDays = 7)
    {
        var items = new List<OfficeReminderItem>();
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT m.document_id, d.name, m.document_number, m.organization_or_project, m.expiry_date, m.reminder_enabled, m.reminder_days_before
            FROM office_document_metadata m
            INNER JOIN documents d ON d.id = m.document_id
            WHERE (d.is_deleted IS NULL OR d.is_deleted = 0)
            ORDER BY m.expiry_date ASC, d.name ASC;
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            int docId = reader.GetInt32(0);
            string docName = reader.GetString(1);
            string? docNum = reader.IsDBNull(2) ? null : reader.GetString(2);
            string? org = reader.IsDBNull(3) ? null : reader.GetString(3);
            DateTime? expiryDate = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4));
            bool reminderEnabled = reader.GetInt32(5) != 0;
            int reminderDays = reader.GetInt32(6);
            if (reminderDays <= 0)
                reminderDays = defaultDueSoonDays;

            OfficeExpiryState state;
            int daysRemaining = 0;

            if (!expiryDate.HasValue)
            {
                state = OfficeExpiryState.None;
            }
            else
            {
                var diff = (expiryDate.Value.Date - asOfDate.Date).TotalDays;
                daysRemaining = (int)diff;
                if (diff < 0)
                {
                    state = OfficeExpiryState.Overdue;
                }
                else if (diff <= reminderDays)
                {
                    state = OfficeExpiryState.DueSoon;
                }
                else
                {
                    state = OfficeExpiryState.Active;
                }
            }

            items.Add(new OfficeReminderItem(
                DocumentId: docId,
                DocumentName: docName,
                DocumentNumber: docNum,
                OrganizationOrProject: org,
                ExpiryDate: expiryDate,
                ExpiryState: state,
                DaysRemaining: daysRemaining,
                ReminderEnabled: reminderEnabled));
        }

        return items;
    }

    private static OfficeDocumentMetadata MapReader(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        DocumentId = reader.GetInt32(1),
        DocumentNumber = reader.IsDBNull(2) ? null : reader.GetString(2),
        ContactName = reader.IsDBNull(3) ? null : reader.GetString(3),
        OrganizationOrProject = reader.IsDBNull(4) ? null : reader.GetString(4),
        EffectiveDate = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)),
        ExpiryDate = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
        ConfidentialityLevel = reader.GetString(7),
        ReminderEnabled = reader.GetInt32(8) != 0,
        ReminderDaysBefore = reader.GetInt32(9),
        CreatedAt = DateTime.Parse(reader.GetString(10)),
        UpdatedAt = DateTime.Parse(reader.GetString(11))
    };
}
