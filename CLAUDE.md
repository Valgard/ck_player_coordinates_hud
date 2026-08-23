# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with
code in this repository.

## What this repo is

A Core Keeper mod that permanently shows the player's **world coordinates and
distance from the Core** on the HUD, in Core Keeper's own map format
(`x, z (distance)`, e.g. `57, -24 (62)`). Vanilla only shows coordinates inside
the map view, and only for the mouse cursor, never for the player. Two
player-facing settings — an `Enabled` toggle and a `Position` choice (four
corners plus below-the-minimap) — via the Mod Settings Menu framework.
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

**Placement needs its own pass**, because the four corner anchors are fixed
values that only in-game inspection can confirm (`CoordinatesHud.AnchorBottom*`
/ `AnchorTop*`). Only `AnchorBottomLeft` is inherited from the shipped 1.0.0
prefab; the other three mirror it and are unverified until someone looks. Walk
all five `Position` values and check: each corner clears the vanilla UI and sits
inside the screen; the right-hand corners are right-aligned so a long string
(stand far from the Core) grows inwards, not off the edge; `BelowMinimap` hangs
under the minimap with a sane gap, follows it, and jumps to the top-right corner
when the minimap goes away — switch it off in the options, and open the big map
with Tab. A switch that only takes effect after the next tile boundary means
the `_lastRendered` reset in `ApplyPosition` is not firing.

## Architecture

Four runtime classes in the `PlayerCoordinatesHud` namespace:

- **`PlayerCoordinatesHudMod` (`IMod`)** — bootstrap. `Init` registers the Mod
  Settings section (a `Toggle` for `enabled`, default on, and a `Choice` for
  `position`, default `BottomLeft`) and binds both handles into `ModConfig`.
  Neither setting is marked `RequiresRestart`: both are read live every frame,
  so a change is visible behind the open menu. `ModObjectLoaded` captures the HUD prefab by
  GameObject name (`"PlayerCoordinatesHUD"`) — routed this way, **not** via
  CoreLib's `UserInterfaceModule.RegisterModUI`, because that path hides the
  UI on `HideAllInventoryAndCraftingUI`, the opposite of this mod's always-on
  intent. `Update` lazily instantiates the captured prefab under
  `Manager.ui.chestInventoryUI.transform.parent` once the UIManager hierarchy
  exists (same pattern as the sibling HUD mods), then feeds the local player's
  world position into `CoordinatesHud.Render` every frame. Re-instantiation is
  gated on the instantiated GameObject, **not** on `CoordinatesHud.Instance` —
  `Instance` is only assigned by that component's own `Awake`, which never
  fires if the Editor wiring (the `hudRoot`/`coordinateText`/
  `coordinateTextOutline` serialized fields) is missing, and an
  `Instance`-based gate would then re-instantiate the prefab every frame.
- **`ModConfig`** — the settings adapter. Two player-facing knobs: `enabled`
  (Toggle, default `true`) and `position` (Choice over the `Position` enum,
  default `BottomLeft`), read from bound `SettingHandle`s (`ModConfig.Bind`,
  called once from `Init`). Singleton shape mirrors the sibling mods
  (`ModConfig.Instance.enabled`). Before `Bind` is called (the brief pre-load
  window), the getters fall back to those defaults. **The `Position` member
  names are persisted data, not just identifiers** — `Choice` stores a setting
  as `value.ToString()` and resolves its label as
  `PlayerCoordinatesHud-Config/position/<name>`, so renaming one silently
  resets every player who chose it and drops its localization.
