#nullable enable
using System;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace ThaiOfflineAllMods;

internal sealed class ThaiFonts : IDisposable
{
    private readonly Asset<DynamicSpriteFont>[] thai;
    private readonly Asset<DynamicSpriteFont>?[] previous = new Asset<DynamicSpriteFont>?[5];
    private readonly bool[] applied = new bool[5];

    internal ThaiFonts(Mod mod) : this([
        mod.Assets.Request<DynamicSpriteFont>("Assets/Fonts/Item_Stack", AssetRequestMode.ImmediateLoad),
        mod.Assets.Request<DynamicSpriteFont>("Assets/Fonts/Mouse_Text", AssetRequestMode.ImmediateLoad),
        mod.Assets.Request<DynamicSpriteFont>("Assets/Fonts/Death_Text", AssetRequestMode.ImmediateLoad),
        mod.Assets.Request<DynamicSpriteFont>("Assets/Fonts/Combat_Text", AssetRequestMode.ImmediateLoad),
        mod.Assets.Request<DynamicSpriteFont>("Assets/Fonts/Combat_Crit", AssetRequestMode.ImmediateLoad),
    ]) { }

    internal ThaiFonts(Asset<DynamicSpriteFont>[] assets)
    {
        if (assets.Length != 5)
            throw new ArgumentException("Five Thai font assets are required.", nameof(assets));
        thai = (Asset<DynamicSpriteFont>[])assets.Clone();
    }

    internal void SetActive(bool active)
    {
        for (int i = 0; i < thai.Length; i++)
        {
            var current = Get(i);
            if (active)
            {
                if (ReferenceEquals(current, thai[i]))
                    continue;
                previous[i] = current;
                applied[i] = true;
                Set(i, thai[i]);
            }
            else if (applied[i])
            {
                if (ReferenceEquals(current, thai[i]))
                    Set(i, previous[i]!);
                applied[i] = false;
                previous[i] = null;
            }
        }
    }

    public void Dispose() => SetActive(false);

    private static Asset<DynamicSpriteFont> Get(int index) => index switch
    {
        0 => FontAssets.ItemStack,
        1 => FontAssets.MouseText,
        2 => FontAssets.DeathText,
        _ => FontAssets.CombatText[index - 3],
    };

    private static void Set(int index, Asset<DynamicSpriteFont> font)
    {
        switch (index)
        {
            case 0: FontAssets.ItemStack = font; break;
            case 1: FontAssets.MouseText = font; break;
            case 2: FontAssets.DeathText = font; break;
            default: FontAssets.CombatText[index - 3] = font; break;
        }
    }
}
