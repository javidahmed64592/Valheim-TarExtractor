using HarmonyLib;
using UnityEngine;

namespace TarExtractorMod
{
    internal static class PlacementState
    {
        /// <summary>True while the player is aiming a Tar Extractor ghost outside a tar pit.</summary>
        internal static bool Blocked;
        internal const string Message = "$msg_tarextractor_needstarpit";
    }

    /// <summary>
    /// Runs every frame while placing. Turns the ghost red (invalid) when it is not in a tar pit.
    /// </summary>
    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    internal static class Player_UpdatePlacementGhost_Patch
    {
        private static void Postfix(Player __instance)
        {
            GameObject ghost = __instance.m_placementGhost;
            if (ghost == null || ghost.GetComponent<TarExtractor>() == null)
            {
                PlacementState.Blocked = false;
                return;
            }

            if (TarPit.IsInTarPit(ghost.transform.position))
            {
                PlacementState.Blocked = false;
                return;
            }

            PlacementState.Blocked = true;
            __instance.m_placementStatus = Player.PlacementStatus.Invalid;
            __instance.SetPlacementGhostValid(false);
        }
    }

    /// <summary>
    /// The game normally blocks an invalid placement silently, so show our own message
    /// when the player presses the place button while the ghost is blocked.
    /// </summary>
    [HarmonyPatch(typeof(Player), "UpdatePlacement")]
    internal static class Player_UpdatePlacement_Patch
    {
        // __0 = takeInput (positional so a parameter rename doesn't break the patch)
        private static void Prefix(Player __instance, bool __0)
        {
            if (!__0 || !PlacementState.Blocked || !__instance.InPlaceMode())
            {
                return;
            }

            if (ZInput.GetButtonDown("Attack") || ZInput.GetButtonDown("JoyPlace"))
            {
                __instance.Message(MessageHud.MessageType.Center, PlacementState.Message);
            }
        }
    }

    /// <summary>
    /// Backstop: even if something lets PlacePiece through, refuse outside a tar pit.
    /// __0 = Piece, __1 = position (positional for the same reason as above).
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
    internal static class Player_PlacePiece_Patch
    {
        private static bool Prefix(Player __instance, Piece __0, Vector3 __1)
        {
            if (__0 == null || __0.GetComponent<TarExtractor>() == null)
            {
                return true;
            }

            if (TarPit.IsInTarPit(__1))
            {
                return true;
            }

            __instance.Message(MessageHud.MessageType.Center, PlacementState.Message);
            return false;
        }
    }
}
