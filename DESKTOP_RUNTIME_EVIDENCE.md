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

**Status (2026-08-30): limitation recorded, install/launch/uninstall は未検証。**

#### 検証済み

2026-08-30 に WSL2 の Ubuntu 24.04.4 LTS（`amd64`）で確認しました。`dpkg`、`dpkg-deb`、`DISPLAY=:0`、`WAYLAND_DISPLAY=wayland-0` は利用可能でした。

- `StudyDocumentManager/StudyDocumentManager.csproj` は `net9.0` です。`scripts/build-debian-package.sh` は `linux-x64` self-contained publish から `document-manager` package を生成します。
- GitHub Actions run `33294846027` の artifact `linux-debian-package-0bc733d88caf193c09ff239c293a027b3ab67d6f` を取得しました。`document-manager_4.1.0_amd64.deb` の SHA-256 は `11A162DC09499A85D93972E618DF2234BBCE026C5B214626712AB226F064EBB7` です。
- Ubuntu の `dpkg-deb` で package metadata `document-manager`、version `4.1.0`、architecture `amd64` を確認しました。`/usr/bin/document-manager`、`/usr/lib/document-manager/DocumentManager`、desktop entry も archive 内にあります。

#### 未検証（PASS ではない）

- install、Xvfb 下の launch/database initialization、purge、user-data retention は未実施です。いずれも PASS ではありません。
- WSL に `dotnet` はありません。既存の Ubuntu distribution は disposable environment として扱えないため、release artifact を投入して lifecycle を確かめる手順が組めません。
- GitHub Release `v3.1.2` は Windows installer と Portable ZIP だけで、versioned `.deb` release asset は公開されていません。
- `admin/**` 配下には Linux 関連の変更を加えていません。

#### 検証のために必要な契約（再起動条件）

下記が揃うまで `#65` は未完了として残します。

1. disposable Ubuntu x64 runner を workflow から起動できること。
2. GitHub Release もしくは同等の信頼ある場所から versioned `.deb` URL と SHA-256 を取得できること。
3. 上記 URL と SHA-256 を入力に install、launch（Xvfb 下の database initialization 確認）、purge、user-data retention を fail-closed で実行する workflow を `.github/workflows/linux-deb-lifecycle.yml` として固定できること。

#### CI contract（提案）

`.github/workflows/linux-deb-lifecycle.yml` は次の minimum contract で audit 可能な形にします。

- `workflow_dispatch` で `package_url`（release asset の HTTPS URL）と `package_sha256`（64 桁 hex）を受け取り、両方が release prefix と正規表現にマッチしない限り fail。
- `runs-on: ubuntu-24.04`、disposable、`timeout-minutes: 10`、`permissions: contents: read`。
- checksum 検証 → `dpkg-deb -f` で `Package=document-manager`、`Architecture=amd64` を assert → install → `xvfb-run` 配下で launch（exit 0 または 124 timeout まで）→ user-data SQLite file 生成を assert → `dpkg --purge` → application files 消失と user-data 存続を assert。
- 上記のいずれかのアサーションが失敗したら `set -euo pipefail` で workflow を fail させます。`continue-on-error` は使いません。

この contract は release が versioned `.deb` asset と SHA-256 を公開したあとに手動で起動し、結果を evidence としてこの台帳へ追記する想定です。contract 自体は fixed ですが、現時点では起動できる artifact がなく PASS は取れていません。
