# Contributing

Contributions are welcome for setup detection, Workshop guidance, physical mapping, profile roles, accessibility, documentation, tests, and compatibility fixes.

## Development

1. Fork the repository.
2. Create a focused branch.
3. Build with `work\RoomLighting.Installer\Build-Installer.ps1`.
4. Run `work\DirectDevicesLogicTest`.
5. Verify the embedded installer payload.
6. Test database changes against a copied Artemis database first.
7. Open a pull request describing the behavior and test evidence.

Do not commit:

- Artemis databases or logs
- private IP addresses or device serial numbers
- account credentials, cookies, or tokens
- `bin`, `obj`, staging folders, or generated installers

## Device support

Prefer an existing official Artemis provider. If the provider is missing, consider contributing it to the official Artemis plugin repository before adding another direct adapter here.

See [Adding device support](docs/ADDING_A_DEVICE.md).
