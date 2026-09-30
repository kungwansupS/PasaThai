#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ThaiOfflineAllMods;

internal sealed class ThaiRuntime : IDisposable
{
    private readonly Mod mod;
    private TranslationCatalog? catalog;
    private ThaiCultureLease? culture;
    private ThaiFonts? fonts;
    private Action<LanguageManager, bool>? reload;
    private Action<LanguageManager, GameCulture>? restoreCulture;
    private GameCulture? previousLanguage;
    private bool hooked;
    private bool disposed;
    private int appliedKeys;

    internal bool Started { get; private set; }
    internal static bool ThaiIsActive => Language.ActiveCulture?.Name == "th-TH";

    internal ThaiRuntime(Mod mod) => this.mod = mod;

    internal void Load()
    {
        // Validate before loading assets; register shared state only after both
        // packaged data and fonts have been loaded successfully.
        _ = FindReloadMethod(typeof(LanguageManager));
        _ = FindActiveCultureSetter(typeof(LanguageManager));
        var translations = new TranslationCatalog(ReadPackagedTranslations());
        var fontAssets = new ThaiFonts(mod);
        Load(translations, fontAssets);
    }

    internal void Load(TranslationCatalog translations, ThaiFonts fontAssets)
    {
        reload = FindReloadMethod(typeof(LanguageManager)).CreateDelegate<Action<LanguageManager, bool>>();
        restoreCulture = FindActiveCultureSetter(typeof(LanguageManager)).CreateDelegate<Action<LanguageManager, GameCulture>>();
        catalog = translations;
        culture = new ThaiCultureLease();
        fonts = fontAssets;
        previousLanguage = Language.ActiveCulture?.Name == "th-TH" ? GameCulture.DefaultCulture : Language.ActiveCulture;
        On_LanguageManager.LoadActiveCultureTranslationsFromSources += LoadTranslations;
        hooked = true;
    }

    internal static MethodInfo FindReloadMethod(Type type) =>
        type.GetMethod("ReloadLanguage", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null, [typeof(bool)], null) is { ReturnType: var result } method && result == typeof(void)
            ? method
            : throw new NotSupportedException("ThaiOfflineAllMods: this tModLoader version changed ReloadLanguage(bool). An updated pack is required.");

    internal static MethodInfo FindActiveCultureSetter(Type type) =>
        type.GetProperty("ActiveCulture", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            is { PropertyType: var propertyType } property && propertyType == typeof(GameCulture) &&
            property.GetSetMethod(true) is { IsStatic: false } setter
            ? setter
            : throw new NotSupportedException("ThaiOfflineAllMods: this tModLoader version changed the ActiveCulture setter. An updated pack is required.");

    internal void Start(bool enabled)
    {
        Started = true;
        SetEnabled(enabled);
        if (enabled)
        {
            const string probe = "Mods.ThaiOfflineAllMods.Runtime.Ready";
            if (!ThaiIsActive || catalog is null || appliedKeys == 0 ||
                !catalog.Entries.TryGetValue(probe, out var expected) || Language.GetTextValue(probe) != expected)
                throw new InvalidOperationException("ThaiOfflineAllMods: Thai activation did not apply the packaged translations.");
            mod.Logger.Info($"Standalone Thai ready: {appliedKeys} registered keys applied from {catalog.Entries.Count} packaged entries.");
        }
    }

    internal void SetEnabled(bool enabled)
    {
        if (disposed || culture is null || reload is null)
            return;
        if (enabled)
        {
            if (!ThaiIsActive)
                previousLanguage = Language.ActiveCulture ?? GameCulture.DefaultCulture;
            if (ReferenceEquals(Language.ActiveCulture, culture.Culture))
                reload(LanguageManager.Instance, true);
            else
                LanguageManager.Instance.SetLanguage(culture.Culture);
        }
        else if (ThaiIsActive)
            LanguageManager.Instance.SetLanguage(previousLanguage ?? GameCulture.DefaultCulture);
        fonts?.SetActive(ThaiIsActive);
    }

    private void LoadTranslations(On_LanguageManager.orig_LoadActiveCultureTranslationsFromSources orig, LanguageManager manager)
    {
        orig(manager);
        if (!disposed && ThaiIsActive && catalog is not null)
            appliedKeys = catalog.Apply(manager);
        // The engine next expands copy commands, refreshes bound text, prepares
        // chat aliases, and calls OnLocalizationsLoaded. Never process early.
    }

    internal void OnLocalizationsLoaded()
    {
        if (!disposed)
            fonts?.SetActive(ThaiIsActive);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        Started = false;
        if (hooked)
        {
            On_LanguageManager.LoadActiveCultureTranslationsFromSources -= LoadTranslations;
            hooked = false;
        }
        try
        {
            // tModLoader closes each mod's package BEFORE calling Mod.Unload.
            // SetLanguage/ReloadLanguage here would read those closed packages.
            // Restore only culture state now; the engine's subsequent
            // UnloadModdedEntries reloads text after clearing ModLoader.Mods.
            // Do not fire localization callbacks into partially unloaded mods.
            if (ThaiIsActive && restoreCulture is not null)
            {
                var previous = previousLanguage ?? GameCulture.DefaultCulture;
                restoreCulture(LanguageManager.Instance, previous);
                Thread.CurrentThread.CurrentCulture = previous.CultureInfo;
                Thread.CurrentThread.CurrentUICulture = previous.CultureInfo;
            }
        }
        finally
        {
            fonts?.Dispose();
            culture?.Dispose();
            catalog = null;
            fonts = null;
            culture = null;
            reload = null;
            restoreCulture = null;
            previousLanguage = null;
        }
    }

    private IEnumerable<(string Name, string Json)> ReadPackagedTranslations()
    {
        var files = mod.GetFileNames().Where(p => p.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var path in files.Where(p => p.StartsWith("Assets/BaseTranslations/", StringComparison.Ordinal))
            .OrderBy(p => p, StringComparer.Ordinal)
            .Concat(files.Where(p => p.StartsWith("Translations/", StringComparison.Ordinal)).OrderBy(p => p, StringComparer.Ordinal)))
        {
            using var stream = mod.GetFileStream(path);
            using var reader = new StreamReader(stream);
            yield return (path, reader.ReadToEnd());
        }
    }
}
