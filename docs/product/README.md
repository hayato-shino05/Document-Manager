# Product Docs

This directory is intentionally generic and mostly empty in Harness v0.

When a user provides a project spec, derive smaller product contract files here
instead of keeping one large spec as the living plan. Name files by the product
domains that actually exist in that spec, for example `overview.md`,
`billing.md`, `workflows.md`, `permissions.md`, or `api-conventions.md`.

Do not create domain files before the spec just to fill the folder. Empty
structure is healthier than fake product truth.

## Update Rule

When behavior changes:

1. Update the affected product doc.
2. Update or create the story packet.
3. Update `docs/TEST_MATRIX.md`.
4. Record a decision if the change affects architecture, scope, risk, or a
   previously settled product rule.

## Project Overlay — Study Document Manager

This repository already has a concrete product. Product docs created here should describe the current desktop application in small durable slices.

### Candidate product doc slices

- overview.md for the desktop study-document workflow.
- documents.md for CRUD, search, filters, deadlines, and importance.
- collections.md for collection behavior.
- notes-and-relations.md for personal notes, related documents, and recent files.
- reports.md for report and TreeMap behavior.
- data-rules.md for user-visible deletion, restore, and backup behavior.

### Current product facts

- The app is a desktop study document manager.
- Main stack: Avalonia, .NET 9, MVVM, SQLite.
- Product behavior exists in code under StudyDocumentManager* and proof exists in StudyDocumentManager.Tests.
