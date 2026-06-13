# Zeus Prototype

Zeus is the proposed next product direction: a friendly lighting control app where Artemis, OpenRGB, native WiZ, WLED, Razer Chroma, and future integrations become backends instead of concepts the user must understand first.

The visual direction is captured in [`DESIGN.md`](../DESIGN.md), using Google's DESIGN.md format. Treat that file as the source of truth for palette, typography, layout, component behavior, and design do's/don'ts.

The prototype focuses on the user experience:

- One-click modes: Watch, CS2, Valorant, Study
- Visual setup instructions
- Friendly device names and Flash/Identify actions
- A room map with monitor, desk, side, rear, ceiling, and future strip placement
- Per-device behavior for Watch and games
- Profiles and auto-switch rules
- Backend/plugin cards that explain what each integration is for
- Performance guidance with one shared sampler and per-device FPS caps

The seeded example setup matches the original room:

- Study WiZ lamp above the monitor
- Upper and lower rear WiZ lights
- Razer keyboard, mouse, and dock
- Lenovo laptop keyboard

Build:

```powershell
dotnet build .\work\Zeus.App\Zeus.App.csproj -c Release
```

Publish self-contained:

```powershell
dotnet publish .\work\Zeus.App\Zeus.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o .\outputs\ZeusPrototype
```

This is not a live device controller yet. The next stage is to connect the UI model to real backends, starting with native WiZ LAN and an Artemis import/backend adapter.

Design lint:

```powershell
npm ci --ignore-scripts
npm run design:lint
```
