#nullable enable
using System.Threading;
using Terraria;
using Terraria.ModLoader;

namespace ThaiOfflineAllMods;

public sealed class ThaiOfflineAllMods : Mod
{
    internal static ThaiRuntime? Runtime { get; private set; }

    public override void Load()
    {
        if (Main.dedServ)
            return;

        Runtime = new ThaiRuntime(this);
        Runtime.Load();
        if (ModLoader.HasMod("ThaiLanguageLibrary"))
            Logger.Warn("ThaiLanguageLibrary is also enabled. This pack is standalone; disable the old library unless another mod requires it. Its hooks are outside this pack's compatibility guarantee.");
    }

    public override void PostSetupContent()
    {
        if (Runtime is { } runtime)
            Main.RunOnMainThread(() => runtime.Start(ModContent.GetInstance<ThaiConfig>().UseThai))
                .GetAwaiter().GetResult();
    }

    public override void Unload()
    {
        var runtime = Runtime;
        Runtime = null;
        if (runtime is not null)
        {
            Main.RunOnMainThread(runtime.Dispose).GetAwaiter().GetResult();
            // The reload worker captured the old Thai ExecutionContext before
            // Dispose restored the main thread. Restore this caller too, before
            // other mods load and perform culture-sensitive string operations.
            var restoredCulture = Terraria.Localization.Language.ActiveCulture.CultureInfo;
            Thread.CurrentThread.CurrentCulture = restoredCulture;
            Thread.CurrentThread.CurrentUICulture = restoredCulture;
        }
    }
}

public sealed class ThaiLocalizationSystem : ModSystem
{
    public override void OnLocalizationsLoaded() => ThaiOfflineAllMods.Runtime?.OnLocalizationsLoaded();
}
