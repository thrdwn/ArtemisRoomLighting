param([string]$InitialTab = "")

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$portableRoot = Split-Path $PSScriptRoot
$toolExe = Join-Path $portableRoot "Tools\SqliteTool.exe"
$workspace = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$dotnet = Join-Path $workspace "work\.dotnet\dotnet.exe"
$tool = Join-Path $workspace "work\SqliteTool\bin\Release\net10.0\SqliteTool.dll"
$artemis = "C:\Program Files\Artemis\Artemis.UI.Windows.exe"

function Invoke-LightingTool {
    param([string[]]$Arguments)
    if (Test-Path -LiteralPath $toolExe) {
        & $toolExe @Arguments
    }
    else {
        $dotnetArguments = @($tool) + $Arguments
        & $dotnet @dotnetArguments
    }
}

$configuration = [pscustomobject]@{
    study = $true
    upperRole = "SoftDepth"
    lowerRole = "Off"
    upperGameRole = "ObjectiveAlerts"
    lowerGameRole = "HealthDamage"
    razerKeyboard = $true
    razerMouse = $true
    razerDock = $true
    lenovoKeyboard = $false
    blackoutOnBlack = $true
    ambientFps = 10
    gameFps = 10
    watchStudyStrength = 135
    watchRearStrength = 165
    watchRazerStrength = 120
    watchColorBoost = 108
    csFlashIntensity = 150
    csFireIntensity = 145
    csSmokeIntensity = 115
    csDeathIntensity = 150
    csImpactIntensity = 135
    csBombIntensity = 130
    csClutchIntensity = 125
    csTeamContrast = 145
    csUtilityBrightness = 150
    csNumpadLayout = "NumLock=SelectedGrenade;Divide=Armor;Multiply=Helmet;Minus=DefuseKit;Num7=Flash;Num8=Smoke;Num9=HE;Plus=Bomb;Num4=Fire;Num5=Decoy;Num6=AmmoState;Num1=Health1;Num2=Health2;Num3=Health3;Enter=Ammo3;Num0=Ammo1;Decimal=Ammo2"
    currentMode = "Study"
    autoGameEnabled = $true
}

try {
    $line = Invoke-LightingTool @("get-watch-config") | Select-Object -Last 1
    if ($line) {
        $configuration = $line | ConvertFrom-Json
    }
}
catch {
    # Defaults keep the control available if Artemis still has the database busy.
}

$background = [System.Drawing.Color]::FromArgb(24, 26, 30)
$surface = [System.Drawing.Color]::FromArgb(34, 37, 43)
$surfaceSelected = [System.Drawing.Color]::FromArgb(44, 103, 190)
$border = [System.Drawing.Color]::FromArgb(72, 77, 87)
$muted = [System.Drawing.Color]::FromArgb(174, 181, 192)
$text = [System.Drawing.Color]::FromArgb(244, 246, 249)

$form = New-Object System.Windows.Forms.Form
$form.Text = "Lighting Control"
$form.ClientSize = New-Object System.Drawing.Size(680, 590)
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedDialog
$form.MaximizeBox = $false
$form.MinimizeBox = $false
$form.StartPosition = [System.Windows.Forms.FormStartPosition]::CenterScreen
$form.BackColor = $background
$form.ForeColor = $text
$form.Font = New-Object System.Drawing.Font("Segoe UI", 10)

$title = New-Object System.Windows.Forms.Label
$title.Text = "Lighting Control"
$title.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 17)
$title.Location = New-Object System.Drawing.Point(24, 18)
$title.AutoSize = $true
$form.Controls.Add($title)

$status = New-Object System.Windows.Forms.Label
$status.Text = "Study keeps the lamp bright; Watch follows the screen more closely."
$status.ForeColor = $muted
$status.Location = New-Object System.Drawing.Point(26, 55)
$status.Size = New-Object System.Drawing.Size(620, 24)
$form.Controls.Add($status)

$modeButtons = @{}
$modeDefinitions = @(
    @("Study", "Study"),
    @("Watch", "Watch")
)

