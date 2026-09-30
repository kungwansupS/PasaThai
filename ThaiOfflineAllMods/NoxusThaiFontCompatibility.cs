#nullable enable

using System;
using System.Globalization;
using System.Reflection;
using System.Linq;
using Microsoft.Xna.Framework;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace ThaiOfflineAllMods;

/// <summary>
/// Makes the custom story fonts and line wrapping in Wrath of the Gods usable
/// with Thai. The upstream mod already falls back to Terraria fonts for Chinese;
/// this applies the equivalent behavior for the added th-TH culture.
/// </summary>
public sealed class NoxusThaiFontCompatibility : ModSystem
{
    private const string FontRegistryTypeName = "NoxusBoss.Assets.Fonts.FontRegistry";
    private const string SolynDialogTypeName =
        "NoxusBoss.Core.Graphics.UI.SolynDialogue.SolynDialogUIManager";

    private static readonly string[] FontPropertyNames =
    [
        "NamelessDeityText",
        "AvatarPoemText",
        "DraedonText",
        "SolynText",
        "SolynTextItalics",
        "SolynFightDialogue",
    ];

    private delegate DynamicSpriteFont OrigFontGetter(object self);
    private delegate DynamicSpriteFont FontGetterHook(OrigFontGetter orig, object self);

    private delegate string OrigWrapText(string text, DynamicSpriteFont font);
    private delegate string WrapTextHook(
        OrigWrapText orig,
        string text,
        DynamicSpriteFont font
    );

    private delegate void OrigRenderTextLineByLine(
        DynamicSpriteFont font,
        string text,
        Vector2 drawPosition,
        Color color,
        float scale,
        float shakeFactor
    );

    private delegate void RenderTextLineByLineHook(
        OrigRenderTextLineByLine orig,
        DynamicSpriteFont font,
        string text,
        Vector2 drawPosition,
        Color color,
        float scale,
        float shakeFactor
    );

    private static bool ThaiIsActive =>
        Language.ActiveCulture?.Name.Equals("th-TH", StringComparison.OrdinalIgnoreCase) == true;

    public override void Load()
    {
        if (!ModLoader.TryGetMod("NoxusBoss", out Mod noxusBoss))
        {
            return;
        }

        int hookedFonts = HookCustomFontGetters(noxusBoss.Code);
        int hookedTextMethods = HookSolynDialogueMethods(noxusBoss.Code);
        Mod.Logger.Info(
            $"Installed Thai compatibility for NoxusBoss " +
            $"({hookedFonts} fonts, {hookedTextMethods} text methods)."
        );
        if (hookedFonts != FontPropertyNames.Length || hookedTextMethods != 2)
            Mod.Logger.Warn($"NoxusBoss {noxusBoss.Version}: some Thai font/dialogue hooks are unavailable. The core translation pack remains active.");
    }

