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

        // Corner anchors, in the same local space the prefab's own (-13.5, -7.5) sits in. These are
        // fixed values, not computed ones: the uiCamera shows a constant world area (16.875 units
        // tall, 30 wide at 16:9), and CK has no aspect-dependent UI placement anywhere in its own
        // code, so the readout follows the game rather than second-guessing it.
        // BottomLeft is the shipped 1.0.0 value; the other three mirror it and are calibrated in game.
        private static readonly Vector2 AnchorBottomLeft = new Vector2(-13.5f, -7.5f);
        private static readonly Vector2 AnchorBottomRight = new Vector2(13.5f, -7.5f);
        private static readonly Vector2 AnchorTopLeft = new Vector2(-13.5f, 7.5f);
        private static readonly Vector2 AnchorTopRight = new Vector2(13.5f, 7.5f);

        // Clearance between the minimap's bottom edge and the readout's centre line. The PugTexts are
        // verticalAlignment: center, so this absorbs half the text height as well as the visual gap.
        private const float MinimapGap = 0.5f;

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

                // Force the change-gated Render to repaint. The string itself is unchanged, so without
                // this the new alignment would not show until the player next crossed a tile boundary.
                _lastRendered = null;
            }
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
            var world = new Vector3(bounds.max.x, bounds.min.y - MinimapGap, 0f);
            var local = transform.parent != null ? transform.parent.InverseTransformPoint(world) : world;
            anchor = new Vector2(local.x, local.y);
            return true;
        }

        private static Vector2 CornerAnchor(ModConfig.Position position)
        {
            switch (position)
            {
                case ModConfig.Position.BottomRight:
                    return AnchorBottomRight;
                case ModConfig.Position.TopLeft:
                    return AnchorTopLeft;
                case ModConfig.Position.TopRight:
                    return AnchorTopRight;
                default:
                    return AnchorBottomLeft;
            }
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
