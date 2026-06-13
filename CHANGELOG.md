# Changelog

## 0.13.1.0

- Fixed a startup crash when saved display dimensions load before the Advanced page is opened

## 0.13.0.0

- Rebuilt the setup assistant as a guided Activity, Devices, Support, Room, and Review wizard
- Added plain-language Watch, Game, and Study behavior choices for each device
- Added device grouping for lights, room lights, keyboard/mouse devices, and unknown devices
- Added recommendation cards for common Artemis plugins like Razer, smart lights, Ambilight, CS2, and Valorant
- Made the room map the primary setup surface with screen, desk, side, and rear zones
- Added v2 setup configuration with device kind, physical zone, mode assignments, and advanced settings
- Added automatic migration for existing v1 setup files
- Moved raw plugin search, RGB calibration, display/FPS, CS2 path, and direct WiZ bridge settings behind Advanced

## 0.12.1.0

- Added a simple Home screen with Watch, Game, and Desk presets
- Moved plugin search, raw mapping, capture settings, and compatibility options into secondary tabs
- Added automatic preset recommendations for rear lights, study lamps, keyboards, mouse, and dock devices
- Changed the primary action to `Start this setup`
- Added a compact found-devices summary so most users can avoid the detailed grid

## 0.12.0.0

- Reframed the project as a provider-independent Artemis Setup Assistant
- Added the live official Workshop plugin catalog
- Added installed and enabled plugin status
- Added direct Artemis deep links for plugins, devices, and Surface Editor
- Added generalized device discovery from the Artemis database
- Added a draggable room and screen map
- Added per-device Watch and game roles
- Added per-device intensity and RGB channel calibration
- Added generic Guided Watch profile generation
- Added generic official Counter-Strike 2 profile assignment
- Added configurable black-zone behavior
- Made the direct WiZ/HID plugin an optional compatibility bridge
- Added provider prerequisites and setup documentation
