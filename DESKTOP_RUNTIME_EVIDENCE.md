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

未実施です。各 Issue の acceptance criteria に沿って更新します。