    private int HookCustomFontGetters(Assembly assembly)
    {
        Type? registryType = assembly.GetType(FontRegistryTypeName, throwOnError: false);
        if (registryType is null)
        {
            return 0;
        }

        int hooked = 0;
        foreach (string propertyName in FontPropertyNames)
        {
            MethodInfo? getter = registryType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "get_" + propertyName && m.ReturnType == typeof(DynamicSpriteFont) &&
                    !m.ContainsGenericParameters && m.GetParameters().Length == 0);
            if (getter is null || getter.IsStatic || getter.ReturnType != typeof(DynamicSpriteFont) || getter.GetParameters().Length != 0)
            {
                continue;
            }

            if (TryHook(getter, new FontGetterHook(UseThaiCapableFont)))
                hooked++;
        }
        return hooked;
    }

    private int HookSolynDialogueMethods(Assembly assembly)
    {
        Type? dialogType = assembly.GetType(SolynDialogTypeName, throwOnError: false);
        if (dialogType is null)
        {
            return 0;
        }

        int hooked = 0;
        MethodInfo? wrapText = FindCompatibleMethod(dialogType, "WrapText", typeof(string), [typeof(string), typeof(DynamicSpriteFont)]);
        if (wrapText is not null)
        {
            if (TryHook(wrapText, new WrapTextHook(WrapThaiText)))
                hooked++;
        }

        MethodInfo? renderText = FindCompatibleMethod(dialogType, "RenderTextLineByLine", typeof(void),
            [typeof(DynamicSpriteFont), typeof(string), typeof(Vector2), typeof(Color), typeof(float), typeof(float)]);
        if (renderText is not null)
        {
            if (TryHook(renderText, new RenderTextLineByLineHook(RenderThaiTextByGrapheme)))
                hooked++;
        }
        return hooked;
    }

    internal static MethodInfo? FindCompatibleMethod(Type type, string name, Type result, Type[] parameters) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == name && m.ReturnType == result &&
                !m.ContainsGenericParameters && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters));

    private bool TryHook(MethodInfo method, Delegate hook)
    {
        try
        {
            MonoModHooks.Add(method, hook);
            return true;
        }
        catch (Exception e)
        {
            Mod.Logger.Warn($"NoxusBoss Thai hook '{method.Name}' was skipped: {e.Message}");
            return false;
        }
    }

    private static DynamicSpriteFont UseThaiCapableFont(
        OrigFontGetter orig,
        object self
    )
    {
        return ThaiIsActive ? FontAssets.DeathText.Value : orig(self);
    }

    private static string WrapThaiText(
        OrigWrapText orig,
        string text,
        DynamicSpriteFont font
    )
    {
        if (!ThaiIsActive)
        {
            return orig(text, font);
        }

        // NoxusBoss normally sends a space-delimited string to Terraria's wrapper.
        // Thai often has no spaces, so Terraria treats the whole sentence as one
        // oversized word and inserts a hyphen after nearly every glyph.
        const float maxWidth = 1820f; // 700 / DialogScale(0.4) * 1.04
        var lines = new System.Collections.Generic.List<string>();
        var currentLine = new System.Text.StringBuilder();
        float currentWidth = 0f;

        TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(text.Replace("\r\n", "\n"));
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            if (element is "\r")
            {
                continue;
            }
            if (element is "\n")
            {
                lines.Add(currentLine.ToString().TrimEnd());
                currentLine.Clear();
                currentWidth = 0f;
                continue;
            }

            float elementWidth = font.MeasureString(element).X;
            if (currentLine.Length > 0 && currentWidth + elementWidth > maxWidth)
            {
                lines.Add(currentLine.ToString().TrimEnd());
                currentLine.Clear();
                currentWidth = 0f;
                if (string.IsNullOrWhiteSpace(element))
                {
                    continue;
                }
            }

            currentLine.Append(element);
            currentWidth += elementWidth;
        }

        if (currentLine.Length > 0 || lines.Count == 0)
        {
            lines.Add(currentLine.ToString().TrimEnd());
        }
        return string.Join('\n', lines);
    }

    private static void RenderThaiTextByGrapheme(
        OrigRenderTextLineByLine orig,
        DynamicSpriteFont font,
        string text,
        Vector2 drawPosition,
        Color color,
        float scale,
        float shakeFactor
    )
    {
        if (!ThaiIsActive)
        {
            orig(font, text, drawPosition, color, scale, shakeFactor);
            return;
        }

        TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            Vector2 displacement = Main.rand.NextVector2Circular(shakeFactor, shakeFactor) * scale;
            Vector2 position = drawPosition + displacement;
            Utils.DrawBorderStringFourWay(
                Main.spriteBatch,
                font,
                element,
                position.X,
                position.Y,
                color,
                Color.Black,
                Vector2.Zero,
                scale
            );
            drawPosition.X += ChatManager.GetStringSize(font, element, Vector2.One).X * scale;
        }
    }
}
