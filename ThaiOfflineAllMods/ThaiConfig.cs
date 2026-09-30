#nullable enable
using System.ComponentModel;
using Terraria;
using Terraria.ModLoader.Config;

namespace ThaiOfflineAllMods;

public sealed class ThaiConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    [DefaultValue(true)]
    public bool UseThai = true;

    public override void OnChanged()
    {
        if (ThaiOfflineAllMods.Runtime is not { Started: true } runtime)
            return;
        Main.QueueMainThreadAction(() =>
        {
            if (ReferenceEquals(ThaiOfflineAllMods.Runtime, runtime))
                runtime.SetEnabled(UseThai);
        });
    }
}
