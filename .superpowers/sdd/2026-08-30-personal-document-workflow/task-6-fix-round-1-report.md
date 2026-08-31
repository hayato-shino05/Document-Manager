# Task 6 修正ラウンド1レポート

## 対応

- `ImportAsync` で `InvalidOperationException` を含む import graph、checksum lookup、DB 制約エラーを安全な `ArchiveImportReport` に変換し、rollback と作成済みファイル削除を維持。
- manifest 読み込み後に `DocumentExportKey` と archive path を canonicalize し、重複キー・重複 checksum・normalized path collision を mutation 前に検証。
- mixed conflict は partial commit せず、全体を conflict report として終了。
- null の `ExportKey` は export 前に生成し、`documents.archive_export_key` へ永続化。
- import staging に per-entry 64 MiB、aggregate 256 MiB、entry 数 5000 の上限を適用。
- migration で既存 duplicate archive key を fail-closed 検出し、`ux_documents_archive_export_key` の unique index を作成。
- `deleted_at` の完全な round-trip と空親ディレクトリ cleanup は既存契約上未解決として残した。

## 検証

- `dotnet build StudyDocumentManager.Data/StudyDocumentManager.Data.csproj -c Debug --no-restore`: 成功、警告0、エラー0。
- `dotnet test ... --filter FullyQualifiedName~PersonalDocumentArchiveTests --no-restore`: 既存の `CategoryManagement.axaml` AVLN1001 により blocked。`WrapPanel` と `StackPanel` のタグ不一致（line 45/73）。
- Test project compile-only: dirty な既存テストの重複メソッドと `PersonalNote` ambiguity により blocked。
- `git diff --check`: 成功。
- GitNexus impact/detect_changes: LadybugDB checkpoint 中で unavailable。代替として CodeGraph と Serena で call path/source を確認し、risk は UNKNOWN と記録。

## Scope

archive import contract、database archive identity migration、focused archive tests のみ変更。その他の dirty files は変更・revert していない。
