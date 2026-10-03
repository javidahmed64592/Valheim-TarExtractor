using System;
using System.IO;
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
        public const string PluginVersion = "0.2.0";

        internal const string PrefabName = "piece_tarextractor";
        private const string SapExtractorPrefab = "piece_sapcollector";

        // Dark, tar-like tint applied to the cloned Sap Extractor materials.
        private static readonly Color TarTint = new Color(0.44f, 0.36f, 0.32f, 1f);

        // Very dark purple replacing the Sap Extractor's green glow and particles.
        private static readonly Color TarGlow = new Color(0.025f, 0.005f, 0.035f, 1f);

        // Shown only while the extractor holds Tar (toggled by TarExtractor).
        internal const string NotEmptyEffectName = "NotEmptyEffect";

        // The Growth's (BlobTar) oozing splashes, moved to where the Sap Extractor's light was.
        private const string GrowthPrefab = "BlobTar";
        private const string GrowthOozePath = "Visual/particles/wetsplsh";

        // Embedded translation files: TarExtractor.Translations.<Language>.json
        private const string TranslationResourcePrefix = "TarExtractor.Translations.";

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

        // Loads every embedded Translations/<Language>.json. Missing languages fall back to English.
        private static void AddLocalization()
        {
            var loc = LocalizationManager.Instance.GetLocalization();
            Assembly assembly = Assembly.GetExecutingAssembly();
            foreach (string resource in assembly.GetManifestResourceNames())
            {
                if (!resource.StartsWith(TranslationResourcePrefix) || !resource.EndsWith(".json")) continue;

                string language = resource.Substring(
                    TranslationResourcePrefix.Length,
                    resource.Length - TranslationResourcePrefix.Length - ".json".Length);
                using (var reader = new StreamReader(assembly.GetManifestResourceStream(resource)))
                {
                    loc.AddJsonFile(language, reader.ReadToEnd());
                }
            }
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
            RecolorEffects(prefab);
            ReplaceLightWithOoze(prefab);

            // Hidden until the extractor holds Tar; also keeps the placement ghost clean.
            Transform notEmpty = FindChild(prefab.transform, NotEmptyEffectName);
            if (notEmpty != null)
            {
                notEmpty.gameObject.SetActive(false);
            }
            else
            {
                Log.LogWarning($"No '{NotEmptyEffectName}' found on the cloned prefab - effects stay always on.");
            }
        }

        // Turns the green glow and sap particles of the cloned extractor dark purple.
        private static void RecolorEffects(GameObject prefab)
        {
            foreach (ParticleSystem system in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.startColor = TarGlow;

                // A coloured gradient over lifetime would tint the particles back to green; keep only its fade.
                ParticleSystem.ColorOverLifetimeModule overLifetime = system.colorOverLifetime;
                if (overLifetime.enabled)
                {
                    overLifetime.color = WhiteKeepAlpha(overLifetime.color);
                }
            }

            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || !materials[i].HasProperty("_EmissionColor")) continue;

                    // Mesh materials were already copied by TintRenderers; particle materials are still shared.
                    if (renderer is ParticleSystemRenderer)
                    {
                        materials[i] = new Material(materials[i]);
                    }
                    materials[i].SetColor("_EmissionColor", TarGlow);
                }
                renderer.sharedMaterials = materials;
            }
        }

        private static ParticleSystem.MinMaxGradient WhiteKeepAlpha(ParticleSystem.MinMaxGradient source)
        {
            switch (source.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, source.color.a));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(
                        new Color(1f, 1f, 1f, source.colorMin.a), new Color(1f, 1f, 1f, source.colorMax.a));
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(WhiteKeepAlpha(source.gradient));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(
                        WhiteKeepAlpha(source.gradientMin), WhiteKeepAlpha(source.gradientMax));
                default:
                    return source;
            }
        }

        private static Gradient WhiteKeepAlpha(Gradient source)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                source.alphaKeys);
            gradient.mode = source.mode;
            return gradient;
        }

        // Removes the Sap Extractor's light and puts a small copy of the Growth's oozing splashes in its place.
        private static void ReplaceLightWithOoze(GameObject prefab)
        {
            Light light = prefab.GetComponentInChildren<Light>(true);
            if (light == null)
            {
                Log.LogWarning("No light found on the cloned prefab - the ooze effect is not added.");
                return;
            }

            Transform parent = light.transform.parent;
            Vector3 position = light.transform.localPosition;
            UnityEngine.Object.DestroyImmediate(light.gameObject);

            GameObject growth = PrefabManager.Instance.GetPrefab(GrowthPrefab);
            Transform ooze = growth != null ? growth.transform.Find(GrowthOozePath) : null;
            if (ooze == null)
            {
                Log.LogWarning($"Could not find '{GrowthOozePath}' on '{GrowthPrefab}' - no ooze effect.");
                return;
            }

            GameObject copy = UnityEngine.Object.Instantiate(ooze.gameObject, parent, false);
            copy.name = "tar_ooze";
            copy.transform.localPosition = position;
            copy.SetActive(true);

            ParticleSystem system = copy.GetComponent<ParticleSystem>();
            ParticleSystem.ShapeModule shape = system.shape;
            shape.radius *= 0.25f;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTimeMultiplier *= 0.3f;
            ParticleSystem.MainModule main = system.main;
            main.startSizeMultiplier *= 0.4f;
            main.startSpeedMultiplier *= 0.5f;
        }

        internal static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }
            return null;
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
