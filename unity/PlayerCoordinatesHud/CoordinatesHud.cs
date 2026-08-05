using Unity.Mathematics;
using UnityEngine;

namespace PlayerCoordinatesHud
{
    /// <summary>
    /// Always-on HUD readout (bottom-left) showing the player's floored world coordinates and the
    /// straight-line distance to the world origin (The Core), in CK's own map format:
    /// <c>123, -456 (478)</c>.
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

        protected void Awake()
        {
            Instance = this;
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
            }
            base.LateUpdate();
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

            if (coordinateText != null)
                coordinateText.Render(text);
            if (coordinateTextOutline != null)
                coordinateTextOutline.Render(text);
            _lastRendered = text;
        }
    }
}
