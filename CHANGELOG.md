# Changelog

All notable changes to this mod are documented here.

## [1.0.1] - 2026-08-06

### Fixed

- Declared as client-only (`requiredOn: Client`). The mod previously declared
  itself as required on client **and** server, which made Core Keeper refuse to
  join any server that does not also have it installed — the player was offered
  only "disable the mod" or "cancel the connection". Nothing in this mod needs
  the server, so joining unmodded servers now works.

## [1.0.0] - 2026-08-06

- Initial release: always-on world coordinates and distance from the Core in the bottom-left HUD corner.
