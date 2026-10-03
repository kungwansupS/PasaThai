# ภาษาไทย [Calamity]

แพ็กภาษาไทยสำหรับ Terraria / tModLoader มีคำแปลและฟอนต์ในตัว
เปิดหรือปิดภาษาไทยได้ที่ Mod Configuration โดยไม่ต้องรีโหลดม็อด
เมื่อปิด จะกลับไปใช้ภาษาที่เลือกไว้ก่อนหน้า

แพ็กนี้ทำงานได้เอง ให้ปิด ThaiLanguageLibrary เมื่อใช้แพ็กนี้
เครดิตคำแปลพื้นฐานและฟอนต์ยังเป็นของ Thai Localization Team

## Build

ใช้ .NET SDK 8 และ tModLoader สาย 2026.8 สำหรับ Terraria 1.4.4.9
ชุดนี้ตรวจด้วย tModLoader 2026.8.3.0 ไม่รับประกันกับสายอื่นที่ยังไม่ได้ทดสอบ
ตั้ง `$tml` ให้ตรงกับที่ติดตั้งในเครื่อง:

```powershell
$tml = 'X:/SteamLibrary/steamapps/common/tModLoader'
dotnet build ThaiOfflineAllMods/ThaiOfflineAllMods.csproj -c Release -p:TMLInstallPath="$tml" -p:BuildMod=false
```

สร้างไฟล์ม็อดลง `.qa`:

```powershell
./tools/Package.ps1 -TML $tml -Out .qa
```

หรือคัดลอกโฟลเดอร์ชั้นใน `ThaiOfflineAllMods` ไปไว้ใน ModSources แล้วใช้ Build + Reload ในเกม

## ทดสอบ

สร้างไฟล์ม็อดตามขั้นตอนด้านบนก่อน แล้วรัน:

```powershell
dotnet build tests/ThaiRuntimeTests.csproj -p:TMLInstallPath="$tml" -p:BuildMod=false
dotnet tests/bin/Debug/net8.0/ThaiRuntimeTests.dll "$tml"
```

ชุดทดสอบใช้ระบบภาษาจริงของ tModLoader ใน process แยก ตรวจการสลับภาษา คืนฟอนต์
การ unload เมื่อปิดไฟล์ม็อดแล้ว และ placeholder ในคำแปล ไม่มีการเปิดเกมหรือใช้เซฟผู้เล่น
baseline ของ placeholder มาจากชุดตรวจเดิม ไม่ได้ยืนยันความหมายของคำแปลกับม็อดรุ่นใหม่ทุกครั้ง
ดูรายละเอียดใน [tests/fixtures/README.md](tests/fixtures/README.md)

## แก้คำแปล

ซอร์สม็อดอยู่ใน `ThaiOfflineAllMods/` คำแปลที่ปรับเพิ่มอยู่ใน `Translations/`
และคำแปลพื้นฐานอยู่ใน `Assets/BaseTranslations/`
รายละเอียดระบบภาษาและข้อจำกัดอยู่ใน [README ของม็อด](ThaiOfflineAllMods/README.md)

## สิทธิ์

ไอคอนและกรอบของม็อดใช้ [CC0 1.0](LICENSE-ART)
นำไปใช้ แก้ไข หรือแจกต่อได้ รวมถึงใช้เชิงพาณิชย์ ไม่ต้องขออนุญาตหรือให้เครดิต
กรอบแบบ PNG พื้นหลังโปร่งใสแยกไว้ใน [art/icon-frame](art/icon-frame/)

โค้ดและคำแปลเผยแพร่ภายใต้ [GNU GPL version 3](LICENSE)
คำแปลพื้นฐานและฟอนต์มาจาก
[ThaiLanguageLibrary](https://github.com/Thai-Localization-Team/ThaiLanguageLibrary)
ดูเครดิตและที่มาของไฟล์ใน [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)
