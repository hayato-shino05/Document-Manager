namespace StudyDocumentManager.Core.Entities;

/// <summary>
/// 個人業務文書向け拡張メタデータ（請求書、契約書、報告書、領収書、申請書など）。
/// </summary>
public sealed class OfficeDocumentMetadata
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string? DocumentNumber { get; set; }
    public string? ContactName { get; set; }
    public string? OrganizationOrProject { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string ConfidentialityLevel { get; set; } = OfficeConfidentialityLevel.Internal;
    public bool ReminderEnabled { get; set; } = true;
    public int ReminderDaysBefore { get; set; } = 3;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 個人業務文書の機密区分。
/// </summary>
public static class OfficeConfidentialityLevel
{
    public const string Public = "public";
    public const string Internal = "internal";
    public const string Confidential = "confidential";
    public const string Restricted = "restricted";

    public static readonly IReadOnlyList<string> All =
    [
        Public,
        Internal,
        Confidential,
        Restricted
    ];

    public static bool IsValid(string? level)
        => !string.IsNullOrEmpty(level) && All.Contains(level);
}

/// <summary>
/// 期限状態の分類。
/// </summary>
public enum OfficeExpiryState
{
    None,
    Active,
    DueSoon,
    Overdue
}

/// <summary>
/// リマインダー・期限切迫表示用アイテム。
/// </summary>
public sealed record OfficeReminderItem(
    int DocumentId,
    string DocumentName,
    string? DocumentNumber,
    string? OrganizationOrProject,
    DateTime? ExpiryDate,
    OfficeExpiryState ExpiryState,
    int DaysRemaining,
    bool ReminderEnabled);
