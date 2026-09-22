# Task 2 修正ラウンド1 レポート

## 対応内容

- `PersonalNoteModel` は未保存の `NoteContent` がある状態でノート選択を変更しても、選択済みノートの内容でドラフトを上書きしないようにした。変更操作は直前の選択へ戻す。
- 未保存ドラフト中の「新規メモ」は無視し、既存のノートを失わないようにした。
- 保存成功後に保存済み内容を先に更新してから一覧を再読み込みし、新規メモ作成時に既存の `general` メモを上書きしないようにした。
- `DashboardModel.ApplyFiltersCore` のキーワード検索を `SearchAdvancedWithNotes` に接続した。ステータス検索は既存の `SearchAdvancedWithStatus` を維持した。
- `PersonalNote` に pinned 状態の表示を追加した。
- 選択変更時のドラフト保護、新規メモと `general` メモの共存、pinned 表示の Avalonia 回帰テストを追加した。

## 影響分析

- `PersonalNoteModel`: MEDIUM、直接6件。動的な `ModelBase` バインディング境界により lower-bound。
- `NewNote`: GitNexus は呼び出し元を解決できず UNKNOWN。テキスト検索で `NewNoteCommand` の XAML／テスト利用を確認した。
- `SaveNoteAsync`: GitNexus は呼び出し元を解決できず UNKNOWN。テキスト検索で `SaveNoteCommand` の XAML／テスト利用を確認した。
- `ReloadNotes`: LOW、直接3件。
- `DashboardModel.ApplyFiltersCore`: LOW、直接2件、関連プロセス2件。
- HIGH／CRITICAL の影響対象は修正していない。

## 検証

- `dotnet build StudyDocumentManager.Data/StudyDocumentManager.Data.csproj -c Debug --no-restore`: 成功、0 warning、0 error。
- `dotnet test StudyDocumentManager.Tests/StudyDocumentManager.Tests.csproj -c Debug --filter "FullyQualifiedName~PersonalNoteUiRegressionTests|FullyQualifiedName~PersonalNoteDataTests" --no-restore`: 実行前の Avalonia XAML コンパイルで失敗。`StudyDocumentManager/Views/CategoryManagement.axaml` の既存未整合タグ（`WrapPanel` と `StackPanel`）が原因で、Task 2 修正箇所に由来しない。
- `dotnet build StudyDocumentManager.sln -c Debug --no-restore`: 同じ既存 `CategoryManagement.axaml` エラーで失敗。
- `git diff --cached --check`: 成功。

## 懸念

- CategoryManagement.axaml の既存 XAML エラーが解消されるまで、Avalonia UI の focused test と solution build を完走できない。
- 実アプリの手動 UI 操作は未実施。
