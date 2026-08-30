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

2026-08-30 に WSL2 の Ubuntu 24.04.4 LTS（`amd64`）で確認しました。`dpkg`、`dpkg-deb`、`DISPLAY=:0`、`WAYLAND_DISPLAY=wayland-0` は利用可能でした。一方、WSL に `dotnet` はなく、既存の Ubuntu distribution は disposable environment として扱えないため、install、launch、uninstall は実行していません。

- `StudyDocumentManager/StudyDocumentManager.csproj` は `net9.0` です。`scripts/build-debian-package.sh` は `linux-x64` self-contained publish から `document-manager` package を生成します。
- GitHub Actions run `33294846027` の artifact `linux-debian-package-0bc733d88caf193c09ff239c293a027b3ab67d6f` を取得しました。`document-manager_4.1.0_amd64.deb` の SHA-256 は `11A162DC09499A85D93972E618DF2234BBCE026C5B214626712AB226F064EBB7` です。
- Ubuntu の `dpkg-deb` で package metadata `document-manager`、version `4.1.0`、architecture `amd64` を確認しました。`/usr/bin/document-manager`、`/usr/lib/document-manager/DocumentManager`、desktop entry も archive 内にあります。
- GitHub Release `v3.1.2` は Windows installer と Portable ZIP だけで、versioned `.deb` release asset はありません。
- install、Xvfb 下の launch/database initialization、purge、user-data retention は未実施です。いずれも PASS ではありません。

`.github/workflows/linux-deb-lifecycle.yml` は versioned `.deb` URL と SHA-256 を入力として受け、disposable Ubuntu runner で checksum、metadata、install、Xvfb 下の launch/database initialization、purge、user-data retention を fail-closed で確認します。release が versioned artifact と checksum を公開した後に実行します。
