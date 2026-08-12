param(
    [Parameter(Mandatory)]
    [string]$SetupExe,
    [string]$WorkingDirectory = (Join-Path ([System.IO.Path]::GetTempPath()) "DocumentManager-installer-smoke"),
    [switch]$VerifyLaunch
)

$ErrorActionPreference = "Stop"

$setupPath = [System.IO.Path]::GetFullPath($SetupExe)
if (-not (Test-Path $setupPath -PathType Leaf)) {
    throw "Setup executable was not found: $setupPath"
}

$workPath = [System.IO.Path]::GetFullPath($WorkingDirectory)
$installPath = Join-Path $workPath "app"
$databasePath = Join-Path $workPath "data\study_documents.db"

if (Test-Path $workPath) {
    for ($attempt = 1; $attempt -le 20; $attempt++) {
        try {
            Remove-Item $workPath -Recurse -Force -ErrorAction Stop
            break
        }
        catch [System.IO.IOException] {
            if ($attempt -eq 20) {
                throw
            }
            Start-Sleep -Milliseconds 250
        }
        catch [System.UnauthorizedAccessException] {
            if ($attempt -eq 20) {
                throw
            }
            Start-Sleep -Milliseconds 250
        }
    }
}

New-Item -ItemType Directory -Path (Split-Path -Parent $databasePath) | Out-Null

function Invoke-SetupProcess([string]$fileName, [string[]]$arguments, [hashtable]$environment) {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new($fileName)
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true

    $startInfo.Arguments = ($arguments | ForEach-Object {
        if ($_ -match '[\s"]') {
            '"' + $_.Replace('"', '\"') + '"'
        }
        else {
            $_
        }
    }) -join ' '

    foreach ($entry in $environment.GetEnumerator()) {
        $startInfo.EnvironmentVariables[$entry.Key] = $entry.Value
    }

    $process = [System.Diagnostics.Process]::Start($startInfo)
    $process.WaitForExit()

    if ($process.ExitCode -ne 0) {
        throw "Process failed with exit code $($process.ExitCode): $fileName"
    }
}

try {
    $environment = @{ SDM_DATABASE_PATH = $databasePath }
    Invoke-SetupProcess $setupPath @("/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-", "/DIR=$installPath") $environment

    $installedExe = Join-Path $installPath "DocumentManager.exe"
    if (-not (Test-Path $installedExe -PathType Leaf)) {
        throw "Installed application executable was not found: $installedExe"
    }

    if ($VerifyLaunch) {
        $appStartInfo = [System.Diagnostics.ProcessStartInfo]::new($installedExe)
        $appStartInfo.WorkingDirectory = $installPath
        $appStartInfo.UseShellExecute = $false
        $appStartInfo.EnvironmentVariables["SDM_DATABASE_PATH"] = $databasePath
        $appProcess = [System.Diagnostics.Process]::Start($appStartInfo)
        if ($null -eq $appProcess) {
            throw "Installed application process could not be started."
        }
        try {
            if ($appProcess.WaitForExit(5000)) {
                throw "Application exited during launch smoke with exit code $($appProcess.ExitCode)."
            }
            if (-not $appProcess.CloseMainWindow() -or -not $appProcess.WaitForExit(10000)) {
                $appProcess.Kill()
                $appProcess.WaitForExit()
            }
        }
        finally {
            if (-not $appProcess.HasExited) {
                $appProcess.Kill()
                $appProcess.WaitForExit()
            }
            $appProcess.Dispose()
        }

        if (-not (Test-Path $databasePath -PathType Leaf)) {
            throw "The isolated application database was not created: $databasePath"
        }
    }

    $uninstaller = Join-Path $installPath "unins000.exe"
    if (-not (Test-Path $uninstaller -PathType Leaf)) {
        throw "Uninstaller was not found: $uninstaller"
    }

    Invoke-SetupProcess $uninstaller @("/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-") $environment

    if (Test-Path $installedExe -PathType Leaf) {
        throw "Application executable still exists after uninstall: $installedExe"
    }

    if ($VerifyLaunch -and -not (Test-Path $databasePath -PathType Leaf)) {
        throw "The isolated application database was removed by uninstall: $databasePath"
    }

    Write-Host "InstallPath=$installPath"
    if ($VerifyLaunch) {
        Write-Host "DatabasePath=$databasePath"
    }
}
finally {
    if (Test-Path $workPath) {
        for ($attempt = 1; $attempt -le 20; $attempt++) {
            try {
                Remove-Item $workPath -Recurse -Force -ErrorAction Stop
                break
            }
            catch [System.IO.IOException] {
                if ($attempt -eq 20) {
                    throw
                }
                Start-Sleep -Milliseconds 250
            }
            catch [System.UnauthorizedAccessException] {
                if ($attempt -eq 20) {
                    throw
                }
                Start-Sleep -Milliseconds 250
            }
        }
    }
}
