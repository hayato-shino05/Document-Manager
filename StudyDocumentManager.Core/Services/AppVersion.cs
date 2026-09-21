namespace StudyDocumentManager.Core.Services;

/// <summary>
/// アプリケーションバージョン管理ユーティリティ
/// セマンティックバージョニング比較のみ — プラットフォーム非依存
/// </summary>
public static class AppVersion
{
    public static string Current => "4.0.0";

    /// <summary>
    /// セマンティックバージョン比較: -1=古い, 0=同じ, 1=新しい
    /// </summary>
    public static int Compare(string current, string latest)
    {
        var currentParts = ParseVersion(current);
        var latestParts = ParseVersion(latest);

        for (int i = 0; i < 3; i++)
        {
            if (currentParts[i] < latestParts[i]) return -1;
            if (currentParts[i] > latestParts[i]) return 1;
        }
        return 0;
    }

    /// <summary>
    /// 新しいバージョンが利用可能かどうかを判定
    /// </summary>
    public static bool IsNewer(string latest) => Compare(Current, latest) < 0;

    private static int[] ParseVersion(string version)
    {
        var parts = version.TrimStart('v', 'V').Split('.');
        var result = new int[3];
        for (int i = 0; i < Math.Min(parts.Length, 3); i++)
        {
            int.TryParse(parts[i], out result[i]);
        }
        return result;
    }
}
