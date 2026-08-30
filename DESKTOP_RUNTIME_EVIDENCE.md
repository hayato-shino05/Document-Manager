# Desktop Runtime Evidence

このファイルは、#60、#64、#65、#68 の検証 evidence と limitation を追跡する台帳です。

## 方針

- Admin Web と `admin/**` は対象外です。
- interactive runtime、package lifecycle、database restore の未確認項目を pass として扱いません。
- production database、real user data、credential は使用しません。
- Issue の sub-task は、検証結果をコメントで記録した後に tick します。

## 追跡対象

- #60: desktop UX/accessibility runtime matrix
- #64: UIA、screen reader、keyboard、DPI proof
- #65: Linux Debian package lifecycle
- #68: legacy `NOCASE UNIQUE` autoindex restore/migration

## Evidence

- #68: legacy `file_path TEXT COLLATE NOCASE UNIQUE` autoindex を含む backup を restore し、migration 後の document data、`archive_export_key`、`status`、BINARY partial path index を検証する focused test を追加した。`NULL`、空文字、大小文字のみ異なる path は許可し、完全一致 path は拒否する。rollback と invalid candidate の live data preservation は `BackupRestoreIntegrityTests` で確認する。

### #65 Linux Debian package lifecycle

**Status (2026-08-30): package/release/lifecycle contract fixed, install/launch/uninstall は未検証。**

#### 検証済み

2026-08-30 に WSL2 の Ubuntu 24.04.4 LTS（`amd64`）で確認しました。`dpkg`、`dpkg-deb`、`DISPLAY=:0`、`WAYLAND_DISPLAY=wayland-0` は利用可能でした。

- `StudyDocumentManager/StudyDocumentManager.csproj` は `net9.0` です。`scripts/build-debian-package.sh` は `linux-x64` self-contained publish から `document-manager` package を生成します。
- GitHub Actions run `33294846027` の artifact `linux-debian-package-0bc733d88caf193c09ff239c293a027b3ab67d6f` を取得しました。`document-manager_4.1.0_amd64.deb` の SHA-256 は `11A162DC09499A85D93972E618DF2234BBCE026C5B214626712AB226F064EBB7` です。
- Ubuntu の `dpkg-deb` で package metadata `document-manager`、version `4.1.0`、architecture `amd64` を確認しました。`/usr/bin/document-manager`、`/usr/lib/document-manager/DocumentManager`、desktop entry も archive 内にあります。

#### 未検証（PASS ではない）

- install、Xvfb 下の launch/database initialization、purge、user-data retention は未実施です。いずれも PASS ではありません。
- WSL に `dotnet` はありません。既存の Ubuntu distribution は disposable environment として扱えないため、release artifact を投入して lifecycle を確かめる手順が組めません。
- 当時確認した GitHub Release `v3.1.2` は Windows installer と Portable ZIP だけで、versioned `.deb` release asset は公開されていませんでした。現在の Release workflow はタグから versioned `.deb` と対応する `.sha256` を生成し、Release assets としてアップロードします。
- `admin/**` 配下には Linux 関連の変更を加えていません。

#### 検証のために必要な契約（再起動条件）

下記が揃うまで `#65` は未完了として残します。

1. disposable Ubuntu x64 runner を workflow から起動できること。
2. Release workflow が versioned `.deb` と対応する `.sha256` を Release assets として公開すること。
3. 上記 asset URL と SHA-256 を入力に install、launch（`xvfb-run` 下の database initialization 確認）、purge、user-data retention を fail-closed で実行する workflow を `.github/workflows/linux-deb-lifecycle.yml` として固定すること。

#### CI contract（固定）

`.github/workflows/linux-deb-lifecycle.yml` は次の minimum contract で audit 可能な形にします。

- `workflow_dispatch` で `package_url`（release asset の HTTPS URL）と `package_sha256`（64 桁 hex）を受け取り、両方が release prefix と正規表現にマッチしない限り fail。
- `runs-on: ubuntu-24.04`、disposable、`timeout-minutes: 10`、`permissions: contents: read`。
- checksum 検証 → `dpkg-deb -f` で `Package=document-manager`、`Architecture=amd64` を assert → install → `xvfb-run` 配下で launch（exit 0 または 124 timeout まで）→ user-data SQLite file 生成を assert → `dpkg --purge` → `/usr/bin/document-manager` と `/usr/lib/document-manager` が消失し、user-data が存続することを assert。
- 上記のいずれかのアサーションが失敗したら `set -euo pipefail` で workflow を fail させます。`continue-on-error` は使いません。

Release workflow が versioned `.deb` asset と SHA-256 を公開したあと、この workflow を手動起動し、結果を evidence としてこの台帳へ追記します。contract は固定済みですが、現時点では実際の versioned release asset に対する install/launch/purge を実行していないため PASS は取れていません。

### #60 / #64 MainWindow・Dashboard・RelatedDocs・AffectedItemsPreviewDialog の UIA Name 補強

**Status (2026-08-30): XAML 補強と source-level/Avalonia headless test は PASS。screen-reader 実機 spot check は未実施。**

#### 検証済み

- `MainWindow.axaml`: toolbar（Add / Open / Export / Refresh / Import / Report / TreeMap / Undo / Back）と language selector に `AutomationProperties.Name` と `HelpText` を追加。`StackPanel` 型の Name 漏洩を抑止。
- `Dashboard.axaml`: search ボタン、advanced filter toggle、apply/clear filter、status bar の quick action（Refresh / Upcoming / Overdue / CopyPath / OpenFolder / About）に `Name` と `HelpText` を追加。
- `RelatedDocuments.axaml`: header back、list の remove、add link に `Name` と `HelpText` を追加。
- `AffectedItemsPreviewDialog.axaml`: `ConfirmButton` は `Content` が code-behind で差し替わるため XAML 側で `Name=Action_Delete`、`HelpText=BE_ConfirmApply` を固定。`CancelButton` も `Name` と `HelpText` を補強。
- すべて既存の 4 ResX（`Strings.resx` / `Strings.en.resx` / `Strings.vi.resx` / `Strings.zh.resx`）のキーを再利用。新規キーは追加していません。
- `StudyDocumentManager.Tests/Issue60ToolbarUiaNameTests.cs`: source-level test 4 件 + Avalonia headless test 1 件 = 5/5 PASS。
- solution Debug build: 0 warning / 0 error。
- full Debug test: 1470/1470 PASS（`dotnet test`）。
- `git diff --check`: clean。

#### 未検証（PASS ではない）

- Windows interactive session の screen-reader（NVDA / Narrator）spot check は未実施です。`MainWindow`、`Dashboard`、主要 dialog の UIA Name / HelpText は XAML 補強後のみ確認しています。
- 150% / 200% DPI での focus order と key traversal の実機確認は未実施です。
- 19 画面 matrix のうち、本変更で触れた view のみ source-level 補強と headless render の 2 系統で確認しています。残りの画面は Issue #60 側の既存 focused test 範囲にとどまっています。
