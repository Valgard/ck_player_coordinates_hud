# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with
code in this repository.

## What this repo is

A Core Keeper mod that permanently shows the player's **world coordinates and
distance from the Core** on the HUD, in Core Keeper's own map format
(`x, z (distance)`, e.g. `57, -24 (62)`). Vanilla only shows coordinates inside
the map view, and only for the mouse cursor, never for the player. Player-facing
settings — an `Enabled` toggle, a `Position` choice (four corners plus
below-the-minimap) and a `Show icon` toggle for the marker beside the numbers —
via the Mod Settings Menu framework.
Hard-depends on CoreLib + Mod Settings Menu. Personal-use, non-commercial
(Pugstorm EULA).

The parent `../CLAUDE.md` holds the mod-agnostic SDK/CrossOver guidance shared
with the sibling mods.

## Build and deploy

```bash
source .envrc           # or, from a worktree: source ../../../.envrc && source .envrc
../utils/build.sh       # Unity batchmode build; on Darwin auto-runs install-macos.sh
                        # from a worktree: ../../../utils/build.sh
```

Unity Editor must be closed (it locks the project). `utils/link.sh` symlinks
the repo's `unity/` mirror into `$SDK_PATH/Assets/`: one **directory** symlink
for `unity/PlayerCoordinatesHud/`, plus file symlinks for the Assets-level
files beside it (`PlayerCoordinatesHud.asset`, `.asset.meta`, `.meta`).
`build.sh` invokes it idempotently on every run, so worktree switches and repo
moves self-heal.

**Concurrent-build / shared-SDK caveat:** all sibling mods share one
`CoreKeeperModSDK` clone with a single `UnityLockfile`. If another session is
building, wait for the lock to release — do not kill it.

**No automated tests** — verification is a build plus a runtime sandbox check
plus manual in-game inspection. The Editor build compiling cleanly does not
prove the mod passes RoslynCSharp's runtime sandbox; after launching CK, grep
`Player.log` (per-launch — the prior session rotates to `Player-prev.log`):

```bash
L="$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log"
command grep -E "PlayerCoordinatesHud|CompileFailed|safetyCheck|error CS" "$L" | head -20
```

Expected: `Successfully compiled PlayerCoordinatesHud safetyCheck=True`, zero
`CompileFailed`, zero `error CS`. Then in-game: the readout appears
bottom-left, updates as you move, survives hiding/showing (inventory open-
close, `Enabled` off-on), stays hidden through both load screens and the
intro cutscene, and its numbers agree with the map view (Tab) for the same
tile. Verified on Core Keeper 1.2.1.5.

**Placement needs its own pass.** The anchors (`LeftEdgeX`, `RightEdgeX`,
`TopY`, `BottomY`, `MinimapBottomY`) were read off the vanilla prefab, never
measured in a running game — so the whole set rests on the dump matching
runtime. **One check settles that:** set `BottomLeft` and see whether the
readout's left edge is *exactly* flush with the status bars, then set
`BelowMinimap` and check the gap to the minimap frame. Flush on both → the
parent transform is identity and the dump is faithful. Both off by the same
amount → the parent is not identity. Off by different amounts → the geometry
is wrong.

Then walk all five `Position` values: each corner clears the vanilla UI; the
right-hand ones are right-aligned so a long string (stand far from the Core,
ideally west/north for the minus signs) grows inwards; `BelowMinimap` sits one
pixel under the minimap and jumps to the top-right corner when it goes away —
switch the minimap off in the options, and open the big map with Tab.
`BottomRight` needs its own look: walk up to a chest so hint rows appear and
disappear **without moving**, and toggle key hints off.

**`TopRight` rests on another mod's geometry, so it gets four steps of its own**
— and they have to be repeated whenever ItemChecklist ships a release, because
nothing else would catch it moving its counter. With that mod installed and its
counter on: the readout sits 8 px left of it on the same row, and walking far
from the Core grows it *leftwards* while the gap stays put. Switch that mod's
counter off in its own settings: the readout takes the whole corner back, with
no restart. Set `BelowMinimap` while the minimap is **visible**: it must stay
under the minimap and must **not** step sideways — that is the case the `if`'s
placement protects, and the failure would look like a bad default rather than a
bug. Then hide the minimap: it jumps to the top-right corner *and* steps aside
there.

