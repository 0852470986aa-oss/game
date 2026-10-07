# คู่มือเริ่มแก้/เพิ่มฟีเจอร์ใน Battlefield of the Stars

คู่มือนี้ทำไว้สำหรับสมาชิกใหม่หรือคนที่ยังไม่คุ้นโปรเจกต์ ใช้ตั้งแต่รับโจทย์จากอาจารย์จนถึงทดสอบงาน **เป็นคู่มืออ้างอิง ไม่ได้หมายความว่าฟีเจอร์ตัวอย่างด้านล่างถูกเพิ่มในเกมแล้ว** โค้ดตัวอย่างบางส่วนเป็นโครงให้ปรับ ห้ามคัดลอกทั้งก้อนโดยไม่ตรวจไฟล์จริงและ Console

## 1. เปิดโปรเจกต์และหาไฟล์

โฟลเดอร์โปรเจกต์บนเครื่องนี้:

```text
C:\Users\08524\Documents\BattlefieldOfTheStarsprojactGame\My project
```

เปิด Unity Hub แล้วเลือกโฟลเดอร์นี้ จากนั้นรอ Unity import/compile เสร็จก่อนแก้โค้ด เปิดไฟล์ C# ได้จาก Project window ใน Unity หรือเปิดโฟลเดอร์ `Assets/Scripts` ด้วย Visual Studio/VS Code

Scene หลักตามลำดับ:

1. `Assets/Scenes/LoginScene.unity` — เข้าใช้ Google/Guest
2. `Assets/Scenes/LobbyScene.unity` — โปรไฟล์ คลัง ห้อง และเมนู
3. `Assets/Scenes/SampleScene.unity` — การแข่งขัน HUD และแผนที่

ข้อมูลยาน/สกิลอยู่ `Assets/Scripts/BattleLoadoutCatalog.cs`; ค่ากลางดาเมจ/สถานะอยู่ `Assets/Scripts/BattleBalance.cs`. ชื่อ `*.Views.cs`, `*.Results.cs` และ `*.Maps.cs` บางตัวเป็น `partial class` ของ Manager เดิม—**อย่า Add Component ซ้ำใน Scene** เพียงเพราะเห็นไฟล์เพิ่ม

คู่มือพาธแบบเต็มอยู่ `CODE_GUIDE_TH.md`; แผนแบ่งอ่านกับเพื่อนอยู่ `EXAM_STUDY_SPLIT_TH.md`; ตัวอย่างประวัติแมตช์ละเอียดอยู่ `EXAMPLE_ADD_MATCH_HISTORY_TH.md`

## 2. ก่อนแก้ ให้แปลงคำสั่งเป็นข้อกำหนด

เขียนตอบตัวเองให้ครบก่อนเปิดไฟล์:

| คำถาม | ตัวอย่าง: “เพิ่มหน้าประวัติแมตช์” |
|---|---|
| ผู้ใช้ทำอะไร | กดปุ่ม MATCH HISTORY ที่หน้า Lobby |
| ระบบควรเกิดอะไร | เปิดหน้ารายการของบัญชีที่ล็อกอิน |
| ใช้ข้อมูลจากไหน | Firebase `match_history` |
| แสดงอะไร | แผนที่ ผล เวลา รางวัล |
| เงื่อนไขผิดพลาด | ไม่มีข้อมูล/ออฟไลน์/permission denied ต้องมีข้อความบอก |
| ขอบเขตที่ห้ามเปลี่ยน | ไม่แตะกติกาแมตช์หรือสูตรรางวัล |
| ทดสอบผ่านอย่างไร | ผู้เล่น A/B เห็นผลถูกฝั่ง; เปิดหน้าไม่สร้าง record ใหม่ |

แยกงานเป็นสามชั้นเสมอ: **ข้อมูล** (Firebase/catalog), **พฤติกรรม** (gameplay/network), และ **หน้าจอ** (UI). งานหนึ่งอาจแตะหลายไฟล์ แต่แต่ละไฟล์ควรมีหน้าที่ชัด อย่าใส่ทุกอย่างไว้ใน `Update()` หรือ Manager เดียวโดยไม่จำเป็น

