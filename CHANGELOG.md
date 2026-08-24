# Changelog

All notable changes to this mod are documented here.

## [1.1.0]

### Added

- **The readout's position is now a setting.** Pick one of the four screen
  corners, or a spot below the minimap, in **Options → Mod settings → Player
  Coordinates HUD**. The default is still the bottom-left corner.
- **The "below minimap" position steps aside when the minimap does.** Whenever
  the minimap is not on screen — switched off in the options, or replaced by
  the big map (Tab) — the readout moves to the top-right corner instead, so it
  stays readable rather than disappearing.
- **The bottom-right corner keeps clear of the on-screen button hints**, moving
  just above them and dropping into the corner when they are hidden.
- **"Below minimap" also gives way to the PvP notice.** In a world with PvP
  enabled the game shows its own label in that exact spot; the readout now sits
  below it instead of on top of it, and moves back up when PvP is switched off.
- **A position marker sits beside the coordinates.** A small icon to the left of
  the readout, modelled on the marker the game puts on your position on the map.
  Switch it off with **Show icon** if you prefer the numbers on their own — the
  line then sits exactly where it did before.

### Changed

- **Every position now lines up with the game's own UI edges** rather than sitting
  at a hand-picked offset. The default corner therefore shifts slightly compared
  to 1.0.1: it is now flush with the status bars.
- In the two right-hand corners the readout is right-aligned, so a long
  coordinate string grows inwards instead of running off the screen edge.
- **The readout now hides when you hide the game's interface** (the toggle-UI
  key, or the matching option). Previously it stayed on an otherwise empty
  screen.

## [1.0.1] - 2026-08-06

### Fixed

- Declared as client-only (`requiredOn: Client`). The mod previously declared
  itself as required on client **and** server, which made Core Keeper refuse to
  join any server that does not also have it installed — the player was offered
  only "disable the mod" or "cancel the connection". Nothing in this mod needs
  the server, so joining unmodded servers now works.

## [1.0.0] - 2026-08-06

- Initial release: always-on world coordinates and distance from the Core in the bottom-left HUD corner.
