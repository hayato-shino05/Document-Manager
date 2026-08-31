# Task 6 修正ラウンド3レポート

## 対応

- `EnsureNoUnsupportedIndexesOrTriggers` の `documents` allow-list に `ux_documents_archive_export_key` を追加。
- archive key unique index の形状も既存 unique constraint と同様に検証し、legacy preflight が正規 index を reject しないよう修正。
- `EnsureArchiveExportKeyUniqueness` は document rebuild 後に実行する既存 round2 修正を維持。

## 検証

- `dotnet build StudyDocumentManager.Data/StudyDocumentManager.Data.csproj -c Debug --no-restore`: 成功、警告0、エラー0。
- `git diff --check`: 成功。
- focused tests は既存 `CategoryManagement.axaml` AVLN1001 および test compile の既存重複定義/`PersonalNote` ambiguity により blocked。
- GitNexus impact/detect_changes は LadybugDB checkpoint 中のため unavailable。CodeGraph と Serena による source fallback を使用し、risk UNKNOWN。
- aggregate/per-entry/checksum/path/DB rollback の追加 focused coverage は scope/time のため unresolved として残す。