## 3. แผนที่ไฟล์ที่ใช้บ่อย

พาธทุกแถวเริ่มจากโฟลเดอร์โปรเจกต์ด้านบน:

| งาน | เริ่มดูไฟล์ | ไฟล์ที่อาจต้องแตะเพิ่ม |
|---|---|---|
| ปุ่ม/หน้าจอใน Lobby | `Assets/Scripts/Managers/LobbyManager.Views.cs` | `LobbyManager.cs`, `LobbyManager.Inventory.cs`, `LobbyManager.Rooms.cs` |
| ปุ่ม/หน้าต่างใน Gameplay | `Assets/Scripts/Managers/GameplayManager.HUD.cs` | `GameplayManager.cs`, `GameplayManager.StatusUI.cs`, `BattleSettingsPanel.cs` |
| Login Google/Guest | `Assets/Scripts/Managers/LoginManager.cs` | `FirebaseManager.cs`, Scene/Prefab ของ Login |
| อ่าน/เขียนข้อมูลบัญชี | `Assets/Scripts/Managers/FirebaseManager.cs` | `LobbyManager.Views.cs`, `LobbyManager.Inventory.cs`, `GameplayManager.Results.cs` |
| ชื่อ/HP/ความเร็ว/โจมตี/ภาพยาน | `Assets/Scripts/BattleLoadoutCatalog.cs` | `PlayerController.cs`, `LobbyManager.Views.cs` |
| สกิล/คูลดาวน์ | `BattleLoadoutCatalog.cs` | `BattleBalance.cs`, `PlayerController.Skills.cs`, `SkillController.cs` |
| ดาเมจ/เวลา Stun/Shield/Poison | `Assets/Scripts/BattleBalance.cs` | `PlayerController.Skills.cs`, `HazardController.cs` |
| กระสุนและจุดยิง | `Assets/Scripts/PlayerController.cs` | `BulletController.cs`, `AutoTurret.cs`, Prefab |
| ป้อมยิง | `Assets/Scripts/AutoTurret.cs` | Prefab/Editor setup, `BulletController.cs`, `BattleBalance.cs` |
| layout/ขอบ/จุดเกิด/สิ่งกำบัง | `Assets/Scripts/Managers/GameplayManager.Maps.cs` | `Assets/Editor/MapLayoutSetup.cs` |
| สร้างฉาก/สุ่ม Hazard | `Assets/Scripts/Managers/MapHazardManager.cs` | `HazardController.cs`, `BattleBalance.cs`, Prefab |
| การชน/ผลของ Hazard | `Assets/Scripts/Managers/HazardController.cs` | `PlayerController.Health.cs`, `BattleBalance.cs` |
| ภาพไอพ่น/เอฟเฟกต์ยาน | `Assets/Scripts/PlayerController.Visuals.cs` | ไฟล์ใน `Assets/Resources/Images/VFX/` และ `.meta` |
| เสียง | `Assets/Scripts/Managers/AudioManager.cs` | `BattleSettingsPanel.cs`, ไฟล์เสียงใต้ `Assets/Resources/` |
| ภาพเหรียญ/บึงใน UI | `Assets/Scripts/PolishSprites.cs` | `Assets/Resources/Images/` |
| ออกจากระบบ | `Assets/Scripts/Managers/LobbyManager.cs` (`OnLogoutButtonClicked`) | `FirebaseManager.cs` (`Logout`), `BattleSettingsPanel.cs` |
| สียาน | `ShipPaint` ใน `Assets/Scripts/BattleLoadoutCatalog.cs` | `LobbyManager.Views.cs` (`BuildPaintPicker`), `LobbyManager.Rooms.cs`, `PlayerController.Cosmetics.cs` |
| อีโมต | `Assets/Scripts/PlayerController.Cosmetics.cs` | `GameplayManager.HUD.cs` (`BuildEmoteControls`, `ShowEmote`) |
| แจ้งยิงโดน/ทิศที่โดนยิง/ตัวเลขดาเมจ | `Assets/Scripts/PlayerController.Health.cs` | `GameplayManager.cs`, `FloatingText.cs` |
| คริสตัลสะท้อนกระสุน | `Assets/Scripts/PrismReflector.cs` | `BulletController.cs` (`BounceRPC`), `GameplayManager.Maps.cs` |
| แท่นวาร์ป | `Assets/Scripts/WarpPad.cs` | `PlayerController.Warp.cs`, `GameplayManager.Maps.cs` (`AddWarpPads`) |
| ภาพบรรยากาศแม็พปริซึม | `Assets/Scripts/PrismArenaVisuals.cs` | `PrismBurst.cs`, `GameplayManager.Maps.cs` |
| แก้ HUD/แม็พที่โค้ดสร้าง ในหน้า Edit | `Assets/Scripts/EditableTemplate.cs` | `EditableLayout.cs`, เมนู `Editable/...` ใน GameplayManager, `EDITABLE_UI_TH.md` |

