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

        // Each default has to hold in two places — here, as the pre-Bind fallback, and in the
        // registration in Init that the menu actually stores. Nothing couples the two, and a
        // divergence is invisible: it only shows during the brief window before Bind, where the
        // getter would silently answer with the other value. Naming them once removes the question.
        public const bool DefaultEnabled = true;
        public const Position DefaultPosition = Position.BottomLeft;
        public const bool DefaultShowIcon = true;

        // Null only in the brief pre-Bind window at mod load, where the defaults above apply.
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
            // Named, not counted: with three handles "a null handle" leaves the reader guessing which
            // setting went quiet, and the guess gets worse with every knob added. Same shape as the
            // wiring check in CoordinatesHud.Awake.
            string missing = "";
            if (enabled == null)
                missing += "enabled";
            if (position == null)
                missing += (missing.Length > 0 ? ", " : "") + "position";
            if (showIcon == null)
                missing += (missing.Length > 0 ? ", " : "") + "showIcon";
            if (missing.Length > 0)
                UnityEngine.Debug.LogError(
                    $"[PlayerCoordinatesHud] Bind received null handle(s): {missing}. Those settings stay at their defaults and ignore the menu."
                );
            if (_enabledHandle != null || _positionHandle != null || _showIconHandle != null)
                UnityEngine.Debug.LogWarning("[PlayerCoordinatesHud] Bind called more than once — the later handles win.");

            _enabledHandle = enabled;
            _positionHandle = position;
            _showIconHandle = showIcon;
        }

        // Master switch. When false the readout is hidden.
        public bool enabled => _enabledHandle != null ? _enabledHandle.Value : DefaultEnabled;

        // Where the readout is drawn (BottomLeft is the 1.0.0 behaviour).
        public Position position => _positionHandle != null ? _positionHandle.Value : DefaultPosition;

        // Whether the marker sits left of the coordinates. With it off, a left-hand corner gives the
        // icon's width back to the text; a right-hand one never spent it, so only the icon goes away.
        public bool showIcon => _showIconHandle != null ? _showIconHandle.Value : DefaultShowIcon;

        private static readonly ModConfig _instance = new ModConfig();
        public static ModConfig Instance => _instance;
    }
}
