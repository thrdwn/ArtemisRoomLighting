---
version: alpha
name: Zeus Lighting
description: A friendly, room-first lighting control system for PC ambient lighting, game events, and study comfort.
colors:
  primary: "#3E91C9"
  secondary: "#67DCCB"
  tertiary: "#F7C95C"
  neutral: "#EEF2F6"
  background: "#101317"
  surface: "#151A20"
  surfaceRaised: "#1D232A"
  surfaceInteractive: "#25313B"
  border: "#40505E"
  text: "#EEF2F6"
  textMuted: "#A8B4C1"
  textInverse: "#071014"
  action: "#3E91C9"
  actionHover: "#56A9DE"
  success: "#7ED9A5"
  warning: "#E6BC70"
  danger: "#F06F61"
  fire: "#F2743D"
  amber: "#F7C95C"
  teal: "#67DCCB"
  purple: "#B57AE6"
  blue: "#66B7FF"
  black: "#000000"
typography:
  display:
    fontFamily: Segoe UI
    fontSize: 34px
    fontWeight: 600
    lineHeight: 1.15
    letterSpacing: 0px
  title:
    fontFamily: Segoe UI
    fontSize: 21px
    fontWeight: 600
    lineHeight: 1.25
    letterSpacing: 0px
  body:
    fontFamily: Segoe UI
    fontSize: 15px
    fontWeight: 400
    lineHeight: 1.45
    letterSpacing: 0px
  label:
    fontFamily: Segoe UI
    fontSize: 13px
    fontWeight: 600
    lineHeight: 1.2
    letterSpacing: 0px
  caption:
    fontFamily: Segoe UI
    fontSize: 12px
    fontWeight: 400
    lineHeight: 1.35
    letterSpacing: 0px
rounded:
  none: 0px
  sm: 4px
  md: 8px
  lg: 12px
  full: 999px
spacing:
  xs: 4px
  sm: 8px
  md: 16px
  lg: 24px
  xl: 32px
  xxl: 48px
components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.textInverse}"
    typography: "{typography.label}"
    rounded: "{rounded.md}"
    padding: 12px
  button-secondary:
    backgroundColor: "{colors.surfaceInteractive}"
    textColor: "{colors.text}"
    typography: "{typography.label}"
    rounded: "{rounded.md}"
    padding: 12px
  card:
    backgroundColor: "{colors.surfaceRaised}"
    textColor: "{colors.text}"
    rounded: "{rounded.md}"
    padding: 16px
  device-chip:
    backgroundColor: "{colors.surfaceInteractive}"
    textColor: "{colors.text}"
    typography: "{typography.label}"
    rounded: "{rounded.full}"
    padding: 8px
  warning:
    backgroundColor: "{colors.warning}"
    textColor: "{colors.textInverse}"
    typography: "{typography.label}"
    rounded: "{rounded.sm}"
    padding: 8px
  status-success:
    backgroundColor: "{colors.success}"
    textColor: "{colors.textInverse}"
    typography: "{typography.label}"
    rounded: "{rounded.full}"
    padding: 8px
  status-danger:
    backgroundColor: "{colors.danger}"
    textColor: "{colors.textInverse}"
    typography: "{typography.label}"
    rounded: "{rounded.full}"
    padding: 8px
  device-keyboard:
    backgroundColor: "{colors.blue}"
    textColor: "{colors.textInverse}"
    typography: "{typography.label}"
    rounded: "{rounded.full}"
    padding: 8px
  device-mouse:
    backgroundColor: "{colors.purple}"
    textColor: "{colors.textInverse}"
    typography: "{typography.label}"
    rounded: "{rounded.full}"
    padding: 8px
  event-fire:
    backgroundColor: "{colors.fire}"
    textColor: "{colors.textInverse}"
    typography: "{typography.label}"
    rounded: "{rounded.sm}"
    padding: 8px
  event-ambient:
    backgroundColor: "{colors.teal}"
    textColor: "{colors.textInverse}"
    typography: "{typography.label}"
    rounded: "{rounded.sm}"
    padding: 8px
---

# Zeus Lighting Design

## Overview

