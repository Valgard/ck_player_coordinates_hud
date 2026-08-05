namespace PlayerCoordinatesHud
{
    /// <summary>
    /// Shared "is the local player actually in a playable world right now?" predicate,
    /// copied from ItemChecklist (Iter-11.6 / Iter-15) where it was derived from CK's own
    /// PlayerController.PlayerInputBlocked gate.
    ///
    /// <para>Why not <c>Manager.main.player != null</c>: the player object is instantiated at
    /// <c>PlayerController.OnOccupied</c>, which fires while the world-load screen is still up,
    /// and it survives the exit-to-menu transition — so a player-null check is true across BOTH
    /// load screens and cannot suppress them. <c>!Manager.load.IsLoading()</c> is the reliable
    /// signal; <c>IsLoadingAndScreenBlack()</c> is deliberately NOT used because it goes false
    /// during the exit fade, letting the HUD flash.</para>
    ///
    /// <para><c>cutsceneIsPlaying</c> covers the spawn-from-Core intro: it fades CK's own HUD via
    /// <c>FadeOutAllGameplayUI()</c>, which does NOT cull a mod's layer-27 HUD.</para>
    /// </summary>
    internal static class WorldState
    {
        public static bool IsInPlayableWorld
        {
            get
            {
                var sceneHandler = Manager.sceneHandler;
                return sceneHandler != null
                    && sceneHandler.isInGame
                    && sceneHandler.isSceneHandlerReady
                    && !sceneHandler.cutsceneIsPlaying
                    && Manager.main != null
                    && Manager.main.player != null
                    && Manager.load != null
                    && !Manager.load.IsLoading();
            }
        }
    }
}
