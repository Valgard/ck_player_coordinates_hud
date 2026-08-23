using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace PlayerCoordinatesHud
{
    /// <summary>
    /// Always-on HUD readout showing the player's floored world coordinates and the straight-line
    /// distance to the world origin (The Core), in CK's own map format: <c>123, -456 (478)</c>.
    /// Its placement is a player setting — four fixed corners plus one that follows CK's minimap;
    /// see <see cref="ApplyPosition"/>.
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

        public static CoordinatesHud Instance { get; private set; }

        // The last string painted. PugText.Render rebuilds the glyph SpriteRenderers, and this HUD
        // runs permanently (unlike CK's original, which only renders while the map is open), so a
        // per-frame unconditional Render would churn constantly. null = nothing painted yet.
        private string _lastRendered;

        // The edges every position is built from. Fixed values, the way CK places its own HUD: the
        // uiCamera shows a constant world area and the game offers no UI-scale option, so these do
        // not move at runtime. Each was read off the vanilla prefab rather than chosen.
        //
        // They must stay in step with THIS prefab too: ApplyPosition writes the root's position every
        // frame, so changing the root in the Editor alone has no effect — and the symmetric 7.8 only
        // works because the Coordinates child hangs 0.0625 below the root, the same depth
        // ItemChecklist's CounterText hangs below its container. Move that child and every position
        // moves with it.
        //
        //   left    HealthBarBackground sits at -10.9375 and is 6.375 wide -> its left edge, so the
        //           readout lines up with the status bars below it.
        //   right   the minimap's right edge is 14.5 (centre 12.5, width 4); one pixel inside it is
        //           where CK puts its own text against that frame (PvPEnabledUI at 14.4375).
        //   bottom  the minimap's bottom edge is 4.9375 (centre 6.0625, height 2.25).
        private const float LeftEdgeX = -14.125f;
        private const float RightEdgeX = 14.4375f;
        private const float TopY = 7.8f;
        private const float BottomY = -7.8f;
        private const float MinimapBottomY = 4.9375f;

        // Clearance between a piece of UI and the nearest edge of the readout — one pixel, which is
        // what CK itself leaves: its PvPEnabledUI sits at y 4.875 against a minimap bottom edge of
        // 4.9375. Half the text height is subtracted separately (see TextCentreDrop), so this stays
        // a pure gap rather than a value silently carrying the font size around with it.
        private const float UIGap = 0.0625f;

        // Stands in for half the text height until the first Render has measured one (CK's UI font
        // is 8px tall, i.e. 0.5 world units at 16 px/unit).
        private const float FallbackHalfTextHeight = 0.25f;

        // CK exposes no manager field for the button hints, so the component is looked up once and
        // cached. The flag separates "not looked up yet" from "looked up, genuinely not there".
        private InGameButtonHintsUI _buttonHints;
        private bool _buttonHintsSearched;

        // Reused across frames so measuring the hints allocates nothing after the first pass.
        private readonly List<Renderer> _boundsScratch = new List<Renderer>();

        // The prefab's own Z, captured before anything moves the root. Every anchor keeps it: the
        // minimap anchor comes from world-space bounds, whose Z belongs to the map, not to this HUD.
        private float _anchorZ;

        // Applied placement, so the per-frame pass only writes when something actually changed.
        // _alignmentApplied starts false to force one initial pass: the prefab ships left-aligned,
        // but the persisted setting may well be a right-hand corner.
        private bool _appliedRightAligned;
        private bool _alignmentApplied;

        protected void Awake()
        {
            Instance = this;
            _anchorZ = transform.localPosition.z;

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
                // Explicit visibility via proven signals. Manager.ui.CalcGameplayUITargetScaleMultiplier()
                // — CK's own HUD idiom — returns (0,0,0) for a mod HUD and is deliberately not used.
                bool show = WorldState.IsInPlayableWorld && !Manager.ui.isAnyInventoryShowing && !Manager.menu.IsAnyMenuActive() && ModConfig.Instance.enabled;
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
        /// Moves the root to the configured anchor and matches the text alignment to it. Runs every
        /// frame while visible, but only writes when the resulting placement actually changed.
        /// </summary>
        private void ApplyPosition()
        {
            var configured = ModConfig.Instance.position;

            Vector2 anchor;
            bool rightAligned;
            if (configured == ModConfig.Position.BelowMinimap && TryGetMinimapAnchor(out anchor))
            {
                // Right-aligned so the readout's right edge lines up with the minimap's — and so the
                // fallback below is a purely vertical move rather than a re-flow of the text.
                rightAligned = true;
            }
            else if (configured == ModConfig.Position.BottomRight && TryGetButtonHintsAnchor(out anchor))
            {
                rightAligned = true;
            }
            else
            {
                // BelowMinimap with no minimap on screen falls back to the top-right corner.
                var effective = configured == ModConfig.Position.BelowMinimap ? ModConfig.Position.TopRight : configured;
                anchor = CornerAnchor(effective);
                rightAligned = effective == ModConfig.Position.BottomRight || effective == ModConfig.Position.TopRight;
            }

            var target = new Vector3(anchor.x, anchor.y, _anchorZ);
            if (transform.localPosition != target)
                transform.localPosition = target;

            if (!_alignmentApplied || _appliedRightAligned != rightAligned)
            {
                SetAlignment(rightAligned);
                _appliedRightAligned = rightAligned;
                _alignmentApplied = true;
                RepaintForNewAlignment();
            }
        }

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

            var mapUI = Manager.ui != null ? Manager.ui.mapUI : null;
            var border = mapUI != null ? mapUI.miniMapBorder : null;

            // activeInHierarchy is the single signal CK itself uses here — PvPTextUI tests exactly
            // this. MapUI clears it in all three cases at once: the player switched the minimap off,
            // the big map replaced it, or an inventory is open.
            if (border == null || !border.gameObject.activeInHierarchy)
                return false;

            // x is the shared right edge, so leaving this position for the top-right fallback is a
            // purely vertical move.
            anchor = new Vector2(RightEdgeX, MinimapBottomY - UIGap - TextCentreDrop());
            return true;
        }

        /// <summary>
        /// The anchor just above CK's on-screen button hints, keeping the bottom-right corner's x;
        /// false when the hints are not drawn, in which case the plain corner anchor applies.
        ///
        /// <para>They cannot be treated as a fixed block: <c>InGameButtonHintsUI</c> lays out only the
        /// buttons that apply right now, right-to-left across several rows, and the whole container
        /// follows the player's own "show key hints" option. So the readout measures what is actually
        /// on screen and sits above that, dropping into the true corner when nothing is shown.</para>
        /// </summary>
        private bool TryGetButtonHintsAnchor(out Vector2 anchor)
        {
            anchor = default;

            if (!_buttonHintsSearched)
            {
                _buttonHints = FindFirstObjectByType<InGameButtonHintsUI>();
                _buttonHintsSearched = true;
            }

            var container = _buttonHints != null ? _buttonHints.container : null;
            if (container == null || !container.activeInHierarchy || !TryGetDrawnBounds(container, out var bounds))
                return false;

            // Only the height comes from the hints — and it is the one edge in this file that genuinely
            // has to be measured, because the hints grow and shrink while you play. x stays the shared
            // right edge so the readout does not drift sideways as buttons appear and disappear, and
            // the gap matches the minimap's so both dynamic positions keep the same visual distance.
            anchor = new Vector2(RightEdgeX, ToLocal(new Vector2(0f, bounds.max.y + UIGap + TextCentreDrop())).y);
            return true;
        }

        /// <summary>
        /// Combined world bounds of everything currently drawn under <paramref name="root"/>, or false
        /// if nothing is. <c>Renderer.bounds</c> already accounts for scaling, and disabled renderers
        /// are skipped, so this measures what the player sees rather than the container's nominal size.
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

        private static Vector2 CornerAnchor(ModConfig.Position position)
        {
            switch (position)
            {
                case ModConfig.Position.BottomRight:
                    return new Vector2(RightEdgeX, BottomY);
                case ModConfig.Position.TopLeft:
                    return new Vector2(LeftEdgeX, TopY);
                case ModConfig.Position.TopRight:
                    return new Vector2(RightEdgeX, TopY);
                default:
                    return new Vector2(LeftEdgeX, BottomY);
            }
        }

        /// <summary>
        /// How far below a top edge this readout's centre line has to sit for its own top edge to
        /// land there — copied from CK's own <c>PvPTextUI</c>, the vanilla element below the minimap:
        /// <c>height / 2 - height % 0.0625</c>.
        ///
        /// <para>The modulo is not noise: it drops the sub-pixel remainder so the glyphs keep landing
        /// on CK's 1/16-unit pixel grid. Half a text height alone would park them between pixels,
        /// which is where point-filtered sprites go blurry.</para>
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
