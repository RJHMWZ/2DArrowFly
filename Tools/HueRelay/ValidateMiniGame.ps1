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
$arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $validationPath + '"'), '-executeMethod', 'HueRelay.Editor.HueMiniGameTools.BatchValidate', '-logFile', ('"' + $logPath + '"'))
$process = Start-Process -FilePath $UnityEditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
foreach ($file in @('validation.txt', 'playmode.txt', 'dependencies.txt')) {
    $report = Join-Path $validationPath ('Logs\MiniGameValidation\' + $file)
    if (Test-Path -LiteralPath $report) {
        Copy-Item -LiteralPath $report -Destination (Join-Path $projectPath ('Logs\MiniGameValidation\' + $file)) -Force
        if ($file -ne 'dependencies.txt') { Get-Content -LiteralPath $report }
    }
}
exit $process.ExitCode
