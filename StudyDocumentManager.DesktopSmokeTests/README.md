# StudyDocumentManager Desktop Smoke Tests

Windows の実ウィンドウを起動し、FlaUI/UI Automation で Avalonia の shell、主要な画面遷移、root marker を確認する xUnit suite です。headless test の代替ではなく、実 desktop runtime の最小 smoke proof を担当します。

## 実行条件

- Windows の interactive desktop session が必要です。
- `StudyDocumentManager.exe` を含む独立した publish folder を用意してください。
- `SDM_DESKTOP_SMOKE_APP` はその publish folder を指す必要があります。
- working tree の `bin\Debug` / `bin\Release` は指定できません。fixture が fail-closed で拒否します。
- CI の non-interactive runner では実行しないでください。環境変数がない場合も、suite は通常の `bin` を推測せず明示的に失敗します。

## PowerShell

リポジトリのルートで実行します。

```powershell
$publishDir = Join-Path $env:TEMP "StudyDocumentManager-desktop-smoke-publish"
dotnet publish ".\StudyDocumentManager\StudyDocumentManager.csproj" `
  -c Debug -r win-x64 --self-contained false -o $publishDir

$env:SDM_DESKTOP_SMOKE_APP = (Resolve-Path $publishDir).Path

dotnet build ".\StudyDocumentManager.DesktopSmokeTests\StudyDocumentManager.DesktopSmokeTests.csproj" -c Debug
dotnet test ".\StudyDocumentManager.DesktopSmokeTests\StudyDocumentManager.DesktopSmokeTests.csproj" -c Debug --no-build
```

### 実行前の条件確認

```powershell
$smokeApp = $env:SDM_DESKTOP_SMOKE_APP
$exe = if ($smokeApp) { Join-Path $smokeApp "StudyDocumentManager.exe" }

[bool]$smokeApp -and
(Test-Path $smokeApp -PathType Container) -and
(Test-Path $exe -PathType Leaf) -and
($smokeApp -notmatch "[\\/]bin[\\/](Debug|Release)([\\/]|$)")
```

`SDM_DESKTOP_SMOKE_APP` が未設定、存在しない、実行ファイルがない、または working-tree の `bin` を指している場合は、publish をやり直してから実行してください。