ถ้าค้นชื่อเมธอดไม่เจอ ให้ค้นข้อความหรือชื่อปุ่มในโปรเจกต์ก่อน แล้วไล่จากผู้เรียกไปยังผู้ทำงานจริง อย่าเดาว่าชื่อไฟล์ที่ดูคล้ายกันคือจุดเดียวที่ต้องแก้

## 4. แม่แบบรับโจทย์และไล่เส้นทางโค้ด

ใช้แบบฟอร์มนี้ก่อนแก้ทุกครั้ง:

```text
โจทย์จากอาจารย์:
สิ่งที่ต้องเห็น/ทำได้หลังแก้:
Scene ที่เกี่ยวข้อง:
ไฟล์เริ่มต้น:
ข้อมูลที่อ่าน/เขียน:
Network owner / Master Client ที่เกี่ยวข้อง:
สิ่งที่ห้ามเปลี่ยน:
กรณีปกติที่จะทดสอบ:
กรณีผิดพลาดที่จะทดสอบ:
หลักฐานว่าผ่าน:
```

วิธีตาม flow: หา callback ของปุ่ม/เหตุการณ์ → หาเมธอดที่ callback เรียก → หาไฟล์ข้อมูลหรือ Component ที่ทำงาน → หาเส้นทางผลลัพธ์/การแสดงผล → ตรวจจุดที่เรียกซ้ำหรือทำงานบนอีกเครื่อง

## 5. ตัวอย่าง A — เพิ่มปุ่มและเปิดหน้าจอ

**โจทย์:** เพิ่มปุ่มใน Lobby แล้วกดเปิดหน้าหนึ่ง

1. เปิด `LobbyManager.Views.cs` หา `BuildHomeScreen()` และดูปุ่มที่สร้างด้วย `UIButton(...)` อยู่แล้ว
2. เพิ่มปุ่มโดยผูกเมธอด callback ชัดเจน:

```csharp
UIButton("NewFeature", root, "NEW PAGE", x, y, width, height,
    OnNewPageClicked);
```

แทนพิกัดให้ไม่ทับปุ่มเดิมและตรวจ `FitLobbyUI()` บนจอเล็ก

3. เพิ่ม callback ใน `LobbyManager` partial class:

```csharp
private void OnNewPageClicked()
{
    // ซ่อนหน้าปัจจุบัน -> เปิด Panel เป้าหมาย -> โหลดข้อมูลถ้าจำเป็น
}
```

4. ถ้ามีหลายหน้าที่ใช้ร่วมกัน ให้ตรวจ `ShowMainPanel`, `ShowInventoryPanel`, `ShowRoomPanel`, `ShowWaitingRoom` ใน `LobbyManager.cs` เพื่อไม่ให้ panel ซ้อนหรือเปิดผิดระหว่างอยู่ในห้อง
5. ทดสอบปุ่ม, ปุ่มกลับ, คลิกซ้ำ, สลับจอ, และกรณีเข้า/ออกห้อง

ใช้ helper UI ที่มีอยู่ก่อนสร้าง Canvas/EventSystem ใหม่: `UIButton`, `UILabel`, `UIPanel`, `BuildSurface` ใน `LobbyManager.Views.cs`

