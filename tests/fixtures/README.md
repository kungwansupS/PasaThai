# Token baseline

`source-tokens.json` เก็บเฉพาะ key กับ placeholder และ markup ที่คำแปลต้องรักษาไว้
สร้างจาก English inventory ที่ใช้ตรวจแพ็กเดิม ไม่ได้แนบข้อความภาษาอังกฤษที่ดึงจากเกมและม็อดอื่น

เมื่อตรวจต้นฉบับเวอร์ชันใหม่แล้ว ให้สร้าง baseline ใหม่ด้วย:

```powershell
python tools/make_token_baseline.py <path-to-audited-source-en.json>
```

ไฟล์ต้นฉบับต้องเป็น JSON แบบ `{"Localization.Key": "English text"}`
การผ่าน test หมายถึงรูปแบบข้อความยังตรงกับ baseline นี้ ไม่ได้ตรวจความหมายของคำแปล

ไฟล์ `.tmod` ที่ test ใช้ให้สร้างด้วย `tools/Package.ps1` จะอยู่ใน `.qa` และไม่เข้า Git
