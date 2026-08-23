# Changelog

All notable changes to this mod are documented here.

## [1.1.0]

### Added

- **The readout's position is now a setting.** Pick one of the four screen
  corners, or a spot below the minimap, in **Options → Mod settings → Player
  Coordinates HUD**. The default is unchanged, so an existing install stays
  exactly where it was.
- **The "below minimap" position follows the minimap**, including as the game
  rescales it. Whenever the minimap is not on screen — switched off in the
  options, or replaced by the big map (Tab) — the readout uses the top-right
  corner instead, so it stays readable rather than disappearing.

### Changed

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
