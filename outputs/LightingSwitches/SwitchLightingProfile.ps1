param(
    [ValidateSet("StudyOnly", "Watch", "GameAuto", "CsAuto")]
    [string]$Mode = "Watch"
)

$ErrorActionPreference = "Stop"
$portableRoot = Split-Path $PSScriptRoot
$toolExe = Join-Path $portableRoot "Tools\SqliteTool.exe"
$workspace = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$dotnet = Join-Path $workspace "work\.dotnet\dotnet.exe"
$tool = Join-Path $workspace "work\SqliteTool\bin\Release\net10.0\SqliteTool.dll"
$artemis = "C:\Program Files\Artemis\Artemis.UI.Windows.exe"

$command = switch ($Mode) {
    "StudyOnly" { "enable-direct-study-ambient" }
    "Watch" { "enable-direct-watch" }
    "GameAuto" { "enable-direct-game-auto" }
    "CsAuto" { "enable-direct-game-auto" }
}

Get-Process | Where-Object { $_.ProcessName -like "*Artemis*" } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

if (Test-Path -LiteralPath $toolExe) {
    & $toolExe $command | Out-Null
}
else {
    & $dotnet $tool $command | Out-Null
}

Start-Process -FilePath $artemis -WindowStyle Hidden
