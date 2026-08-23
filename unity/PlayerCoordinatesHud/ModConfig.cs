using ModSettingsMenu.Settings;

namespace PlayerCoordinatesHud
{
    /// <summary>
    /// Settings adapter. Two player-facing knobs — the <c>enabled</c> master toggle and the
    /// <c>position</c> choice — are live in-game settings from the Mod Settings Menu framework,
    /// bound once in <see cref="PlayerCoordinatesHudMod.Init"/>. The singleton shape mirrors the
    /// sibling mods so consumers read <c>ModConfig.Instance.enabled</c>.
    /// </summary>
    internal sealed class ModConfig
    {
        /// <summary>
        /// Where the readout sits. The four corners are fixed anchor points calibrated against the
        /// uiCamera's constant view area; <see cref="BelowMinimap"/> is the only value that moves at
        /// runtime, tracking CK's own minimap and falling back to <see cref="TopRight"/> whenever the
        /// minimap is off screen.
        ///
        /// <para>The member names are load-bearing twice over: <c>Choice</c> persists a setting as its
        /// token (<c>value.ToString()</c>) and looks the label up as
        /// <c>PlayerCoordinatesHud-Config/position/&lt;name&gt;</c>. Renaming one therefore silently
        /// resets every player who chose it back to the default AND drops its localization.</para>
        /// </summary>
        public enum Position
        {
            /// <summary>The 1.0.0 position, kept as the default so an existing install does not move.</summary>
            BottomLeft,

            BottomRight,

            TopLeft,

            TopRight,

            /// <summary>Hangs below CK's minimap, following it as it is scaled or moved. Whenever the
            /// minimap is not on screen — switched off in the options, replaced by the big map, or
            /// hidden behind an open inventory — the readout uses <see cref="TopRight"/> instead.</summary>
            BelowMinimap,
        }

        // Null only in the brief pre-Bind window at mod load, where the defaults below apply.
        private SettingHandle<bool> _enabledHandle;
        private SettingHandle<Position> _positionHandle;

        public void Bind(SettingHandle<bool> enabled, SettingHandle<Position> position)
        {
            _enabledHandle = enabled;
            _positionHandle = position;
        }

        // Master switch (default true). When false the readout is hidden.
        public bool enabled => _enabledHandle != null ? _enabledHandle.Value : true;

        // Where the readout is drawn (default BottomLeft, the 1.0.0 behaviour).
        public Position position => _positionHandle != null ? _positionHandle.Value : Position.BottomLeft;

        private static readonly ModConfig _instance = new ModConfig();
        public static ModConfig Instance => _instance;
    }
}
