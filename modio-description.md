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
  minimap. The below-minimap option follows the minimap as the game rescales
  it, and steps aside to the top-right corner whenever the minimap isn't on
  screen — so it never lands on top of the big map.
- Stays out of the way. Auto-hides during inventory, menus, and loading
  screens; it never overlaps the vanilla UI. Keeps showing while the big
  map (Tab) is open, though — the map's own readout follows your mouse, so
  the two together give you both your position and the cursor's.
- Configurable in-game. Open **Options → Mod settings** to move the readout or
  switch it off entirely. No config files.
- English & German, following your in-game language.

## Requirements

- CoreLib (required)
- Mod Settings Menu (required) — hosts the in-game settings screen

mod.io will prompt you to install both when you subscribe.

## Good to know

- Verified on Core Keeper 1.2.1.5.
- A show/hide hotkey is being considered for a future update.

---

*Built with the official Pugstorm Core Keeper Mod SDK. Personal-use,
non-commercial (Core Keeper EULA). Not affiliated with or endorsed by
Pugstorm.*