## 6. ตัวอย่าง B — เพิ่มข้อมูลลง Firebase

**โจทย์:** เพิ่มเวลาเล่น/สถิติ/ประวัติ

1. ดู `FirebaseManager.cs` ว่าข้อมูลปัจจุบันอ่าน/เขียนที่ path ไหนและใช้ชื่อ key แบบใด อย่าตั้งชื่อ field ซ้ำความหมายหรือสลับตัวพิมพ์
2. กำหนด schema ก่อน เช่น:

```text
users/{uid}/total_wins
match_history/{match_id}/user_a
match_history/{match_id}/user_b
match_history/{match_id}/Result
match_history/{match_id}/map_name
```

3. แยกเมธอดฐานข้อมูลออกจาก UI; ให้ `FirebaseManager` รับ/ส่งชนิดข้อมูลธรรมดา แล้ว UI นำไปแสดง
4. ถ้าเพิ่มเหรียญหรือยอดสะสมที่หลายเครื่องอาจแก้พร้อมกัน ให้ใช้ Firebase transaction ไม่อ่านแล้วเขียนค่าทับแบบธรรมดา
5. ระวังว่ากฎ `auth != null` ที่เขียน/อ่านได้ทั้งรากเป็นกฎทดลองที่เปิดกว้าง ไม่ใช่กฎที่ควรใช้กับเกมเผยแพร่จริง
6. อย่าลบ/เปลี่ยน field เก่าทันที; ผู้เล่นอาจมีข้อมูลเดิมและอีกเครื่องอาจยังใช้บิลด์เก่า

การทำงานกับประวัติแมตช์มีตัวอย่างเฉพาะที่ละเอียดกว่าใน `EXAMPLE_ADD_MATCH_HISTORY_TH.md`

## 7. ตัวอย่าง C — ปรับค่ายานหรือค่าสกิล

**โจทย์:** ทำยานเร็วขึ้น หรือปรับคูลดาวน์

1. ยาน: เปิด `Assets/Scripts/BattleLoadoutCatalog.cs` หา `Ships` และแก้ argument ที่ตรงกับยาน โดย constructor ระบุ `name, hp, atk, spd, skill, price, spritePath, shotInterval, acceleration, turnSpeed`
2. สกิล: `Skills` อยู่ไฟล์เดียวกัน; เวลาดาเมจ/ระยะ Stun, Shield, Poison และ Hazard ร่วมอยู่ `BattleBalance.cs`
3. พฤติกรรมการยิง/สกิลอยู่ `PlayerController.cs`, `PlayerController.Skills.cs` และ `SkillController.cs`; แก้ค่ากลางอย่างเดียวอาจไม่พอถ้าพฤติกรรมไม่รองรับ
4. ลำดับ index เป็นสัญญาข้อมูล: ยาน `0..2`, สกิล `0..3`, สียาน `0..5`, อีโมต `0..3`; **ห้ามสลับลำดับ** เพราะ Firebase/Photon อาจอ้าง index เดิม (เพิ่มได้เฉพาะต่อท้าย)
5. ทดสอบทุกยาน/สกิลที่ได้รับผล ตรวจหน้าคลังกับในสนามจริง และทดสอบสองเครื่องหากมี RPC/Photon

## 8. ตัวอย่าง D — เพิ่ม Hazard หรือสิ่งกีดขวางในแผนที่

แยกให้ชัดก่อนว่าเป็น:

- **ของตกแต่งนิ่ง ๆ:** เริ่มที่ `GameplayManager.Maps.cs` หรือเครื่องมือ `Assets/Editor/MapLayoutSetup.cs`; ตรวจ sorting/collider และว่าแต่ละ client สร้างเหมือนกัน
- **อันตรายที่สุ่ม/เกิดตามเวลา:** เริ่ม `MapHazardManager.cs`; ผลตอนชน/สถานะ/ดาเมจอยู่ `HazardController.cs`; ค่าตัวเลขกลางอยู่ `BattleBalance.cs`
- **ของที่มีผลต่อกระสุน/ยานแต่ไม่สุ่ม (เช่น คริสตัลสะท้อน แท่นวาร์ป):** สร้างใน `GameplayManager.Maps.cs` ให้ทุกเครื่องสร้างตำแหน่งเดียวกัน แล้วให้เจ้าของ (คนยิง/เจ้าของยาน) ตัดสินผลและส่ง RPC ดูตัวอย่าง `BulletController.BounceRPC` และ `PlayerController.WarpRPC`
- **ป้อม/สิ่งที่ยิงได้:** `AutoTurret.cs` และ Prefab/จุด `firePoint`; ตรวจว่าเฉพาะ Master Client สร้างกระสุนร่วมและกระสุนออกจากปลายปืน

