# Player Coordinates HUD

Core Keeper shows coordinates only inside the map view, and only for whatever
your mouse is pointing at — never for you, and never while you are out walking.
This puts your own position on screen, permanently, updating as you move, with
a small position marker beside the numbers.

Same format the map uses: `x, z (distance)` — so `57, -24 (62)` is where you
are and how far you are from the Core.

It stays out of the way, hiding during inventory, menus and loading screens,
and when you hide the interface — and it never overlaps the vanilla UI. It does
keep showing while the big map is open, which is deliberate: the map's own
readout follows your cursor, so the two together tell you where you are *and*
what you are pointing at.

## Where it sits

Any of the four screen corners, or below the minimap, lined up with the game's
own UI edges. It gets out of the way of what turns up there: the below-minimap
spot moves to the top-right when the minimap is gone, and slides under the
game's PvP notice in a PvP world; the bottom-right corner keeps clear of the
button hints; and the top-right corner shares its row with **Item Checklist**'s
counter, the two standing side by side rather than on top of each other.

## Settings

**Options → Mod settings** — pick the corner, hide the marker, or switch the
readout off. No config files, no restart. There is no hotkey of its own: Core
Keeper's hide-interface key hides this along with everything else.

## Requirements

**CoreLib** and **Mod Settings Menu** — mod.io offers both when you subscribe.

Client-side only.
