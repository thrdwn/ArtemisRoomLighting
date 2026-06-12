# Contributing

Contributions are welcome for additional devices, lighting effects, setup detection, documentation, and tests.

## Development

1. Fork the repository.
2. Create a focused branch.
3. Build the installer with `work\RoomLighting.Installer\Build-Installer.ps1`.
4. Run `work\DirectDevicesLogicTest` and the installer payload verification.
5. Open a pull request describing the hardware and test evidence.

Do not commit:

- Artemis databases or logs
- private IP addresses
- device serial numbers
- account credentials or tokens
- `bin`, `obj`, installer staging folders, or generated EXEs

## Device contributions

Include the vendor/product IDs, interface and report details, a link to public protocol documentation when available, and graceful behavior when the device is absent. Keep device-specific protocol code isolated from game and ambient-lighting policy.

See [docs/ADDING_A_DEVICE.md](docs/ADDING_A_DEVICE.md).
