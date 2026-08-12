# Execution Plan: インストーラー言語選択と OS ロケール連携

Date: 2026-08-12

## Status

Active

## Outcome

Inno Setup のウィザードで日本語、英語、ベトナム語、中国語を選択でき、Windows UI language に一致する言語を自動選択します。アプリは保存済み `app_settings.language` を優先し、未設定、空値、空白値、無効値のときだけ `CultureInfo.CurrentUICulture` を `SupportedLanguage` に変換して保存します。対応外ロケールは日本語へフォールバックします。

## Context

- Issue: #28
- Pull request: #29
- Runtime language source: `StudyDocumentManager/Services/LocalizationService.cs`
- Startup composition: `StudyDocumentManager/App.axaml.cs`
- Persisted language: `app_settings.language` through `ISettingsService`
- Installer: `setup.iss`
- Supported values: `Japanese`, `English`, `Vietnamese`, `Chinese`

## Scope

In scope:

- `CultureInfo.TwoLetterISOLanguageName` based mapping for `ja`, `en`, `vi`, `zh`.
- Case-insensitive enum parsing and saved-setting precedence.
- Startup ordering: database initialization, language decision, `SetLanguage`, `Resources["Loc"]`, then `MainWindowModel` creation.
- Inno Setup language entries and Windows UI language detection.
- Resolver and startup behavior tests, Release build/test evidence.

Out of scope:

- Passing the Inno Setup selection into the application.
- Database schema or migration changes.
- New ResX keys or translations.
- Existing installer elevation and `{localappdata}` policy.

## Approach

1. Resolve `SupportedLanguage` from a saved enum name when valid.
2. Otherwise map `CultureInfo.TwoLetterISOLanguageName` case-insensitively; regional variants and neutral cultures use the same two-letter code, and unsupported or invalid values fall back to Japanese.
3. Persist the normalized language before registering `Resources["Loc"]`.
4. Keep `MainWindowModel` settings loading compatible because the startup value is already persisted before model construction.
5. Configure Inno Setup with official compiler language files and `LanguageDetectionMethod=uilanguage`, `ShowLanguageDialog=auto`. The installer choice is UI-only and never overwrites app settings.

## Risks And Recovery

- Risk: a missing Inno Setup compiler prevents syntax validation. Mitigation: record `ISCC.exe` unavailable, keep the script minimal, and validate it in Windows packaging CI or a machine with Inno Setup installed.
- Risk: startup language is overwritten by model initialization. Mitigation: initialize and persist language before `MainWindowModel` is resolved; test the existing saved-language flow.
- Recovery: revert the latest PR commit, rerun Debug/Release build and tests, and keep the Issue open until the failed stage is corrected.

## Progress

- [x] Add OS locale to `SupportedLanguage` resolver with saved-value precedence.
- [x] Apply language before `Resources["Loc"]` registration and `MainWindowModel` creation.
- [x] Add four Inno Setup language entries and Windows UI language detection.
- [x] Add resolver tests and run Debug/Release verification.
- [x] Run Inno Setup compiler syntax/build verification with Inno Setup 6.7.0 and verify `DocumentManager_v4.0.0_Setup.exe` output.
- [ ] Run installer lifecycle verification from a clean Windows environment, then re-read review threads and CI.

## Decisions

- 2026-08-12: The installer language is UI-only. Passing it to the app could overwrite a language selected by an existing user during reinstall or upgrade.
- 2026-08-12: The app fallback language is Japanese, matching the existing product default.
- 2026-08-12: Locale input is normalized through `TwoLetterISOLanguageName`; `ja-JP`, `ja`, and casing variants resolve identically.

## Validation

- Focused proof: `SupportedLanguageResolverTests`, 8 passing cases for supported, unsupported, saved, and invalid values.
- Integration or end-to-end proof: Release build succeeded with 0 warnings and 0 errors; Release test suite passed 900/900. Inno Setup 6.7.0 built `DocumentManager_v4.0.0_Setup.exe`; `Check & Build`, Linux package, and Vercel Preview checks passed on PR #33.
- Repository-required checks: `git diff --check` passed. The remaining installer proof requires a clean Windows VM for standard-user/UAC behavior and user-data retention.

## Result

Installer language entries and OS-locale startup resolution are implemented and compiler-verified. The clean-VM installer lifecycle and desktop runtime smoke gates remain release requirements; valid review findings and CI failures must be resolved before a marketing release.
