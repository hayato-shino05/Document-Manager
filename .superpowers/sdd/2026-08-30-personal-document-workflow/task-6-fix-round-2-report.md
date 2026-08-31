# Task 6 修正ラウンド2レポート

## 対応

- `ux_documents_archive_export_key` を `ValidateDocumentIndexesAndTriggers` の allow-list に追加。
- archive key unique index を schema validation で正しく認識し、通常の `InitializeDatabase` 再実行で unsupported index 扱いされないよう修正。
- `DatabaseIntegrityTests` に `InitializeDatabase_WithArchiveKeyIndex_IsIdempotent` を追加し、2回初期化後も index が1件であることを検証。

## 検証

- `dotnet build StudyDocumentManager.Data/StudyDocumentManager.Data.csproj -c Debug --no-restore`: 成功、警告0、エラー0。
- `git diff --check`: 成功。
- 全体 test は前回と同じ既存 `CategoryManagement.axaml` AVLN1001 により blocked。
- GitNexus impact は LadybugDB checkpoint 中のため unavailable。CodeGraph/source fallback を使用し risk UNKNOWN。
