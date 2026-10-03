using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using ThaiOfflineAllMods;

internal static class Tests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
    private static readonly List<object> Results = new();
    private static int failures;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    internal static int Run(string? output = null)
    {
        Test("catalog rejects malformed data with filename", () =>
        {
            Expect<InvalidDataException>(() => new TranslationCatalog([("broken.json", "{")]));
            Expect<InvalidDataException>(() => new TranslationCatalog([("duplicate.json", "{\"UI\":{\"Name\":\"a\",\"Name\":\"b\"}}") ]));
            Expect<InvalidDataException>(() => new TranslationCatalog([("invalid.json", "{\"UI\":{\"Count\":7}}") ]));
        });
        Test("native loader changes existing mod keys without creating unloaded keys", () =>
        {
            var manager = LanguageManager.Instance;
            Language.GetOrRegister("Mods.RuntimeFixture.Message", () => "before");
            var catalog = new TranslationCatalog([("test.json", "{\"Mods\":{\"RuntimeFixture\":{\"Message\":\"หลังแปล\",\"Unloaded\":\"ไม่สร้าง\"}}}")]);
            Equal(1, catalog.Apply(manager));
            Equal("หลังแปล", Language.GetTextValue("Mods.RuntimeFixture.Message"));
            True(!Language.Exists("Mods.RuntimeFixture.Unloaded"));
        });
        Test("culture ownership survives repeated acquire and release", () =>
        {
            int before = GameCulture.KnownCultures.Count();
            for (int i = 0; i < 3; i++)
            {
                var first = new ThaiCultureLease();
                var second = new ThaiCultureLease();
                True(ReferenceEquals(first.Culture, second.Culture));
                second.Dispose();
                True(GameCulture.KnownCultures.Contains(first.Culture));
                first.Dispose();
                Equal(before, GameCulture.KnownCultures.Count());
            }
        });
        Test("culture does not remove a registration owned by another mod", () =>
        {
            var registry = (Dictionary<int, GameCulture>)typeof(GameCulture).GetField("_legacyCultures", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            int id = registry.Keys.Max() + 1;
            var foreign = new GameCulture("th-TH", id);
            using (var lease = new ThaiCultureLease()) True(ReferenceEquals(foreign, lease.Culture));
            True(ReferenceEquals(registry[id], foreign));
            registry.Remove(id);
        });
        Test("font restore preserves both combat fonts and later third-party replacements", () =>
        {
            var original = Fonts("original");
            SetFonts(original);
            var thai = Fonts("thai");
            using (var lease = new ThaiFonts(thai))
            {
                lease.SetActive(true);
                lease.SetActive(true);
                True(ReferenceEquals(FontAssets.CombatText[0], thai[3]));
                True(ReferenceEquals(FontAssets.CombatText[1], thai[4]));
                lease.SetActive(false);
                True(ReferenceEquals(FontAssets.CombatText[0], original[3]));
                True(ReferenceEquals(FontAssets.CombatText[1], original[4]));
                lease.SetActive(true);
                FontAssets.MouseText = original[1];
            }
            True(ReferenceEquals(FontAssets.MouseText, original[1]));
            True(ReferenceEquals(FontAssets.CombatText[0], original[3]));
            True(ReferenceEquals(FontAssets.CombatText[1], original[4]));
        });
        Test("ambience follows language and restores originals on unload", () =>
        {
            var patch = new TerrariaAmbienceThaiCompatibility();
            Equal(4, patch.CaptureFlavorDictionaries(typeof(AmbienceFixture)));
            patch.UpdateFlavorLanguage(false);
            Equal("Unavailable", AmbienceFixture.FlavorsMorning[0]);
            patch.UpdateFlavorLanguage(true);
            Equal("ยังไม่พร้อม", AmbienceFixture.FlavorsMorning[0]);
            AmbienceFixture.FlavorsNight[0] = "another mod";
            patch.Unload();
            Equal("Unavailable", AmbienceFixture.FlavorsMorning[0]);
            Equal("another mod", AmbienceFixture.FlavorsNight[0]);
        });
        Test("changed engine signatures report compatibility error", () =>
        {
            True(ThaiRuntime.FindReloadMethod(typeof(LanguageManager)).GetParameters()[0].ParameterType == typeof(bool));
            Expect<NotSupportedException>(() => ThaiRuntime.FindReloadMethod(typeof(ChangedEngineFixture)));
            True(ThaiRuntime.FindActiveCultureSetter(typeof(LanguageManager)).GetParameters()[0].ParameterType == typeof(GameCulture));
            Expect<NotSupportedException>(() => ThaiRuntime.FindActiveCultureSetter(typeof(ChangedCultureFixture)));
            True(NoxusThaiFontCompatibility.FindCompatibleMethod(typeof(ChangedNoxusFixture), "WrapText", typeof(string),
                [typeof(string), typeof(DynamicSpriteFont)]) is null);
        });
        Test("translation placeholders and UI tokens match the audited source baseline", VerifyData);
        Test("real tModLoader language pipeline activates and reloads without library", VerifyPipeline);
        Test("unload with closed mod packages restores English safely", () => VerifyClosedPackageUnload("en-US"));
        Test("unload preserves a non-English previous language", () => VerifyClosedPackageUnload("fr-FR"));

        output ??= Path.Combine(Root, ".qa", "tests.json");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(new { passed = Results.Count - failures, failed = failures, tests = Results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Passed {Results.Count - failures}/{Results.Count}. {output}");
        return failures == 0 ? 0 : 1;
    }

    private static void VerifyClosedPackageUnload(string previousLanguage)
    {
        var manager = LanguageManager.Instance;
        typeof(ModLoader).GetProperty("Mods")!.SetValue(null, Array.Empty<Mod>());
        manager.SetLanguage(previousLanguage);
        string baseText = Language.GetTextValue("UI.Back");
        const string key = "Mods.ThaiOfflineAllMods.Runtime.Ready";
        Language.GetOrRegister(key, () => "English ready");
        var catalog = new TranslationCatalog([("closed-package.json",
            "{\"UI\":{\"Back\":\"ย้อนกลับทดสอบ\"},\"Mods\":{\"ThaiOfflineAllMods\":{\"Runtime\":{\"Ready\":\"พร้อม\"}}}}")]);
        var originalFonts = Fonts("before-unload");
        SetFonts(originalFonts);
        var mod = new ThaiOfflineAllMods.ThaiOfflineAllMods();
        typeof(Mod).GetProperty("Logger")!.SetValue(mod, log4net.LogManager.GetLogger("ThaiRuntimeTests"));
        var runtime = new ThaiRuntime(mod);
        runtime.Load(catalog, new ThaiFonts(Fonts("thai-unload")));
        runtime.Start(true);
        Equal("ย้อนกลับทดสอบ", Language.GetTextValue("UI.Back"));

        // Match the engine's real ordering: the mod remains in ModLoader.Mods,
        // but Mod.Close() has already released its TmodFile handle before Unload.
        var fileType = typeof(Mod).Assembly.GetType("Terraria.ModLoader.Core.TmodFile", true)!;
        string package = Path.Combine(Root, ".qa/ThaiOfflineAllMods.tmod");
        object file = Activator.CreateInstance(fileType, PrivateInstance, null, [package, null, null], null)!;
        using (var handle = (IDisposable)fileType.GetMethod("Open")!.Invoke(file, null)!)
            typeof(Mod).GetProperty("File", PrivateInstance)!.SetValue(mod, file);
        typeof(ModLoader).GetProperty("Mods")!.SetValue(null, new Mod[] { mod });
        try
        {
            runtime.Dispose();
            runtime.Dispose();
            Equal(previousLanguage, Language.ActiveCulture.Name);
            Equal(previousLanguage, Thread.CurrentThread.CurrentCulture.Name);
            Equal(previousLanguage, Thread.CurrentThread.CurrentUICulture.Name);
            True(ReferenceEquals(originalFonts[1], FontAssets.MouseText));
            True(!GameCulture.KnownCultures.Any(c => c.Name == "th-TH"));
        }
        finally
        {
            typeof(ModLoader).GetProperty("Mods")!.SetValue(null, Array.Empty<Mod>());
            // This is the native final localization cleanup after the engine
            // removes the old mods. It must remove stale translated vanilla text.
            manager.GetType().GetMethod("UnloadModdedEntries", PrivateInstance)!.Invoke(manager, null);
            runtime.Dispose();
        }
        Equal(baseText, Language.GetTextValue("UI.Back"));
        True(!Language.Exists(key));
    }

    private static void VerifyPipeline()
    {
        // This is an isolated process with the actual game assembly and native
        // localization hook. No game window or user profile is opened.
        typeof(ModLoader).GetProperty("Mods")!.SetValue(null, Array.Empty<Mod>());
        LanguageManager.Instance.SetLanguage("en-US");
        string key = "Mods.ThaiOfflineAllMods.Runtime.Ready";
        Language.GetOrRegister(key, () => "English ready");
        Language.GetOrRegister("Mods.RuntimeFixture.Source", () => "English {0}");
        Language.GetOrRegister("Mods.RuntimeFixture.Copy", () => "English copy {0}");
        var bound = Language.GetText("Mods.RuntimeFixture.Copy").WithFormatArgs(7);
        var catalog = new TranslationCatalog([("fixture.json", "{\"Mods\":{\"ThaiOfflineAllMods\":{\"Runtime\":{\"Ready\":\"พร้อม\"}},\"RuntimeFixture\":{\"Source\":\"ข้อความ {0}\",\"Copy\":\"{$Mods.RuntimeFixture.Source}\"}}}")]);
        var mod = new ThaiOfflineAllMods.ThaiOfflineAllMods();
        typeof(Mod).GetProperty("Logger")!.SetValue(mod, log4net.LogManager.GetLogger("ThaiRuntimeTests"));
        SetFonts(Fonts("base"));
        var runtime = new ThaiRuntime(mod);
        runtime.Load(catalog, new ThaiFonts(Fonts("thai")));
        try
        {
            runtime.Start(true);
            Equal("th-TH", Language.ActiveCulture.Name);
            Equal("พร้อม", Language.GetTextValue(key));
            Equal("ข้อความ 7", bound.Value);
            runtime.SetEnabled(true); // same-culture reload used to be a no-op
            Equal("พร้อม", Language.GetTextValue(key));
            Equal("ข้อความ 7", bound.Value);
            runtime.SetEnabled(false);
            Equal("en-US", Language.ActiveCulture.Name);
            runtime.SetEnabled(true);
            Equal("พร้อม", Language.GetTextValue(key));
        }
        finally { runtime.Dispose(); }
        Equal("en-US", Language.ActiveCulture.Name);
        True(!GameCulture.KnownCultures.Any(c => c.Name == "th-TH"));
    }

    private static void VerifyData()
    {
        string modRoot = Path.Combine(Root, "ThaiOfflineAllMods");
        var files = Directory.EnumerateFiles(Path.Combine(modRoot, "Assets/BaseTranslations"), "*.json").Order()
            .Concat(Directory.EnumerateFiles(Path.Combine(modRoot, "Translations"), "*.json").Order());
        var catalog = new TranslationCatalog(files.Select(p => (p, File.ReadAllText(p))));
        True(catalog.Entries.Count > 18000);
        var source = JsonSerializer.Deserialize<Dictionary<string, string[][]>>(File.ReadAllText(Path.Combine(Root, "tests/fixtures/source-tokens.json")))!;
        var pack = new TranslationCatalog(Directory.EnumerateFiles(Path.Combine(modRoot, "Translations"), "*.json").Select(p => (p, File.ReadAllText(p))));
        foreach (var (key, thai) in pack.Entries)
        {
            if (!source.TryGetValue(key, out var expected)) continue;
            string[] patterns = [@"\{[^{}]+\}", @"<[^>]+>", @"\[c/[^:\]]+:", @"\[(?:i(?:/s[\d.]+)?|rg(?:/s[\d.]+)?|cbuff|cbuffplr|centeritem):[^\]]+\]"];
            Equal(patterns.Length, expected.Length, key);
            for (int i = 0; i < patterns.Length; i++)
            {
                var thaiTokens = Regex.Matches(thai, patterns[i]).Select(m => m.Value).ToList();
                // Thai has no grammatical plural, so a plural selector like {^0:day;days} may be dropped; any kept must match.
                Equal(string.Join('|', expected[i].Where(t => !t.StartsWith("{^") || thaiTokens.Contains(t)).Order()),
                    string.Join('|', thaiTokens.Order()), key);
            }
        }
        Equal("?", pack.Entries["Mods.BetterGuide.Classes.UnrankedBadge"]);
        True(pack.Entries["ItemTooltip.EncumberingStone"].Contains("<right>"));
    }

    private static Asset<DynamicSpriteFont>[] Fonts(string prefix) => Enumerable.Range(0, 5)
        .Select(i => (Asset<DynamicSpriteFont>)Activator.CreateInstance(typeof(Asset<DynamicSpriteFont>), PrivateInstance,
            null, [prefix + i], null)!).ToArray();
    private static void SetFonts(Asset<DynamicSpriteFont>[] fonts)
    {
        FontAssets.ItemStack = fonts[0]; FontAssets.MouseText = fonts[1]; FontAssets.DeathText = fonts[2];
        FontAssets.CombatText = [fonts[3], fonts[4]];
    }
    private static void Test(string name, Action action)
    {
        try { action(); Results.Add(new { name, passed = true }); Console.WriteLine("PASS " + name); }
        catch (Exception e)
        {
            failures++; Results.Add(new { name, passed = false, error = e.ToString() });
            Console.WriteLine("FAIL " + name + ": " + e);
        }
    }
    private static void True(bool value) { if (!value) throw new Exception("Assertion failed"); }
    private static void Equal<T>(T expected, T actual, string context = "")
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"{context}: expected {expected}, actual {actual}");
    }
    private static void Expect<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name);
    }
}

public static class AmbienceFixture
{
    public static Dictionary<int, string> FlavorsMorning = new() { [0] = "Unavailable" };
    public static Dictionary<int, string> FlavorsDay = new() { [0] = "Unavailable" };
    public static Dictionary<int, string> FlavorsEvening = new() { [0] = "Unavailable" };
    public static Dictionary<int, string> FlavorsNight = new() { [0] = "Unavailable" };
}
internal sealed class ChangedEngineFixture { internal void ReloadLanguage(string culture) { } }
internal sealed class ChangedCultureFixture { public string ActiveCulture { get; private set; } = "en-US"; }
internal static class ChangedNoxusFixture { private static int WrapText(string text, DynamicSpriteFont font) => 0; }