Zeus is a practical control room for PC lighting. It should feel calm, visual, and direct: a normal PC user should understand the first screen without knowing what Artemis, OpenRGB, GSI, SDK, HID, or RGB.NET mean.

The emotional target is capable and immersive, not flashy. The product helps users feel in control of a room full of lights by making every device identifiable, draggable, testable, and reversible.

Zeus must support two layers of experience:

- **Simple mode:** one-click Watch, Game, Study, and Custom profiles.
- **Power mode:** per-device roles, position mapping, backend selection, game-event routing, and performance caps.

## Colors

The palette is dark because lighting control is often used while watching or gaming. It should not blast the user's eyes before the room lighting even starts.

- **Background (#101317):** app foundation, close to black but not pure black.
- **Surface (#151A20):** main workspace panels.
- **Surface Raised (#1D232A):** cards, device panels, profile rows, and settings groups.
- **Action (#3E91C9):** primary action blue for mode switching, selected navigation, and active controls.
- **Success (#7ED9A5):** connected devices, ready plugins, successful tests.
- **Warning (#E6BC70):** missing support, backend conflicts, color calibration warnings.
- **Danger (#F06F61):** destructive or critical device conflicts.
- **Fire, Amber, Teal, Purple, Blue:** game and device identity accents, used sparingly and meaningfully.

Avoid one-note blue or purple screens. Zeus should use dark neutral structure with purposeful lighting colors.

## Typography

Use Segoe UI to feel native on Windows. Headings should be clear but not huge. Compact panels, cards, and device lists should use smaller text that scans quickly.

- **Display:** app title and major page titles only.
- **Title:** cards, mode names, and section headers.
- **Body:** explanations and instructions.
- **Label:** buttons, device names, tabs, and status chips.
- **Caption:** metadata, backend names, FPS caps, and helper text.

Do not scale font size with viewport width. Letter spacing stays at `0px`.

## Layout

The app is room-first. The main layout uses:

- Left navigation for Home, Room, Devices, Profiles, Plugins, and Performance.
- Main workspace for the selected task.
- A persistent status line for plain-language feedback.
- A room map with monitor, desk, rear, side, ceiling, and future strip zones.

Normal users should be guided through Flash, Name, Place, Test, and Choose Behavior. Advanced backend details are secondary.

## Elevation & Depth

Use restrained contrast instead of heavy shadows. Cards are simply raised surfaces with clear spacing. The app should feel like a tool, not a marketing landing page.

Use color and border state to communicate:

- Selected mode
- Selected device
- Connected or missing backend
- Conflict or warning
- Live test state

## Shapes

Use `8px` radius for cards and buttons. Use pill shapes only for device chips, status chips, and compact tags. Avoid nested card stacks.

The room map may use softly rounded zones to distinguish monitor, desk, side walls, rear wall, and ceiling.

## Components

Primary components:

- **Mode card:** Watch, CS2, Valorant, Study. Shows purpose, trigger, and active/running state.
- **Device chip:** friendly name, kind, backend, and Flash affordance.
- **Room map bubble:** draggable colored dot for each device.
- **Device editor:** Flash, zone, Watch behavior, Game behavior, brightness, FPS cap, color test.
- **Backend card:** status, purpose, examples, and configure action.
- **Event palette row:** game event name, target lights, intensity, and color behavior.

Every device must have a Flash action. Users should never be forced to identify hardware from raw IDs.

## Do's and Don'ts

Do:

- Show visual instructions before technical options.
- Let users drag devices into physical positions.
- Keep one-click profile switching visible.
- Support auto-switch rules for apps and games.
- Make color tests include orange, teal, warm white, cool white, skin tones, and black.
- Warn when two backends may control the same physical device.
- Cap FPS by device type to avoid pointless CPU/GPU/network load.

Don't:

- Lead with plugin IDs, provider GUIDs, database paths, or raw device identifiers.
- Hide basic actions like Flash, Test, Rename, Place, and Disable.
- Treat room lights behind the user like health indicators they cannot see.
- Make every light full brightness all the time.
- Make the UI a single-hue dark-blue dashboard.
- Require users to read a long guide before getting a working Watch setup.
