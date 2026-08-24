# Player Coordinates HUD

A Core Keeper mod that permanently shows your **world coordinates and
distance from the Core** on screen — in Core Keeper's own map format, e.g.
`57, -24 (62)`.

Vanilla only shows coordinates inside the map view, and only for wherever your
mouse cursor happens to be — never for the player, and never outside the map.
This mod puts your own position on screen at all times, while you're actually
playing.

## Features

- **Always-on readout.** Your current tile coordinates and straight-line
  distance to the world origin (The Core), updating as you move.
- **Same format as the map.** `x, z (distance)` — identical numbers to what
  the map view shows for the same tile, so the two are directly comparable.
- **Pick where it sits.** Any of the four screen corners, or below the
  minimap — lined up with the game's own UI edges. The below-minimap position
  moves to the top-right corner whenever the minimap is not on screen, so it
  never ends up on top of the big map, and in a PvP world it sits below the
  game's own PvP notice rather than on it. The bottom-right corner keeps clear
  of the on-screen button hints.
- **Auto-hides during inventory / menus / load screens, and when you hide the
  game's interface** (the toggle-UI key) — never overlaps the
  vanilla UI. Stays visible while the big map (Tab) is open, though: the
  map's own coordinate readout follows your mouse cursor, so having this
  one keep following your player at the same time gives you both at a
  glance.
- **A position marker beside the numbers**, drawn after the one the game puts
  on your position on the map. Switch it off if you prefer the numbers alone.
- **Configurable in-game.** Open **Options → Mod settings** to change the
  position, hide the marker, or switch the readout off entirely. No config
  files.
- **English and German.** The settings labels and hint follow the in-game
  language.

## Requirements

- Core Keeper (verified on 1.2.1.5)
- [CoreLib](https://mod.io/g/corekeeper/m/corelib) — required dependency
- *Mod Settings Menu* — required dependency. Hosts the in-game settings
  screen (Options → Mod settings) where the readout is configured.

## Installation

Subscribe in-game through the **Mods** menu (or on the mod.io website) and
restart the game. CoreLib and the Mod Settings Menu must both be installed
alongside this mod.

## How to use

Nothing to do — the readout appears as soon as you're in a world, and follows
you as you move. To move it, hide its marker or turn it off, open **Options →
Mod settings → Player Coordinates HUD**; changes take effect as soon as you
close the menu, with no restart.

## Known Limitations

- **No hotkey.** There is currently no keybind to show/hide the readout on
  demand — only the Mod settings toggle.

## Localisation

The mod ships **English and German** and follows the in-game language.
Translations to other Core Keeper languages are welcome — open an issue or
pull request on the source repository.

## License

Personal-use, non-commercial — Pugstorm Core Keeper EULA. Built against the
official `CoreKeeperModSDK`. Source on GitHub; contributions and translations
welcome.
