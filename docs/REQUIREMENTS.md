# Requirements and Provider Notes

## Base requirements

- Windows 10 or Windows 11 x64
- Artemis `1.2025.1222.3` or newer
- Artemis opened once so `C:\ProgramData\Artemis\artemis.db` exists
- Internet access for the live Workshop catalog
- Administrator permission to back up and configure shared Artemis data

## Common providers

| Provider | Additional requirement |
| --- | --- |
| Razer Devices | Razer Synapse must detect the device. Close competing Chroma control apps while testing. |
| Corsair Devices | Install Corsair iCUE and allow SDK control. |
| Logitech Devices | Install Logitech G Hub or Logitech Gaming Software. |
| SteelSeries Devices | Install SteelSeries GG and enable game integrations. |
| ASUS Devices | Install Armoury Crate or Aura and its lighting service. |
| MSI Devices | Install the MSI Center or Mystic Light components required by the device. |
| Windows Dynamic Lighting | Windows 11 and compatible hardware are required. |
| Philips Hue | A Hue Bridge must be reachable on the same LAN. |
| Nanoleaf | Enable LAN control and keep the panels on the same network. |
| WLED | The WLED controller must be reachable on the same LAN. |
| OpenRGB Devices | Optional. Requires the OpenRGB SDK server. Native providers do not need it. |

Plugin requirements can change. The assistant opens the plugin's official Artemis details page, which is the source of truth for current installation notes.

## Profile requirements

`Guided Watch` needs at least one imported profile containing an Ambilight layer. The assistant uses that layer as a template and filters its LEDs by mapped Artemis device ID.

Game role assignment needs the matching official game profile. Counter-Strike 2 also needs its Game State Integration configuration file in the CS2 `cfg` directory.

## Frame rates

PC RGB devices can often update at 30 to 60 FPS. Network bulbs and bridges usually have lower practical rates and may become unstable when flooded with updates. Select a rate appropriate for the slowest devices in the chosen profile.

The assistant allows 1 to 60 FPS but defaults to 30 FPS.

## Conflicts

Only one provider should control a physical device. Disable duplicate official, OpenRGB, vendor, or direct providers when colors freeze, blink, or alternate unexpectedly.