$modeX = 24
foreach ($definition in $modeDefinitions) {
    $radio = New-Object System.Windows.Forms.RadioButton
    $radio.Appearance = [System.Windows.Forms.Appearance]::Button
    $radio.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $radio.FlatAppearance.BorderColor = $border
    $radio.FlatAppearance.BorderSize = 1
    $radio.TextAlign = [System.Drawing.ContentAlignment]::MiddleCenter
    $radio.Text = $definition[0]
    $radio.Tag = $definition[1]
    $radio.Location = New-Object System.Drawing.Point($modeX, 86)
    $radio.Size = New-Object System.Drawing.Size(308, 48)
    $radio.BackColor = $surface
    $radio.ForeColor = $text
    $radio.Add_CheckedChanged({
        if ($this.Checked) {
            $this.BackColor = $surfaceSelected
            foreach ($other in $modeButtons.Values) {
                if ($other -ne $this) {
                    $other.BackColor = $surface
                }
            }
        }
    })
    $modeButtons[$definition[1]] = $radio
    $form.Controls.Add($radio)
    $modeX += 324
}

$savedMode = if ($configuration.PSObject.Properties.Name -contains "currentMode") {
    [string]$configuration.currentMode
}
else {
    "Study"
}
if (-not $modeButtons.ContainsKey($savedMode)) {
    $savedMode = "Study"
}
$modeButtons[$savedMode].Checked = $true

$tabs = New-Object System.Windows.Forms.TabControl
$tabs.Location = New-Object System.Drawing.Point(24, 150)
$tabs.Size = New-Object System.Drawing.Size(632, 350)
$tabs.BackColor = $surface
$tabs.ForeColor = [System.Drawing.Color]::Black
$form.Controls.Add($tabs)

function New-Tab {
    param([string]$Name)
    $page = New-Object System.Windows.Forms.TabPage
    $page.Text = $Name
    $page.BackColor = $surface
    $page.ForeColor = $text
    $page.Padding = New-Object System.Windows.Forms.Padding(18)
    [void]$tabs.TabPages.Add($page)
    return $page
}

function New-Check {
    param(
        [System.Windows.Forms.Control]$Parent,
        [string]$Label,
        [bool]$Checked,
        [int]$X,
        [int]$Y,
        [int]$Width = 270
    )
    $control = New-Object System.Windows.Forms.CheckBox
    $control.Text = $Label
    $control.Checked = $Checked
    $control.Location = New-Object System.Drawing.Point($X, $Y)
    $control.Size = New-Object System.Drawing.Size($Width, 28)
    $control.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $control.ForeColor = $text
    $Parent.Controls.Add($control)
    return $control
}

function New-Combo {
    param(
        [System.Windows.Forms.Control]$Parent,
        [string]$Label,
        [System.Collections.IDictionary]$Choices,
        [string]$Current,
        [int]$Y
    )
    $caption = New-Object System.Windows.Forms.Label
    $caption.Text = $Label
    $caption.Location = New-Object System.Drawing.Point(24, $Y)
    $caption.Size = New-Object System.Drawing.Size(225, 26)
    $caption.ForeColor = $text
    $Parent.Controls.Add($caption)

    $combo = New-Object System.Windows.Forms.ComboBox
    $combo.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDownList
    $combo.Location = New-Object System.Drawing.Point(274, ($Y - 3))
    $combo.Size = New-Object System.Drawing.Size(300, 30)
    foreach ($display in $Choices.Keys) {
        [void]$combo.Items.Add($display)
    }
    $selected = $Choices.Keys | Where-Object { $Choices[$_] -eq $Current } | Select-Object -First 1
    if (-not $selected) {
        $selected = $Choices.Keys | Select-Object -First 1
    }
    $combo.SelectedItem = $selected
    $Parent.Controls.Add($combo)
    return $combo
}

