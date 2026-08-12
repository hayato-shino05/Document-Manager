param(
    [string]$Version,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$appVersionFile = Join-Path $repoRoot "StudyDocumentManager.Core\Services\AppVersion.cs"
$appVersionMatch = Select-String -Path $appVersionFile -Pattern 'Current => "([^"]+)"'
if (-not $appVersionMatch) {
    throw "Could not read AppVersion.Current"
}

$appVersion = $appVersionMatch.Matches[0].Groups[1].Value
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $appVersion
}

if ($Version -ne $appVersion) {
    throw "Version '$Version' does not match AppVersion.Current '$appVersion'"
}

$publishDir = Join-Path $repoRoot "artifacts\publish\win-x64"
$installerDir = Join-Path $repoRoot "artifacts\installer"
$setupScript = Join-Path $repoRoot "setup.iss"
$isccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

if (-not (Test-Path $isccPath)) {
    throw "ISCC.exe was not found at $isccPath"
}

Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $installerDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishDir | Out-Null
New-Item -ItemType Directory -Path $installerDir | Out-Null

dotnet publish "StudyDocumentManager\StudyDocumentManager.csproj" `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:Version=$Version `
    -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$publishedExe = Join-Path $publishDir "DocumentManager.exe"
if (-not (Test-Path $publishedExe)) {
    throw "Publish output is missing DocumentManager.exe"
}

& $isccPath "/DMyAppVersion=$Version" "/DPublishDir=$publishDir" "/DOutputDir=$installerDir" $setupScript
if ($LASTEXITCODE -ne 0) {
    throw "ISCC.exe failed with exit code $LASTEXITCODE"
}

$setupExe = Join-Path $installerDir "DocumentManager_v${Version}_Setup.exe"
if (-not (Test-Path $setupExe)) {
    throw "Setup EXE was not generated: $setupExe"
}

Write-Host "PublishDir=$publishDir"
Write-Host "SetupExe=$setupExe"
