# แก้ HUD / แม็พ ที่โค้ดสร้างตอนรัน ในหน้า Edit

## วิธีใหม่ (8 ต.ค. 2026 ดึก): จัด UI ตอนกด Play แล้วกดบันทึก — ใช้ได้ทุกหน้า (`UiLayout.cs`, สวิตช์ `SavedUiLayout`)
ใช้กับ UI ที่โค้ดจัดตำแหน่งตอนรัน (หน้าเลือกโหมด, หน้าเตรียมพร้อมรบ, โรงเก็บยาน/ไอเท็ม/ร้านค้า, ปุ่มมุมขวาบนในสนาม ฯลฯ) ซึ่งเดิมลากแก้แล้วกลับที่เดิม
1. กด **Play** แล้วไปหน้าที่อยากแก้ (เช่น เปิดโรงเก็บยาน > ไอเท็ม)
2. ใน **Hierarchy** เลือกวัตถุที่อยากย้าย (Ctrl+คลิก เลือกหลายชิ้นได้) แล้วลากในหน้า Scene หรือแก้ Pos X/Y, Width/Height ใน Inspector
3. **คลิกขวาที่วัตถุนั้นใน Hierarchy > UI Layout > Save Position** (ต้องกด *ก่อน* หยุด Play — ปกติ Unity ล้างทุกอย่างที่แก้ตอน Play)
   หรือเมนูด้านบน `GameObject > UI Layout > Save Position`
4. หยุด Play แล้วกด Play ใหม่ = ชิ้นนั้นอยู่ที่ใหม่ (บันทึกใน `Assets/Resources/UILayoutOverrides.json` ไฟล์นี้ไปกับ Build ด้วย)

- กลับไปใช้ตำแหน่งจากโค้ด: เลือกชิ้นนั้น > คลิกขวา > **UI Layout > Use Code Position Again** / ล้างทั้งหมด: **UI Layout > Clear ALL Saved Positions**
- บันทึกได้ทั้งตอน Play และในหน้า Edit (ของที่อยู่ใน Scene อยู่แล้ว)
- ย้าย "กล่องแม่" = ของข้างในย้ายตามทั้งหมด ไม่ต้องบันทึกทีละชิ้น
- ชิ้นที่ใช้ชื่อซ้ำ (เช่น การ์ดไอเท็ม `Item0`…`Item7`, ช่องนักบินห้องหลายคน) บันทึกตามลำดับช่อง: ย้าย `Item0` = การ์ดใบแรกทุกหน้า / ช่องนักบินที่ย้ายจะอยู่ที่เดิมทุกโหมด
- **ไม่ควรย้าย:** ตัวกรอบใหญ่ที่โค้ดปรับให้พอดีจอทุกเฟรม (`BattleHUD`, `ResultSurface`, `LobbySurface`) — ให้ย้ายของ *ข้างใน* แทน
- ตัวเลข/ข้อความ/สี/เปิดปิด ยังเป็นโค้ดคุมเหมือนเดิม (ระบบนี้จำแค่ ตำแหน่ง ขนาด จุดยึด และ Scale)
- ไม่มีไฟล์บันทึก หรือปิด `FeatureFlags.SavedUiLayout` = เหมือนเดิมทุกอย่าง


## หน้าตาใหม่ 8 ตุลาคม 2026 (สำคัญ: อ่านก่อนลากแก้)
- **หน้าเลือกวิธีเล่น (PlayMenu)** และ **ห้องรอ (Waiting Room)** ถูกจัดใหม่ด้วยโค้ดตอนกด Play (`LobbyManager.NewLayout.cs`) ตำแหน่งที่ลากใน Scene ของ 2 หน้านี้จะถูกทับตอนเล่น
  - อยากลากแก้เองใน Scene: ตั้ง `FeatureFlags.NewLobbyLayout = false` ก่อน / อยากเลื่อนตำแหน่งในแบบใหม่: แก้ตัวเลขใน `ApplyModeCardLayout` / `LayoutPrepRoom`
  - การจัดใหม่ไม่ถูกบันทึกลง Scene (กันปุ่มซ้อนเวลา Build Lobby Navigation ซ้ำ)
- **หน้าตั้งค่า** (ทั้งแบบต้นแบบใน Scene `BattleSettingsTemplate` และแบบโค้ด) ถูกย้ายแถวเข้ากล่องเลื่อนตอนเปิด (`BattleSettingsPanel.Unified.cs`) ปิดได้ด้วย `FeatureFlags.UnifiedSettings = false`
- หน้าหลัก: ปุ่ม HOW TO PLAY ถูกซ่อนและปุ่ม RANKED ย้ายมาแทนที่ตอนเล่น (`ApplyHomeRankedLayout` ใน `LobbyManager.Polish.cs`)

