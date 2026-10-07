# แผนแบ่งศึกษาโค้ดกับเพื่อนเพื่อเตรียมสอบ

เอกสารนี้เป็นแผนแบ่งอ่านและซ้อมอธิบายโค้ด ไม่ใช่การแบ่งไฟล์ให้คัดลอกหรือแก้ทับกัน

## ที่อยู่โปรเจกต์

โฟลเดอร์หลัก:

```text
C:\Users\08524\Documents\BattlefieldOfTheStarsprojactGame\My project
```

พาธในรายการด้านล่างเริ่มจากโฟลเดอร์หลักนี้ เช่น `Assets/Scripts/PlayerController.cs`

คู่มือแผนที่โค้ดฉบับละเอียดอยู่ที่ `Docs.คู่มือ/CODE_GUIDE_TH.md` ให้อ่านควบคู่กับแผนแบ่งงานฉบับนี้

## ภาพรวมที่ทั้งสองคนควรเข้าใจก่อน

อ่าน Scene ตามลำดับการเล่น:

1. `Assets/Scenes/LoginScene.unity` — จุดเข้าเกม
2. `Assets/Scenes/LobbyScene.unity` — โปรไฟล์ คลัง เลือกยาน/สกิล และห้องรอ
3. `Assets/Scenes/SampleScene.unity` — ตัวเกม การต่อสู้ และแม็พ

จากนั้นดูไฟล์เหล่านี้ร่วมกัน:

- `Assets/Scripts/BattleLoadoutCatalog.cs` — ข้อมูลยานและสกิลที่หลายระบบใช้ร่วมกัน
- `Assets/Scripts/BattleBalance.cs` — ค่าดาเมจและค่ากลางของการต่อสู้
- `Assets/Scripts/Managers/GameplayManager.cs` — จุดควบคุมหลักระหว่างการแข่งขัน
- `Assets/Scripts/Managers/LobbyManager.cs` — จุดควบคุมหลักของ Lobby
- `Docs.คู่มือ/CODE_GUIDE_TH.md` — คำอธิบายพาธและจุดเริ่มอ่านของแต่ละระบบ

คลาสที่แบ่งเป็น `.cs` หลายไฟล์ เช่น `PlayerController.*.cs` ยังเป็นคลาสเดียวกันผ่าน `partial class` ไม่ใช่ Component แยกคนละตัว และสคริปต์เกมของโปรเจกต์ยังอยู่ใน Assembly เดียวกัน

## คนที่ 1 — Gameplay, ยาน, สกิล และแม็พ

### ไฟล์ที่รับไปศึกษา

| พาธจากโฟลเดอร์หลัก | ควรศึกษาเรื่อง |
|---|---|
| `Assets/Scripts/PlayerController.cs` | จุดเริ่มของยาน การอัปเดต การยิง และการหาตำแหน่งปล่อยกระสุนจากหัวเรือ |
| `Assets/Scripts/PlayerController.Movement.cs` | การอ่านจอยสติ๊ก การเคลื่อนที่ และการเล็ง |
| `Assets/Scripts/PlayerController.Skills.cs` | การใช้สกิล โล่ สตั้น slow และการเรียก RPC |
| `Assets/Scripts/PlayerController.Health.cs` | HP รับดาเมจ ตาย และเกิดใหม่ |
| `Assets/Scripts/PlayerController.Visuals.cs` | ภาพยาน ไอพ่น และเอฟเฟกต์ภาพที่ผูกกับยาน |
| `Assets/Scripts/BattleLoadoutCatalog.cs` | ค่าสถานะ/ภาพ/ชื่อยานและข้อมูลสกิล |
| `Assets/Scripts/BattleBalance.cs` | ค่าดาเมจและเวลาสถานะที่ระบบต่อสู้ใช้ร่วมกัน |
| `Assets/Scripts/BulletController.cs` | การเคลื่อนที่ของกระสุน การตรวจชน และการทำดาเมจ |
| `Assets/Scripts/SkillController.cs` | การเคลื่อนที่/ล็อกเป้าหมาย/ระเบิดของ Nova, Stun และ Seeker |
| `Assets/Scripts/AutoTurret.cs` | การหาเป้าหมาย หมุนป้อม ตรวจทางยิง และปล่อยกระสุน |
| `Assets/Scripts/Managers/GameplayManager.Maps.cs` | Layout ขอบเขตแม็พ จุดเกิด และสิ่งกำบัง |
| `Assets/Scripts/Managers/MapHazardManager.cs` | การสุ่ม/สร้างอันตรายและภาพบรรยากาศแม็พ |
| `Assets/Scripts/Managers/HazardController.cs` | การชน ดาเมจ และสถานะผิดปกติจาก Hazard |
| `Assets/Scripts/PlayerController.Warp.cs`, `Assets/Scripts/WarpPad.cs` | แท่นวาร์ปแม็พปริซึม: ใครสั่งวาร์ป ส่ง RPC อย่างไร คูลดาวน์ 3 วินาที |
| `Assets/Scripts/PrismReflector.cs` + ส่วน `BounceRPC` ใน `BulletController.cs` | กระสุนเด้งคริสตัล 2 ครั้ง: คนยิงคำนวณแล้วแจ้งเครื่องอื่น |
| `Assets/Scripts/PrismArenaVisuals.cs`, `Assets/Scripts/PrismBurst.cs` | ภาพบรรยากาศแม็พปริซึม (ภาพล้วน ไม่มีผลต่อการเล่น) |
| `Assets/Scripts/PlayerController.Cosmetics.cs` | สียานและอีโมตฝั่งยาน (`ShowEmoteRPC`) |

