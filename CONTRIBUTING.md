# Contributing to Study Document Manager

Thank you for contributing to Study Document Manager. This guide covers the current development workflow for the Avalonia and .NET 9 codebase.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Repository Setup](#repository-setup)
- [Build and Test](#build-and-test)
- [Development Notes](#development-notes)
- [Pull Requests](#pull-requests)
- [Bug Reports and Feature Requests](#bug-reports-and-feature-requests)
- [Related Documentation](#related-documentation)

## Prerequisites

- .NET 9 SDK
- Git
- An editor or IDE that supports .NET desktop development

SQLite is created locally by the application. No separate database server setup is required.

## Repository Setup

```bash
git clone https://github.com/hayato-shino05/study-document-manager.git
cd study-document-manager
```

Create a focused branch for each change.

- New feature: `feature/short-description`
- Bug fix: `fix/short-description`
- Documentation: `docs/short-description`

Example:

```bash
git checkout -b feature/language-menu-polish
```

## Build and Test

Use the verified local commands below.

```powershell
dotnet build "StudyDocumentManager.sln" -c Debug
dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Debug
```

If your change affects startup, routing, schema, localization, or theme resources, read the project guidance files before editing.

## Development Notes

### Architecture boundaries

- `StudyDocumentManager` contains Avalonia views, models, services, and themes.
- `StudyDocumentManager.Core` contains entities, DTOs, and service or repository contracts.
- `StudyDocumentManager.Data` contains SQLite schema, migrations, and repository implementations.
- `StudyDocumentManager.Tests` contains xUnit coverage.

### UI and theme work

Do not hardcode shared colors or brushes in views. Use the existing theme resources instead.

- `StudyDocumentManager/Themes/ColorTokens.axaml`
- `StudyDocumentManager/Themes/AppTheme.axaml`
- `StudyDocumentManager/Themes/SharedStyles.axaml`

Keep view state in `Models/*Model.cs`. Use code-behind only when Avalonia event bridging or control lifecycle work requires it.

### Data and schema work

When changing schema or repository behavior, keep these surfaces in sync.

- `StudyDocumentManager.Data/Helpers/DatabaseHelper.cs`
- `StudyDocumentManager.Data/Helpers/DatabaseMigrator.cs`
- `StudyDocumentManager.Core/Interfaces/*.cs`
- `StudyDocumentManager.Data/Repositories/*.cs`
- `DATABASE.md`
- affected tests in `StudyDocumentManager.Tests`

### Localization work

Current localization is implemented through `.resx` resources, `LocalizationService`, and `LocalizeExtension`. If you touch labels, menus, or dialogs, verify both the resource keys and the runtime language-switching behavior.

## Pull Requests

Before opening a pull request:

1. Keep the diff focused on one task.
2. Run the relevant build and test commands.
3. Update documentation when the contract or workflow changes.
4. Summarize what changed, how it was verified, and any remaining limitation.

Recommended commit message style:

- `feat: add collection filter shortcut`
- `fix: preserve selection after dashboard refresh`
- `docs: rewrite readme for avalonia app`

## Bug Reports and Feature Requests

When opening an issue, include:

- a short summary
- the affected screen, service, or workflow
- reproduction steps
- expected behavior
- actual behavior
- screenshots or logs when relevant

For feature requests, describe the user problem first, then the proposed behavior.

## Related Documentation

- [PROJECT_STRUCTURE.md](./PROJECT_STRUCTURE.md)
- [DATABASE.md](./DATABASE.md)
