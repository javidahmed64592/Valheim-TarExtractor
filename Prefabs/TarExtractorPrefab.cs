using System;
using System.Reflection;

using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

using TarExtractorMod.Extractor;

using UnityEngine;

using Object = UnityEngine.Object;

namespace TarExtractorMod.Prefabs;

/// <summary>Builds the Tar Extractor piece from a clone of the vanilla Sap Extractor.</summary>
internal static class TarExtractorPrefab
{
    internal const string PrefabName = "piece_tarextractor";
    private const string SapExtractorPrefab = "piece_sapcollector";

    /// <summary>
    /// Dark, tar-like tint applied to the cloned Sap Extractor materials.
    /// </summary>
    private static readonly Color TarTint = new(0.44f, 0.36f, 0.32f, 1f);

    /// <summary>
    /// Very dark purple replacing the Sap Extractor's green glow and particles.
    /// </summary>
    private static readonly Color TarGlow = new(0.025f, 0.005f, 0.035f, 1f);

    /// <summary>
    /// Shown only while the extractor holds Tar (toggled by TarExtractor).
    /// </summary>
    internal const string NotEmptyEffectName = "NotEmptyEffect";

    /// <summary>
    /// The Growth's (BlobTar) oozing splashes, moved to where the Sap Extractor's light was.
    /// </summary>
    private const string GrowthPrefab = "BlobTar";
    private const string GrowthOozePath = "Visual/particles/wetsplsh";

    public static void Register()
    {
        PrefabManager.OnVanillaPrefabsAvailable -= Register;

        PieceConfig config = new()
        {
            Name = "$piece_tarextractor",
            Description = "$piece_tarextractor_description",
            PieceTable = "Hammer",
            Category = "Crafting",
            CraftingStation = "piece_workbench",
            // Same requirements as the vanilla Sap Extractor.
            Requirements =
            [
                new RequirementConfig("YggdrasilWood", 10, 0, true),
                new RequirementConfig("BlackMetal", 5, 0, true),
                new RequirementConfig("DvergrNeedle", 1, 0, true),
            ],
        };

        // Clones the vanilla Sap Extractor prefab under a new name.
        CustomPiece piece = new(PrefabName, SapExtractorPrefab, config);
        ConvertToTarExtractor(piece.PiecePrefab);
        _ = PieceManager.Instance.AddPiece(piece);
    }

    private static void ConvertToTarExtractor(GameObject prefab)
    {
        Piece piece = prefab.GetComponent<Piece>();

        Component sap = prefab.GetComponent("SapCollector");

        // --- The vanilla extractor draws from an Ancient Root's resource pool; we replace its logic.
        if (sap != null)
        {
            Object.DestroyImmediate(sap);
        }
        else
        {
            TarExtractorPlugin._log.LogWarning("No SapCollector component found on the cloned prefab - the vanilla " +
                           "component may have been renamed.");
        }

        // --- The vanilla piece must connect to an Ancient Root. Clear any such requirement.
        ClearConnectionRequirement(piece);

        _ = prefab.AddComponent<TarExtractor>();

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
            TarExtractorPlugin._log.LogWarning($"No '{NotEmptyEffectName}' found on the cloned prefab - effects stay always on.");
        }
    }

    /// <summary>
    /// Turns the green glow and sap particles of the cloned extractor dark purple.
    /// </summary>
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
                if (materials[i] == null || !materials[i].HasProperty("_EmissionColor"))
                {
                    continue;
                }

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
        return source.mode switch
        {
            ParticleSystemGradientMode.Color => new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, source.color.a)),
            ParticleSystemGradientMode.TwoColors => new ParticleSystem.MinMaxGradient(
                                    new Color(1f, 1f, 1f, source.colorMin.a), new Color(1f, 1f, 1f, source.colorMax.a)),
            ParticleSystemGradientMode.Gradient => new ParticleSystem.MinMaxGradient(WhiteKeepAlpha(source.gradient)),
            ParticleSystemGradientMode.TwoGradients => new ParticleSystem.MinMaxGradient(
                                    WhiteKeepAlpha(source.gradientMin), WhiteKeepAlpha(source.gradientMax)),
            _ => source,
        };
    }

    private static Gradient WhiteKeepAlpha(Gradient source)
    {
        Gradient gradient = new();
        gradient.SetKeys(
            [new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f)],
            source.alphaKeys);
        gradient.mode = source.mode;
        return gradient;
    }

    /// <summary>
    /// Removes the Sap Extractor's light and puts a small copy of the Growth's oozing splashes in its place.
    /// </summary>
    private static void ReplaceLightWithOoze(GameObject prefab)
    {
        Light light = prefab.GetComponentInChildren<Light>(true);
        if (light == null)
        {
            TarExtractorPlugin._log.LogWarning("No light found on the cloned prefab - the ooze effect is not added.");
            return;
        }

        Transform parent = light.transform.parent;
        Vector3 position = light.transform.localPosition;
        Object.DestroyImmediate(light.gameObject);

        GameObject growth = PrefabManager.Instance.GetPrefab(GrowthPrefab);
        Transform ooze = growth != null ? growth.transform.Find(GrowthOozePath) : null;
        if (ooze == null)
        {
            TarExtractorPlugin._log.LogWarning($"Could not find '{GrowthOozePath}' on '{GrowthPrefab}' - no ooze effect.");
            return;
        }

        GameObject copy = Object.Instantiate(ooze.gameObject, parent, false);
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
            if (child.name == name)
            {
                return child;
            }
        }
        return null;
    }

    private static void ClearConnectionRequirement(Piece piece)
    {
        foreach (FieldInfo field in typeof(Piece).GetFields(
                     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.Name.IndexOf("mustconnect", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            object cleared = field.FieldType == typeof(bool) ? false : null;
            field.SetValue(piece, cleared);
            TarExtractorPlugin._log.LogInfo($"Cleared Piece.{field.Name} on the Tar Extractor prefab.");
        }
    }

    private static void TintRenderers(GameObject prefab)
    {
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            // Skip particle systems / trails - only recolour regular meshes.
            if (renderer is ParticleSystemRenderer or TrailRenderer)
            {
                continue;
            }

            Material[] source = renderer.sharedMaterials;
            Material[] tinted = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null)
                {
                    continue;
                }

                tinted[i] = new Material(source[i]);
                if (tinted[i].HasProperty("_Color"))
                {
                    tinted[i].color = TarTint;
                }
            }
            renderer.sharedMaterials = tinted;
        }
    }
}
