# Security

Do not report credentials, private IP addresses, device serial numbers, or other personal data in a public issue.

For a potential security vulnerability, contact a repository maintainer privately through the security advisory feature on GitHub. Include the affected version and a minimal reproduction without personal data.

The installer:

- requires administrator permission because Artemis plugins and data live under `C:\ProgramData`
- creates a timestamped backup before changing the Artemis database or plugin
- reads the public Artemis Workshop catalog over HTTPS
- communicates with WiZ bulbs only over the local network
- listens for CS2 game-state data only on `127.0.0.1`