function Get-ConfigInt {
    param(
        [string]$Name,
        [int]$Default
    )
    if ($configuration.PSObject.Properties.Name -contains $Name) {
        return [Math]::Min(200, [Math]::Max(0, [int]$configuration.$Name))
    }

    return $Default
}

function New-PercentSlider {
    param(
        [System.Windows.Forms.Control]$Parent,
        [string]$Label,
        [int]$Value,
        [int]$X,
        [int]$Y
    )
    $caption = New-Object System.Windows.Forms.Label
    $caption.Text = $Label
    $caption.Location = New-Object System.Drawing.Point($X, $Y)
    $caption.Size = New-Object System.Drawing.Size(210, 22)
    $caption.ForeColor = $text
    $Parent.Controls.Add($caption)

    $track = New-Object System.Windows.Forms.TrackBar
    $track.Location = New-Object System.Drawing.Point($X, ($Y + 22))
    $track.Size = New-Object System.Drawing.Size(190, 38)
    $track.Minimum = 0
    $track.Maximum = 200
    $track.TickFrequency = 50
    $track.LargeChange = 10
    $track.SmallChange = 5
    $track.Value = [Math]::Min(200, [Math]::Max(0, $Value))
    $track.BackColor = $surface
    $Parent.Controls.Add($track)

    $valueLabel = New-Object System.Windows.Forms.Label
    $valueLabel.Location = New-Object System.Drawing.Point(($X + 202), ($Y + 25))
    $valueLabel.Size = New-Object System.Drawing.Size(54, 24)
    $valueLabel.ForeColor = $muted
    $valueLabel.Text = "$($track.Value)%"
    $Parent.Controls.Add($valueLabel)

    $track.Tag = $valueLabel
    $track.Add_ValueChanged({
        $this.Tag.Text = "$($this.Value)%"
    })
    return $track
}

$devicesTab = New-Tab "Devices"
$study = New-Check $devicesTab "Study lamp" ([bool]$configuration.study) 24 24
$razerKeyboard = New-Check $devicesTab "Razer keyboard" ([bool]$configuration.razerKeyboard) 316 24
$razerMouse = New-Check $devicesTab "Razer mouse" ([bool]$configuration.razerMouse) 316 62
$razerDock = New-Check $devicesTab "Razer dock" ([bool]$configuration.razerDock) 316 100
$lenovoKeyboard = New-Check $devicesTab "Laptop keyboard" ([bool]$configuration.lenovoKeyboard) 316 138

$ambientRoleChoices = [ordered]@{
    "Soft depth" = "SoftDepth"
    "Positional ambient" = "PositionalAmbient"
    "Off" = "Off"
}
$savedUpperRole = if ($configuration.PSObject.Properties.Name -contains "upperRole") { [string]$configuration.upperRole } else { "Off" }
$savedLowerRole = if ($configuration.PSObject.Properties.Name -contains "lowerRole") { [string]$configuration.lowerRole } else { "Off" }
$upperRole = New-Combo $devicesTab "Upper room light" $ambientRoleChoices $savedUpperRole 205
$lowerRole = New-Combo $devicesTab "Lower room light" $ambientRoleChoices $savedLowerRole 253

$watchTab = New-Tab "Watch"
$watchStudyStrength = New-PercentSlider $watchTab "Study brightness" (Get-ConfigInt "watchStudyStrength" 135) 24 26
$watchRearStrength = New-PercentSlider $watchTab "Room immersion" (Get-ConfigInt "watchRearStrength" 165) 24 96
$watchRazerStrength = New-PercentSlider $watchTab "Desk devices" (Get-ConfigInt "watchRazerStrength" 120) 324 26
$watchColorBoost = New-PercentSlider $watchTab "Color saturation" (Get-ConfigInt "watchColorBoost" 108) 324 96

$gamesTab = New-Tab "Games"
$autoGameEnabledValue = if ($configuration.PSObject.Properties.Name -contains "autoGameEnabled") {
    [bool]$configuration.autoGameEnabled
}
else {
    $true
}
$autoGame = New-Check $gamesTab "Automatically switch for CS2 and Valorant" $autoGameEnabledValue 24 20 550

