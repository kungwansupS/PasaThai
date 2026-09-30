# Third-party notices

This standalone edition was adapted on 2026-09-22. It is not an upstream
ThaiLanguageLibrary release. The runtime is maintained within ThaiOfflineAllMods.

The project authors' rights in `icon.png` and `icon_workshop.png` are dedicated
to the public domain under CC0 1.0 Universal; see `LICENSE-ART` for the terms.
This does not change the licenses of the code, translations or fonts described below.

The font assets in `Assets/Fonts` and base translations in
`Assets/BaseTranslations` originate from Thai Localization Team's
ThaiLanguageLibrary (0.14.0 base translations and the local 0.12.4.2 font bundle), licensed under GNU GPL version 3.
The original license is included as `LICENSE.txt` and runtime C# source is included in the
mod package. Retain these notices when redistributing the modified package.

Upstream source: https://github.com/Thai-Localization-Team/ThaiLanguageLibrary

Base JSON was normalized to remove duplicate properties and trailing commas,
retaining the last property value used by the original Newtonsoft.Json loader.
Gameplay corrections are applied in the separate `Translations/th-TH.json` overlay.

This edition replaces the original language-menu IL patch, file export/settings
implementation, font replacement lifecycle, and inter-mod registration API.
Upstream translation and font credit remains with Thai Localization Team.

## Font build inputs

The retained font bundle consists of five compiled `.xnb` files. No `.ttf`, `.otf`,
`.spritefont` or font build project was available in the working source used for this copy.
The mod builds using the supplied `.xnb` files; this copy does not claim to contain everything
needed to regenerate the fonts. The preferred font source and any underlying font license
still need to be checked with the upstream maintainers before claiming complete asset-source coverage.

## Games and other mods

Terraria belongs to Re-Logic. Other mods and their names belong to their respective authors.
This repository does not redistribute their assemblies, decompiled source, or full English text inventories.