- **`CoordinatesHud : UIelement`** — owns the two `PugText`s
  (`coordinateText` white foreground, `coordinateTextOutline` black
  drop-shadow, offset 1px down-right) and the `hudRoot` child it toggles.
  `LateUpdate` decides visibility from explicit signals
  (`WorldState.IsInPlayableWorld && !Manager.ui.isAnyInventoryShowing &&
  !Manager.menu.IsAnyMenuActive() && ModConfig.Instance.enabled`) —
  deliberately not CK's own HUD idiom
  `Manager.ui.CalcGameplayUITargetScaleMultiplier()`, which returns `(0,0,0)`
  for a mod HUD. `Render(float3 playerWorldPos)` is called every frame from
  `PlayerCoordinatesHudMod.Update` but only actually repaints the `PugText`s
  when the formatted string changed (a `_lastRendered` cache) — this HUD runs
  permanently, unlike CK's own `CoordinatesUI`, which only renders while the
  map is open, so an unconditional `Render` would churn every frame.
  `ApplyPosition` (also from `LateUpdate`, only while visible) moves the root
  to the configured anchor and matches the text alignment to it. Four of the
  five positions are fixed vectors; `BelowMinimap` derives its anchor from
  `Manager.ui.mapUI.miniMapBorder.bounds` — **world-space** bounds, so the
  minimap's own per-frame rescaling is already baked in — converted into the
  HUD's local space via the parent, with the prefab's original Z kept (the
  minimap's Z belongs to the map, not to this HUD).
- **`WorldState`** — the shared `IsInPlayableWorld` predicate, copied from
  ItemChecklist (its Iter-11.6/Iter-15 fixes): `isInGame &&
  isSceneHandlerReady && !cutsceneIsPlaying && Manager.main.player != null &&
  !Manager.load.IsLoading()`. A bare `player != null` check is insufficient —
  the player object exists from `PlayerController.OnOccupied`, which fires
  while the world-load screen is still up and survives the exit-to-menu
  transition, so it is true across both load screens on its own.

`unity/` is the canonical source — a 1:1 mirror of the SDK's `Assets/` tree
holding every file the Editor generates for the mod: the `.cs` sources, both
`.asmdef` files, the ModBuilderSettings `.asset`, the HUD prefab, the
localization generator outputs (gitignored, produced from
`localization/localization.yaml` at build time), and all `.meta` GUID
carriers.

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
- **`math.floor`, never an `(int)` cast — and never round.** `CoordinatesHud.Render`
  floors both axes with `(int)math.floor(playerWorldPos.x/.z)`. A cast
  truncates toward zero, so a position like `-14.3` casts to `-14` but floors
  to `-15` — a one-tile discrepancy that only shows up west/north of the Core,
  exactly where a tester is unlikely to look first. CK's own `CoordinatesUI`
  floors too, so this keeps the two surfaces in exact agreement. The distance
  is then computed from the **already-floored integers**
  (`sqrt(x*x + z*z)`, both `int`) and formatted with `.ToString("F0")` rather
  than rounded — that formatting choice is also copied directly from CK's own
  call, so there is no independent rounding-mode decision to get wrong.
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
- **Changing the text alignment must reset `_lastRendered`.** The `Render`
  change-gate compares the formatted *string*, which alignment does not
  change — so switching between a left and a right corner would leave the old
  alignment on screen until the player happened to cross a tile boundary.
  `ApplyPosition` sets `_lastRendered = null` whenever it flips the alignment,
  which is the only reason the switch looks instant. Any future property that
  affects how the text is drawn rather than what it says needs the same reset.
- **`miniMapBorder.gameObject.activeInHierarchy` is one signal for three
  cases.** CK clears it when the player switched the minimap off in the
  options, when the big map replaced it, and when an inventory is open — so
  `BelowMinimap` needs no separate `Manager.prefs.showMinimap` or
  `mapUI.IsShowingBigMap` term. CK's own `PvPTextUI`, the vanilla element that
  sits below the minimap, tests exactly this and nothing else.

## Deferred (not yet built)

- **A rebindable show/hide hotkey.** Would need CoreLib's
  `ControlMappingModule` (own control-mapping category, per the
  `AddNewCategory` pattern ItemChecklist's Iter-34 established — the default
  `-1`/"Mods" bucket suppresses its own sub-header) plus two loc terms. The
  visibility decision lives only in `CoordinatesHud.LateUpdate`, so a hotkey
  is one more term ANDed into that single `bool show` expression.
- **An icon beside the readout**, drawn as original pixel art with CK's own
  map marker as the visual reference only. This mod ships no sprites at all
  today — no `Art/` tree — so it means standing up the whole sibling pipeline
  (`.pixaki` master → `utils/pixaki_to_sheet.py` → sheet + `.meta` → prefab
  reference), and the prefab needs a new child, which per the parent
  `CLAUDE.md` means an Editor session rather than hand-written YAML. The side
  it sits on hangs off the same `rightAligned` decision `ApplyPosition`
  already makes.

  Worth knowing before starting: CK's marker is **4 × 4 px**
  (`Assets/Sprite/player_marker.asset` in the AssetRipper dump, atlas
  `Texture2D/sactx-0-256x128-Uncompressed-ui-ed19f136.png`), one white ring
  around a 2 × 2 colour core, and its corner pixels are **opaque map-background
  blue, not transparent** — fine on the map, wrong over gameplay. So it is a
  reference for the shape, and genuinely not a sprite to reuse.

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
`unity/PlayerCoordinatesHud/Editor/logo.png`; the real mod ID lands in
`unity/PlayerCoordinatesHud/Editor/PlayerCoordinatesHud_modio.asset` (currently
`modId: 0`, unpublished). Set the mod.io profile type tag to **`Script`** (an
`Asset` tag silently disables the mod's scripts).

## Conventions

- Commit messages: Conventional Commits (`type(scope): subject`), imperative,
  no emoji.
- Documentation files (`CLAUDE.md`, `README.md`, `docs/`) are English; chat
  answers are German.
- Prefer `git commit --amend` / `git reset --soft` over fix-up commits on a
  personal branch, and `git rebase` over `git merge`.