$gameRoleChoices = [ordered]@{
    "Objective alerts" = "ObjectiveAlerts"
    "Damage alerts" = "HealthDamage"
    "Team / agent mood" = "TeamAgentMood"
    "Map mood" = "MapMood"
    "Full game mix" = "FullGameMix"
    "Off" = "Off"
}
$savedUpperGameRole = if ($configuration.PSObject.Properties.Name -contains "upperGameRole") { [string]$configuration.upperGameRole } else { "ObjectiveAlerts" }
$savedLowerGameRole = if ($configuration.PSObject.Properties.Name -contains "lowerGameRole") { [string]$configuration.lowerGameRole } else { "HealthDamage" }
$upperGameRole = New-Combo $gamesTab "Upper room light" $gameRoleChoices $savedUpperGameRole 78
$lowerGameRole = New-Combo $gamesTab "Lower room light" $gameRoleChoices $savedLowerGameRole 130

$gameStudy = New-Object System.Windows.Forms.Label
$gameStudy.Text = "CS2 study lamp: bright daylight plus full event takeovers"
$gameStudy.Location = New-Object System.Drawing.Point(24, 202)
$gameStudy.Size = New-Object System.Drawing.Size(550, 25)
$gameStudy.ForeColor = $muted
$gamesTab.Controls.Add($gameStudy)

$updateGameControls = {
    $upperGameRole.Enabled = $autoGame.Checked
    $lowerGameRole.Enabled = $autoGame.Checked
    $gameStudy.Enabled = $autoGame.Checked
}
$autoGame.Add_CheckedChanged($updateGameControls)
& $updateGameControls

$eventsTab = New-Tab "CS2 Events"
$csFlashIntensity = New-PercentSlider $eventsTab "Flash white" (Get-ConfigInt "csFlashIntensity" 150) 24 22
$csFireIntensity = New-PercentSlider $eventsTab "Molly / fire" (Get-ConfigInt "csFireIntensity" 145) 24 84
$csSmokeIntensity = New-PercentSlider $eventsTab "Smoke haze" (Get-ConfigInt "csSmokeIntensity" 115) 24 146
$csDeathIntensity = New-PercentSlider $eventsTab "Death red" (Get-ConfigInt "csDeathIntensity" 150) 24 208
$csImpactIntensity = New-PercentSlider $eventsTab "Damage / grenade" (Get-ConfigInt "csImpactIntensity" 135) 24 270
$csBombIntensity = New-PercentSlider $eventsTab "Bomb / defuse" (Get-ConfigInt "csBombIntensity" 130) 324 22
$csClutchIntensity = New-PercentSlider $eventsTab "Clutch / MVP" (Get-ConfigInt "csClutchIntensity" 125) 324 84
$csTeamContrast = New-PercentSlider $eventsTab "Study CT/T contrast" (Get-ConfigInt "csTeamContrast" 145) 324 146
$csUtilityBrightness = New-PercentSlider $eventsTab "Utility key brightness" (Get-ConfigInt "csUtilityBrightness" 150) 324 208

