using HarmonyLib;

using TarExtractorMod.Extractor;

using UnityEngine;

namespace TarExtractorMod.Patches;

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
            PlacementState._blocked = false;
            return;
        }

        string problem = TarPit.PlacementProblem(ghost.transform.position);
        if (problem == null)
        {
            PlacementState._blocked = false;
            return;
        }

        PlacementState._blocked = true;
        PlacementState._message = problem;
        __instance.m_placementStatus = Player.PlacementStatus.Invalid;
        __instance.SetPlacementGhostValid(false);
    }
}
