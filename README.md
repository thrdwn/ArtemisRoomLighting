# Artemis Room Lighting

Direct ambient and game-reactive lighting for Artemis RGB without using OpenRGB as a bridge.

The project combines screen capture, WiZ room lights, selected Razer HID devices, a Lenovo Legion 4-zone keyboard, and event-driven CS2/Valorant lighting. A self-contained Windows installer configures the plugin, CS2 game-state integration, and Start menu controls.

## Supported hardware

- Up to three optional WiZ RGB lights assigned as study, upper/rear, and lower/rear positions
- Razer BlackWidow V3
- Razer DeathAdder V2 Pro Wireless
- Razer Mouse Dock Chroma
- Lenovo Legion 5 2021 4-zone keyboard controllers using HID IDs `048D:C965` or `048D:C963`

Unsupported hardware is simply skipped. See [Adding a device](docs/ADDING_A_DEVICE.md) to contribute another adapter.

## Install

1. Install Artemis and open it once.
2. Download `ArtemisRoomLightingSetup-0.11.0.0.exe` from the latest GitHub release.
3. Run the installer and approve the Windows administrator prompt.
4. Enter only the WiZ IP addresses and device families available on that PC.
5. Choose **Install**.

The installer backs up the existing Artemis database and prior plugin before making changes. Updates preserve existing lighting settings by default.

## Features

- Study and cinematic Watch modes
- Position-aware screen zones and black-screen behavior
- Automatic CS2 and Valorant activation
- CS2 health, armor, ammo, utility, flash, smoke, fire, damage, death, clutch, bomb, defuse, detonation, round win, and MVP effects
- Configurable rear-light roles and event intensity
- Start menu controls for mode switching
- Portable, self-contained installer

## Build

Requirements:

- Windows 10 or newer
- .NET 10 SDK
- PowerShell 5.1 or newer

```powershell
powershell -ExecutionPolicy Bypass -File .\work\RoomLighting.Installer\Build-Installer.ps1
```

The installer is written to `outputs\ArtemisRoomLightingSetup-0.11.0.0.exe`.

## Publishing

The repository contains no passwords, API keys, personal access tokens, personal paths, or private LAN addresses. GitHub Actions builds the installer and the release workflow uses the repository-scoped `GITHUB_TOKEN` supplied by GitHub.

Creating the GitHub repository still requires a GitHub account with permission to create a repository. An organization is recommended so ownership is shared rather than tied to one person's account. Users can download releases from a public repository without signing in.

## License

This project is distributed under the PolyForm Noncommercial License 1.0.0 because it incorporates mapping code from Artemis.Plugins under that license. See [LICENSE](LICENSE) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
