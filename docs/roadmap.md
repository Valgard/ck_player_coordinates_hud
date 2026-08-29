# Player Coordinates HUD — Roadmap

Points that are **deliberately cut to stand alone**, not a shopping list for a
release. The useful question is "which point next?", never "what goes into
version X" — a version collects whatever happened to be finished by then.

Each entry records what is already settled and what still has to be decided, so
picking one up does not mean re-deriving the groundwork.

## New screenshots, showing what 1.1.0 added

This mod has exactly one picture of itself — `sources/`'s
`core_keeper_player_coordinates_hud_hud.png` — and it was taken before any of
1.1.0 existed.

**Settled.** The picture is demonstrably stale, not merely old: it shows the
readout in the bottom-left corner with no marker beside it, sitting at the
hand-picked offset that 1.1.0 replaced with an edge-flush alignment. So the
three visible things that release added — the five positions, the position
marker, and the alignment to the game's own UI edges — are shown nowhere, and
neither is the settings section that drives them.

Where a picture goes is settled too, and only one of the destinations is
automated: `CK_DISCORD_MEDIA` in `.envrc` names that file for the Discord post,
while the mod.io and Workshop galleries are filled in by hand — the publish
pipeline uploads the logo and nothing else.

**To decide.** How many pictures and of what: one per position overstates a
choice most players make once, one of the default corner understates that there
is a choice at all, and the settings section is a candidate subject in its own
right because it is where the choice is made. Whether the marker is shown in
both states or only on. Whether the new pictures replace the existing file —
which would keep `CK_DISCORD_MEDIA` a single entry — or join it, since that
variable takes a pipe-separated list. And whether the Discord thread gets the
new images as a comment, or keeps the ones it already carries.
