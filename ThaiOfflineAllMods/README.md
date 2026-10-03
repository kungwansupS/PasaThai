# ภาษาไทย [Calamity] 1.4.2

แพ็กนี้มีระบบภาษาและฟอนต์ในตัวแล้ว ไม่ต้องเปิด ThaiLanguageLibrary ร่วมด้วย
เปิด/ปิดภาษาไทยได้ที่ Mod Configuration → ภาษาไทย → เปิดภาษาไทย โดยไม่ต้องรีโหลดม็อด
ค่าเริ่มต้นคือเปิดภาษาไทย ปิดเพื่อกลับไปใช้ภาษาที่เลือกไว้ก่อนหน้า
ใช้การตั้งค่านี้แทนปุ่มเพิ่มภาษาในเมนูเดิมของเกม เพื่อหลีกเลี่ยงการแก้ IL ตามหมายเลขตัวแปร

อัปเกรดจาก 1.3.1: อัปเดต ThaiOfflineAllMods แล้วปิด ThaiLanguageLibrary
หากมีม็อดอื่นที่ระบุว่าต้องใช้ไลบรารีนั้นจริง ให้ตรวจความเข้ากันได้ก่อนเปิดร่วมกัน
ไม่ต้องเปลี่ยนโลก ตัวละคร หรือไฟล์คำแปลเดิม และไม่ต้องถอนการสมัคร Workshop

## Runtime design

- No ThaiLanguageLibrary assembly/API dependency, exported utility UI, or custom language-state file.
- Base Thai data and five fonts are bundled with upstream credit and GPL-3.0 license.
- The pack owns only the Thai culture it registers and the font slots it replaces.
- JSON is loaded once in deterministic order: base translations, then pack overrides.
- One language-source hook calls the original engine method and applies entries using
  the public JSON loader before tModLoader expands copy commands and recalculates bound text.
- Missing keys for unloaded mods are skipped, not registered or reported as successfully translated.
- Startup checks an actual translated key and the active language; an unsupported activation cannot silently succeed.
- Optional NoxusBoss and TerrariaAmbience hooks validate signatures. Changed optional hooks warn and preserve the core pack.
- No runtime network calls.
- During unload, restore culture state and fonts without loading any translations.
  tModLoader closes mod packages before calling `Mod.Unload`, then reloads text
  through `UnloadModdedEntries` after it clears the old mods. Calling `SetLanguage`
  during `Mod.Unload` is unsafe even if it succeeds while the mod is fully loaded.
- Restore the culture of the unload caller as well as the main thread: a reload
  worker captures the old Thai ExecutionContext before unload starts. Leaving it
  in that culture breaks culture-sensitive key parsing in Boss Checklist 2.2.4.

## Update boundaries

An update to a disabled ThaiLanguageLibrary does not change this pack's behavior.
Running both language runtimes is outside the guarantee: an independent mod can still replace engine hooks or fonts.
The remaining tModLoader internals are `ReloadLanguage(bool)`, the `ActiveCulture`
setter used only during unload, and cleanup of `_legacyCultures`. Their shape is checked before use.
The generated hook also depends on `LoadActiveCultureTranslationsFromSources`.
A tModLoader engine update can still require a new build; future versions cannot be promised compatible without testing.

Translation contents also need review when translated mods change their English text.
The regression test compares the pack's placeholders and UI tokens against the audited English inventory.
It does not claim to automatically validate the meaning of newly changed source text.

## Verification

From the `thai` directory:

```powershell
dotnet build ThaiOfflineAllMods/ThaiOfflineAllMods.csproj -p:BuildMod=false
dotnet build tests/ThaiRuntimeTests.csproj
dotnet tests/bin/Debug/net8.0/ThaiRuntimeTests.dll
```

Tests execute the actual tModLoader localization pipeline, culture ownership,
font restoration, repeated switching, bound references, malformed data, optional
method-signature changes, and packaged data validation.
The closed-package regression reproduces the real unload ordering and checks both
English and a non-English previous language, plus removal of stale Thai text by
the engine's final cleanup. Native reload QA also exercises actual mod unloading.
`tests/NativeQa` is excluded from the production mod and runs only in a disposable profile.

ไอคอนของม็อดใช้ [CC0 1.0](LICENSE-ART) ใช้ แก้ไข และแจกต่อได้โดยไม่ต้องขออนุญาตหรือให้เครดิต
โค้ด คำแปล และฟอนต์ยังใช้ GPL-3.0 ดูที่มาใน [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)
และ [LICENSE.txt](LICENSE.txt)