### ควรอธิบายให้ได้

- ค่าความเร็ว/HP/โจมตีของยานมาจากไหน และถูกนำไปตั้งให้ PlayerController ตอนไหน
- เมื่อผู้เล่นกดยิง เกิดอะไรตั้งแต่ตรวจปุ่ม เลือกตำแหน่งเกิดกระสุน จนกระสุนตรวจการชน
- Nova, Shield, Stun และ Seeker ทำงานต่างกันตรงไหน
- การลด HP และการเกิดใหม่ทำงานผ่านไฟล์ใดบ้าง
- แม็พทั้งสามเลือกด้วย index อะไร: `0 = แมงกะพรุน`, `1 = ปริซึม`, `2 = หุ่นยนต์`
- การตัดสินใจว่าใครสร้าง Hazard หรือทำดาเมจในเกมออนไลน์ถูกควบคุมอย่างไร
- ทำไมกระสุนเด้งและการวาร์ปต้องให้เจ้าของ (คนยิง/เจ้าของยาน) เป็นคนตัดสิน แล้วส่ง RPC ให้เครื่องอื่น

### ซ้อมโจทย์ปากเปล่า

1. ถ้าอาจารย์ให้ยานเร็วขึ้น จะเริ่มแก้ที่ไฟล์และข้อมูลใด เพราะอะไร
2. ถ้ากระสุนชนเสาแล้วไม่ทำดาเมจ จะตรวจเส้นทางโค้ดตั้งแต่ไฟล์ไหนถึงไฟล์ไหน
3. ถ้าจะเพิ่มเวลาสตั้น ต้องดูค่ากลางและจุดเรียกใช้ตรงไหน
4. ถ้าผู้เล่นเกิดใหม่ซ้อน/HP ไม่เต็ม จะตรวจขั้นตอนใดใน `PlayerController.Health.cs`
5. ถ้าจะเพิ่มจำนวนบึงหรือเปลี่ยนจังหวะสุ่ม ต้องแยกตรวจไฟล์สร้าง Hazard กับไฟล์รับการชนอย่างไร
6. ถ้าอาจารย์ให้กระสุนเด้งคริสตัลได้ 3 ครั้ง หรือย้ายแท่นวาร์ป จะแก้ที่ไหน
7. ถ้าวาร์ปแล้วอีกเครื่องเห็นยานไหลข้ามแม็พ จะตรวจ `WarpRPC` ตรงไหน

## คนที่ 2 — ล็อกอิน, Lobby, บัญชี, UI และเสียง

### ไฟล์ที่รับไปศึกษา

