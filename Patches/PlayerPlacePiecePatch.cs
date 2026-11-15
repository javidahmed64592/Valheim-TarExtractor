using HarmonyLib;

using TarExtractorMod.Extractor;

using UnityEngine;

namespace TarExtractorMod.Patches;

/// <summary>
/// Backstop: even if something lets PlacePiece through, refuse outside a tar pit or in a full one.
/// __0 = Piece, __1 = position (positional for the same reason as above).
/// </summary>
[HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
internal static class Player_PlacePiece_Patch
{
    /// <summary>
    /// PlacePiece returns void, so skipping the original is enough to cancel placement.
    /// </summary>
    private static bool Prefix(Player __instance, Piece __0, Vector3 __1)
    {
        if (__0 == null || __0.GetComponent<TarExtractor>() == null)
        {
            return true;
        }

        string problem = TarPit.PlacementProblem(__1);
        if (problem == null)
        {
            return true;
        }

        PlacementState.Show(__instance, problem);
        return false;
    }
}