Two failure modes worth naming, because both are silent:
- A position change that only takes effect after the next tile boundary means
  `RepaintForNewAlignment` is not firing (or not passing `force: true`).
- Changing the position while the settings screen is open shows nothing — the
  readout is hidden behind any menu. It applies on close, in the same frame.

## Architecture

Four runtime classes in the `PlayerCoordinatesHud` namespace:

- **`PlayerCoordinatesHudMod` (`IMod`)** — bootstrap. `Init` registers the Mod
  Settings section (a `Toggle` for `enabled`, default on, a `Choice` for
  `position`, default `BottomLeft`, and a `Toggle` for `showIcon`, default on)
  and binds the handles into `ModConfig` — **by name**, because two of the three
  are `SettingHandle<bool>` with the same default, so a swap would compile,
  clear both guards and log a plausible line. None of them is marked
  `RequiresRestart`: all are read live every frame, so a change takes effect the
  moment the menu closes — **not** while it is open, since the visibility gate
  hides the readout behind any menu. `ModObjectLoaded` captures the HUD prefab
  by GameObject name (`"PlayerCoordinatesHUD"`) — routed this way, **not** via
  CoreLib's `UserInterfaceModule.RegisterModUI`, because that path hides the UI
  on `HideAllInventoryAndCraftingUI`, the opposite of this mod's always-on
  intent. `Update` lazily instantiates the captured prefab under
  `Manager.ui.chestInventoryUI.transform.parent` once the UIManager hierarchy
  exists (same pattern as the sibling HUD mods), then feeds the local player's
  world position into `CoordinatesHud.Render` every frame. Re-instantiation is
  gated on the instantiated GameObject, **not** on `CoordinatesHud.Instance` —
  `Instance` is only assigned by that component's own `Awake`, which never fires
  if the Editor wiring (the `hudRoot`/`coordinateText`/
  `coordinateTextOutline`/`icon` serialized fields) is missing, and an
  `Instance`-based gate would then re-instantiate the prefab every frame.
- **`ModConfig`** — the settings adapter. The player-facing knobs: `enabled`
  (Toggle, default `true`), `position` (Choice over the `Position` enum, default
  `BottomLeft`) and `showIcon` (Toggle, default `true`), read from bound
  `SettingHandle`s (`ModConfig.Bind`, called once from `Init`). Singleton shape
  mirrors the sibling mods (`ModConfig.Instance.enabled`). Before `Bind` is
  called (the brief pre-load window), the getters fall back to those defaults.
  **The `Position` member names are persisted data, not just identifiers** —
  `Choice` stores a setting as `value.ToString()` and resolves its label as
  `PlayerCoordinatesHud-Config/position/<name>`, so renaming one silently resets
  every player who chose it and drops its localization.