| พาธจากโฟลเดอร์หลัก | ควรศึกษาเรื่อง |
|---|---|
| `Assets/Scripts/Managers/LoginManager.cs` | การเข้าเกมด้วย Google/Guest และผลสำเร็จ/ล้มเหลว |
| `Assets/Scripts/Managers/FirebaseManager.cs` | อ่านโปรไฟล์ ยอดเหรียญ ซื้อยาน และบันทึกผลการแข่งขัน |
| `Assets/Scripts/Managers/LobbyManager.cs` | เริ่มระบบ Lobby สถานะหน้า Ready และการเปลี่ยนหน้า |
| `Assets/Scripts/Managers/LobbyManager.Views.cs` | การสร้าง/แสดงหน้า Home, Hangar, การ์ดแม็พ และเหรียญ |
| `Assets/Scripts/Managers/LobbyManager.Inventory.cs` | ซื้อ/เลือกยาน และติดตั้งสกิล |
| `Assets/Scripts/Managers/LobbyManager.Rooms.cs` | Quick Match, ค้นหา/สร้าง/เข้าห้อง และ Photon callbacks |
| `Assets/Scripts/BattleLoadoutCatalog.cs` | แหล่งข้อมูลยาน/สกิลที่ Lobby ใช้แสดงผล |
| `Assets/Scripts/BattleSettingsPanel.cs` | แผงตั้งค่า เสียง ความไวเล็ง และยืนยันออก |
| `Assets/Scripts/Managers/AudioManager.cs` | เล่นเสียง/เพลง ปรับระดับเสียง และเก็บค่าที่ตั้งไว้ |
| `Assets/Scripts/UIButton.cs` | รับ pointer และลากปุ่มยิงบนหน้าจอ |
| `Assets/Scripts/UIJoystick.cs` | รับการลากจอยสติ๊ก |
| `Assets/Scripts/PolishSprites.cs` | ภาพเหรียญและภาพบึงที่แสดงใน UI |
| `Assets/Scripts/Managers/LobbyManager.cs` (`OnLogoutButtonClicked`) + `FirebaseManager.Logout` | ปุ่ม LOG OUT ในหน้าตั้งค่า Lobby: ออก Google/Firebase แล้วกลับหน้า Login |
| `Assets/Scripts/Managers/LobbyManager.Views.cs` (`BuildPaintPicker`) + `ShipPaint` ใน `BattleLoadoutCatalog.cs` | ปุ่มเลือกสียาน 6 สี เก็บใน PlayerPrefs ส่งผ่าน Photon property `ShipSkin` |
| `Assets/Scripts/Managers/GameplayManager.HUD.cs` (`BuildEmoteControls`, `ShowEmote`) | ปุ่มอีโมตและฟองข้อความเหนือยาน |
| `Assets/Scripts/Managers/GameplayManager.cs` (`ShowIncomingDamage`, HIT) + `FloatingText.cs` | ข้อความ HIT ลูกศรทิศที่โดนยิง และตัวเลขดาเมจลอย |
| `Assets/Scripts/EditableTemplate.cs` + `Docs.คู่มือ/EDITABLE_UI_TH.md` | วิธีเอา UI ที่สร้างด้วยโค้ดมาแก้ในหน้า Edit |

### ควรอธิบายให้ได้

- ลำดับจากกด Login ไป Firebase แล้วถึง Lobby เป็นอย่างไร
- ข้อมูลเหรียญ/ยานที่เลือกเก็บไว้ที่ไหน และ Lobby นำมาแสดงอย่างไร
- การซื้อของทำไมต้องใช้ Firebase transaction แทนการเขียนยอดทับตรง ๆ
- การสร้าง/เข้าห้อง Photon ต่างจากการเริ่มเกมอย่างไร และใครมีสิทธิ์กดเริ่ม
- ส่วนใดของ Lobby เป็นการสร้าง UI ตอนรัน และส่วนใดเป็น Scene/Prefab ที่ทำไว้แล้ว
- ค่าตั้งค่าเสียงและความไวเล็งถูกบันทึกและนำกลับมาใช้ตอนไหน
- ออกจากระบบแล้วข้อมูลอะไรถูกล้าง และทำไมต้องออกทั้ง Google และ Firebase
- สียานเก็บที่ไหน และคู่แข่งเห็นสีเราได้อย่างไร (Photon Custom Properties)

### ซ้อมโจทย์ปากเปล่า

