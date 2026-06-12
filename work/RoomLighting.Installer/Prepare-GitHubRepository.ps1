param(
    [string]$Destination
)

$ErrorActionPreference = "Stop"

$workspace = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path $workspace "outputs\GitHub\ArtemisRoomLighting"
}
$destinationRoot = [System.IO.Path]::GetFullPath($Destination)
$allowedRoot = [System.IO.Path]::GetFullPath((Join-Path $workspace "outputs\GitHub"))
if (-not $destinationRoot.StartsWith($allowedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The public repository destination must stay under $allowedRoot"
}

if (Test-Path -LiteralPath $destinationRoot) {
    Remove-Item -LiteralPath $destinationRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $destinationRoot | Out-Null

function Copy-SourceDirectory {
    param(
        [string]$Source,
        [string]$DestinationPath,
        [string[]]$Exclude = @()
    )
    New-Item -ItemType Directory -Force -Path $DestinationPath | Out-Null
    Get-ChildItem -LiteralPath $Source -File | Where-Object {
        $_.Name -notin $Exclude
    } | Copy-Item -Destination $DestinationPath -Force
}

$template = Join-Path $workspace "work\PublicRepositoryTemplate"
Copy-Item -Path (Join-Path $template "*") -Destination $destinationRoot -Recurse -Force

Copy-SourceDirectory `
    (Join-Path $workspace "work\Artemis.Plugins.DirectDevices") `
    (Join-Path $destinationRoot "work\Artemis.Plugins.DirectDevices") `
    @("Directory.Build.props")
Copy-Item -LiteralPath (Join-Path $workspace "work\Artemis.Plugins.DirectDevices\Directory.Build.props") `
    -Destination (Join-Path $destinationRoot "work\Artemis.Plugins.DirectDevices") -Force

Copy-SourceDirectory `
    (Join-Path $workspace "work\SqliteTool") `
    (Join-Path $destinationRoot "work\SqliteTool")
Copy-SourceDirectory `
    (Join-Path $workspace "work\DirectDevicesLogicTest") `
    (Join-Path $destinationRoot "work\DirectDevicesLogicTest")
Copy-SourceDirectory `
    (Join-Path $workspace "work\RoomLighting.Installer") `
    (Join-Path $destinationRoot "work\RoomLighting.Installer") `
    @("Payload.zip")

New-Item -ItemType Directory -Force -Path (Join-Path $destinationRoot "work\RoomLighting.Installer\Assets") | Out-Null
Copy-Item -Path (Join-Path $workspace "work\RoomLighting.Installer\Assets\*") `
    -Destination (Join-Path $destinationRoot "work\RoomLighting.Installer\Assets") -Force

New-Item -ItemType Directory -Force -Path (Join-Path $destinationRoot "outputs\LightingSwitches") | Out-Null
Copy-Item -Path (Join-Path $workspace "outputs\LightingSwitches\*") `
    -Destination (Join-Path $destinationRoot "outputs\LightingSwitches") -Recurse -Force

$mappingRoot = Join-Path $destinationRoot "work\src\Artemis.Plugins"
$mappingDestination = Join-Path $mappingRoot "src\Devices\Artemis.Plugins.Devices.Razer"
New-Item -ItemType Directory -Force -Path $mappingDestination | Out-Null
Copy-Item -LiteralPath (Join-Path $workspace "work\src\Artemis.Plugins\src\Devices\Artemis.Plugins.Devices.Razer\LedMappings.cs") `
    -Destination $mappingDestination -Force
Copy-Item -LiteralPath (Join-Path $workspace "work\src\Artemis.Plugins\LICENSE") `
    -Destination (Join-Path $destinationRoot "LICENSE") -Force

Write-Host "GitHub-ready repository prepared at:"
Write-Host "  $destinationRoot"
