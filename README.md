# Artemis Setup Assistant

A guided setup layer for Artemis RGB. It helps people discover existing Artemis plugins, understand their prerequisites, map devices to their physical room positions, and choose what each device should do in Watch, Game, Study, or Custom setups.

The assistant uses the official Artemis Workshop ecosystem instead of maintaining another copy of every hardware driver. Razer, Corsair, Logitech, SteelSeries, ASUS, Hue, Nanoleaf, WLED, Windows Dynamic Lighting, OpenRGB, and future providers all use the same mapping flow once Artemis detects their devices.

The repo also contains an early **Zeus** prototype, a cleaner future direction where Artemis, OpenRGB, WiZ, WLED, Razer, and game integrations become backends behind a room-first UI. Its visual system is captured in [`DESIGN.md`](DESIGN.md) and linted with Google's [`@google/design.md`](https://github.com/google-labs-code/design.md) CLI.

## What it solves

- Walk through a simple Activity -> Devices -> Support -> Room -> Review wizard
- See recommended plugins for common devices, room lights, Ambilight, CS2, and Valorant
- Open recommended plugins directly in Artemis
- Detect and group devices registered by any Artemis provider
- Drag devices onto a room, screen, desk, and rear-light map
- Choose plain-language Watch, Game, and Study behavior per device
- Set per-device brightness from the normal flow
- Keep RGB calibration, FPS, paths, IPs, and the raw plugin catalog behind Advanced
- Build a generic `Guided Watch` profile from an imported Ambilight profile
- Fill the official Counter-Strike 2 profile with the selected devices
- Back up the Artemis database before every apply
- Keep the older direct WiZ bridge as an optional compatibility fallback

## Requirements

1. Windows 10 or Windows 11 x64.
2. Artemis `1.2025.1222.3` or newer.
3. Open Artemis once before running the assistant.
4. Internet access to browse the Workshop.
5. Install the vendor software or SDK required by your chosen provider.
6. Import an Ambilight profile before generating `Guided Watch`.
7. Import the official Counter-Strike 2 profile before assigning CS2 roles.

See [requirements and provider notes](docs/REQUIREMENTS.md) for vendor-specific details.
See the [setup and role guide](docs/SETUP_GUIDE.md) for mapping examples.

## Install

1. Download `ArtemisRoomLightingSetup-0.13.1.0.exe` from the latest release.
2. Run it and approve the administrator prompt.
3. Use **Support** to install or enable the providers for your hardware.
4. Restart Artemis so the devices appear.
5. Use **Devices** and **Room** to place each device and assign its behavior.
6. Review the setup, then choose **Apply setup**.
7. Make any final outline adjustments in the Artemis Surface Editor that opens afterward.

The installer does not require a GitHub login, API key, or personal credentials.

## Existing Artemis plugins

The assistant intentionally does not bundle official Workshop plugins. Artemis remains their installer and update manager. The catalog includes device providers, game integrations, layer brushes, effects, modules, and profiles.

Open a catalog entry with **Open selected in Artemis**, install it there, and return to the assistant. Unsupported or absent hardware is never treated as a fatal error.

OpenRGB remains optional. Users who prefer native Razer or another native Artemis provider do not need OpenRGB as a bridge.

## Optional direct bridge

The bundled compatibility plugin can directly control configured WiZ LAN bulbs. Its Razer and Lenovo adapters are disabled by default in the generalized setup to avoid conflicts with official providers.

Use the bridge only when a native Workshop provider does not support the hardware reliably. Never control one physical device through two providers at the same time.

## Build

Requirements:

- Windows 10 or newer
- .NET 10 SDK
- PowerShell 5.1 or newer

```powershell
powershell -ExecutionPolicy Bypass -File .\work\RoomLighting.Installer\Build-Installer.ps1
```

The installer is written to `outputs\ArtemisRoomLightingSetup-0.13.1.0.exe`.

Design lint:

```powershell
npm ci --ignore-scripts
npm run design:lint
```

## License

This project is distributed under the PolyForm Noncommercial License 1.0.0 because the optional compatibility bridge incorporates mapping code from Artemis.Plugins under that license. See [LICENSE](LICENSE) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
