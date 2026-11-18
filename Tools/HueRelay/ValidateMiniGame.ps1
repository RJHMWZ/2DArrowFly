param([string]$UnityEditorPath)
$ErrorActionPreference = 'Stop'
$projectPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$validationPath = Join-Path $projectPath 'Logs\MiniGameValidation\ValidationProject'
if (-not $UnityEditorPath) {
    $activeEditor = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
        Where-Object { $_.CommandLine -like "*$projectPath*" -and $_.CommandLine -notlike '*AssetImportWorker*' } |
        Select-Object -First 1
    if ($activeEditor) { $UnityEditorPath = $activeEditor.ExecutablePath }
}
if (-not $UnityEditorPath -or -not (Test-Path -LiteralPath $UnityEditorPath)) {
    throw 'Pass -UnityEditorPath with the full path to Unity 2022.3 Unity.exe.'
}
New-Item -ItemType Directory -Path $validationPath -Force | Out-Null
foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $projectPath $folder) -Destination $validationPath -Recurse -Force
}
# Mirror deleted source assets into this disposable copy. Check every absolute
# target before removing it, and use native PowerShell file operations throughout.
$validationAssets = [IO.Path]::GetFullPath((Join-Path $validationPath 'Assets')) + '\'
$sourceAssets = [IO.Path]::GetFullPath((Join-Path $projectPath 'Assets')) + '\'
foreach ($file in Get-ChildItem -LiteralPath $validationAssets -File -Recurse) {
    $targetPath = [IO.Path]::GetFullPath($file.FullName)
    if (-not $targetPath.StartsWith($validationAssets, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Validation cleanup target is outside the disposable Assets folder.'
    }
    $relativePath = $targetPath.Substring($validationAssets.Length)
    if (-not (Test-Path -LiteralPath (Join-Path $sourceAssets $relativePath))) {
        Remove-Item -LiteralPath $targetPath
    }
}
# The isolated check does not need an agent connection to the editor.
$manifestPath = Join-Path $validationPath 'Packages\manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifest.dependencies.PSObject.Properties.Remove('com.coplaydev.unity-mcp')
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
$logPath = Join-Path $projectPath 'Logs\MiniGameValidation\unity-validation.log'
# Reports from an older run must never be presented as this run's result.
$reportDirectory = Join-Path $validationPath 'Logs\MiniGameValidation'
foreach ($reportName in @('validation.txt', 'playmode.txt', 'dependencies.txt')) {
    $previousReport = Join-Path $reportDirectory $reportName
    if (Test-Path -LiteralPath $previousReport) { Remove-Item -LiteralPath $previousReport }
}
$arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $validationPath + '"'), '-executeMethod', 'HueRelay.Editor.HueMiniGameTools.BatchValidate', '-logFile', ('"' + $logPath + '"'))
$process = Start-Process -FilePath $UnityEditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) {
    if (Test-Path -LiteralPath $logPath) {
        Select-String -LiteralPath $logPath -Pattern 'error CS|Exception:|Aborting batchmode' |
            Select-Object -First 20 | ForEach-Object { $_.Line }
    }
    Write-Error ('Unity validation failed. See ' + $logPath) -ErrorAction Continue
    exit $process.ExitCode
}
foreach ($requiredReport in @('validation.txt', 'playmode.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $reportDirectory $requiredReport))) {
        throw ('Unity exited without producing the required report: ' + $requiredReport)
    }
}
foreach ($file in @('validation.txt', 'playmode.txt', 'dependencies.txt')) {
    $report = Join-Path $validationPath ('Logs\MiniGameValidation\' + $file)
    if (Test-Path -LiteralPath $report) {
        Copy-Item -LiteralPath $report -Destination (Join-Path $projectPath ('Logs\MiniGameValidation\' + $file)) -Force
        if ($file -ne 'dependencies.txt') { Get-Content -LiteralPath $report }
    }
}
exit $process.ExitCode
