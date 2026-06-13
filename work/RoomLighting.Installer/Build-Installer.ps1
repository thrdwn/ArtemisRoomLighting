param(
    [string]$Configuration = "Release",
    [string]$DotnetPath = ""
)

$ErrorActionPreference = "Stop"

$workspace = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$dotnet = $DotnetPath
if ([string]::IsNullOrWhiteSpace($dotnet)) {
    $bundledDotnet = Join-Path $workspace "work\.dotnet\dotnet.exe"
    $dotnet = if (Test-Path -LiteralPath $bundledDotnet) { $bundledDotnet } else { "dotnet" }
}

$sdks = & $dotnet --list-sdks
if ($LASTEXITCODE -ne 0 -or -not $sdks) {
    throw "A .NET 10 SDK is required. Pass -DotnetPath or install the SDK from https://dotnet.microsoft.com/download."
}

$pluginProject = Join-Path $workspace "work\Artemis.Plugins.DirectDevices\Artemis.Plugins.DirectDevices.csproj"
$toolProject = Join-Path $workspace "work\SqliteTool\SqliteTool.csproj"
$installerProject = Join-Path $PSScriptRoot "RoomLighting.Installer.csproj"
$stage = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "stage"))
$publish = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "publish"))
$payload = Join-Path $PSScriptRoot "Payload.zip"
$output = Join-Path $workspace "outputs\ArtemisRoomLightingSetup-0.13.1.0.exe"

function Reset-BuildDirectory {
    param([string]$Path)
    $resolved = [System.IO.Path]::GetFullPath($Path)
    if (-not $resolved.StartsWith($workspace, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to reset a directory outside the workspace: $resolved"
    }
    if (Test-Path -LiteralPath $resolved) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
    New-Item -ItemType Directory -Path $resolved | Out-Null
}

Reset-BuildDirectory $stage
Reset-BuildDirectory $publish

& $dotnet build $pluginProject -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Plugin build failed." }

$toolStage = Join-Path $stage "Tools"
New-Item -ItemType Directory -Path $toolStage | Out-Null
& $dotnet publish $toolProject -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $toolStage
if ($LASTEXITCODE -ne 0) { throw "Configuration tool publish failed." }

$pluginStage = Join-Path $stage "Plugin"
$switchStage = Join-Path $stage "LightingSwitches"
$gsiStage = Join-Path $stage "Cs2Gsi"
New-Item -ItemType Directory -Path $pluginStage | Out-Null
New-Item -ItemType Directory -Path $switchStage | Out-Null
New-Item -ItemType Directory -Path $gsiStage | Out-Null

$pluginBuild = Join-Path $workspace "work\Artemis.Plugins.DirectDevices\bin\$Configuration\net10.0"
Copy-Item -Path (Join-Path $pluginBuild "*") -Destination $pluginStage -Recurse -Force
Copy-Item -Path (Join-Path $workspace "outputs\LightingSwitches\*") -Destination $switchStage -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Assets\gamestate_integration_artemis_room_lighting.cfg") -Destination $gsiStage -Force

if (Test-Path -LiteralPath $payload) {
    Remove-Item -LiteralPath $payload -Force
}
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $payload -CompressionLevel Optimal

& $dotnet publish $installerProject -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publish
if ($LASTEXITCODE -ne 0) { throw "Installer publish failed." }

Copy-Item -LiteralPath (Join-Path $publish "ArtemisRoomLightingSetup.exe") -Destination $output -Force

$hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
Write-Host ""
Write-Host "Installer built:"
Write-Host "  $output"
Write-Host "SHA256:"
Write-Host "  $hash"