- **`CoordinatesHud : UIelement`** — owns the two `PugText`s (`coordinateText`
  white foreground, `coordinateTextOutline` black drop-shadow, offset 1px
  down-right), the `icon` SpriteRenderer beside them, and the `hudRoot` child it
  toggles. `LateUpdate` decides visibility from explicit signals
  (`WorldState.IsInPlayableWorld && !Manager.prefs.hideInGameUI &&
  !Manager.ui.isAnyInventoryShowing && !Manager.menu.IsAnyMenuActive() &&
  ModConfig.Instance.enabled`) — deliberately not CK's own HUD idiom
  `Manager.ui.CalcGameplayUITargetScaleMultiplier()`, which is a **global**
  scale rather than a per-element one and collapses to `(0,0,0)` for several
  unrelated reasons at once (hidden UI, fades, load screens); `WorldState`
  already covers the latter. **`hideInGameUI` is load-bearing**, not a niche
  setting: it is a regular keybind (`PlayerInput.InputType.TOGGLE_UI`), and
  without that term the readout is the one thing left on an empty screen — with
  `BelowMinimap` additionally jumping to the top-right corner, because CK
  deactivates the minimap along with the rest. `Render(float3 playerWorldPos)`
  is called every frame from `PlayerCoordinatesHudMod.Update` but only actually
  repaints the `PugText`s when the formatted string changed (a `_lastRendered`
  cache) — this HUD runs permanently, unlike CK's own `CoordinatesUI`, which
  only renders while the map is open, so an unconditional `Render` would churn
  every frame. `ApplyPosition` (also from `LateUpdate`, only while visible)
  moves the root to the configured anchor, matches the text alignment to it, and
  then calls `ApplyIcon` to lay out the marker and text within the row. **All
  anchors are constants** read off the vanilla prefab — the uiCamera is not
  screen-driven (its `PugCamera` runs `OutputMode.Fixed` at 480×270 and PugRP
  forces the aspect from those numbers), so the visible area is always ±15 ×
  ±8.4375 and the edges never move. Measured at runtime instead: the drawn
  bounds of CK's button hints and of its PvP label, which move with what they
  contain; the drawn left edge of ItemChecklist's counter, which shares the
  top-right row — found as a sibling under the same HUD parent, by name prefix,
  so that optional mod needs no assembly reference; the text height and its
  drawn left edge via `PugText.dimensions`; the marker's width off its own
  sprite; and the text row's own depth below the root, read back from the prefab
  transform (`RowDrop`). The prefab's own Z is kept, since anchors are 2-D.
  `ApplyIcon`, called from the end of `ApplyPosition`, then lays out marker and
  text within that row: the marker leads the value on **both** sides of the
  screen (as ItemChecklist's does) rather than mirroring with the corner. What
  mirrors is which half moves — a left-hand corner pins the icon to the anchor
  and shifts both text rows right by the icon's width plus the gap, a right-hand
  one leaves the text ending at the anchor and hangs the icon off
  `dimensions.xMin`. Both stay flush with the anchor. `showIcon: false` returns
  that width to the text in a left-hand corner; in a right-hand one the text
  never moved, so only the marker goes. The shift is written as prefab-base +
  offset, never accumulated, so the per-frame pass is idempotent and the outline
  keeps its 1 px down-right offset for free. The icon's **y stays prefab
  geometry** — see the gotcha below.
- **`WorldState`** — the shared `IsInPlayableWorld` predicate, copied from
  ItemChecklist (its Iter-11.6/Iter-15 fixes): `isInGame &&
  isSceneHandlerReady && !cutsceneIsPlaying && Manager.main.player != null &&
  !Manager.load.IsLoading()`. A bare `player != null` check is insufficient —
  the player object exists from `PlayerController.OnOccupied`, which fires
  while the world-load screen is still up and survives the exit-to-menu
  transition, so it is true across both load screens on its own.

`unity/` is the canonical source — a 1:1 mirror of the SDK's `Assets/` tree
holding every file the Editor generates for the mod: the `.cs` sources, both
`.asmdef` files, the ModBuilderSettings `.asset`, the HUD prefab, the sprite
sheet under `Art/UI/`, the localization generator outputs (gitignored, produced
from `localization/localization.yaml` at build time), and all `.meta` GUID
carriers.

`Art/UI/player_position.png` is **generated**, not hand-edited: the master is
`sources/player_position.pixaki`, cut by `../utils/pixaki_to_sheet.py` against
the sprite definition in `sources/player_position.json`. That definition pins
**both halves of the prefab's sprite reference**, because each is derived from
something that can change without anyone meaning to: the sheet **GUID** hashes
the output path (so cutting the same master from a worktree would orphan the
reference), and the sprite's **internalID** hashes the layer name (so renaming
`Player` in the master would). A broken reference shows as an empty patch of
HUD, not as an error. The definition also excludes the layers that must never
ship: CK's extracted originals, the 10×10 `Ping` alternative, and the master's
working background.
`../utils/pixaki_inspect.py` prints any layer as a character grid with its
palette — which is how a single stray pixel and a forgotten background layer
were caught here.

## Mod-specific gotchas

