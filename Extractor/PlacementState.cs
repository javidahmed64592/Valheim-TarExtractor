namespace TarExtractorMod.Extractor;

internal static class PlacementState
{
    /// <summary>True while the player is aiming a Tar Extractor ghost outside a tar pit.</summary>
    internal static bool _blocked;

    /// <summary>Why the ghost is blocked, shown when the player tries to place it.</summary>
    internal static string _message;

    internal static void Show(Player player, string message)
    {
        int max = TarExtractorPlugin._maxPerPit.Value;
        player.Message(MessageHud.MessageType.Center,
            Localization.instance.Localize(message, max.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }
}
