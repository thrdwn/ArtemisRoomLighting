# Adding Device Support

## Preferred path

Do not add another driver here when Artemis already has a provider for the device family.

1. Install the matching provider from the Artemis Workshop.
2. Confirm the device appears in Artemis device settings.
3. Restart the Setup Assistant.
4. Refresh detected devices.
5. Map the device and assign Watch and game roles.

No assistant code change is required.

## Missing provider

When Artemis does not support the hardware, contribute a provider to the official [Artemis.Plugins repository](https://github.com/Artemis-RGB/Artemis.Plugins) when practical. This gives the whole Artemis ecosystem one maintained implementation.

Useful contribution evidence includes:

- hardware model and public product page
- USB vendor/product IDs or documented network protocol
- vendor SDK and runtime requirements
- solid-color and per-LED tests
- disconnect and reconnect behavior
- tested Windows and Artemis versions

## Compatibility bridge

Add code to `Artemis.Plugins.DirectDevices` only when an official provider is not practical or when experimental direct control is the purpose of the change.

Keep device protocol code separate from ambient and game policy. Missing hardware and network failures must remain non-fatal.