- **The `hudRoot` topology is load-bearing, not cosmetic.** `CoordinatesHud`
  sits on the prefab's **always-active root** GameObject and toggles a
  separate child (`hudRoot`, named `HudRoot` in the prefab) that carries the
  two `PugText`s. If the component instead toggled its own GameObject,
  disabling it would stop its own `LateUpdate` from running — and a disabled
  `MonoBehaviour` never re-enables itself, so the readout could never come
  back on. Hidden once (inventory open, a menu, `Enabled` toggled off), gone
  for the rest of the session. Any prefab rework must keep the toggled
  GameObject a *child* of the component's own root, never the root itself.
- **`math.floor`, never an `(int)` cast — and never round.**
  `CoordinatesHud.Render` floors both axes with
  `(int)math.floor(playerWorldPos.x/.z)`. A cast truncates toward zero, so a
  position like `-14.3` casts to `-14` but floors to `-15` — a one-tile
  discrepancy that only shows up west/north of the Core, exactly where a tester
  is unlikely to look first. CK's own `CoordinatesUI` floors too, so this keeps
  the two surfaces in exact agreement. The distance is then computed from the
  **already-floored integers** (`sqrt(x*x + z*z)`, both `int`) and formatted
  with `.ToString("F0")` rather than rounded — that formatting choice is also
  copied directly from CK's own call, so there is no independent rounding-mode
  decision to get wrong.
- **`maxWidth` must stay `0` on both `PugText`s.** Any non-zero value routes
  every `Render` call through CK's `PugFont` word-wrap path
  (`AddNewLinesToLinesExceedingMaxWidth`), which throws
  `IndexOutOfRangeException` on certain inputs — a per-frame exception once
  this HUD's change-gated `Render` actually fires. This is the same CK bug
  class ItemChecklist hit in its search field and worked around the same way:
  keep single-line labels at `maxWidth: 0`.
- **The prefab is a modified copy of CK's own `CoordinatesUI` subtree** (taken
  from the game's global manager prefab), not built from scratch: CK's own
  `CoordinatesUI` component was dropped, the distance-related child text
  objects were removed, a new `HudRoot` child was inserted as the toggled
  container, and the remaining children were renamed off Pugstorm's own typo
  (`coorindates`) to `Coordinates`/`CoordinatesOutline`. This is safe because
  Unity wires serialized references by `fileID`, not by GameObject name — the
  renames don't break anything. The **only** name-based lookup anywhere in
  this mod is the prefab **root**'s GameObject name
  (`"PlayerCoordinatesHUD"`), matched in `PlayerCoordinatesHudMod.ModObjectLoaded`.
  Renaming the root would silently stop the HUD from ever being instantiated.
- **Changing the text alignment needs `PugText.Render(text, false, true)` —
  clearing this mod's own `_lastRendered` is not enough.** There are *two*
  caches in the path and both compare the string. Ours is `_lastRendered`;
  PugText's is `HasCorrectGlyphs`, which validates its existing glyphs against
  language, the string, the format fields, `orderInLayer` and `maxWidth` —
  **`style.horizontalAlignment` is not in that list**. Alignment is baked into
  each glyph's local offset at draw time, so with the string unchanged PugText
  keeps the old offsets and the readout stays laid out for the previous corner.
  Clearing only `_lastRendered` opens our gate and PugText's still blocks
  behind it; the symptom is a readout that keeps the wrong alignment until the
  player crosses a tile boundary, i.e. it looks *almost* right and fixes itself
  while you walk. `force: true` is CK's own escape hatch. This only bites on a
  left↔right change — two anchors on the same side share an alignment, and the
  glyphs are children of the root, so they follow a pure move for free.
- **The icon's y was calibrated by eye, twice, and the arithmetic was wrong
  both times.** First: a `PugText` transform is the vertical **centre** of its
  line, not its top edge, so the marker matches the text's own y instead of
  hanging below it. Then: that box centre is still a pixel below the *optical*
  centre of digits, because the 10 px line box reserves room for descenders
  numbers never use. Hence `y: 0` in the prefab — one pixel above the text
  row's own `-0.0625` — and hence `ApplyIcon` writing **only x** and leaving the
  height to the prefab. The general form is in
  `../docs/ck/prefabs-and-rendering.md`.
