# Historical Implementation Record: Localization and Schema Neutralization

## Scope

This file records the historical implementation scope for the transition from the legacy WinForms codebase to the current Avalonia and .NET 9 application.

It is not an active execution plan.

## Delivered Outcomes

The current repository already includes the following completed outcomes:

- English entity property names in the active codebase
- English SQLite table and column names in the active schema
- localization resources for Japanese, English, Vietnamese, and Chinese
- runtime language switching through `LocalizationService` and `LocalizeExtension`
- language persistence through `app_settings.language`
- a language selector exposed from the main shell model

## Historical Mapping Context

The transition preserved compatibility with older data and naming conventions.

### Entity Property Mapping

| Legacy | Current |
| --- | --- |
| `Ten` | `Name` |
| `MonHoc` | `Subject` |
| `Loai` | `Type` |
| `DuongDan` | `FilePath` |
| `GhiChu` | `Notes` |
| `NgayThem` | `CreatedAt` |
| `KichThuoc` | `FileSize` |
| `TacGia` | `Author` |
| `QuanTrong` | `IsImportant` |

### Schema Naming Direction

| Legacy | Current |
| --- | --- |
| `tai_lieu` | `documents` |
| `danh_muc` | `categories` |
| `loai_tai_lieu` | `document_types` |

## Current Source of Truth

Use these files for the active implementation:

- `AGENTS.md`
- `CLAUDE.md`
- `PROJECT_STRUCTURE.md`
- `DATABASE.md`
- `docs/ARCHITECTURE.md`
- `docs/TEST_MATRIX.md`
- source files under `StudyDocumentManager*`

## Notes

Keep this file only as migration history. Do not use it as a current design or execution guide.
