using ModSettingsMenu.Settings;

namespace PlayerCoordinatesHud
{
    /// <summary>
    /// Settings adapter. Every player-facing knob is a live in-game setting from the Mod Settings
    /// Menu framework, bound once in <see cref="PlayerCoordinatesHudMod.Init"/>. The singleton shape
    /// mirrors the sibling mods so consumers read <c>ModConfig.Instance.enabled</c>.
    /// </summary>
    internal sealed class ModConfig
    {
        /// <summary>
        /// Where the readout sits. Every anchor is a constant, lined up with CK's own UI edges; what
        /// varies at runtime is only which anchor applies. <see cref="BelowMinimap"/> steps aside to
        /// <see cref="TopRight"/> while the minimap is not drawn, and <see cref="BottomRight"/> rides
        /// above CK's on-screen button hints, whose height changes as they come and go.
        ///
        /// <para>The member names are load-bearing twice over: <c>Choice</c> persists a setting as its
        /// token (<c>value.ToString()</c>) and looks the label up as
        /// <c>PlayerCoordinatesHud-Config/position/&lt;name&gt;</c>. Renaming one therefore silently
        /// resets every player who chose it back to the default AND drops its localization.</para>
        /// </summary>
        public enum Position
        {
            /// <summary>1.0.0's corner, and still the default. Not its exact coordinate: it is now
            /// flush with CK's status bars, a little further out than the hand-picked original.</summary>
            BottomLeft,

            /// <summary>Sits just above CK's on-screen button hints and drops into the corner when
            /// they are hidden — the one position whose anchor is measured rather than fixed, because
            /// the hints grow and shrink with the situation.</summary>
            BottomRight,

            TopLeft,

            TopRight,

            /// <summary>Sits below CK's minimap. Whenever the minimap is not on screen — switched off
            /// in the options, replaced by the big map, or hidden behind an open inventory — the
            /// readout uses <see cref="TopRight"/> instead.</summary>
            BelowMinimap,
        }

        // Null only in the brief pre-Bind window at mod load, where the defaults below apply.
        private SettingHandle<bool> _enabledHandle;
        private SettingHandle<Position> _positionHandle;
        private SettingHandle<bool> _showIconHandle;

        /// <summary>
        /// Attaches the live setting handles. Called exactly once, from <c>Init</c>. Both guards below
        /// are for a mistake that would otherwise be invisible: a null handle silently pins the getter
        /// to its default, and a second Bind silently swaps which handles the mod reads — in both cases
        /// the settings screen keeps working while the mod ignores it.
        /// </summary>
        public void Bind(SettingHandle<bool> enabled, SettingHandle<Position> position, SettingHandle<bool> showIcon)
        {
            if (enabled == null || position == null || showIcon == null)
                UnityEngine.Debug.LogError(
                    "[PlayerCoordinatesHud] Bind called with a null handle — that setting will stay at its default and ignore the menu."
                );
            if (_enabledHandle != null || _positionHandle != null || _showIconHandle != null)
                UnityEngine.Debug.LogWarning("[PlayerCoordinatesHud] Bind called more than once — the later handles win.");

            _enabledHandle = enabled;
            _positionHandle = position;
            _showIconHandle = showIcon;
        }

        // Master switch (default true). When false the readout is hidden.
        public bool enabled => _enabledHandle != null ? _enabledHandle.Value : true;

        // Where the readout is drawn (default BottomLeft, the 1.0.0 behaviour).
        public Position position => _positionHandle != null ? _positionHandle.Value : Position.BottomLeft;

        // Whether the marker sits left of the coordinates (default true). With it off the text takes
        // the icon's width back, so the readout is flush with its anchor either way.
        public bool showIcon => _showIconHandle != null ? _showIconHandle.Value : true;

        private static readonly ModConfig _instance = new ModConfig();
        public static ModConfig Instance => _instance;
    }
}
