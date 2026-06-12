# Architecture

## Setup assistant

`RoomLighting.Installer` is the main product. It reads Artemis' Workshop manifests and database to provide four guided areas:

- requirements and Artemis deep links
- the live official Workshop plugin catalog
- provider-independent device mapping and roles
- optional compatibility settings

Device assignments are stored in:

`C:\ProgramData\ArtemisRoomLighting\setup.json`

Assignments use the stable Artemis device identifier and provider GUID. No brand-specific model is required by the mapper.

## Artemis integration

The assistant leaves plugin installation and updating to Artemis. It launches official routes such as:

- `artemis://workshop/entries/plugins/details/{entryId}`
- `artemis://settings/devices`
- `artemis://surface-editor`

The assistant stops Artemis before database changes, creates a timestamped backup, updates device positions and RGB scales, configures profiles, and restarts Artemis.

## Generic profiles

`SqliteTool configure-ecosystem` collects known LEDs from imported Artemis profiles.

- `Guided Watch` clones an installed Ambilight layer and creates one layer per selected device.
- The official `Counter-Strike 2` profile receives LEDs according to Full game, Team ambient, or Impact alerts roles.

This works across providers because profile LEDs reference Artemis device identifiers, not vendor APIs.

## Optional compatibility plugin

`Artemis.Plugins.DirectDevices` remains available for direct WiZ, selected HID hardware, and the richer experimental CS2/Valorant event engine developed before the generalized setup assistant.

It is not the default device ecosystem. New hardware should normally be supported through an existing or contributed Artemis Workshop provider.
