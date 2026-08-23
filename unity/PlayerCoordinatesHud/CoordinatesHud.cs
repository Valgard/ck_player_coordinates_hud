using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace PlayerCoordinatesHud
{
    /// <summary>
    /// Always-on HUD readout showing the player's floored world coordinates and the straight-line
    /// distance to the world origin (The Core), in CK's own map format: <c>123, -456 (478)</c>.
    /// Its placement is a player setting: four corners plus a spot below the minimap. All anchors are
    /// constants; what varies at runtime is which of them applies — the below-minimap spot steps aside
    /// to the top-right corner while the minimap is not drawn, it steps below CK's PvP label when a
    /// world has PvP enabled, and the bottom-right corner rides above CK's button hints, whose
    /// occupied height depends on which of them apply. See <see cref="ApplyPosition"/>.
    ///
    /// <para>This is NOT a modal CoreLib UI — it is instantiated directly by
    /// <see cref="PlayerCoordinatesHudMod"/> under the in-game HUD root and must never be passed to
    /// <c>UserInterfaceModule.RegisterModUI</c> (that path hides the UI on
    /// <c>HideAllInventoryAndCraftingUI</c>, the opposite of always-on).</para>
    ///
    /// <para><strong>Topology matters:</strong> this component sits on the ALWAYS-ACTIVE prefab root
    /// and toggles the separate <see cref="hudRoot"/> child. Toggling its own GameObject would stop
    /// its <c>LateUpdate</c> from running, so it could never turn the readout back on — hide once,
    /// gone for the session.</para>
    /// </summary>
    public class CoordinatesHud : UIelement
    {
        // Editor-wired in PlayerCoordinatesHUD.prefab.
        public GameObject hudRoot; // the toggled container (NOT this component's own GameObject)
        public PugText coordinateText; // white foreground text
        public PugText coordinateTextOutline; // black drop-shadow text, 1px down-right, drawn behind
        public SpriteRenderer icon; // the position marker, drawn left of the text

        public static CoordinatesHud Instance { get; private set; }

        // The last string painted. PugText.Render rebuilds the glyph SpriteRenderers, and this HUD
        // runs permanently (unlike CK's original, which only renders while the map is open), so a
        // per-frame unconditional Render would churn constantly. null = nothing painted yet.
        private string _lastRendered;

        // The edges every position is built from. Fixed values, the way CK places its own HUD, and it
        // stays correct on every display: the uiCamera is not screen-driven at all. Its PugCamera runs
        // OutputMode.Fixed at 480x270, and PugRP forces the camera aspect from those numbers rather
        // than from the window, so the visible area is always exactly +-15 x +-8.4375 world units.
        //
        // The three vanilla edges were read off the vanilla prefab; the row height was not:
        //
        //   left    HealthBarBackground sits at -10.9375 and is 6.375 wide -> its left edge -14.125.
        //   right   the minimap's right edge is 14.5 (centre 12.5, width 4); one pixel inside it is
        //           where CK puts its own text against that frame (PvPEnabledUI at 14.4375).
        //   bottom  the minimap's bottom edge is 4.9375 (centre 6.0625, height 2.25).
        //   rows    NOT vanilla values. TopY tracks ItemChecklist's HUD row (its hudRoot sits at
        //           (10, 7.8)) so a top corner lines up with the mod-HUD row already there — nudged
        //           to 7.8125 because 7.8 is 124.8 px, i.e. off CK's 1/16 pixel grid. The 0.2 px
        //           that buys is invisible; landing between pixels is not, for point-filtered text.
        //           BottomY does NOT mirror it: the text always hangs RowDrop below its anchor, so a
        //           mirrored anchor would leave the bottom margin 2 px tighter than the top. -7.6875
        //           equalises them at 6 px, and puts both anchors AND both text centres on the grid
        //           (125 / -123 and 124 / -124 px). The 1.0.0 prefab used -7.5.
        private const float LeftEdgeX = -14.125f;
        private const float RightEdgeX = 14.4375f;
        private const float TopY = 7.8125f;
        private const float BottomY = -7.6875f;
        private const float MinimapBottomY = 4.9375f;

        // Clearance between a piece of UI and the nearest edge of the readout — one pixel, which is
        // what CK itself leaves: its PvPEnabledUI sits at y 4.875 against a minimap bottom edge of
        // 4.9375. Half the text height is subtracted separately (see TextCentreDrop), so this stays
        // a pure gap rather than a value silently carrying the font size around with it.
        private const float UIGap = 0.0625f;

        // Stands in for half the text height until the first Render has measured one. The prefab uses
        // fontFace thinTiny (Font5), whose charDims.y is 10 -> 10/16 = 0.625 tall, so half is 0.3125.
        private const float FallbackHalfTextHeight = 0.3125f;

        // Fallback for the Coordinates child's depth below the root, used only if that text is not
        // wired; the live value is read from the transform so it cannot drift from the prefab.
        private const float FallbackRowDrop = 0.0625f;

        // Space between the icon's right edge and where the text starts — two pixels. Narrower than
        // ItemChecklist's 6.6 px, which reads as generous next to a lone counter but would pull a
        // three-part readout apart. Its own width is measured off the sprite, not stated here.
        private const float IconGap = 0.125f;

        // Stands in for the sprite's width while none is assigned. 6 px at the sheet's 16 pixels per
        // unit, i.e. the marker this mod ships.
        private const float FallbackIconWidth = 0.375f;

        // Nudge that keeps the sprite OFF the 1/16 grid. Both placements land exactly on it otherwise
        // — half of a 6 px icon is 3/16, and a glyph-measured text edge is a whole number of pixels —
        // and a small point-filtered sprite sitting precisely on a texel boundary renders distorted,
        // because the rasteriser's rounding is ambiguous there and kicks one pixel row into the next
        // cell. At 0.08 px the shift is invisible; the distortion it avoids is not. Documented in
        // docs/ck/prefabs-and-rendering.md, found on ItemChecklist's 5x5 clear button.
        private const float IconOffGrid = 0.005f;

        // CK exposes no manager field for the button hints, so the component is looked up once and
        // cached. The flag is only set on a HIT: FindFirstObjectByType excludes inactive objects, so
        // latching a miss would disable the bottom-right dodge for the rest of the session.
        private InGameButtonHintsUI _buttonHints;
        private bool _buttonHintsSearched;

        // Same treatment for CK's PvP label, which shares the spot below the minimap whenever the
        // world has PvP enabled (a pause-menu switch, so it can appear mid-session).
        private PvPTextUI _pvpText;
        private bool _pvpTextSearched;

        // Reused across frames so measuring the hints allocates nothing after the first pass. Keep the
        // field typed as List<Renderer>: the foreach below then uses List's struct enumerator, while an
        // IList<Renderer> would box one per frame.
        private readonly List<Renderer> _boundsScratch = new List<Renderer>();

        // The prefab's own Z, captured before anything moves the root. Every anchor keeps it: anchors
        // are 2-D and say nothing about depth, so the prefab is the only source for it.
        private float _anchorZ;

        // Applied alignment, so the per-frame pass only repaints when it actually changed. null = never
        // applied, which forces one initial pass: the prefab ships left-aligned, but the persisted
        // setting may well be a right-hand corner.
        private bool? _appliedRightAligned;

        // The prefab's own x for both text rows, captured before anything shifts them. Every shift is
        // written as base + offset rather than added to the current value, so the per-frame pass
        // cannot accumulate; and the outline keeps its 1 px lead over the text without that offset
        // being restated here.
        private float _textBaseX;
        private float _outlineBaseX;

        // One-shot diagnostics. These conditions are structural — a serialized field that is null once
        // is null for the session — so a plain bool is enough and the frame path stays log-free after
        // the first occurrence.
        private bool _loggedMissingMapUI;
        private bool _loggedMissingHints;
        private bool _loggedUnknownPosition;
        private bool _loggedMissingIconSprite;

        protected void Awake()
        {
            Instance = this;
            _anchorZ = transform.localPosition.z;

            // Read before the first ApplyPosition, which is what moves these.
            if (coordinateText != null)
                _textBaseX = coordinateText.transform.localPosition.x;
            if (coordinateTextOutline != null)
                _outlineBaseX = coordinateTextOutline.transform.localPosition.x;

            // One-shot: this only ever fires once for the lifetime of the mod's HUD instance (the
            // caller in PlayerCoordinatesHudMod never re-instantiates the prefab). Without it, a
            // missed Editor wiring (Task 4) silently degrades to "loads, does nothing" — indistinguishable
            // from the visibility gate legitimately deciding to hide the HUD.
            string missing = "";
            if (hudRoot == null)
                missing += "hudRoot";
            if (coordinateText == null)
                missing += (missing.Length > 0 ? ", " : "") + "coordinateText";
            if (coordinateTextOutline == null)
                missing += (missing.Length > 0 ? ", " : "") + "coordinateTextOutline";
            if (icon == null)
                missing += (missing.Length > 0 ? ", " : "") + "icon";
            if (missing.Length > 0)
                Debug.LogError(
                    $"[PlayerCoordinatesHud] CoordinatesHud is missing serialized field(s): {missing}. Wire them on the PlayerCoordinatesHUD prefab in the Unity Editor."
                );
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        protected override void LateUpdate()
        {
            if (hudRoot != null)
            {
                // Explicit visibility via proven signals, rather than CK's own HUD idiom
                // Manager.ui.CalcGameplayUITargetScaleMultiplier(): that is a global scale, not a
                // per-element one, and it collapses to (0,0,0) for several unrelated reasons at once
                // (hidden UI, fades, load screens) — WorldState already covers the latter properly.
                //
                // hideInGameUI is in the list for a reason, though: it is not a niche setting but a
                // regular keybind (PlayerInput.InputType.TOGGLE_UI). Without this term the readout
                // stays on an otherwise empty screen — and BelowMinimap additionally jumps to the
                // top-right corner, because CK deactivates the minimap along with everything else.
                bool show =
                    WorldState.IsInPlayableWorld
                    && !Manager.prefs.hideInGameUI
                    && !Manager.ui.isAnyInventoryShowing
                    && !Manager.menu.IsAnyMenuActive()
                    && ModConfig.Instance.enabled;
                if (hudRoot.activeSelf != show)
                    hudRoot.SetActive(show);

                // Placed only while visible: BelowMinimap has to follow the minimap every frame, and
                // tracking it behind a hidden HUD would be pure waste.
                if (show)
                    ApplyPosition();
            }
            base.LateUpdate();
        }

        /// <summary>
        /// Moves the root to the configured anchor, matches the text alignment to it, and lays out the
        /// icon and text within the row. Runs every frame while visible, but only writes when the
        /// resulting placement actually changed.
        /// </summary>
        private void ApplyPosition()
        {
            var configured = ModConfig.Instance.position;
            var corner = EffectiveCorner(configured);

            // Two positions dodge UI that comes and goes; when it is not there, they simply ARE their
            // corner. The other three are their corner, always.
            if (!TryGetDodgingAnchor(configured, out var anchor))
                anchor = CornerAnchor(corner);

            // Alignment follows the corner the position belongs to — and deliberately not whether the
            // dodge succeeded: both dodging positions are right-hand ones, so a dodge that fails is a
            // purely vertical move rather than a re-flow of the text.
            bool rightAligned = IsRightAligned(corner);

            var target = new Vector3(anchor.x, anchor.y, _anchorZ);
            if (transform.localPosition != target)
                transform.localPosition = target;

            if (_appliedRightAligned != rightAligned)
            {
                SetAlignment(rightAligned);
                _appliedRightAligned = rightAligned;
                RepaintForNewAlignment();
            }

            // After the repaint above, never before it: the icon is placed against the text's measured
            // left edge, and an alignment change moves that edge from one side of the row to the other.
            ApplyIcon(rightAligned);
        }

        /// <summary>
        /// Places the marker left of the coordinates and shifts the text clear of it.
        ///
        /// <para><strong>Always left, on both sides of the screen</strong> — the icon leads the value
        /// the way ItemChecklist's does, rather than mirroring with the corner. What differs between
        /// the corners is which of the two moves: a left-hand corner anchors the icon and pushes the
        /// text right by its width, a right-hand one leaves the text ending at the anchor and hangs
        /// the icon off its left edge. Either way the pair is flush with the anchor, and turning the
        /// icon off gives the width straight back.</para>
        ///
        /// <para><strong>That left edge is measured, not computed.</strong> <c>PugText.dimensions</c>
        /// is a Rect the engine fills when it draws, and it lays out <c>xMin</c> per alignment — 0 for
        /// left-aligned text, minus the drawn width for right-aligned. So one expression covers both
        /// corners, and neither needs this mod to know how wide a glyph is. CK positions its own UI
        /// off the same field (<c>Pug.Other:343386</c>).</para>
        ///
        /// <para>The consequence to expect in a right-hand corner: that edge moves as the coordinates
        /// grow a digit, and the icon moves with it. The alternative — pinning the icon to the outside
        /// — would hold still but put it behind the value it labels.</para>
        /// </summary>
        private void ApplyIcon(bool rightAligned)
        {
            if (icon == null)
                return;

            bool wanted = ModConfig.Instance.showIcon;
            if (icon.enabled != wanted)
                icon.enabled = wanted;

            SetTextShift(rightAligned || !wanted ? 0f : IconWidth() + IconGap);
            if (!wanted)
                return;

            // Reads the edge the line above just moved, which is why both corners resolve through one
            // expression. On the very first frame a freshly shown HUD has nothing drawn yet, so the
            // Rect is empty and this lands as though the text were zero-wide; Render runs in Update,
            // ahead of this LateUpdate, so it is right from the following frame on.
            float centre = TextLeftEdge() - IconGap - IconWidth() * 0.5f + IconOffGrid;

            // x only. Where the marker sits within the row is prefab geometry, calibrated by eye
            // against the text like the row's own offsets, and nothing here knows better than that.
            var current = icon.transform.localPosition;
            var target = new Vector3(centre, current.y, current.z);
            if (current != target)
                icon.transform.localPosition = target;
        }

        /// <summary>
        /// Moves both text rows sideways by <paramref name="shift"/>, measured from the position the
        /// prefab gave them. Writing base + shift rather than adding keeps the outline's 1 px lead
        /// intact without restating it, and makes the per-frame call idempotent.
        /// </summary>
        private void SetTextShift(float shift)
        {
            SetLocalX(coordinateText, _textBaseX + shift);
            SetLocalX(coordinateTextOutline, _outlineBaseX + shift);
        }

        private static void SetLocalX(PugText text, float x)
        {
            if (text == null)
                return;
            var current = text.transform.localPosition;
            var target = new Vector3(x, current.y, current.z);
            if (current != target)
                text.transform.localPosition = target;
        }

        /// <summary>
        /// The left edge of the drawn text, in the row's own space. Zero while nothing is wired, which
        /// puts the icon at the anchor — the same place the prefab already has it.
        /// </summary>
        private float TextLeftEdge() => coordinateText != null ? coordinateText.transform.localPosition.x + coordinateText.dimensions.xMin : 0f;

        /// <summary>
        /// The marker's width in world units, off the sprite itself, so re-cutting the sheet at another
        /// size needs no change here.
        /// </summary>
        private float IconWidth()
        {
            var sprite = icon != null ? icon.sprite : null;
            if (sprite != null)
                return sprite.bounds.size.x;

            // Structural, so once is enough: a SpriteRenderer with no sprite draws nothing, and the
            // gap it leaves in the row would otherwise be the only hint that the wiring is incomplete.
            if (!_loggedMissingIconSprite)
            {
                _loggedMissingIconSprite = true;
                Debug.LogWarning(
                    "[PlayerCoordinatesHud] The icon SpriteRenderer has no sprite assigned — nothing will be drawn and the text is spaced for a default-width marker. Assign Art/UI/player_position's 'Player' sprite in the Unity Editor."
                );
            }
            return FallbackIconWidth;
        }

        /// <summary>
        /// The corner a position belongs to. <c>BelowMinimap</c> is the only value that is not itself a
        /// corner; naming its fallback here keeps that mapping in one place instead of inline at each
        /// use, so a position added later is handled by editing one expression.
        /// </summary>
        private static ModConfig.Position EffectiveCorner(ModConfig.Position configured) =>
            configured == ModConfig.Position.BelowMinimap ? ModConfig.Position.TopRight : configured;

        /// <summary>
        /// Whether a corner grows its text leftwards. Derived from the corner rather than tracked, so
        /// it cannot disagree with the anchor it belongs to.
        /// </summary>
        private static bool IsRightAligned(ModConfig.Position corner) => corner == ModConfig.Position.BottomRight || corner == ModConfig.Position.TopRight;

        /// <summary>
        /// The anchor for a position that gets out of the way of other UI, or false when that UI is
        /// not on screen — in which case the plain corner applies. Only two positions dodge anything;
        /// every other value returns false here without asking.
        /// </summary>
        private bool TryGetDodgingAnchor(ModConfig.Position configured, out Vector2 anchor)
        {
            switch (configured)
            {
                case ModConfig.Position.BelowMinimap:
                    return TryGetMinimapAnchor(out anchor);
                case ModConfig.Position.BottomRight:
                    return TryGetButtonHintsAnchor(out anchor);
                default:
                    anchor = default;
                    return false;
            }
        }

        /// <summary>
        /// How far the visible text hangs below the root, read from the prefab rather than assumed.
        /// The anchor formulas describe where the TEXT should sit, but what gets written is the root —
        /// so without this the readout lands one pixel off every piece of UI it is measured against.
        /// </summary>
        private float RowDrop() => coordinateText != null ? -coordinateText.transform.localPosition.y : FallbackRowDrop;

        /// <summary>
        /// Redraws the current string after an alignment change.
        ///
        /// <para><strong>Why this needs <c>force</c>.</strong> PugText keeps its own glyph cache, and
        /// <c>HasCorrectGlyphs</c> decides it is still valid by comparing language, the string, the
        /// format fields, <c>orderInLayer</c> and <c>maxWidth</c> — the alignment is <em>not</em> in
        /// that list. Alignment is baked into each glyph's local offset when the text is drawn, so
        /// with the string unchanged PugText keeps the old offsets and the readout stays laid out for
        /// the previous corner until the coordinates happen to change. <c>force: true</c> is CK's own
        /// escape hatch for exactly that, and it is why clearing this mod's <c>_lastRendered</c> gate
        /// alone is not enough: that only opens our cache, PugText's still blocks behind it.</para>
        /// </summary>
        private void RepaintForNewAlignment()
        {
            // Nothing painted yet: the first Render will already use the current alignment.
            if (_lastRendered == null)
                return;
            if (coordinateText != null)
                coordinateText.Render(_lastRendered, false, true);
            if (coordinateTextOutline != null)
                coordinateTextOutline.Render(_lastRendered, false, true);
        }

        /// <summary>
        /// The anchor directly below CK's minimap; false when the minimap is not on screen, which
        /// sends the readout to the top-right corner instead.
        ///
        /// <para>Structured the way CK's own <c>PvPTextUI</c> does it: the minimap's geometry is a
        /// constant, so only two things are actually asked at runtime — whether it is being drawn,
        /// and how tall the text is.</para>
        /// </summary>
        private bool TryGetMinimapAnchor(out Vector2 anchor)
        {
            anchor = default;

            var border = Manager.ui.mapUI != null ? Manager.ui.mapUI.miniMapBorder : null;
            if (border == null)
            {
                // Structural, not a normal state: both are serialized fields that vanilla always
                // wires. Reaching this means a game update moved them, and the readout would silently
                // sit in a corner forever without saying why.
                if (!_loggedMissingMapUI)
                {
                    _loggedMissingMapUI = true;
                    Debug.LogWarning("[PlayerCoordinatesHud] MapUI.miniMapBorder not found — the below-minimap position falls back to the top-right corner.");
                }
                return false;
            }

            // Beyond here it is ordinary: activeInHierarchy is the single signal covering all three
            // ways the minimap leaves the screen at once. MapUI.LateUpdate deactivates the border's
            // parent container when the player turns the minimap off (Manager.prefs.showMinimap) or
            // opens an inventory, and UpdateUIScaling deactivates the border itself for the big map.
            // CK's own PvPTextUI reads the same flag — to pick its offset, not its visibility.
            if (!border.gameObject.activeInHierarchy)
                return false;

            // Normally the minimap's bottom edge — but CK's own PvP label lives in exactly this spot
            // when the world has PvP enabled, at (14.4375, 4.875), which is where this readout would
            // otherwise sit. Verified in game: the two render on top of each other, both unreadable.
            // PvP is a pause-menu switch, so it can appear and vanish mid-session; measure rather
            // than decide once.
            float edge = MinimapBottomY;
            if (TryGetPvpLabelBottom(out var pvpBottom))
                edge = pvpBottom;

            // x is the shared right edge, so leaving this position for the top-right fallback is a
            // purely vertical move. RowDrop compensates that the ANCHOR moves the root while the
            // formula describes the text.
            anchor = new Vector2(RightEdgeX, edge - UIGap - TextCentreDrop() + RowDrop());
            return true;
        }

        /// <summary>
        /// The bottom edge of CK's PvP label in this HUD's local space, or false when it is not drawn
        /// — which is the usual case, since it only appears in a world with PvP switched on.
        ///
        /// <para>Measured rather than assumed for the same reason as the button hints: the label
        /// carries localised text, so its height is not ours to predict, and PvP is toggled from the
        /// pause menu at any time.</para>
        /// </summary>
        private bool TryGetPvpLabelBottom(out float bottomLocal)
        {
            bottomLocal = 0f;

            if (!_pvpTextSearched)
            {
                _pvpText = FindFirstObjectByType<PvPTextUI>();
                // Latch only a hit, as with the button hints: a miss may just mean the object was
                // inactive in that one frame, and latching it would lose the dodge for the session.
                if (_pvpText != null)
                    _pvpTextSearched = true;
            }

            var label = _pvpText != null ? _pvpText.text : null;
            if (label == null || !label.gameObject.activeInHierarchy)
                return false;
            if (!TryGetDrawnBounds(label.gameObject, out var bounds))
                return false;

            bottomLocal = ToLocal(new Vector2(0f, bounds.min.y)).y;
            return true;
        }

        /// <summary>
        /// The anchor just above CK's on-screen button hints, keeping the bottom-right corner's x;
        /// false when the hints are not drawn, in which case the plain corner anchor applies.
        ///
        /// <para>They cannot be treated as a fixed block, but not for the obvious reason. Buttons
        /// behave in two different ways: some appear and disappear with the context, while others
        /// stay put and merely switch between an active and an inactive look — the interact hand does
        /// the latter, so walking up to a chest changes its colour and adds its key label without
        /// moving the block's top edge at all. The container as a whole also follows the player's own
        /// "show key hints" option. So the readout measures what is actually drawn and sits above
        /// that, dropping into the true corner when nothing is shown.</para>
        ///
        /// <para><strong>One frame of lag is expected here and is not a bug.</strong> CK lays the
        /// buttons out in its own <c>LateUpdate</c>, and two <c>LateUpdate</c>s have no defined order
        /// without a script execution order entry — so when the set of hints changes, this may measure
        /// the previous frame's positions. It shows as the readout settling one frame late, never as a
        /// wrong resting position, and fixing it would mean an execution-order dependency on CK.</para>
        /// </summary>
        private bool TryGetButtonHintsAnchor(out Vector2 anchor)
        {
            anchor = default;

            if (!_buttonHintsSearched)
            {
                _buttonHints = FindFirstObjectByType<InGameButtonHintsUI>();

                // Only latch a HIT. The parameterless overload skips inactive objects, so a miss can
                // simply mean "asked one frame too early" — latching that would kill the dodge for the
                // whole session, silently, with the readout parked on top of the hints.
                if (_buttonHints != null)
                    _buttonHintsSearched = true;
                else if (!_loggedMissingHints)
                {
                    _loggedMissingHints = true;
                    Debug.LogWarning(
                        "[PlayerCoordinatesHud] InGameButtonHintsUI not found yet — retrying; the bottom-right position uses the plain corner until it is."
                    );
                }
            }

            var container = _buttonHints != null ? _buttonHints.container : null;
            if (container == null)
                return false;

            // From here the false cases are ordinary and deliberately quiet: the container is switched
            // by the player's own "show key hints" option every frame, and nothing drawn means nothing
            // to dodge — the plain corner is then the right answer.
            if (!container.activeInHierarchy || !TryGetDrawnBounds(container, out var bounds))
                return false;

            // Only the height comes from the hints — the one edge in this file that genuinely has to be
            // measured, because the hints grow and shrink while you play. x stays the shared right edge
            // so the readout does not drift sideways as buttons appear and disappear, and the gap
            // matches the minimap's so both dynamic positions keep the same distance from what they
            // dodge. Convert FIRST, then add: the gap and the drop are lengths of this local frame,
            // and adding them to a world value would let a scaled parent stretch them.
            anchor = new Vector2(RightEdgeX, ToLocal(new Vector2(0f, bounds.max.y)).y + UIGap + TextCentreDrop() + RowDrop());
            return true;
        }

        /// <summary>
        /// Combined world bounds of everything currently drawn under <paramref name="root"/>, or false
        /// if nothing is. Two filters apply: <c>GetComponentsInChildren(false, …)</c> already skips
        /// every renderer on an INACTIVE GameObject — which is the normal state for most hint buttons,
        /// since CK only lays out the ones that apply right now — and disabled renderers are dropped
        /// below. <c>Renderer.bounds</c> accounts for scaling, so this measures what the player sees
        /// rather than the container's nominal size (the container has no renderer of its own at all).
        /// </summary>
        private bool TryGetDrawnBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            root.GetComponentsInChildren(false, _boundsScratch);

            bool any = false;
            foreach (var renderer in _boundsScratch)
            {
                if (!renderer.enabled)
                    continue;
                if (any)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    any = true;
                }
            }
            return any;
        }

        private Vector2 CornerAnchor(ModConfig.Position position)
        {
            switch (position)
            {
                case ModConfig.Position.BottomRight:
                    return new Vector2(RightEdgeX, BottomY);
                case ModConfig.Position.TopLeft:
                    return new Vector2(LeftEdgeX, TopY);
                case ModConfig.Position.TopRight:
                    return new Vector2(RightEdgeX, TopY);
                case ModConfig.Position.BottomLeft:
                default:
                    // BottomLeft is listed explicitly so every member is visible here. A Position added
                    // later without its own case lands in this branch and gets the 1.0.0 corner, which
                    // is the safe answer — but it is reported once, because otherwise the omission only
                    // shows as "the new option does nothing". Guarded by a flag rather than logged
                    // outright: this runs every frame.
                    if (position != ModConfig.Position.BottomLeft && !_loggedUnknownPosition)
                    {
                        _loggedUnknownPosition = true;
                        Debug.LogError($"[PlayerCoordinatesHud] Position '{position}' has no anchor — falling back to BottomLeft. Add a case to CornerAnchor.");
                    }
                    return new Vector2(LeftEdgeX, BottomY);
            }
        }

        /// <summary>
        /// The distance from one of this readout's own edges to its centre line — copied from CK's
        /// <c>PvPTextUI</c>, the vanilla element below the minimap: <c>height / 2 - height % 0.0625</c>.
        ///
        /// <para>CK only ever subtracts it (its text hangs below the minimap), but the value is a
        /// half-height and therefore direction-free: subtract it to put the readout's TOP edge on a
        /// line, add it to put its BOTTOM edge there. Both uses are in this file — below the minimap
        /// and above the button hints — so the plus sign there is not a slip.</para>
        ///
        /// <para><strong>The modulo term is always zero — keep it anyway.</strong> It cannot be
        /// otherwise: <c>dimensions.height</c> is <c>charDims.y / pixelsPerUnit</c> with an integer
        /// pixel height over 16, so it is always a multiple of 0.0625. It is kept verbatim because
        /// that is the evidence this formula is Pugstorm's and not ours — worth more at the next game
        /// update than the line it costs. It does NOT snap anything to the pixel grid; that would be
        /// <c>RoundToPixelPerfectPosition.RoundFloat</c>, which CK deliberately does not call here.</para>
        ///
        /// <para>Measured from the live text rather than assumed, so a taller font or a language with
        /// different metrics still lines up. <c>dimensions</c> is only filled once something has been
        /// rendered, hence the fallback.</para>
        /// </summary>
        private float TextCentreDrop()
        {
            float height = coordinateText != null ? coordinateText.dimensions.height : 0f;
            if (height <= 0f)
                return FallbackHalfTextHeight;
            return height * 0.5f - height % 0.0625f;
        }

        /// <summary>
        /// World XY into this HUD's own local space, so an anchor stays correct even if the parent CK
        /// hands the HUD is moved or scaled.
        /// </summary>
        private Vector2 ToLocal(Vector2 world)
        {
            if (transform.parent == null)
                return world;
            var local = transform.parent.InverseTransformPoint(new Vector3(world.x, world.y, 0f));
            return new Vector2(local.x, local.y);
        }

        /// <summary>
        /// Right-aligns both PugTexts for the right-hand anchors. Without it the readout grows outwards
        /// from its anchor, so a long string ("1234, -5678 (9012)") would run off the screen edge
        /// instead of extending inwards.
        /// </summary>
        private void SetAlignment(bool rightAligned)
        {
            var alignment = rightAligned ? PugTextStyle.HorizontalAlignment.right : PugTextStyle.HorizontalAlignment.left;
            if (coordinateText != null)
                coordinateText.style.horizontalAlignment = alignment;
            if (coordinateTextOutline != null)
                coordinateTextOutline.style.horizontalAlignment = alignment;
        }

        /// <summary>
        /// Paint the readout for the given player world position. Called every frame from
        /// <see cref="PlayerCoordinatesHudMod.Update"/>, but only actually renders when the string
        /// changed (i.e. when the player crossed a tile boundary).
        /// </summary>
        public void Render(float3 playerWorldPos)
        {
            if (hudRoot == null || !hudRoot.activeSelf)
                return;

            // CK's world is the XZ plane — .y is height (~0) and is not a map axis.
            // floor, NOT an (int) cast: they differ for negative coordinates, which are ordinary
            // here (-14.3 floors to -15, casts to -14), and CK's own CoordinatesUI floors too.
            int x = (int)math.floor(playerWorldPos.x);
            int z = (int)math.floor(playerWorldPos.z);

            // Distance is computed from the FLOORED integers, exactly as CK does it, so the two
            // surfaces agree on the arithmetic. Formatted with "F0" rather than rounded to an int:
            // that is literally CK's own call, so there is no rounding-mode question to get wrong.
            float dist = math.sqrt((float)x * x + (float)z * z);
            string text = x + ", " + z + " (" + dist.ToString("F0") + ")";

            if (text == _lastRendered)
                return;

            // Only record the paint if something was actually painted — with both fields unwired
            // (Task 4 wiring missing), _lastRendered must stay null so the bookkeeping never claims
            // a paint that never happened.
            bool painted = false;
            if (coordinateText != null)
            {
                coordinateText.Render(text);
                painted = true;
            }
            if (coordinateTextOutline != null)
            {
                coordinateTextOutline.Render(text);
                painted = true;
            }
            if (painted)
                _lastRendered = text;
        }
    }
}
