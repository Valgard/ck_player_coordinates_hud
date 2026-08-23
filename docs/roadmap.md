# Player Coordinates HUD — Roadmap

Points that are **deliberately cut to stand alone**, not a shopping list for a
release. The useful question is "which point next?", never "what goes into
version X" — a version collects whatever happened to be finished by then.

Each entry records what is already settled and what still has to be decided, so
picking one up does not mean re-deriving the groundwork. None of them is
started.

## Give way to ItemChecklist's HUD

Both HUDs now occupy the same row: ICL's HUD container sits at `(10, 7.8)` and
this mod's top corners at `y 7.8`, so with both installed and the readout set to
a top-right position, they overlap. It should step below ICL when ICL is
present, and keep the plain corner when it is not.

**Settled.** The mechanics are the same shape as the minimap and button-hint
cases already in `CoordinatesHud`: find the object, measure what is drawn, sit
clear of it, fall back when it is absent. ICL's HUD root is named
`ItemChecklistHUD` (`(Clone)` once instantiated) and lands under the same parent
this mod uses, so a name lookup finds it **without an assembly reference** — and
that matters: ICL is a separate mod that may simply not be installed, so the
dependency has to stay optional.

**Open — and it is a design question, not an implementation detail.** ICL's
`CounterText` is LEFT-aligned at `+0.6` inside its container, while this readout
is right-aligned in right-hand positions. Two stacked rows only look deliberate
if they share an edge, so one side has to give: either this readout switches to
left-aligned when it tucks under ICL, or both keep the right edge and their left
ends stay ragged. Decide that before writing code.

## A rebindable show/hide hotkey

Would need CoreLib's `ControlMappingModule` with its own control-mapping
category, per the `AddNewCategory` pattern ItemChecklist's Iter-34 established —
the default `-1`/"Mods" bucket suppresses its own sub-header — plus two
localisation terms.

**Settled.** The visibility decision lives in exactly one place,
`CoordinatesHud.LateUpdate`, so the hotkey is one more term ANDed into that
single `bool show` expression.

## An icon beside the readout

Original pixel art, with CK's own map marker as a **visual reference only**.

**Cost is mostly setup, not drawing.** This mod ships no sprites at all today —
there is no `Art/` tree — so it means standing up the whole sibling pipeline
(`.pixaki` master → `utils/pixaki_to_sheet.py` → sheet + `.meta` → prefab
reference). The prefab also needs a new child, which per the parent `CLAUDE.md`
means a Unity Editor session rather than hand-written YAML. Which side the icon
sits on falls out of the same `rightAligned` decision `ApplyPosition` already
makes.

**Why CK's marker is a reference and not a sprite to reuse.** It is **4 × 4 px**
(`Assets/Sprite/player_marker.asset` in the AssetRipper dump, atlas
`Texture2D/sactx-0-256x128-Uncompressed-ui-ed19f136.png`): a white ring around a
2 × 2 colour core. Its corner pixels are **opaque map-background blue, not
transparent** — Pugstorm fakes the rounded corner with the map's own backdrop.
Fine on the map, visibly wrong over gameplay.
