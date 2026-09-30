#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria.Localization;

namespace ThaiOfflineAllMods;

// Only the registration created by this instance may be removed on unload.
internal sealed class ThaiCultureLease : IDisposable
{
    private readonly Dictionary<int, GameCulture> registry;
    private readonly bool owned;
    internal GameCulture Culture { get; }

    internal ThaiCultureLease()
    {
        registry = typeof(GameCulture).GetField("_legacyCultures", BindingFlags.NonPublic | BindingFlags.Static)
            ?.GetValue(null) as Dictionary<int, GameCulture>
            ?? throw new NotSupportedException("ThaiOfflineAllMods: this tModLoader version changed the culture registry. An updated pack is required.");
        var existing = GameCulture.KnownCultures.FirstOrDefault(c => c.Name == "th-TH");
        if (existing is not null)
        {
            Culture = existing;
            return;
        }
        int id = Math.Max(9, registry.Keys.DefaultIfEmpty(9).Max()) + 1;
        Culture = new GameCulture("th-TH", id);
        owned = true;
    }

    public void Dispose()
    {
        if (owned && registry.TryGetValue(Culture.LegacyId, out var current) && ReferenceEquals(current, Culture))
            registry.Remove(Culture.LegacyId);
    }
}
