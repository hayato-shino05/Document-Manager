$testsDir = $PSScriptRoot
$files = Get-ChildItem -Path $testsDir -Filter '*.cs' -File

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    $original = $content

    # Property access: .Ten → .Name, .MonHoc → .Subject, etc.
    $content = $content -replace '\.Ten\b', '.Name'
    $content = $content -replace '\.MonHoc\b', '.Subject'
    $content = $content -replace '\.Loai\b', '.Type'
    $content = $content -replace '\.DuongDan\b', '.FilePath'
    $content = $content -replace '\.GhiChu\b', '.Notes'
    $content = $content -replace '\.KichThuoc\b', '.FileSize'
    $content = $content -replace '\.TacGia\b', '.Author'
    $content = $content -replace '\.QuanTrong\b', '.IsImportant'
    $content = $content -replace '\.NgayThem\b', '.CreatedAt'

    # Object initializer: { Ten = → { Name =
    $content = $content -replace '\{ Ten ', '{ Name '
    $content = $content -replace ', Ten ', ', Name '
    $content = $content -replace '\{ MonHoc ', '{ Subject '
    $content = $content -replace ', MonHoc ', ', Subject '
    $content = $content -replace '\{ Loai ', '{ Type '
    $content = $content -replace ', Loai ', ', Type '
    $content = $content -replace '\{ DuongDan ', '{ FilePath '
    $content = $content -replace ', DuongDan ', ', FilePath '
    $content = $content -replace '\{ GhiChu ', '{ Notes '
    $content = $content -replace ', GhiChu ', ', Notes '
    $content = $content -replace '\{ KichThuoc ', '{ FileSize '
    $content = $content -replace ', KichThuoc ', ', FileSize '
    $content = $content -replace '\{ TacGia ', '{ Author '
    $content = $content -replace ', TacGia ', ', Author '
    $content = $content -replace '\{ QuanTrong ', '{ IsImportant '
    $content = $content -replace ', QuanTrong ', ', IsImportant '
    $content = $content -replace '\{ NgayThem ', '{ CreatedAt '
    $content = $content -replace ', NgayThem ', ', CreatedAt '

    if ($content -ne $original) {
        [System.IO.File]::WriteAllText($file.FullName, $content, [System.Text.UTF8Encoding]::new($false))
        Write-Host "Updated: $($file.Name)"
    }
}
Write-Host "Bulk rename complete."
