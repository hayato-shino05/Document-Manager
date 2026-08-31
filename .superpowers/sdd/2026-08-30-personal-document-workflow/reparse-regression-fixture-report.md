# Reparse regression fixture report

## 変更内容

`PersonalDocumentArchiveTests` の次の 2 テストで、manifest の `Files` と `Checksums` を ZIP の `linked/document.pdf` entry と一致させた。

- `Import_RejectsExistingReparsePointBeforeMutation`
- `Import_RejectsDanglingReparsePointBeforeMutation`

ZIP entry は UTF-8 の `archive-content` bytes を持ち、checksum は実際の SHA-256 値から生成する。これにより archive entry/checksum validation を通過した後、destination reparse-point validation が `invalid-destination-path` を返すことを検証する。

両テストで `ArchiveTransactionOutcome.NotStarted`、空の DB、既存 reparse point の target への未書き込みを確認する。

## 検証

- 対象テスト実行: 失敗。既存の `StudyDocumentManager/Views/CategoryManagement.axaml` の AVLN1001 により test project build が停止した。
- AVLN1001 の内容: `WrapPanel` start tag と `StackPanel` end tag の不一致（line 45/73）。今回の変更範囲外。
- `StudyDocumentManager.Data` の build と変更差分確認は親エージェント側で実施する。
