#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using MonoMod.Cil;
using Terraria.ModLoader;

namespace ThaiOfflineAllMods;

/// <summary>
/// Translates TerrariaAmbience's forecast overlay. Those strings are embedded
/// directly in code instead of its localization files, so a normal language
/// pack cannot reach them.
/// </summary>
public sealed class TerrariaAmbienceThaiCompatibility : ModSystem
{
    private readonly System.Collections.Generic.List<FlavorOverride> flavorOverrides = new();
    private const string DisplayTypeName =
        "TerrariaAmbience.Content.AmbientAndMore.AmbientDisplaySystem";

    private static readonly Dictionary<string, string[]> FlavorTranslations = new()
    {
        ["FlavorsMorning"] =
        [
            "ยังไม่พร้อม",
            "เสียงจิ้งหรีด",
            "เสียงจิ้งหรีดร้องระงม",
            "เสียงจิ้งหรีดและจักจั่น",
            "เสียงจิ้งหรีดดัง",
            "เสียงจักจั่นและแมลง",
            "เสียงจิ้งหรีดและจักจั่นร้องระงม",
        ],
        ["FlavorsDay"] =
        [
            "ยังไม่พร้อม",
            "เสียงอีกาและนกร้อง",
            "เสียงนกร้อง",
            "เสียงนกร้องดัง",
            "เสียงนกร้องเบาๆ",
            "เสียงจิ้งหรีดและแมลงเบาๆ",
            "เสียงนกร้องต่อเนื่อง",
        ],
        ["FlavorsEvening"] =
        [
            "ยังไม่พร้อม",
            "เสียงจักจั่น",
            "เสียงจิ้งหรีดและจักจั่น",
            "เสียงจักจั่นดัง",
            "เสียงจิ้งหรีดร้องระงม",
            "เสียงจิ้งหรีดและจักจั่นดังเป็นระยะ",
            "เสียงสัตว์เล็กเบาๆ มากมาย",
        ],
        ["FlavorsNight"] =
        [
            "ยังไม่พร้อม",
            "เสียงจิ้งหรีดเบาๆ",
            "เสียงจิ้งหรีดร้องระงม",
            "เสียงจิ้งหรีดร้องจิ๊บๆ",
            "เสียงจักจั่นดัง",
            "เสียงสัตว์เล็กดังมากมาย",
            "เสียงจิ้งหรีดและกบ",
        ],
    };

    private static readonly Dictionary<string, string> UiLiteralTranslations = new()
    {
        ["Morning: "] = "เช้า: ",
        ["\nDaytime: "] = "\nกลางวัน: ",
        ["Evening: "] = "เย็น: ",
        ["\nNight: "] = "\nกลางคืน: ",
        ["Hide ambient forecast"] = "ซ่อนพยากรณ์เสียงบรรยากาศ",
        ["Display ambient forecast"] = "แสดงพยากรณ์เสียงบรรยากาศ",
    };

    public override void Load()
    {
        if (!ModLoader.TryGetMod("TerrariaAmbience", out Mod ambienceMod))
        {
            return;
        }

        Type? displayType = ambienceMod.Code.GetType(DisplayTypeName, throwOnError: false);
        if (displayType is null)
        {
            Mod.Logger.Warn("TerrariaAmbience forecast type was not found; compatibility skipped.");
            return;
        }

        int translatedFlavors = CaptureFlavorDictionaries(displayType);
        MethodInfo? drawMethod = displayType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "PostDrawInterface" && m.ReturnType == typeof(void) &&
                m.GetParameters() is [{ ParameterType: var type }] && type == typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch));
        bool uiHooked = false;
        if (drawMethod?.ReturnType == typeof(void) && drawMethod.GetParameters() is [{ ParameterType: var parameter }] &&
            parameter == typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch))
        {
            try
            {
                MonoModHooks.Modify(drawMethod, ReplaceUiLiterals);
                uiHooked = true;
            }
            catch (Exception e)
            {
                Mod.Logger.Warn($"TerrariaAmbience forecast hook is not compatible: {e.Message}");
            }
        }

        Mod.Logger.Info(
            $"Installed Thai compatibility for TerrariaAmbience " +
            $"({translatedFlavors} forecast descriptions, UI labels: {uiHooked})."
        );
        if (!uiHooked)
            Mod.Logger.Warn($"TerrariaAmbience {ambienceMod.Version}: forecast UI signature is unavailable; original labels are retained.");
    }

    internal int CaptureFlavorDictionaries(Type displayType)
    {
        int translated = 0;
        foreach ((string fieldName, string[] values) in FlavorTranslations)
        {
            FieldInfo? field = displayType.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.Static
            );
            if (field?.GetValue(null) is not IDictionary dictionary || dictionary.IsReadOnly)
            {
                continue;
            }

            for (int index = 0; index < values.Length; index++)
            {
                if (!dictionary.Contains(index) || dictionary[index] is not string original)
                    continue;
                flavorOverrides.Add(new FlavorOverride(dictionary, index, original, values[index]));
                translated++;
            }
        }
        return translated;
    }

    public override void OnLocalizationsLoaded() => UpdateFlavorLanguage(ThaiRuntime.ThaiIsActive);

    internal void UpdateFlavorLanguage(bool thai)
    {
        foreach (var entry in flavorOverrides)
            entry.Apply(thai);
    }

    public override void Unload()
    {
        UpdateFlavorLanguage(false);
        flavorOverrides.Clear();
    }

    internal static string TranslateLiteral(string value) =>
        ThaiRuntime.ThaiIsActive && UiLiteralTranslations.TryGetValue(value, out var thai) ? thai : value;

    private static void ReplaceUiLiterals(ILContext il)
    {
        foreach (var instruction in il.Instrs.Where(i => i.OpCode == Mono.Cecil.Cil.OpCodes.Ldstr &&
            i.Operand is string text && UiLiteralTranslations.ContainsKey(text)).ToArray())
        {
            var cursor = new ILCursor(il);
            cursor.Goto(instruction, MoveType.After);
            cursor.EmitDelegate<Func<string, string>>(TranslateLiteral);
        }
    }

    private sealed record FlavorOverride(IDictionary Dictionary, int Index, string Original, string Thai)
    {
        internal void Apply(bool thai)
        {
            if (Dictionary[Index] is string current && (current == Original || current == Thai))
                Dictionary[Index] = thai ? Thai : Original;
        }
    }
}
