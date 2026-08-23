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

        // Corner anchors, in the same local space the prefab's own (-13.5, -7.5) sits in — fixed
        // values, exactly as CK positions its own HUD. BottomLeft is the shipped 1.0.0 value and is
        // confirmed correct in game; the other three mirror it and still need calibration.
        private static readonly Vector2 AnchorBottomLeft = new Vector2(-13.5f, -7.5f);
        private static readonly Vector2 AnchorBottomRight = new Vector2(13.5f, -7.5f);

        // The top row sits at 7.925, not the mirrored 7.5, so the text lines up with ItemChecklist's
        // HUD row. Container heights are NOT the thing to match: ICL's container sits at 7.8 with its
        // CounterText 0.0625 below it (world 7.7375), while this prefab's Coordinates child hangs
        // 0.1875 below its root — so equal text height means 7.7375 + 0.1875.
        private static readonly Vector2 AnchorTopLeft = new Vector2(-13.5f, 7.925f);
        private static readonly Vector2 AnchorTopRight = new Vector2(13.5f, 7.925f);

        // Clearance between the minimap's bottom edge and the readout's centre line. The PugTexts are
        // verticalAlignment: center, so this absorbs half the text height as well as the visual gap.
        private const float MinimapGap = 0.5f;

        // The same clearance above CK's on-screen button hints, for the bottom-right anchor.
        private const float HintsGap = 0.5f;

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
        /// The anchor directly below CK's minimap, converted into this HUD's own local space; false
        /// when the minimap is not on screen.
        /// </summary>
        private bool TryGetMinimapAnchor(out Vector2 anchor)
        {
            anchor = default;

            var mapUI = Manager.ui != null ? Manager.ui.mapUI : null;
            var border = mapUI != null ? mapUI.miniMapBorder : null;

            // activeInHierarchy is the single signal CK itself uses here — PvPTextUI, the game's own
            // element below the minimap, tests exactly this. MapUI clears it in all three cases at
            // once: the player switched the minimap off, the big map replaced it, or an inventory is open.
            if (border == null || !border.gameObject.activeInHierarchy)
                return false;

            // World-space bounds, so the minimap's own runtime scaling (MapUI.UpdateUIScaling rescales
            // it every frame from CalcGameplayUITargetScaleMultiplier) is already accounted for.
            var bounds = border.bounds;

            // A renderer can be active and still have nothing to measure — no sprite assigned yet, or
            // scaled to nothing by UpdateUIScaling. Its bounds then collapse to a point at the origin,
            // and using that would drop the readout into the middle of the screen instead of a corner.
            if (bounds.size.x < 0.01f || bounds.size.y < 0.01f)
                return false;

            // Only the height comes from the minimap; x is the shared right edge, so leaving this
            // position for the top-right fallback is a purely vertical move.
            anchor = new Vector2(RightEdge(), ToLocal(new Vector2(0f, bounds.min.y - MinimapGap)).y);
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

            // Only the height comes from the hints; x is the shared right edge, so the readout does
            // not drift sideways as buttons appear and disappear.
            anchor = new Vector2(RightEdge(), ToLocal(new Vector2(0f, bounds.max.y + HintsGap)).y);
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

        private Vector2 CornerAnchor(ModConfig.Position position)
        {
            switch (position)
            {
                case ModConfig.Position.BottomRight:
                    return new Vector2(RightEdge(), AnchorBottomRight.y);
                case ModConfig.Position.TopLeft:
                    return AnchorTopLeft;
                case ModConfig.Position.TopRight:
                    return new Vector2(RightEdge(), AnchorTopRight.y);
                default:
                    return AnchorBottomLeft;
            }
        }

        /// <summary>
        /// The x every right-hand position shares: the right edge of CK's minimap, which is the
        /// game's own established right-hand UI edge.
        ///
        /// <para>Without this the positions disagree — "below minimap" would take its x from the
        /// minimap while the corners took a mirrored constant, so leaving the minimap position (or
        /// switching the minimap off) shifted the readout sideways instead of just moving it up.</para>
        ///
        /// <para>Read from the renderer's serialized <c>size</c> and its transform rather than from
        /// <c>bounds</c>, so the edge is still known while the minimap is hidden — that is exactly
        /// when the fallback needs it. Falls back to the mirrored corner value if the minimap is
        /// unavailable or reports nothing measurable.</para>
        /// </summary>
        private float RightEdge()
        {
            var mapUI = Manager.ui != null ? Manager.ui.mapUI : null;
            var border = mapUI != null ? mapUI.miniMapBorder : null;
            if (border == null)
                return AnchorBottomRight.x;

            float halfWidth = border.size.x * 0.5f * Mathf.Abs(border.transform.lossyScale.x);
            if (halfWidth < 0.01f)
                return AnchorBottomRight.x;

            return ToLocal(new Vector2(border.transform.position.x + halfWidth, 0f)).x;
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