$numpadTab = New-Object System.Windows.Forms.Panel
$defaultNumpadLayout = "NumLock=SelectedGrenade;Divide=Armor;Multiply=Helmet;Minus=DefuseKit;Num7=Flash;Num8=Smoke;Num9=HE;Plus=Bomb;Num4=Fire;Num5=Decoy;Num6=AmmoState;Num1=Health1;Num2=Health2;Num3=Health3;Enter=Ammo3;Num0=Ammo1;Decimal=Ammo2"
$numpadKeyOrder = @(
    "NumLock", "Divide", "Multiply", "Minus",
    "Num7", "Num8", "Num9", "Plus",
    "Num4", "Num5", "Num6",
    "Num1", "Num2", "Num3", "Enter",
    "Num0", "Decimal"
)
$numpadKeyLabels = @{
    NumLock = "Num"
    Divide = "/"
    Multiply = "*"
    Minus = "-"
    Num7 = "7"
    Num8 = "8"
    Num9 = "9"
    Plus = "+"
    Num4 = "4"
    Num5 = "5"
    Num6 = "6"
    Num1 = "1"
    Num2 = "2"
    Num3 = "3"
    Enter = "Enter"
    Num0 = "0"
    Decimal = "."
}
$numpadChoices = [ordered]@{
    "Off" = "Off"
    "Team color" = "Team"
    "Selected grenade" = "SelectedGrenade"
    "Armor" = "Armor"
    "Helmet" = "Helmet"
    "Defuse kit" = "DefuseKit"
    "Flashbang" = "Flash"
    "Smoke" = "Smoke"
    "HE grenade" = "HE"
    "Molly / incendiary" = "Fire"
    "Decoy" = "Decoy"
    "Health segment 1" = "Health1"
    "Health segment 2" = "Health2"
    "Health segment 3" = "Health3"
    "Magazine segment 1" = "Ammo1"
    "Magazine segment 2" = "Ammo2"
    "Magazine segment 3" = "Ammo3"
    "Reload / low ammo" = "AmmoState"
    "Reserve ammo" = "ReserveAmmo"
    "C4 / planted bomb" = "Bomb"
    "Round kills" = "RoundKills"
}
$numpadShortLabels = @{
    Off = "Off"
    Team = "Team"
    SelectedGrenade = "Selected"
    Armor = "Armor"
    Helmet = "Helmet"
    DefuseKit = "Kit"
    Flash = "Flash"
    Smoke = "Smoke"
    HE = "HE"
    Fire = "Fire"
    Decoy = "Decoy"
    Health1 = "HP 1"
    Health2 = "HP 2"
    Health3 = "HP 3"
    Ammo1 = "Ammo 1"
    Ammo2 = "Ammo 2"
    Ammo3 = "Ammo 3"
    AmmoState = "Ammo!"
    ReserveAmmo = "Reserve"
    Bomb = "Bomb"
    RoundKills = "Kills"
}
$numpadColors = @{
    Off = [System.Drawing.Color]::FromArgb(42, 45, 51)
    Team = [System.Drawing.Color]::FromArgb(55, 105, 175)
    SelectedGrenade = [System.Drawing.Color]::FromArgb(105, 75, 165)
    Armor = [System.Drawing.Color]::FromArgb(30, 105, 195)
    Helmet = [System.Drawing.Color]::FromArgb(35, 145, 170)
    DefuseKit = [System.Drawing.Color]::FromArgb(20, 145, 105)
    Flash = [System.Drawing.Color]::FromArgb(178, 178, 158)
    Smoke = [System.Drawing.Color]::FromArgb(55, 105, 115)
    HE = [System.Drawing.Color]::FromArgb(95, 145, 35)
    Fire = [System.Drawing.Color]::FromArgb(185, 58, 10)
    Decoy = [System.Drawing.Color]::FromArgb(125, 48, 170)
    Health1 = [System.Drawing.Color]::FromArgb(165, 40, 30)
    Health2 = [System.Drawing.Color]::FromArgb(190, 112, 20)
    Health3 = [System.Drawing.Color]::FromArgb(35, 145, 72)
    Ammo1 = [System.Drawing.Color]::FromArgb(35, 125, 155)
    Ammo2 = [System.Drawing.Color]::FromArgb(35, 135, 170)
    Ammo3 = [System.Drawing.Color]::FromArgb(40, 145, 185)
    AmmoState = [System.Drawing.Color]::FromArgb(175, 45, 30)
    ReserveAmmo = [System.Drawing.Color]::FromArgb(42, 115, 150)
    Bomb = [System.Drawing.Color]::FromArgb(190, 95, 10)
    RoundKills = [System.Drawing.Color]::FromArgb(175, 135, 20)
}

