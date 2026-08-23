using ModSettingsMenu.Settings;
using PugMod;
using Unity.Mathematics;
using UnityEngine;

namespace PlayerCoordinatesHud
{
    /// <summary>
    /// Mod bootstrap. Pugstorm's loader instantiates this on game start and calls the IMod lifecycle
    /// methods. Registers the Mod settings section, captures the HUD prefab by GameObject name, and
    /// lazily instantiates it under the in-game HUD root once the UIManager hierarchy exists — the
    /// same pattern both sibling HUD mods use.
    /// </summary>
    public sealed class PlayerCoordinatesHudMod : IMod
    {
        // Captured in ModObjectLoaded, instantiated in Update.
        private static GameObject _hudPrefab;

        // The instantiated HUD GameObject. Gating re-instantiation on this (NOT on
        // CoordinatesHud.Instance) matters: Instance is only assigned by that component's own Awake,
        // and until Task 4's Editor wiring attaches CoordinatesHud to the prefab root, no Awake ever
        // fires — an Instance-based gate would then re-instantiate the prefab every single frame.
        private static GameObject _hudInstance;

        public void EarlyInit()
        {
            Debug.Log("[PlayerCoordinatesHud] EarlyInit");
        }

        public void Init()
        {
            // Section uses the default AsDeclared sort, so builder-call order IS render order.
            // No RequiresRestart on either setting: both are read live every frame by
            // CoordinatesHud (visibility in LateUpdate, placement in ApplyPosition), so a change
            // in the menu is visible behind it immediately.
            ModSettings
                .Section(this)
                .Hint("Show your position and distance from the Core on the HUD.")
                .Toggle(out var en, "enabled", true)
                .Choice(
                    out var position,
                    "position",
                    new[]
                    {
                        ModConfig.Position.BottomLeft,
                        ModConfig.Position.BottomRight,
                        ModConfig.Position.TopLeft,
                        ModConfig.Position.TopRight,
                        ModConfig.Position.BelowMinimap,
                    },
                    ModConfig.Position.BottomLeft
                )
                .Build();
            ModConfig.Instance.Bind(en, position);
            Debug.Log($"[PlayerCoordinatesHud] Settings bound. enabled={ModConfig.Instance.enabled}, position={ModConfig.Instance.position}");
        }

        public void ModObjectLoaded(Object obj)
        {
            // Name-based routing: this HUD is instantiated by our own lazy path below, NOT via
            // UserInterfaceModule.RegisterModUI (that is for modal windows).
            if (obj is GameObject go && go.name == "PlayerCoordinatesHUD")
                _hudPrefab = go;
        }

        public void Shutdown() { }

        public void Update()
        {
            if (_hudPrefab != null && _hudInstance == null && Manager.ui != null && Manager.ui.chestInventoryUI != null)
            {
                _hudInstance = Object.Instantiate(_hudPrefab, Manager.ui.chestInventoryUI.transform.parent);
                if (_hudInstance.GetComponent<CoordinatesHud>() == null)
                    Debug.LogError(
                        "[PlayerCoordinatesHud] PlayerCoordinatesHUD prefab root has no CoordinatesHud component attached — the HUD will not render. Wire it in the Unity Editor."
                    );
            }

            var hud = CoordinatesHud.Instance;
            if (hud == null)
                return;

            // Guarded here, not in the HUD: Update runs during load and on the main menu, where the
            // player does not exist, and the visibility gate lives in LateUpdate on another object.
            var pos = TryGetLocalPlayerPosition();
            if (!pos.HasValue)
                return;

            hud.Render(pos.Value);
        }

        /// <summary>
        /// The local player's world position, or null if no player exists (main menu, world-load
        /// screen, before PlayerController.OnOccupied has fired).
        /// </summary>
        private static float3? TryGetLocalPlayerPosition()
        {
            var p = Manager.main?.player;
            if (p == null)
                return null;
            var v = p.WorldPosition;
            return new float3(v.x, v.y, v.z);
        }
    }
}
