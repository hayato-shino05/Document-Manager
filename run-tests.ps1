# Script: Run all tests for StudyDocumentManager new source
# Usage: .\run-tests.ps1

Write-Host "=== Building test project ===" -ForegroundColor Cyan
dotnet build "tests\StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -v:minimal

if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD FAILED - Stopping." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== Running tests ===" -ForegroundColor Cyan
dotnet test "tests\StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" --no-build -v:normal --logger "console;verbosity=detailed"

Write-Host ""
if ($LASTEXITCODE -eq 0) {
    Write-Host "ALL TESTS PASSED" -ForegroundColor Green
} else {
    Write-Host "SOME TESTS FAILED" -ForegroundColor Red
}