1. ถ้าเหรียญในหน้าคลังไม่ตรงกับฐานข้อมูล จะไล่ตรวจจากส่วนอ่านข้อมูลไปถึง UI ที่ไฟล์ใดบ้าง
2. ถ้าซื้อยานสำเร็จแต่ภาพยังไม่เปลี่ยน จะตรวจ callback และการ refresh จุดใด
3. ถ้าผู้เล่นเข้าห้องแล้วอีกคนไม่เห็น Ready จะเริ่มตรวจ Photon callback หรือ property ใด
4. ถ้าปุ่มบนหน้าล็อบบี้กดไม่ติด จะตรวจ callback ที่สร้าง UI กับ EventSystem/UIButton อย่างไร
5. ถ้าต้องเพิ่มปุ่มยืนยันออกจากเกม จะวางพฤติกรรมในแผงตั้งค่าและรักษาการทำงานของห้องอย่างไร
6. ถ้ากด LOG OUT แล้วเข้าใหม่ยังเป็นบัญชีเดิม จะตรวจที่ไหน
7. ถ้าจะเพิ่มข้อความอีโมตใหม่ ต้องแก้ไฟล์ใด และทำไมต้องเพิ่มต่อท้าย

## หัวข้อที่ทั้งสองคนควรช่วยกันซ้อม

| หัวข้อ | คำตอบที่ควรเตรียม |
|---|---|
| Photon | อธิบายว่าเจ้าของยานกับ Master Client มีหน้าที่ต่างกันอย่างไร และป้องกันการทำงานซ้ำอย่างไร |
| Firebase | อธิบายข้อมูลใดเก็บในฐานข้อมูล และเหตุใดการซื้อ/เพิ่มเหรียญต้องระวังยอดชนกัน |
| Prefab/Resources | อธิบายว่า Prefab ที่อยู่ `Assets/Resources/` ถูกเรียกใช้ด้วยชื่อหรือ path อย่างไร |
| Scene/UI | แยกว่า UI บางส่วนสร้างตอนรัน กับ object ที่ผูกไว้ใน Scene/Prefab อย่างไร |
| partial class | อธิบายว่าแยกไฟล์เพื่อหาโค้ดง่าย แต่ยังเป็นคลาสเดียวและใช้ตัวแปรร่วมกันได้ |
| การทดสอบ | อธิบายสิ่งที่ทดสอบจริง เช่น สองเครื่อง แม็พ สกิล และมือถือ โดยเล่าขั้นตอนและผล ไม่ตอบเพียงว่า “ผ่าน” |

## ผลงานที่แต่ละคนควรเตรียมก่อนวันสอบ

แต่ละคนควรทำโน้ตสั้น ๆ หนึ่งหน้า โดยมี:

1. พาธไฟล์หลักที่รับผิดชอบ 3–5 ไฟล์
2. แผนภาพหรือคำอธิบายลำดับการทำงานหนึ่งเรื่อง เช่น ยิงกระสุน หรือเข้าห้อง
3. ตัวอย่างหนึ่งจุดที่แก้ได้ พร้อมบอกไฟล์และผลที่คาดว่าจะเกิด
4. คำศัพท์ที่อาจารย์อาจถาม เช่น RPC, transaction, prefab, partial class
5. หลักฐานการทดสอบส่วนของตัวเอง เช่น ขั้นตอนทดสอบ/ภาพหน้าจอ/ผลที่สังเกตได้

## กติกาแบ่งไฟล์และทำงานร่วมกัน

- ใช้แผนนี้แบ่ง “เจ้าของหัวข้อศึกษา” ไม่ได้แปลว่าต้องแก้โค้ดทุกไฟล์ที่อ่าน
- ตกลงกันก่อนหากจะเปลี่ยนไฟล์ที่หลายระบบเรียกใช้ เช่น `PlayerController.cs`, `BattleLoadoutCatalog.cs`, `Managers/GameplayManager.cs` และ `Managers/LobbyManager.cs`
- อย่าเปลี่ยนชื่อ MonoBehaviour, `[PunRPC]`, ลำดับ index ยาน/สกิล/แม็พ หรือ GUID ใน `.meta` โดยไม่ตรวจทุกจุดที่อ้างถึง
- ถ้าทำงานคนละเครื่อง ให้แยก branch หรือส่งชุดไฟล์ที่ระบุชัด อย่าเอาไฟล์ทั้งโปรเจกต์ไปวางทับโดยไม่ตรวจรายการเปลี่ยนแปลง
- เจ้าของโปรเจกต์แจ้งว่าทดสอบเกมทั้งหมดแล้วผ่าน; ก่อนสอบให้ทั้งสองคนซ้อมอธิบายระบบและทวนผลทดสอบที่ทำจริงอีกครั้ง
