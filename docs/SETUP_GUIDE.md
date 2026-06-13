# Setup and Role Guide

## Recommended order

1. Choose **Activity**: Watch, Game, Study, or Custom.
2. Open **Devices** and confirm detected hardware.
3. Open **Support** for recommended Artemis plugins and profiles.
4. Install or enable missing support in Artemis, then restart Artemis if needed.
5. Return to the assistant and scan again.
6. Open **Room**, drag each device to its physical position, and choose behavior.
7. Open **Review** and apply the setup.
8. Inspect the result in the Artemis Surface Editor that opens afterward.

## Watch roles

| Role | Behavior | Good use |
| --- | --- | --- |
| Screen color | Full positional Ambilight response | Monitor lamp, keyboard, mouse, desk strip |
| Soft room depth | Positional response at reduced intensity | Rear and side room lights |
| Gentle glow | Stable dim neutral light | Bias light or a lamp that should not chase every frame |
| Off | Excluded from Guided Watch | Work-only or distracting devices |

When **Turn mapped lights off for sampled black zones** is disabled, Screen sample and Soft depth devices receive a dim neutral floor so black scenes do not fully extinguish them.

## Game roles

| Role | CS2 layers |
| --- | --- |
| Main game effects | Menu, CT, T, Damage, Death, Kill |
| Team mood | Menu, CT, T |
| Impact only | Damage, Death, Kill |
| Off | No CS2 layers |

The optional compatibility bridge contains additional experimental bomb, flash, fire, smoke, clutch, defuse, detonation, and MVP behavior. Those richer events are separate from the generic official CS2 profile.

## Positioning

The map is direction-based. A device at the top samples the upper screen area; rear-right devices sample the right/lower region and are usually best assigned Soft depth.

Use **Advanced** only when a multi-LED keyboard, strip, or panel needs RGB calibration, capture FPS changes, IP fallback settings, or raw plugin search.

## Color calibration

The `R`, `G`, and `B` columns are multipliers:

- `1.00` keeps the channel unchanged.
- Values below `1.00` reduce that channel.
- Values above `1.00` increase it.

For a bulb that renders orange too pink, reduce blue and possibly red slightly before increasing green. Make small changes, such as `0.95` or `1.05`, and test several colors.

## Provider conflicts

Blinking, alternating colors, and devices becoming stale often indicate two controllers. Check:

- Artemis official provider and OpenRGB both controlling the device
- vendor lighting software retaining exclusive control
- official provider and direct compatibility bridge both enabled
- two active Artemis profiles selecting the same LEDs

Use one device provider per physical device.
