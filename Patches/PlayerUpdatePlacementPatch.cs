using HarmonyLib;

using TarExtractorMod.Core;
using TarExtractorMod.Extractor;

namespace TarExtractorMod.Patches;

/// <summary>
/// The game normally blocks an invalid placement silently, so show our own message
/// when the player presses the place button while the ghost is blocked.
/// </summary>
[HarmonyPatch(typeof(Player), "UpdatePlacement")]
[Feature(Feature)]
internal static class Player_UpdatePlacement_Patch
{
    /// <summary>Without it, a blocked placement fails silently, as in vanilla.</summary>
    public const string Feature = "Blocked placement message";

    /// <summary>
    /// __0 = takeInput (positional so a parameter rename doesn't break the patch)
    /// </summary>
    private static void Prefix(Player __instance, bool __0)
    {
        if (!__0 || !PlacementState._blocked || !__instance.InPlaceMode())
        {
            return;
        }

        if (ZInput.GetButtonDown("Attack") || ZInput.GetButtonDown("JoyPlace"))
        {
            PlacementState.Show(__instance, PlacementState._message);
        }
    }
}
