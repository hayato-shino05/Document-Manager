# Harness

The project goal is to provide a reusable operating harness that lets humans and
agents turn a future product spec into safe, validated work.

The app is what users touch. The harness is what agents touch.

## Mental Model

```text
------------------+
| Human intent    |
+------------------+
         |
         v
+------------------+
| Feature intake   |
+------------------+
         |
         v
+------------------+
| Story packet     |
+------------------+
         |
         v
+------------------+
| Agent work loop  |
+------------------+
         |
         v
+------------------+
| Product delta    |
+------------------+
         |
         v
+------------------+
| Validation proof |
+------------------+
         |
         v
+------------------+
| Harness delta    |
+------------------+
         |
         v
+------------------+
| Next intent      |
+------------------+
```

Every task has two possible outputs:

1. Product delta: app code, tests, API shape, data model, or product docs.
2. Harness delta: docs, templates, validation expectations, backlog items, or
   decision records that make the next task easier.

## Harness v0 Scope

Harness v0 includes:

- Agent entrypoint.
- Empty product documentation structure.
- Feature intake and risk lanes.
- Story templates.
- Decision log template.
- Validation report template.
- Test matrix placeholder.
- Harness growth backlog.

Harness v0 deliberately excludes:

- A project-specific `SPEC.md`.
- Pre-sliced product domains.
- A locked application stack.
- App source scaffolding.
- Package scripts.
- Test runner config.
- CI workflows.
- Database migrations or infrastructure.

Those should arrive only when a selected story needs them.

## Source Hierarchy

```text
User-provided spec or prompt
  input material for first buildout or future changes

docs/product/*
  current product contract derived from accepted input

docs/stories/*
  story-sized work packets and historical evidence

docs/TEST_MATRIX.md
  behavior-to-proof control panel

docs/decisions/*
  why the contract changed
```

Before implementation, product docs describe intent. After implementation,
product docs plus executable tests become the living contract.

## Spec Lifecycle

Harness v0 starts without a tracked project spec. When the human provides a
specification, treat it as input material, not as a permanent operating manual.
Use it to populate product docs, story packets, architecture decisions, and
validation expectations during the first buildout.

After the specification has been decomposed, do not keep extending it as the
living product plan. Ongoing work should update the smaller product docs,
stories, test matrix, and decision records.

Ongoing work should enter the harness as one of these input types:

- New spec: a project specification that needs to become product docs and
  initial story candidates.
- Spec slice: a selected behavior from the provided spec.
- Change request: a bounded behavior change, bug fix, or product refinement.
- New initiative: a larger product area that needs multiple stories.
- Maintenance request: dependency, architecture, performance, security, or
  operational work.
- Harness improvement: a process, template, proof, or agent-instruction change.

The spec-to-work loop is:

```text
human intent or supplied spec
  -> classify input type
  -> update or create product contract
  -> create story packet or initiative notes when needed
  -> define validation proof
  -> implement or document the blocker
  -> update product docs, stories, test matrix, and decisions
  -> capture harness friction
```

Large product areas should use scoped initiative notes instead of a second
monolithic specification. An initiative should explain the goal, affected
product docs, candidate stories, validation shape, open decisions, and exit
criteria. If initiative work becomes a repeated pattern, add a template or
proposal to `docs/HARNESS_BACKLOG.md`.

## Growth Rule

The harness grows from friction.

When an agent is confused, repeats manual reasoning, needs a new validation
command, discovers a missing rule, or sees a recurring failure pattern, it must
either improve the harness directly or add a proposal to `HARNESS_BACKLOG.md`.

## Future Validation Ladder

No validation scripts exist yet. When implementation begins, the expected ladder
is:

```text
validate:quick
  format, lint, typecheck, unit tests, architecture check

test:integration
  backend, database, provider, or service checks as the stack requires

test:e2e
  user-visible end-to-end flows

test:platform
  shell, mobile, desktop, or deployment smoke checks as the stack requires

test:release
  full suite, log checks, and performance smoke
```

Agents must not claim these commands pass until they exist and have been run.

## Project Overlay — Study Document Manager

This repository already has a concrete implementation. The harness here is used to steer changes to an existing Avalonia desktop app rather than to start from zero.

### Current application reality

- Product surface: desktop.
- Desktop framework: Avalonia 11.2.7.
- Runtime: .NET 9.0.
- Presentation model: MVVM with `CommunityToolkit.Mvvm`.
- Data storage: SQLite via `Microsoft.Data.Sqlite`.
- Startup composition: `StudyDocumentManager/Program.cs` and `StudyDocumentManager/App.axaml.cs`.
- Shell orchestration: `StudyDocumentManager/Views/MainWindow.axaml` + `StudyDocumentManager/Models/MainWindowModel.cs`.

### How to apply the harness in this repo

When the harness says product delta, read that as changes to the existing `StudyDocumentManager*` projects.

When the harness says story packet, the packet should point to the actual current areas, such as:

- `StudyDocumentManager/Views/*`
- `StudyDocumentManager/Models/*`
- `StudyDocumentManager.Core/Interfaces/*`
- `StudyDocumentManager.Data/Repositories/*`
- `StudyDocumentManager.Data/Helpers/DatabaseHelper.cs`
- `StudyDocumentManager.Tests/*`

### Repo-specific source hierarchy

```text
user request or selected story
  -> AGENTS.md
  -> docs/HARNESS.md + docs/FEATURE_INTAKE.md
  -> PROJECT_STRUCTURE.md
  -> DATABASE.md when data is involved
  -> current source files under StudyDocumentManager*
  -> docs/TEST_MATRIX.md for expected proof
```

### Current constraints worth preserving

- Do not replace the current Avalonia stack unless the user explicitly requests it.
- Do not route new data access around the Core interfaces and Data repositories without a strong reason.
- Do not reintroduce old WinForms naming or schema as active truth. Historical names are migration context only.
- Keep shared theme information in the existing Avalonia theme resources instead of scattering UI literals.
