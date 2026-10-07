# แก้ HUD / แม็พ ที่โค้ดสร้างตอนรัน ในหน้า Edit

เดิม HUD ในฉากต่อสู้ หน้าผลแมตช์ หน้าตั้งค่า มินิแม็พ และของบางส่วนในแม็พ ถูกโค้ดสร้างตอนกด Play เท่านั้น
หน้า Edit จึงไม่มีให้เห็น ตอนนี้กดเมนูเพื่อ "บันทึกลง Scene" ได้แล้ว หลังจากนั้นแก้ตำแหน่ง ขนาด สี ฟอนต์ ข้อความคงที่ ได้ใน Scene/Inspector
และตอนรันเกมจะใช้ของที่บันทึกไว้ (ถ้ายังไม่กดเมนู เกมทำงานแบบเดิมทุกอย่าง)

## วิธีใช้ (ทำครั้งเดียว)

1. ปิด Play mode แล้วเปิด `Assets/Scenes/SampleScene.unity`
2. เลือกวัตถุที่มี Component **GameplayManager**
3. ใน Inspector คลิกขวาที่หัว Component **GameplayManager** (หรือกดปุ่ม ⋮) แล้วเลือก
   - `Editable/0. Build Everything into Scene` = ทำทั้งหมดด้านล่างในครั้งเดียว
   - `Editable/1. Build Battle HUD into Scene` = HUD, หน้าผลแมตช์, มินิแม็พ, ป้ายชื่อยาน, ข้อความ KILL
   - `Editable/2. Build Map Layouts into Scene` = ของในแม็พ 0 (แมงกะพรุน), 1 (ปริซึม), 2 (หุ่นยนต์)
   - `Editable/3. Build Settings Panel Template` = หน้าตั้งค่าตอนแข่ง
4. **Save Scene** (Ctrl+S)
5. ล็อบบี้: เปิด `LobbyScene` → คลิกขวาที่ **LobbyManager** → `UI Preview/Build Settings Panel Template` → Save

ถ้าผลไม่ถูกใจ กด Ctrl+Z หรือปิด Scene โดยไม่ Save ได้ (ไฟล์โค้ดเดิมสำรองไว้ที่ `Backup-before-editable-HUD-20261006`)

## หาของที่บันทึกไว้ได้ที่ไหน

| สิ่งที่อยากแก้ | อยู่ใน Hierarchy |
|---|---|
| HUD ทั้งหมด (เวลา คะแนน จอย ปุ่มยิง ปุ่มสกิล) | `Canvas/BattleHUD` |
| มินิแม็พ | `Canvas/BattleHUD/ArenaMinimap` |
| นับถอยหลัง / HIT / ลูกศรโดนยิง / หน้ายานพัง | `BattleHUD/BattleCountdown`, `HitConfirmation`, `IncomingDamage`, `RespawnPanel` (ซ่อนอยู่ ติ๊กเปิดเพื่อดู) |
| ป้ายชื่อเหนือยาน | `BattleHUD/ShipNameplates/ShipNameplate_Template` |
| ข้อความ KILL +1 | `Canvas/KillMessage_Template` |
| หน้าผลแมตช์ | `ResultSurface` ใต้วัตถุที่ใส่ไว้ในช่อง Result Panel ของ GameplayManager |
| หน้าตั้งค่า | `BattleSettingsTemplate` (วัตถุบนสุดของ Scene) |
| แม็พ 0 | `Map0_Layout/EnergyCoreArtwork`, `FloatingJellyfish` |
| แม็พ 1 | `Map1_Layout/PrismPlayableLayout` |
| แม็พ 2 | `Map2_Layout/RockObstacles`, `TurretObstacles`, `RedCoreObstacles` |

วัตถุที่มี Component **EditableTemplate** คือต้นแบบ: ติ๊กเปิดเพื่อแก้ได้เลย ตอนกด Play มันซ่อนตัวเองอัตโนมัติ

## ข้อควรรู้

- **โค้ดยังคุมค่าที่เปลี่ยนระหว่างเล่น:** เช่น ตัวเลขเวลา คะแนน เลือด สีหลอดเลือด เปิด/ปิดตามสถานะ ตำแหน่งป้ายชื่อที่ตามยาน
  ขนาดกรอบ BattleHUD ที่ปรับให้พอดีจอ ให้แก้ของ "ข้างใน" BattleHUD ไม่ใช่ตัว BattleHUD เอง
- **GravityWell ในแม็พ 0** ผูกกับแรงดึงจริงในเกม ย้ายในหน้า Edit แล้วตอนรันจะกลับที่เดิม (ย่อ/ขยายได้)
- **แม็พ 2** มี Component **EditableLayout** = ตำแหน่งหิน/ป้อม/แกนพลังงานใน Scene คือของจริง ลบ Component ออกถ้าอยากกลับไปใช้ตำแหน่งจากโค้ด
- **แม็พมีผลกับการเล่นออนไลน์:** ย้ายสิ่งกีดขวางแล้ว ทั้งสองเครื่องต้องใช้ Build เวอร์ชันเดียวกัน
- **ยังสร้างด้วยโค้ดอย่างเดียว:** กำแพงขอบสนามและกล่องชนที่มองไม่เห็น (เป็นกติกาขนาดสนาม), เศษหินหมุนวนแม็พ 0,
  หัวป้อมปืนที่ตัดจากภาพตอนรัน, ยาน/กระสุน/สกิล/อันตรายในแม็พ (แก้ที่ Prefab ใน `Assets/Resources`)
- **ถ้าอยากสร้างใหม่:** ลบ `BattleHUD` และ `ResultSurface` แล้วกดเมนูอีกครั้ง

## ไฟล์ที่เปลี่ยน

`GameplayManager.cs`, `GameplayManager.HUD.cs`, `GameplayManager.StatusUI.cs`, `GameplayManager.Maps.cs`,
`MapHazardManager.cs`, `LobbyManager.Views.cs`, `BattleSettingsPanel.cs`, `RadarMinimap.cs`, `ControlRingGraphic.cs`
และไฟล์ใหม่ `EditableTemplate.cs`, `EditableLayout.cs`