เช็กลิสต์ Hazard ใหม่:

1. เลือกแผนที่ด้วย index ที่มีอยู่ (`0 = แมงกะพรุน`, `1 = ปริซึม`, `2 = หุ่นยนต์`)
2. กำหนดเวลาเตือน, อายุ, collider, damage/status, และวิธีหยุดเมื่อจบเกม
3. ระบุผู้มีสิทธิ์สร้างใน Photon—โดยทั่วไปการสุ่ม/สร้างของร่วมต้องเป็น Master Client เพื่อไม่ให้เกิดซ้ำสองชุด
4. ระบุว่า damage ทำบน owner ของยานหรือ RPC ใด; ห้ามทุก client ทำ damage ซ้ำให้ผู้เล่นคนเดียว
5. ตรวจ safe spawn, bounds, จำนวนสูงสุด, การ clear object และเมื่อ Master Client หลุด/เปลี่ยน
6. ทดสอบเดินเข้า-ออกซ้ำ, ผู้เล่นสองฝั่ง, เกิดใหม่, จบเกม/กลับห้อง และ map index อื่นที่ Hazard ไม่ควรปรากฏ

อย่าใส่ collider ให้ของตกแต่งถ้าไม่ต้องการให้ชน และอย่าทำให้ฉากหลังเคลื่อนไหวมีผลกับ physics โดยไม่ตั้งใจ

## 9. ตัวอย่าง E — เพิ่ม/เปลี่ยนภาพ เสียง หรือ Prefab

- ภาพที่โหลดด้วย `Resources.Load<Sprite>("Images/ชื่อไฟล์")` ต้องอยู่ใต้ `Assets/Resources/Images/` และ path ไม่ใส่นามสกุล `.png`; ตรวจ Texture Type เป็น Sprite (2D and UI)
- Prefab ที่เรียกด้วย `Resources.Load`/`PhotonNetwork.Instantiate` ต้องอยู่ใต้โฟลเดอร์ Resources ตามชื่อ/path ที่โค้ดเรียก และมี PhotonView/Collider/Script ที่ต้องใช้ครบ
- เปลี่ยนภาพเดิมให้รักษาชื่อไฟล์และ `.meta` หาก Scene/Prefab อ้าง GUID อยู่; อย่าลบ `.meta`
- เสียง: ตรวจ path ที่ AudioManager โหลด, mixer/volume ที่ผู้เล่นตั้ง, และเรียก `PlaySFX`/`PlayBGM` ให้ถูกประเภท
- ถ้าเปลี่ยน sprite sheet ให้ยืนยันการตั้ง slicing และชื่อเฟรมที่ `SkillSheetVisual.Load(...)` ค้นหา

## 10. ตัวอย่าง F — แก้ปัญหา “กดแล้วไม่เกิดอะไร”

ไล่ตามลำดับนี้ อย่าเริ่มจากเขียนใหม่ทันที:

1. ปุ่มมีอยู่/active/interactable หรือไม่
2. callback ถูกผูกกับปุ่มหรือไม่ (`UIButton(..., callback)` หรือ `onClick.AddListener`)
3. callback ถูกเรียกจริงหรือมี guard เช่น `if (!profileLoaded) return` ปิดทาง
4. เมธอดถัดไปพบ Manager/Prefab/Component หรือไม่
5. มี error/warning ใน Console หรือ Firebase Permission denied หรือไม่
6. ใน Photon เป็น local owner/Master Client ที่ควรทำ action หรือไม่
7. ผลลัพธ์เกิดแต่ UI ไม่ refresh หรือไม่