## การจัดเมนู Lobby ใหม่ (7 ตุลาคม 2026)

เปิด Unity รอคอมไพล์ แล้วเลือกเมนูด้านบน `Tools > Battlefield > Build Lobby Navigation`
คำสั่งจะสำรอง LobbyScene เดิมไว้ใน `RecoveryBackups/Navigation-วันเวลา` เปิดฉาก Lobby
สร้างเมนูใหม่ ตรวจว่าการสร้างซ้ำไม่เพิ่มปุ่มซ้อนและไม่ทับตำแหน่งปุ่มเล่นด่วนที่ผู้ใช้แก้ แล้วบันทึก Scene
หากกำลังแก้ Scene อื่นอยู่ Unity จะถามให้บันทึกก่อนเปลี่ยนฉาก

- หน้าหลัก: QUICK MATCH, CHOOSE MODE, HANGAR, HOW TO PLAY; แถวล่าง MISSIONS / PROFILE / SOCIAL
- CHOOSE MODE: สร้าง/เข้าห้อง, แรงค์, การ์ดโหมดเล่นคนเดียว 12 แบบ (แสดงตาม FeatureFlags), ความยาก, VS BOT, TRAINING
- HANGAR: SHIPS / SKILLS / UPGRADES / ITEMS / SHOP; สามแท็บท้ายเปิดระบบ Workshop เดิมเหนือหน้าโรงเก็บยาน
- ห้องรอ: ROOM SETTINGS เปิดกติกาแยก; แถวล่างคง LEAVE ROOM / READY / START BATTLE
- ปุ่ม Back/Esc ปิดหน้าที่อยู่ด้านบนก่อน; ปุ่มเดิมยังเรียกการซื้อ/เริ่มเกม/เปลี่ยนกติกาเดิม

ที่ Component LobbyManager มีเมนู `UI Preview/Show Mode Selection` และ `UI Preview/Show Room Rules`
สำหรับเปิดหน้าที่ต้องการลากจัดวาง (กดเมื่อไม่ได้ Play) แล้วกด Ctrl+S

พาธใน Hierarchy เริ่มจาก Panel ที่อ้างอิงในช่อง Main Panel / Waiting Room Panel / Inventory Panel:

| ส่วน | พาธใต้ Panel |
|---|---|
| ปุ่มหน้าหลัก | `LobbySurface/QuickMatch`, `ChooseMode`, `OpenHangar`, `Missions`, `Profile`, `Social` |
| หน้าเลือกโหมด | `LobbySurface/PlayMenu` |
| กติกาห้อง | `LobbySurface/RoomRulesMenu` |
| แท็บโรงเก็บยาน | `LobbySurface/ShipsTab`, `SkillsTab`, `WorkshopTab0` ถึง `WorkshopTab2` |

โค้ดหลัก: `Assets/Scripts/Managers/LobbyManager.Navigation.cs`
ตัวสร้าง Scene: `Assets/Editor/LobbyNavigationSetup.cs`
ตำแหน่งเริ่มต้นจะตั้งครั้งเดียว โดยใช้วัตถุลูก `NavigationLayoutV1` เป็นเครื่องหมาย
หลังจากนั้นลากปรับตำแหน่งได้ อย่าลบเครื่องหมายหรือเปลี่ยนชื่อปุ่มที่โค้ดใช้อ้างอิง
รายละเอียดที่เปลี่ยนตามข้อมูลใน Social, Workshop, Ranked และ Missions/Profile ยังสร้างใหม่ตอนเปิด/อัปเดตหน้า
จึงต้องแก้ layout ของรายการเหล่านั้นในไฟล์หน้าจอที่เกี่ยวข้อง ส่วนเมนูหลัก/เลือกโหมด/กติกาห้องแก้ใน Scene ได้

หลังจัดหน้าให้ลอง: เล่นด่วน, เปิด/ปิดเลือกโหมด, เลือก Campaign/ฝึก/บอท, เปิดทุกแท็บโรงเก็บยาน,
กลับจาก Workshop, สร้างห้อง, เปลี่ยนกติกาแล้วกด Ready ใหม่, เปิด Social จากห้องรอ และใช้ Back/Esc
ตรวจภาพบนมือถือทั้งจอ 16:9 และจอกว้างก่อนสร้างบิลด์ใช้งานจริง

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
