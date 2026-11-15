using System;
using System.IO;
using System.Reflection;

using Jotunn.Entities;
using Jotunn.Managers;

namespace TarExtractorMod.Core;

internal static class Translations
{
    /// <summary>
    /// Embedded translation files: TarExtractor.Translations.<Language>.json
    /// </summary>
    private const string TranslationResourcePrefix = "TarExtractor.Translations.";

    /// <summary>
    /// Loads every embedded Translations/<Language>.json. Missing languages fall back to English.
    /// </summary>
    public static void Load()
    {
        CustomLocalization loc = LocalizationManager.Instance.GetLocalization();
        Assembly assembly = Assembly.GetExecutingAssembly();
        foreach (string resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(TranslationResourcePrefix, StringComparison.Ordinal) || !resource.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            string language = resource.Substring(
                TranslationResourcePrefix.Length,
                resource.Length - TranslationResourcePrefix.Length - ".json".Length);
            using StreamReader reader = new(assembly.GetManifestResourceStream(resource));
            loc.AddJsonFile(language, reader.ReadToEnd());
        }
    }
}
