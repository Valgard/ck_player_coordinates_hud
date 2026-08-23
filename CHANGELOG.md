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

### Changed

- **Every position now lines up with the game's own UI edges** rather than sitting
  at a hand-picked offset. The default corner therefore shifts slightly compared
  to 1.0.1: it is now flush with the status bars.
- In the two right-hand corners the readout is right-aligned, so a long
  coordinate string grows inwards instead of running off the screen edge.

## [1.0.1] - 2026-08-06

### Fixed

- Declared as client-only (`requiredOn: Client`). The mod previously declared
  itself as required on client **and** server, which made Core Keeper refuse to
  join any server that does not also have it installed — the player was offered
  only "disable the mod" or "cancel the connection". Nothing in this mod needs
  the server, so joining unmodded servers now works.

## [1.0.0] - 2026-08-06

- Initial release: always-on world coordinates and distance from the Core in the bottom-left HUD corner.