function ConvertFrom-NumpadLayout {
    param([string]$Serialized)
    $result = @{}
    foreach ($part in $Serialized -split ";") {
        $pair = $part -split "=", 2
        if ($pair.Count -eq 2 -and $numpadKeyOrder -contains $pair[0] -and $numpadShortLabels.ContainsKey($pair[1])) {
            $result[$pair[0]] = $pair[1]
        }
    }
    return $result
}

$numpadAssignments = ConvertFrom-NumpadLayout $defaultNumpadLayout
$savedNumpadLayout = if ($configuration.PSObject.Properties.Name -contains "csNumpadLayout") {
    [string]$configuration.csNumpadLayout
}
else {
    $defaultNumpadLayout
}
foreach ($entry in (ConvertFrom-NumpadLayout $savedNumpadLayout).GetEnumerator()) {
    $numpadAssignments[$entry.Key] = $entry.Value
}

$numpadButtons = @{}
$script:selectedNumpadKey = "NumLock"
$selectedKeyCaption = New-Object System.Windows.Forms.Label
$selectedKeyCaption.Location = New-Object System.Drawing.Point(332, 30)
$selectedKeyCaption.Size = New-Object System.Drawing.Size(240, 26)
$selectedKeyCaption.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 11)
$selectedKeyCaption.ForeColor = $text
$numpadTab.Controls.Add($selectedKeyCaption)

$assignmentCombo = New-Object System.Windows.Forms.ComboBox
$assignmentCombo.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDownList
$assignmentCombo.Location = New-Object System.Drawing.Point(332, 66)
$assignmentCombo.Size = New-Object System.Drawing.Size(240, 30)
foreach ($display in $numpadChoices.Keys) {
    [void]$assignmentCombo.Items.Add($display)
}
$numpadTab.Controls.Add($assignmentCombo)

$assignmentSwatch = New-Object System.Windows.Forms.Panel
$assignmentSwatch.Location = New-Object System.Drawing.Point(332, 108)
$assignmentSwatch.Size = New-Object System.Drawing.Size(240, 10)
$numpadTab.Controls.Add($assignmentSwatch)

function Update-NumpadButton {
    param([string]$Key)
    $assignment = [string]$numpadAssignments[$Key]
    $button = $numpadButtons[$Key]
    $button.Text = "$($numpadKeyLabels[$Key])`n$($numpadShortLabels[$assignment])"
    $button.BackColor = $numpadColors[$assignment]
    $button.FlatAppearance.BorderColor = if ($Key -eq $script:selectedNumpadKey) {
        [System.Drawing.Color]::White
    }
    else {
        $border
    }
    $button.FlatAppearance.BorderSize = if ($Key -eq $script:selectedNumpadKey) { 2 } else { 1 }
}

function Select-NumpadKey {
    param([string]$Key)
    $script:selectedNumpadKey = $Key
    $selectedKeyCaption.Text = "$($numpadKeyLabels[$Key]) key"
    $selectedDisplay = $numpadChoices.Keys | Where-Object { $numpadChoices[$_] -eq $numpadAssignments[$Key] } | Select-Object -First 1
    $assignmentCombo.SelectedItem = $selectedDisplay
    $assignmentSwatch.BackColor = $numpadColors[[string]$numpadAssignments[$Key]]
    foreach ($buttonKey in $numpadButtons.Keys) {
        Update-NumpadButton $buttonKey
    }
}

function New-NumpadKey {
    param(
        [string]$Key,
        [int]$X,
        [int]$Y,
        [int]$Width = 66,
        [int]$Height = 44
    )
    $button = New-Object System.Windows.Forms.Button
    $button.Tag = $Key
    $button.Location = New-Object System.Drawing.Point($X, $Y)
    $button.Size = New-Object System.Drawing.Size($Width, $Height)
    $button.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $button.ForeColor = $text
    $button.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 8)
    $button.TextAlign = [System.Drawing.ContentAlignment]::MiddleCenter
    $button.Add_Click({ Select-NumpadKey ([string]$this.Tag) })
    $numpadButtons[$Key] = $button
    $numpadTab.Controls.Add($button)
}

