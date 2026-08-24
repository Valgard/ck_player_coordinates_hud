# Player Coordinates HUD

**Know exactly where you are, all the time.**

Core Keeper only shows coordinates inside the map view, and only for wherever
your mouse happens to be pointing — never for you, and never while you're
actually out exploring. Player Coordinates HUD fixes that: it puts your
current position on screen, permanently.

## Features

- Always-on world coordinates and distance from the Core, updating live as
  you move — no need to open the map.
- Same numbers the map itself uses: `x, z (distance)`, e.g. `57, -24 (62)`.
- Put it where you want it: any of the four screen corners, or below the
  minimap — lined up with the game's own UI edges. The below-minimap option
  steps aside to the top-right corner whenever the minimap isn't on screen, so
  it never lands on top of the big map, and in a PvP world it slides below the
  game's own PvP notice instead of covering it. The bottom-right corner keeps
  clear of the on-screen button hints.
- Stays out of the way. Auto-hides during inventory, menus and loading screens,
  and when you hide the game's interface with the toggle-UI key; it never
  overlaps the vanilla UI. Keeps showing while the big map (Tab) is open,
  though — the map's own readout follows your mouse, so the two together give
  you both your position and the cursor's.
- A position marker beside the numbers, modelled on the one the game puts on
  your position on the map. Switch it off if you prefer the numbers alone.
- Configurable in-game. Open **Options → Mod settings** to move the readout,
  hide the marker, or switch it off entirely. No config files.
- English & German, following your in-game language.

## Requirements

- CoreLib (required)
- Mod Settings Menu (required) — hosts the in-game settings screen

mod.io will prompt you to install both when you subscribe.

## Good to know

- Verified on Core Keeper 1.2.1.5.
- There is no separate hotkey for the readout: switch it in the Mod settings,
  or use Core Keeper's own hide-interface key, which hides it along with
  everything else.

---

*Built with the official Pugstorm Core Keeper Mod SDK. Personal-use,
non-commercial (Core Keeper EULA). Not affiliated with or endorsed by
Pugstorm.*
