# Player Coordinates HUD

Core Keeper shows coordinates only inside the map view, and only for whatever
your mouse is pointing at — never for you, and never while you are out walking.
This puts your own position in the bottom-left corner, permanently, updating as
you move.

Same format the map uses: `x, z (distance)` — so `57, -24 (62)` is where you
are and how far you are from the Core.

It stays out of the way, hiding during inventory, menus and loading screens,
and never overlapping the vanilla UI. It does keep showing while the big map is
open, which is deliberate: the map's own readout follows your cursor, so the
two together tell you where you are *and* what you are pointing at.

## Settings

**Options → Mod settings** has an on/off switch. The corner is fixed for now;
repositioning and a hotkey are on the list.

## Requirements

**CoreLib** and **Mod Settings Menu** — mod.io offers both when you subscribe.

Client-side only.