เพิ่ม log ชั่วคราวในจุดรับเหตุการณ์และจุดทำงาน หลีกเลี่ยง log ทุกเฟรม แล้วลบหรือปรับเป็นข้อความที่เหมาะก่อนส่ง

## 11. กติกาสำคัญก่อนแก้โค้ด

- ตรวจ Git changes ก่อนเริ่ม; งานที่ยังไม่ commit เป็นของผู้ทำงาน ห้ามทับ/ย้อนโดยไม่เข้าใจ
- แก้ทีละส่วนเล็ก ๆ และเก็บ diff ให้อ่านรู้เรื่อง
- อย่าเปลี่ยนชื่อ `MonoBehaviour`, `[PunRPC]`, public method ที่ UI เรียก, index ยาน/สกิล/แม็พ, Firebase key หรือ GUID ใน `.meta` โดยไม่ไล่จุดอ้างอิงทั้งหมด
- ห้ามกดเมนู Setup/Generate ใน `Assets/Editor/` ถ้ายังไม่รู้ว่าจะเขียนทับ Scene/Prefab ใด
- อย่าเปิด Firebase Rules แบบเขียนได้ทั้งฐานข้อมูลในระบบจริง; ระหว่างทดสอบก็อย่าลืมคืนกฎที่ปลอดภัยก่อนเผยแพร่
- อย่าตีความ compile ผ่านว่าเกมถูกต้อง; UI, network และ Firebase ต้องทดสอบใน runtime

## 12. วิธีทดสอบและส่งงาน

หลังแก้แต่ละงาน:

1. รอ Unity compile และตรวจ Console แยก Error, Warning และข้อความปกติ
2. เปิด Scene ที่เกี่ยวข้องและทำขั้นตอนโจทย์ตั้งแต่ต้นจนจบ
3. ทดสอบกรณีขอบ: ไม่มีข้อมูล, กดซ้ำ, ผู้เล่นอีกฝั่ง, จบเกม/กลับ Lobby, และจอมือถือเมื่อ UI เปลี่ยน
4. สำหรับ Firebase ดู path จริงใน Console และเช็กว่าข้อมูลถูกบัญชี/ถูกฝั่ง ไม่ดูแค่ข้อความสำเร็จบนหน้าจอ
5. สำหรับ Photon ทดสอบสอง client และระบุว่าใครเป็น Master/Owner
6. ตรวจว่าของเก่าไม่เสีย เช่น ซื้อยาน, เลือกสกิล, ยิง, กลับห้อง
7. สรุปไฟล์ที่แก้, พฤติกรรมที่เปลี่ยน, วิธีทดสอบ และข้อจำกัดที่ยังไม่ได้ทดสอบ

แม่แบบบันทึกส่งงาน:

```text
โจทย์:
แก้ไฟล์:
เปลี่ยนอะไร:
วิธีทดสอบ:
ผลที่เห็น:
ทดสอบบน (Editor / มือถือ / สองเครื่อง):
ยังไม่ได้ทดสอบ:
```

## 13. ฝึกตอบอาจารย์

ทุกฟีเจอร์ควรตอบได้สี่ประโยค:

1. “โจทย์ต้องการให้ผู้เล่นทำ/เห็นอะไร”
2. “ข้อมูลหรือเหตุการณ์เริ่มที่ไฟล์ใด”
3. “ไฟล์ใดประมวลผล และไฟล์ใดแสดงผล”
4. “ทดสอบกรณีปกติและกรณีผิดพลาดอย่างไร”

ตัวอย่างประวัติแมตช์: “ปุ่มอยู่ใน Lobby; `FirebaseManager` อ่านและคัด record ตาม UID; `LobbyManager.Views` แสดงผล; ผมตรวจผู้เล่น A/B, ผลแพ้ชนะ, รายการว่าง และ permission error”

## พาธไฟล์เต็ม

```text
C:\Users\08524\Documents\BattlefieldOfTheStarsprojactGame\My project\Docs.คู่มือ\PROJECT_CHANGE_PLAYBOOK_TH.md
```