New-NumpadKey "NumLock" 18 20
New-NumpadKey "Divide" 90 20
New-NumpadKey "Multiply" 162 20
New-NumpadKey "Minus" 234 20
New-NumpadKey "Num7" 18 70
New-NumpadKey "Num8" 90 70
New-NumpadKey "Num9" 162 70
New-NumpadKey "Plus" 234 70 66 94
New-NumpadKey "Num4" 18 120
New-NumpadKey "Num5" 90 120
New-NumpadKey "Num6" 162 120
New-NumpadKey "Num1" 18 170
New-NumpadKey "Num2" 90 170
New-NumpadKey "Num3" 162 170
New-NumpadKey "Enter" 234 170 66 94
New-NumpadKey "Num0" 18 220 138 44
New-NumpadKey "Decimal" 162 220

$assignmentCombo.Add_SelectedIndexChanged({
    if (-not $script:selectedNumpadKey -or -not $assignmentCombo.SelectedItem) {
        return
    }
    $assignment = [string]$numpadChoices[[string]$assignmentCombo.SelectedItem]
    $numpadAssignments[$script:selectedNumpadKey] = $assignment
    $assignmentSwatch.BackColor = $numpadColors[$assignment]
    Update-NumpadButton $script:selectedNumpadKey
})

$resetNumpad = New-Object System.Windows.Forms.Button
$resetNumpad.Text = "Reset layout"
$resetNumpad.Location = New-Object System.Drawing.Point(332, 220)
$resetNumpad.Size = New-Object System.Drawing.Size(118, 40)
$resetNumpad.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
$resetNumpad.FlatAppearance.BorderColor = $border
$resetNumpad.ForeColor = $text
$resetNumpad.Add_Click({
    $defaults = ConvertFrom-NumpadLayout $defaultNumpadLayout
    foreach ($key in $numpadKeyOrder) {
        $numpadAssignments[$key] = $defaults[$key]
    }
    Select-NumpadKey "NumLock"
})
$numpadTab.Controls.Add($resetNumpad)

Select-NumpadKey "NumLock"

$behaviorTab = New-Tab "Behavior"
$blackout = New-Check $behaviorTab "Turn sampled lights off for black screen zones" ([bool]$configuration.blackoutOnBlack) 24 28 550

function New-Fps {
    param(
        [System.Windows.Forms.Control]$Parent,
        [string]$Label,
        [int]$Value,
        [int]$Maximum,
        [int]$Y
    )
    $caption = New-Object System.Windows.Forms.Label
    $caption.Text = $Label
    $caption.Location = New-Object System.Drawing.Point(24, $Y)
    $caption.Size = New-Object System.Drawing.Size(310, 26)
    $caption.ForeColor = $text
    $Parent.Controls.Add($caption)

    $number = New-Object System.Windows.Forms.NumericUpDown
    $number.Location = New-Object System.Drawing.Point(410, ($Y - 3))
    $number.Size = New-Object System.Drawing.Size(90, 30)
    $number.Minimum = 1
    $number.Maximum = $Maximum
    $number.Value = [Math]::Min($Maximum, [Math]::Max(1, $Value))
    $Parent.Controls.Add($number)

    $suffix = New-Object System.Windows.Forms.Label
    $suffix.Text = "FPS"
    $suffix.Location = New-Object System.Drawing.Point(510, $Y)
    $suffix.Size = New-Object System.Drawing.Size(50, 26)
    $suffix.ForeColor = $muted
    $Parent.Controls.Add($suffix)
    return $number
}

$ambientFpsValue = if ($configuration.PSObject.Properties.Name -contains "ambientFps") { [int]$configuration.ambientFps } else { 10 }
$gameFpsValue = if ($configuration.PSObject.Properties.Name -contains "gameFps") { [int]$configuration.gameFps } else { 10 }
$ambientFps = New-Fps $behaviorTab "Study / Watch update rate" $ambientFpsValue 20 102
$gameFps = New-Fps $behaviorTab "CS2 / Valorant update rate" $gameFpsValue 30 154

