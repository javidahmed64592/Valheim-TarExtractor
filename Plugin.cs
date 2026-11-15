using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

using HarmonyLib;

using Jotunn.Managers;
using Jotunn.Utils;

using TarExtractorMod.Core;
using TarExtractorMod.Prefabs;

namespace TarExtractorMod;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency(Jotunn.Main.ModGuid)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
public class TarExtractorPlugin : BaseUnityPlugin
{
    public const string PluginGUID = "javidahmed64592.tarextractor";
    public const string PluginName = "Tar Extractor";
    public const string PluginVersion = "0.4.0";

    internal static ManualLogSource _log;
    internal static ConfigEntry<float> _secondsPerTar;
    internal static ConfigEntry<int> _maxTar;
    internal static ConfigEntry<int> _maxPerPit;

    private Harmony _harmony;

    private void Awake()
    {
        _log = Logger;

        _secondsPerTar = Config.Bind(
            "Tar Extractor", "Seconds Per Tar", 60f,
            new ConfigDescription(
                "Seconds it takes the Tar Extractor to produce 1 Tar. (Synced with server)",
                null,
                new ConfigurationManagerAttributes { IsAdminOnly = true }));

        _maxTar = Config.Bind(
            "Tar Extractor", "Max Tar", 200,
            new ConfigDescription(
                "Maximum amount of Tar a Tar Extractor can hold. (Synced with server)",
                null,
                new ConfigurationManagerAttributes { IsAdminOnly = true }));

        _maxPerPit = Config.Bind(
            "Tar Extractor", "Max Per Pit", 0,
            new ConfigDescription(
                "Most Tar Extractors that can be built in one tar pit. 0 means no limit. Extractors already built " +
                "are kept when the limit is lowered. (Synced with server)",
                new AcceptableValueRange<int>(0, 100),
                new ConfigurationManagerAttributes { IsAdminOnly = true }));

        Translations.Load();

        // The Sap Extractor prefab only exists once vanilla prefabs are loaded.
        PrefabManager.OnVanillaPrefabsAvailable += TarExtractorPrefab.Register;

        _harmony = new Harmony(PluginGUID);
        _harmony.PatchAll();
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }
}
