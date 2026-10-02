using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace TarExtractorMod
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class TarExtractorPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "javidahmed64592.tarextractor";
        public const string PluginName = "Tar Extractor";
        public const string PluginVersion = "0.1.0";

        internal const string PrefabName = "piece_tarextractor";
        private const string SapExtractorPrefab = "piece_sapcollector";

        // Dark, tar-like tint applied to the cloned Sap Extractor materials.
        private static readonly Color TarTint = new Color(0.22f, 0.18f, 0.16f, 1f);

        internal static ManualLogSource Log;
        internal static ConfigEntry<float> SecondsPerTar;
        internal static ConfigEntry<int> MaxTar;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            SecondsPerTar = Config.Bind(
                "Tar Extractor", "Seconds Per Tar", 60f,
                new ConfigDescription(
                    "Seconds it takes the Tar Extractor to produce 1 Tar.",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            MaxTar = Config.Bind(
                "Tar Extractor", "Max Tar", 200,
                new ConfigDescription(
                    "Maximum amount of Tar a Tar Extractor can hold.",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            AddLocalization();

            // The Sap Extractor prefab only exists once vanilla prefabs are loaded.
            PrefabManager.OnVanillaPrefabsAvailable += AddTarExtractor;

            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private static void AddLocalization()
        {
            var loc = LocalizationManager.Instance.GetLocalization();
            loc.AddTranslation("English", new Dictionary<string, string>
            {
                { "piece_tarextractor", "Tar Extractor" },
                { "piece_tarextractor_description", "Extract tar from tar pits." },
                { "piece_tarextractor_extract", "Extract Tar" },
                { "piece_tarextractor_empty", "No Tar collected yet" },
                { "piece_tarextractor_extracted", "Tar extracted" },
                { "msg_tarextractor_needstarpit", "Must be placed in a tar pit" },
            });
        }

        private void AddTarExtractor()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= AddTarExtractor;

            var config = new PieceConfig
            {
                Name = "$piece_tarextractor",
                Description = "$piece_tarextractor_description",
                PieceTable = "Hammer",
                Category = "Crafting",
                CraftingStation = "piece_workbench",
                // Same requirements as the vanilla Sap Extractor.
                Requirements = new[]
                {
                    new RequirementConfig("YggdrasilWood", 10, 0, true),
                    new RequirementConfig("BlackMetal", 5, 0, true),
                    new RequirementConfig("DvergrNeedle", 1, 0, true),
                },
            };

            // Clones the vanilla Sap Extractor prefab under a new name.
            var piece = new CustomPiece(PrefabName, SapExtractorPrefab, config);
            ConvertToTarExtractor(piece.PiecePrefab);
            PieceManager.Instance.AddPiece(piece);
        }

        private static void ConvertToTarExtractor(GameObject prefab)
        {
            Piece piece = prefab.GetComponent<Piece>();

            // --- Diagnostics: shows what the cloned Piece / SapCollector expose in this game version.
            LogFields("Piece", piece, "connect");
            Component sap = prefab.GetComponent("SapCollector");
            LogFields("SapCollector", sap, null);

            // --- The vanilla extractor draws from an Ancient Root's resource pool; we replace its logic.
            if (sap != null)
            {
                UnityEngine.Object.DestroyImmediate(sap);
            }
            else
            {
                Log.LogWarning("No SapCollector component found on the cloned prefab - the vanilla " +
                               "component may have been renamed. Check the diagnostic output above.");
            }

            // --- The vanilla piece must connect to an Ancient Root. Clear any such requirement.
            ClearConnectionRequirement(piece);

            prefab.AddComponent<TarExtractor>();

            TintRenderers(prefab);
        }

        private static void ClearConnectionRequirement(Piece piece)
        {
            foreach (FieldInfo field in typeof(Piece).GetFields(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.Name.IndexOf("mustconnect", StringComparison.OrdinalIgnoreCase) < 0) continue;

                object cleared = field.FieldType == typeof(bool) ? (object)false : null;
                field.SetValue(piece, cleared);
                Log.LogInfo($"Cleared Piece.{field.Name} on the Tar Extractor prefab.");
            }
        }

        private static void TintRenderers(GameObject prefab)
        {
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                // Skip particle systems / trails - only recolour regular meshes.
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer) continue;

                Material[] source = renderer.sharedMaterials;
                var tinted = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i] == null) continue;
                    tinted[i] = new Material(source[i]);
                    if (tinted[i].HasProperty("_Color"))
                    {
                        tinted[i].color = TarTint;
                    }
                }
                renderer.sharedMaterials = tinted;
            }
        }

        private static void LogFields(string label, object target, string nameFilter)
        {
            if (target == null) return;
            Log.LogInfo($"[diagnostic] {label} fields:");
            foreach (FieldInfo field in target.GetType().GetFields(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (nameFilter != null &&
                    field.Name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                Log.LogInfo($"    {field.FieldType.Name} {field.Name} = {field.GetValue(target)}");
            }
        }
    }
}