if (-not [string]::IsNullOrWhiteSpace($InitialTab)) {
    $initialPage = $tabs.TabPages | Where-Object { $_.Text -eq $InitialTab } | Select-Object -First 1
    if ($initialPage) {
        $tabs.SelectedTab = $initialPage
    }
}

$saveAndStart = {
    try {
        $selectedMode = ($modeButtons.Values | Where-Object Checked | Select-Object -First 1).Tag
        $values = @(
            [int]$study.Checked,
            [string]$ambientRoleChoices[[string]$upperRole.SelectedItem],
            [string]$ambientRoleChoices[[string]$lowerRole.SelectedItem],
            [string]$gameRoleChoices[[string]$upperGameRole.SelectedItem],
            [string]$gameRoleChoices[[string]$lowerGameRole.SelectedItem],
            [int]$razerKeyboard.Checked,
            [int]$razerMouse.Checked,
            [int]$razerDock.Checked,
            [int]$lenovoKeyboard.Checked,
            [int]$blackout.Checked,
            [int]$ambientFps.Value,
            [int]$gameFps.Value
        ) | ForEach-Object { $_.ToString() }

        $form.Enabled = $false
        $status.Text = "Applying $($modeButtons[$selectedMode].Text)..."
        [System.Windows.Forms.Application]::DoEvents()

        Get-Process | Where-Object { $_.ProcessName -like "*Artemis*" } | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 700
        Invoke-LightingTool (@("set-watch-config") + $values) | Out-Null
        Invoke-LightingTool @(
            "set-watch-tuning",
            ([int]$watchStudyStrength.Value).ToString(),
            ([int]$watchRearStrength.Value).ToString(),
            ([int]$watchRazerStrength.Value).ToString(),
            ([int]$watchColorBoost.Value).ToString()
        ) | Out-Null
        Invoke-LightingTool @(
            "set-cs-event-tuning",
            ([int]$csFlashIntensity.Value).ToString(),
            ([int]$csFireIntensity.Value).ToString(),
            ([int]$csSmokeIntensity.Value).ToString(),
            ([int]$csDeathIntensity.Value).ToString(),
            ([int]$csImpactIntensity.Value).ToString(),
            ([int]$csBombIntensity.Value).ToString(),
            ([int]$csClutchIntensity.Value).ToString(),
            ([int]$csTeamContrast.Value).ToString(),
            ([int]$csUtilityBrightness.Value).ToString()
        ) | Out-Null

        Invoke-LightingTool @(
            "set-control-mode",
            [string]$selectedMode,
            ([int]$autoGame.Checked).ToString()
        ) | Out-Null
        Start-Process -FilePath $artemis -WindowStyle Hidden
        $form.Close()
    }
    catch {
        $form.Enabled = $true
        $status.Text = "Could not apply lighting mode."
        [System.Windows.Forms.MessageBox]::Show(
            $_.Exception.Message,
            "Lighting Control",
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
    }
}

$cancel = New-Object System.Windows.Forms.Button
$cancel.Text = "Cancel"
$cancel.Location = New-Object System.Drawing.Point(444, 526)
$cancel.Size = New-Object System.Drawing.Size(92, 40)
$cancel.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
$cancel.FlatAppearance.BorderColor = $border
$cancel.ForeColor = $text
$cancel.Add_Click({ $form.Close() })
$form.Controls.Add($cancel)

$apply = New-Object System.Windows.Forms.Button
$apply.Text = "Apply and Start"
$apply.Location = New-Object System.Drawing.Point(548, 526)
$apply.Size = New-Object System.Drawing.Size(108, 40)
$apply.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
$apply.FlatAppearance.BorderSize = 0
$apply.BackColor = $surfaceSelected
$apply.ForeColor = $text
$apply.Add_Click($saveAndStart)
$form.Controls.Add($apply)
$form.AcceptButton = $apply
$form.CancelButton = $cancel

[void]$form.ShowDialog()
