#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Terraria.Localization;

namespace ThaiOfflineAllMods;

// Packaged data does not depend on another mod's API or static fields.
internal sealed class TranslationCatalog
{
    internal IReadOnlyDictionary<string, string> Entries { get; }
    private readonly string nativeJson;

    internal TranslationCatalog(IEnumerable<(string Name, string Json)> files)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, json) in files)
        {
            try
            {
                var document = JObject.Parse(json, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                });
                Flatten(document, "", entries);
            }
            catch (Exception e) when (e is JsonException or InvalidDataException)
            {
                throw new InvalidDataException($"Invalid Thai translation file '{name}': {e.Message}", e);
            }
        }
        if (entries.Count == 0)
            throw new InvalidDataException("No packaged Thai translations were found.");

        Entries = entries;
        // The public JSON loader takes category -> suffix. Dots in a suffix
        // preserve the full identity of nested mod localization keys.
        var native = new JObject();
        foreach (var (key, value) in entries)
        {
            int dot = key.IndexOf('.');
            if (dot <= 0 || dot == key.Length - 1)
                throw new InvalidDataException($"Invalid localization key '{key}'.");
            string category = key[..dot];
            var group = native[category] as JObject;
            if (group is null)
                native[category] = group = new JObject();
            group[key[(dot + 1)..]] = value;
        }
        nativeJson = native.ToString(Formatting.None);
    }

    internal int Apply(LanguageManager manager)
    {
        int present = Entries.Keys.Count(Language.Exists);
        manager.LoadLanguageFromFileTextJson(nativeJson, canCreateCategories: false);
        return present;
    }

    private static void Flatten(JToken token, string prefix, Dictionary<string, string> entries)
    {
        switch (token)
        {
            case JObject obj:
                foreach (var property in obj.Properties())
                    Flatten(property.Value, property.Name == "$parentVal" ? prefix :
                        prefix.Length == 0 ? property.Name : prefix + "." + property.Name, entries);
                break;
            case JArray array:
                for (int i = 0; i < array.Count; i++)
                    Flatten(array[i], prefix + "." + i, entries);
                break;
            case JValue { Type: JTokenType.String } value:
                entries[prefix] = (string)value!;
                break;
            default:
                throw new InvalidDataException($"Translation '{prefix}' must be a string.");
        }
    }
}
