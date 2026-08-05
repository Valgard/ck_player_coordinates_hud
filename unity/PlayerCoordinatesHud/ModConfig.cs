using ModSettingsMenu.Settings;

namespace PlayerCoordinatesHud
{
    /// <summary>
    /// Settings adapter. One player-facing knob — the <c>enabled</c> master toggle — is a live
    /// in-game setting from the Mod Settings Menu framework, bound once in
    /// <see cref="PlayerCoordinatesHudMod.Init"/>. The singleton shape mirrors the sibling mods so
    /// consumers read <c>ModConfig.Instance.enabled</c>.
    /// </summary>
    internal sealed class ModConfig
    {
        // Null only in the brief pre-Bind window at mod load, where the default below applies.
        private SettingHandle<bool> _enabledHandle;

        public void Bind(SettingHandle<bool> enabled)
        {
            _enabledHandle = enabled;
        }

        // Master switch (default true). When false the readout is hidden.
        public bool enabled => _enabledHandle != null ? _enabledHandle.Value : true;

        private static readonly ModConfig _instance = new ModConfig();
        public static ModConfig Instance => _instance;
    }
}
