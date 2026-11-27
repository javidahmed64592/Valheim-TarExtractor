using HarmonyLib;

using TarExtractorMod.Core;
using TarExtractorMod.Extractor;

namespace TarExtractorMod.Patches;

/// <summary>
/// When placing a blocked Tar Extractor fails, vanilla shows "Invalid placement". This replaces it with the
/// reason: outside a tar pit, or the pit is full. TryPlacePiece updates the ghost first, so the reason is current,
/// and this runs after vanilla's message, so it isn't overwritten.
/// </summary>
[HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
[Feature(Feature)]
internal static class Player_TryPlacePiece_Patch
{
    /// <summary>Without it, vanilla's "Invalid placement" message is shown instead.</summary>
    public const string Feature = "Blocked placement message";

    /// <summary>
    /// __0 = piece (positional so a parameter rename doesn't break the patch)
    /// </summary>
    private static void Postfix(Player __instance, Piece __0, bool __result)
    {
        if (__result || !PlacementState._blocked || __0 == null || __0.GetComponent<TarExtractor>() == null)
        {
            return;
        }

        PlacementState.Show(__instance, PlacementState._message);
    }
}