- **A small sprite must not land on the 1/16 grid.** Both of `ApplyIcon`'s
  placements would: half a 6 px icon is `3/16`, and a glyph-measured text edge
  is a whole number of pixels. Point-filtered sprites render distorted exactly
  on a texel boundary, so `IconOffGrid` (0.005) shifts them off it — 0.08 px,
  invisible, and the reason the ring renders round.
- **`miniMapBorder.gameObject.activeInHierarchy` is one signal for three
  cases** — but they come from two different places, which is why grepping one
  method makes it look wrong. `MapUI.LateUpdate` deactivates the border's
  parent `container` when `Manager.prefs.showMinimap` is off (a real options
  entry, `RadicalOptionsMenuOption_ShowMinimap`) or an inventory is open;
  `MapUI.UpdateUIScaling` deactivates the border itself for the big map.
  `activeInHierarchy` sees both, so `BelowMinimap` needs no separate term.
  CK's own `PvPTextUI` reads the same flag — but note what for: it picks
  between two vertical offsets, not its own visibility (that is
  `pvpMode && !isShowingMap`).
- **`EffectiveCorner` answers which corner a position *belongs to*, never where
  the readout currently stands.** It maps `BelowMinimap` to `TopRight`
  unconditionally — including while that position is successfully sitting under
  the minimap — so anything keyed on the corner has to live inside the branch
  where the plain corner actually applied. Testing it one level out is how the
  ItemChecklist step would also shove the below-minimap row sideways, and the
  symptom would not read as a bug: a misplaced row looks like a bad default.

## Not yet built

One point stands in `docs/roadmap.md`: the mod's only screenshot predates
1.1.0, so the positions, the marker and the edge-flush alignment are shown
nowhere. No code work — but the picture is what the mod.io and Workshop
galleries and the Discord post all show. An idea gets written up there first —
what is settled, what still has to be decided — so that picking it up later
does not mean re-deriving the groundwork.

## macOS / CrossOver

Deployed through the fake-mod.io workaround (see parent `../CLAUDE.md`). This
mod's fake mod.io ID is **`9999990`**; every sibling uses a distinct ID in the
`9999990`..`9999999` block. Do not open the in-game Mods menu while a fake-ID
install is active; re-run `../utils/build.sh` to restore if the cache is
wiped.

**Required dependencies:** declared in `unity/PlayerCoordinatesHud.asset`'s
`dependencies:` list (`CoreLib`, `ModSettingsMenu`, both `required: 1`) and in
the runtime asmdef's `references`. Only `ModSettingsMenu` supplies C# types
this mod actually compiles against; the CoreLib reference is harmless and
kept for family consistency with the sibling mods, but the load-bearing part
of the CoreLib dependency is the **manifest** entry — it drives the Roslyn
compile order at load time, independent of whether this mod's code touches
any CoreLib type. The loader refuses to load the mod without either
dependency.

## Publishing to mod.io

`../utils/upload.sh` publishes this mod via the shared
`CoreKeeperModUtils.CLIPublishHelper.Publish` Editor class. The version comes
from the topmost `## [x.y.z]` entry of `CHANGELOG.md`; the profile logo is
`unity/PlayerCoordinatesHud/Editor/logo.png`; the real mod ID lives in
`unity/PlayerCoordinatesHud/Editor/PlayerCoordinatesHud_modio.asset`, written
there by the first successful publish — read it there rather than from any
line of prose, this one included.

**The tags are synchronised by the publish, not set by hand.** The `Type` group
comes from `CK_MODIO_TYPE` in `.envrc`, the `Access Type` group is derived from
the ModBuilderSettings' `skipSafetyChecks`, and both are diffed against what
mod.io currently carries, so a surplus tag is removed rather than left
standing. That last part is why a hand-set `Asset` tag no longer needs
guarding against, though it remains worth knowing what one would do: it
silently disables the mod's scripts. Mechanics in `../docs/publishing.md`.

## Conventions

- Commit messages: Conventional Commits (`type(scope): subject`), imperative,
  no emoji.
- Documentation files (`CLAUDE.md`, `README.md`, `docs/`) are English; chat
  answers are German.
- Prefer `git commit --amend` / `git reset --soft` over fix-up commits on a
  personal branch, and `git rebase` over `git merge`.
