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
