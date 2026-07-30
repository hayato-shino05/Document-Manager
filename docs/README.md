# Documentation Map

This directory holds the project harness and any product contract derived from a
future user-provided spec.

## Main Files

- `HARNESS.md`: how humans and agents collaborate.
- `FEATURE_INTAKE.md`: how prompts become tiny, normal, or high-risk work.
- `ARCHITECTURE.md`: architecture discovery and boundary rules.
- `TEST_MATRIX.md`: living map of behavior to proof.
- `HARNESS_BACKLOG.md`: improvements discovered while working.
- `GLOSSARY.md`: shared terms.

## Folders

- `product/`: current product truth, empty until a spec is derived.
- `stories/`: feature packets and backlog.
- `decisions/`: durable decisions and tradeoffs.
- `demo/`: concrete walkthroughs that show how the harness transforms input
  into agent-ready work.
- `templates/`: reusable spec-intake, story, plan, decision, and validation
  formats.

## Current State

Harness v0 exists before implementation. These docs define how the project will
grow; they do not imply that app code, tests, CI, or deployment automation exist
yet.

## Project Overlay — Study Document Manager

This repository already contains an implemented desktop application. The harness docs coexist with live product code and should be read as the operating layer above the current Avalonia app.

### Implemented stack

- UI: Avalonia 11.2.7 desktop application.
- Runtime: .NET 9.0.
- Pattern: MVVM via CommunityToolkit.Mvvm.
- DI: Microsoft.Extensions.DependencyInjection.
- Persistence: SQLite via Microsoft.Data.Sqlite.
- Projects: StudyDocumentManager, StudyDocumentManager.Core, StudyDocumentManager.Data, StudyDocumentManager.Tests.

### Repo-specific reading order

After the generic harness files, read:

- PROJECT_STRUCTURE.md
- DATABASE.md
- StudyDocumentManager/App.axaml.cs
- StudyDocumentManager/Views/MainWindow.axaml
- StudyDocumentManager/Models/*Model.cs
- StudyDocumentManager.Data/Helpers/DatabaseHelper.cs
- StudyDocumentManager.Tests/*

### Important note

The generic harness text about future implementation explains the harness model, but must not be interpreted as meaning this repository is empty. Implementation already exists and harness work should guide changes to that implementation.
