# Adding a Device

## HID devices

1. Add a `DirectDeviceDefinition` containing the LEDs and one or more `HidTarget` signatures.
2. Add a small update queue or sender that owns the HID handle and reconnects after transient failures.
3. Register the definition in `DirectDevicesProvider`.
4. Add optional game/ambient rendering only after basic solid-color control works.
5. Keep absent hardware non-fatal.

Useful target fields:

- USB vendor ID and product ID
- HID interface number
- usage page and usage
- feature report length

Do not use serial numbers or a single machine's complete device path.

## Network lights

Create an adapter that accepts a color, brightness, and off state. Keep discovery/configuration separate from frame rendering. Network errors must not stop other devices from updating.

## UI and installer

If a device family needs user configuration:

1. Add neutral plugin defaults.
2. Add a `SqliteTool` command for the setting.
3. Add an optional installer field or checkbox.
4. Add the device to Lighting Control only when users need ongoing mode-specific selection.

## Pull-request evidence

Include:

- hardware model
- protocol or public reference
- tested operating system and Artemis version
- solid-color test
- disconnect/reconnect test
- game or ambient test when applicable
