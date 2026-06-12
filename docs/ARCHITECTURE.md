# Architecture

## Plugin

`Artemis.Plugins.DirectDevices` registers supported devices with Artemis and starts three independent lighting paths:

- `DirectAmbientController`: DX11 screen capture and position-aware Watch/Study rendering
- `DirectCsController`: CS2 Game State Integration listener on loopback port `9697`
- `DirectValorantController`: Valorant log/screen-derived events

Device protocol classes are kept separate:

- `WizUpdateQueue`: WiZ UDP `setPilot`
- `WindowsHidFeatureSender`: HID feature reports
- `RazerReports` and `RazerUpdateQueue`: Razer custom-frame reports
- `LenovoUpdateQueue`: Lenovo 4-zone reports

## Configuration

`SqliteTool` updates Artemis plugin settings while Artemis is stopped. The Lighting Control PowerShell UI calls the same tool and then restarts Artemis to apply changes.

## Installer

`RoomLighting.Installer` embeds:

- compiled plugin
- self-contained configuration tool
- Lighting Control scripts
- CS2 GSI configuration

The installer backs up the Artemis database and previous plugin, supports optional device families, creates Start menu controls, and starts Artemis after installation.
