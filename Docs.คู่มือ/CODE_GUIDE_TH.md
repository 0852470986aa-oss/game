# คู่มือโปรเจกต์ Battlefield of the Stars

คู่มือนี้ใช้หาตำแหน่งโค้ด อธิบายว่าแต่ละไฟล์รับผิดชอบอะไร และช่วยแบ่งงานระหว่างสมาชิกในทีม

## ที่อยู่โปรเจกต์และวิธีเปิดไฟล์

โฟลเดอร์หลักของโปรเจกต์บนเครื่องนี้คือ:

```text
C:\Users\08524\Documents\BattlefieldOfTheStarsprojactGame\My project
```

พาธในตารางด้านล่างเริ่มนับจากโฟลเดอร์หลักนี้ เช่น `Assets/Scripts/PlayerController.cs` หมายถึงไฟล์เต็ม:

```text
C:\Users\08524\Documents\BattlefieldOfTheStarsprojactGame\My project\Assets\Scripts\PlayerController.cs
```

เปิดโปรเจกต์ผ่าน Unity Hub โดยเลือกโฟลเดอร์หลักข้างบน จากนั้นเปิดสคริปต์ได้จาก Project window ที่ `Assets > Scripts` หรือเปิดโฟลเดอร์ `Assets/Scripts` ใน VS Code/Visual Studio

เวอร์ชัน Unity ที่บันทึกไว้ในโปรเจกต์: **Unity 6.3 LTS (6000.3.5f2)**

## ภาพรวมโครงสร้าง

| โฟลเดอร์ | เนื้อหา |
|---|---|
| `Assets/Scenes/` | Scene หลักของเกม |
| `Assets/Scripts/` | โค้ดเกมที่ทีมเขียนเอง |
| `Assets/Scripts/Managers/` | ตัวจัดการล็อกอิน ล็อบบี้ เกม เสียง Firebase และอันตรายบนแม็พ |
| `Assets/Resources/` | Prefab/ภาพ/เสียงที่โค้ดโหลดด้วย `Resources.Load` หรือ Photon สร้างผ่านชื่อ Prefab |
| `Assets/Resources/Images/` | ภาพ UI ยาน ฉาก และเอฟเฟกต์ |
| `Assets/Editor/` | เครื่องมือสำหรับ Unity Editor เท่านั้น ไม่ใช่ระบบที่รันในเกม |
| `Docs.คู่มือ/` | คู่มือโค้ด แผนแบ่งอ่านเตรียมสอบ แผนงาน และบันทึกการตรวจสอบ/ทดสอบ |
| `ProjectSettings/` | การตั้งค่าของโปรเจกต์ Unity และแพลตฟอร์ม |

Scene หลัก:

| Scene | พาธ | หน้าที่ |
|---|---|---|
| Login | `Assets/Scenes/LoginScene.unity` | เข้าใช้ด้วย Google หรือ Guest |
| Lobby | `Assets/Scenes/LobbyScene.unity` | หน้าแรก คลัง เลือกยาน/สกิล และห้องรอ |
| Gameplay | `Assets/Scenes/SampleScene.unity` | การแข่งขัน HUD และแม็พที่เลือก |

## หาโค้ดตามระบบ

### ยาน ผู้เล่น และการต่อสู้

| ต้องการแก้ | ไฟล์จากโฟลเดอร์หลัก | จุดเริ่มอ่าน/หน้าที่ |
|---|---|---|
| ค่าสถานะ/ชื่อ/ราคา/ภาพของยาน และข้อมูลสกิล | `Assets/Scripts/BattleLoadoutCatalog.cs` | `Ships`, `Skills`; ข้อมูลที่ Lobby และ PlayerController ใช้ร่วมกัน |
| ค่าดาเมจ/ระยะเวลาสถานะร่วม | `Assets/Scripts/BattleBalance.cs` | ค่าคงที่ เช่น ดาเมจกระสุน/สกิล/อันตราย ระวังไม่สลับ index |
| วงจรชีวิตยาน การยิง และอัปเดตเครือข่าย | `Assets/Scripts/PlayerController.cs` | `Start`, `Update`, `FixedUpdate`, `HandleShooting`, `Shoot`, `GetFirePosition` |
| การเคลื่อนที่และเล็ง | `Assets/Scripts/PlayerController.Movement.cs` | `HandleMovement`, `HandleAiming`; ถูกเรียกจากคลาส PlayerController หลัก |
| การกดใช้สกิล โล่ สตั้น และ slow จากบึง | `Assets/Scripts/PlayerController.Skills.cs` | `UseSkill`, `ActivateShieldRPC`, `ApplyStunRPC`, `UpdateEffectiveSpeed` |
| เลือด รับความเสียหาย ตาย เกิดใหม่ | `Assets/Scripts/PlayerController.Health.cs` | `TakeDamage`, `Die`, RPC และ `RespawnRoutine` |
| ภาพยาน ไอพ่น และเอฟเฟกต์ภาพ | `Assets/Scripts/PlayerController.Visuals.cs` | การอัปเดตภาพ, `ShipEffectSprite`, `PlaySheetBurst` |
| กระสุน การชน และความเสียหายจากกระสุน | `Assets/Scripts/BulletController.cs` | `ProjectileSweep`, `FixedUpdate`, `OnTriggerEnter2D` |
| การทำงาน/ภาพ/ระเบิดของ Nova, Stun, Seeker | `Assets/Scripts/SkillController.cs` | `OnPhotonInstantiate`, `Update`, `DetonateNova`, `DealDamage`; ในไฟล์เดียวกันมี `SkillSheetVisual` สำหรับภาพสกิล |
| ป้อมปืนแม็พหุ่นยนต์ | `Assets/Scripts/AutoTurret.cs` | `detectionRadius`, `fireRate`, `damage`, `ClearSight`, การเล็งและจุดยิง |
| สียาน (ภาพยานเดิมคูณสี) และอีโมตระหว่างแข่ง | `Assets/Scripts/PlayerController.Cosmetics.cs` | `ApplyPaint`, `Emotes`, `TrySendEmote`, `ShowEmoteRPC`; ไม่มีผลต่อค่าพลัง |
| รายการสียาน 6 สี และการเก็บค่า | `Assets/Scripts/BattleLoadoutCatalog.cs` | คลาส `ShipPaint` (`Names`, `Colors`, `Local`, `For`); เก็บใน PlayerPrefs และส่งผ่าน Photon property `ShipSkin` |
| วาร์ปเมื่อแตะแท่นวาร์ป | `Assets/Scripts/PlayerController.Warp.cs` | `TryWarp` (เจ้าของยานเท่านั้น), `WarpRPC`, `WarpCooldown = 3` วินาที |
| แจ้งยิงโดน / ลูกศรทิศที่โดนยิง | `Assets/Scripts/PlayerController.Health.cs` → `Assets/Scripts/Managers/GameplayManager.cs` | `ConfirmHitToShooter` แล้วเรียก `ShowIncomingDamage`, ข้อความ HIT (`CreateHitConfirmation`) |
| ตัวเลขดาเมจลอย | `Assets/Scripts/FloatingText.cs` | การลอย/จาง/ขยายของตัวเลข |
| กระสุนเด้งเมื่อชนคริสตัล | `Assets/Scripts/BulletController.cs` | `bouncesLeft`, `BounceRPC`, `ApplyBounce`; คนยิงคำนวณแล้วส่งให้เครื่องอื่น |

### แม็พและอันตราย

| ต้องการแก้ | ไฟล์จากโฟลเดอร์หลัก | จุดเริ่มอ่าน/หน้าที่ |
|---|---|---|
| เลือก Layout, ขอบสนาม, จุดเกิดปลอดภัย | `Assets/Scripts/Managers/GameplayManager.Maps.cs` | `GetCurrentMapIndex`, `GetArenaMin/Max`, `TryFindSafeSpawn` |
| ตำแหน่งเสาปริซึม/หินและสิ่งกำบัง | `Assets/Scripts/Managers/GameplayManager.Maps.cs` | `DecoratePrism`, `PrepareMechCover` |
| คริสตัลสะท้อนกระสุน (แม็พปริซึม) | `Assets/Scripts/Managers/GameplayManager.Maps.cs`, `Assets/Scripts/PrismReflector.cs` | กลุ่ม `Crystal_*` ได้ Component `PrismReflector` (`MaxBounces = 2`) |
| แท่นวาร์ป 4 แท่น (2 คู่) | `Assets/Scripts/Managers/GameplayManager.Maps.cs`, `Assets/Scripts/WarpPad.cs` | `AddWarpPads`, `CreateWarpPad`; คู่ A ฟ้า (-40,-30)↔(40,30), คู่ B ชมพู (40,-30)↔(-40,30) |
| ภาพบรรยากาศแม็พปริซึม (ภาพล้วน ไม่มีตัวชน) | `Assets/Scripts/PrismArenaVisuals.cs` | พื้นหลังเดิมวางแถวพื้น (`GroundFraction`), ละอองแสง หมอก เศษคริสตัลหมุน แสงเรือง; มีคลาส `PrismFx` สำหรับเอฟเฟกต์แสงวาบ |
| แสงวาบสั้น ๆ (ตอนเด้ง/วาร์ป) | `Assets/Scripts/PrismBurst.cs` | `Begin` ขยายแล้วจางหาย |
| แมงกะพรุน แกนกลาง ภาพฉาก และเอฟเฟกต์ดูด | `Assets/Scripts/Managers/MapHazardManager.cs` | `JellyArenaVisuals`, `MapHazardManager`, `Start`, ฟังก์ชันสร้าง/สุ่ม Hazard |
| หินลาวา อุกกาบาต สายฟ้า บึง และการชน | `Assets/Scripts/Managers/HazardController.cs` | `MoltenContactSurface`, `HazardController`, `HazardRoutine`, `OnTriggerEnter2D` |
| ค่าเสียหายและช่วงเวลาของอันตราย | `Assets/Scripts/BattleBalance.cs` | ปรับค่ากลางร่วม; พฤติกรรม/ภาพให้แก้ในไฟล์ Hazard ที่เกี่ยวข้องด้วย |
| Prefab อันตรายที่ถูกสร้างขณะเล่น | `Assets/Resources/Hazard_*.prefab` | ตรวจ Component/Collider/ภาพของ Prefab; อย่าลบ `.meta` |

**ลำดับแม็พในโค้ด:** `0 = แมงกะพรุน`, `1 = ปริซึม`, `2 = หุ่นยนต์` อย่าสลับ index เพราะถูกส่งผ่านข้อมูลห้อง/เกมและอาจไม่ตรงกับลำดับในรายงาน

### ล็อกอิน ล็อบบี้ ห้อง และข้อมูลผู้เล่น

| ต้องการแก้ | ไฟล์จากโฟลเดอร์หลัก | จุดเริ่มอ่าน/หน้าที่ |
|---|---|---|
| หน้าล็อกอินและปุ่ม Google/Guest | `Assets/Scripts/Managers/LoginManager.cs` | `LoginGoogle`, `LoginGuest`, callback สำเร็จ/ผิดพลาด |
| ออกจากระบบ (ปุ่ม LOG OUT ในหน้าตั้งค่าของ Lobby) | `Assets/Scripts/Managers/LobbyManager.cs`, `Assets/Scripts/Managers/FirebaseManager.cs` | `OnLogoutButtonClicked` → `FirebaseManager.Logout` (ออก Google + Firebase) แล้วกลับ LoginScene |
| ปุ่มเลือกสียาน (PAINT) ในหน้าคลัง | `Assets/Scripts/Managers/LobbyManager.Views.cs` | `BuildPaintPicker`, `SelectPaint`, `RefreshPaintSwatches`; ส่งค่าไปห้องใน `LobbyManager.Rooms.cs` |
| อ่าน/เขียนโปรไฟล์ เหรียญ ยาน และผลแข่ง | `Assets/Scripts/Managers/FirebaseManager.cs` | `GetCoinBalance`, `PurchaseShip`, `AddCoins`, `RecordMatchResult` |
| จุดเริ่มและสถานะหลักของ Lobby | `Assets/Scripts/Managers/LobbyManager.cs` | `Start`, เปลี่ยนหน้า, Ready/เริ่มเกม และการกู้การเชื่อมต่อ |
| สร้างหน้าหลัก/คลัง/แสดงเหรียญและยาน | `Assets/Scripts/Managers/LobbyManager.Views.cs` | `BuildLobbyUI`, `BuildHomeScreen`, `BuildHangarScreen`, `BuildMapCards` |
| ซื้อ/เลือกยาน และติดตั้งสกิล | `Assets/Scripts/Managers/LobbyManager.Inventory.cs` | `OnInventoryActionClicked`, `OnInstallSkillClicked` |
| Quick Match, ค้นหา/สร้าง/เข้าห้อง และ callback Photon | `Assets/Scripts/Managers/LobbyManager.Rooms.cs` | `OnJoinedRoom`, `OnRoomListUpdate`, `OnCreateRoomConfirm`, `OnDisconnected` |
| ข้อมูลยาน/สกิลที่หน้า Lobby แสดง | `Assets/Scripts/BattleLoadoutCatalog.cs` | ใช้ไฟล์นี้แก้ catalog; อย่าเปลี่ยนลำดับรายการโดยไม่ตรวจข้อมูล Firebase |

### UI ระหว่างเล่นและผลการแข่งขัน

| ต้องการแก้ | ไฟล์จากโฟลเดอร์หลัก | จุดเริ่มอ่าน/หน้าที่ |
|---|---|---|
| ตัวควบคุมการแข่งขัน เวลา คะแนน และการประสานฉาก | `Assets/Scripts/Managers/GameplayManager.cs` | จุดเริ่ม/Update/การเชื่อมต่อ Photon และ state หลักของเกม |
| HUD, หลอดเลือด, ปุ่มบนจอ และหน้าสรุป | `Assets/Scripts/Managers/GameplayManager.HUD.cs` | `StyleBattleHUD`, `BuildBattleControls`, `BuildResultUI` |
| สถานะสกิล/ติด Stun/แจ้งกำจัด | `Assets/Scripts/Managers/GameplayManager.StatusUI.cs` | `ShowKillMessage`, `UpdateCombatStatuses`, `UpdateSkillUI` |
| ผลแข่งและกลับห้องเดิม | `Assets/Scripts/Managers/GameplayManager.Results.cs` | `ShowResultScreen` และขั้นตอนกลับ/เล่นซ้ำ |
| แผงตั้งค่าในเกม/ความไวเล็ง/ยืนยันออก | `Assets/Scripts/BattleSettingsPanel.cs` | `Show`, `Build`, `Row`, `Confirm`; การตั้งค่าบันทึกผ่าน PlayerPrefs |
| ปุ่มยิงแบบกด/ลาก | `Assets/Scripts/UIButton.cs` | `OnPointerDown`, `OnDrag`, `OnPointerUp`, `AimDirection` |
| จอยสติ๊กเคลื่อนที่ | `Assets/Scripts/UIJoystick.cs` | `OnDrag`, `OnPointerDown`, `OnPointerUp` |
| วงปุ่ม/วงคูลดาวน์ | `Assets/Scripts/ControlRingGraphic.cs` | `SetRing`, `OnPopulateMesh` |
| ย่อ/หมุนเรดาร์ | `Assets/Scripts/RadarMinimap.cs` | สคริปต์เรดาร์ที่ใช้กับหน้า Gameplay |
| ปุ่มอีโมตบน HUD และฟองข้อความเหนือยาน | `Assets/Scripts/Managers/GameplayManager.HUD.cs` | `BuildEmoteControls`, `WireEmoteControls`, `ShowEmote`, `CreateEmoteBubble` |
| แก้ HUD/หน้าผล/หน้าตั้งค่า/ของในแม็พ ในหน้า Edit | `Assets/Scripts/EditableTemplate.cs`, `Assets/Scripts/EditableLayout.cs` + เมนู `Editable/...` ใน GameplayManager | วิธีใช้อยู่ `Docs.คู่มือ/EDITABLE_UI_TH.md` |

### ภาพ เสียง กล้อง และเครื่องมือ Editor

| ต้องการแก้ | ไฟล์/โฟลเดอร์จากโฟลเดอร์หลัก | หมายเหตุ |
|---|---|---|
| ไอคอนเหรียญ/ภาพบึงที่ครอปขณะรัน | `Assets/Scripts/PolishSprites.cs` | `Coin`, `Swamp`, `AddCoinIcon`; แหล่งภาพอยู่ `Assets/Resources/Images/` |
| เสียง เพลง และระดับเสียง | `Assets/Scripts/Managers/AudioManager.cs` | `PlayBGM`, `PlaySFX`, `SetMasterVolume`, `SetMusicVolume`, `SetSFXVolume` |
| กล้องตามยาน/สั่น | `Assets/Scripts/CameraFollow.cs`, `Assets/Scripts/CameraShake.cs` | แยกจากระบบต่อสู้หลัก |
| เครื่องมือจัด Layout แม็พ | `Assets/Editor/MapLayoutSetup.cs` | ใช้ใน Unity Editor เพื่อสร้าง/จัดฉาก ไม่ใช่โค้ดที่รันทุกแมตช์โดยตรง |
| เครื่องมือจัด Scene/Prefab | `Assets/Editor/SceneSetupTool.cs`, `Assets/Editor/PrefabSetupTool.cs` | อย่ากด Setup/Generate โดยไม่ทราบว่าจะเขียนทับหรือสร้างอะไร |
| ตรวจภาพ Sprite/สร้างฟอนต์ไทย | `Assets/Editor/CheckSprites.cs`, `Assets/Editor/ThaiFont.cs` | ใช้เฉพาะงาน Editor |
| Prefab ยาน/กระสุน/สกิล | `Assets/Resources/PlayerShip.prefab`, `Assets/Resources/BulletPrefab.prefab`, `Assets/Resources/Skill_*.prefab` | โค้ดสร้างบาง Prefab ด้วยชื่อจาก Resources/Photon |

## ลำดับการทำงานของเกมแบบย่อ

1. `LoginScene` ใช้ `LoginManager` และ `FirebaseManager` เพื่อเข้าเกม/โหลดบัญชี
2. `LobbyScene` ใช้ `LobbyManager` เพื่อโหลดข้อมูล เลือกยาน/สกิล และเข้าห้องผ่าน Photon
3. ผู้เล่น Ready และโฮสต์เริ่มเกม จากนั้นโหลด `SampleScene` และเลือก Layout ผ่าน `GameplayManager`
4. `PlayerController` อ่านค่ายานจาก `BattleLoadoutCatalog` แล้วจัดการการเคลื่อนที่ การเล็ง และการยิง
5. กระสุน สกิล และ Hazard ตรวจการชน แล้วอัปเดต HP/สถานะผ่าน PlayerController กับ Photon RPC
6. เมื่อจบเกม `GameplayManager` แสดงผลและใช้ Firebase บันทึกผล ก่อนออกหรือกลับห้องเดิม

## ค่าตัวเลขและกติกาปัจจุบัน (ตรวจจากโค้ด 7 ต.ค. 2026)

| ยาน | HP | ความเร็ว | ยิงทุก (วินาที) | ดาเมจ/นัด | สกิลเริ่มต้น | ราคา |
|---|---|---|---|---|---|---|
| Nebula Ghost | 90 | 8.4 | 0.20 | 6 | STUN | ฟรี |
| Comet Crusher | 150 | 5.6 | 0.44 | 12 | SHIELD | 2800 |
| Stellar Striker | 115 | 7.0 | 0.28 | 8 | NOVA | 3089 |

| สกิล | ผล | คูลดาวน์ |
|---|---|---|
| STUN | ดาเมจ 8, ติดสตั้น 1 วินาที | 12 วินาที |
| SHIELD | อมตะ 2 วินาที, เร็วขึ้น x1.2 | 16 วินาที |
| NOVA | ระเบิดดาเมจ 30 (ดีเลย์ 1.5 วินาที) | 12 วินาที |
| SEEKER | ขีปนาวุธติดตาม ดาเมจ 22 | 11 วินาที |

กติกา (`GameplayManager.cs`): ใครได้ 3 Kill ก่อนชนะ (`targetKills = 3`), แมตช์ 3 นาที (`matchDuration = 180`) หมดเวลาแล้วคะแนนเท่ากัน = DRAW, ยานถูกทำลายแล้วเกิดใหม่ภายใน 3 วินาที, ผู้เล่นหลุดกลางเกม = ยกเลิกแมตช์และกลับห้องรอ

ค่าในตารางมาจาก `BattleLoadoutCatalog.cs` และ `BattleBalance.cs` ถ้าแก้ค่าในโค้ด ให้แก้ตารางนี้และเล่มรายงานให้ตรงกันด้วย

## สิ่งที่เพิ่มเมื่อ 6 ต.ค. 2026

| ระบบ | ผู้เล่นเห็นอะไร | ไฟล์หลัก |
|---|---|---|
| ออกจากระบบ | Lobby → ปุ่มตั้งค่า → LOG OUT → กลับหน้า Login | `LobbyManager.cs`, `FirebaseManager.cs` (`Logout`), `BattleSettingsPanel.cs` |
| สียาน | หน้าคลังมีแถบ PAINT 6 สี ใช้กับทุกยาน คู่แข่งเห็นสีเดียวกัน | `ShipPaint` ใน `BattleLoadoutCatalog.cs`, `LobbyManager.Views.cs`, `PlayerController.Cosmetics.cs` |
| อีโมต | ปุ่มบน HUD เลือก GG / NICE SHOT! / OOPS! / HELLO! ขึ้นฟองเหนือยาน (คูลดาวน์ 2.5 วินาที) | `PlayerController.Cosmetics.cs`, `GameplayManager.HUD.cs` |
| แจ้งยิงโดน/โดนยิง | ข้อความ HIT เมื่อยิงโดน, ลูกศรบอกทิศเมื่อโดนยิง, ตัวเลขดาเมจลอย | `PlayerController.Health.cs`, `GameplayManager.cs`, `FloatingText.cs` |
| แม็พปริซึมใหม่ | สนามเล็กลง, คริสตัลสะท้อนกระสุนได้ 2 ครั้ง, แท่นวาร์ป 2 คู่ (คูลดาวน์ 3 วินาที), ของขยับในฉาก | `GameplayManager.Maps.cs`, `PrismReflector.cs`, `WarpPad.cs`, `PlayerController.Warp.cs`, `BulletController.cs`, `PrismArenaVisuals.cs`, `PrismBurst.cs` |
| ภาพลื่นขึ้น | ไอพ่น กล้อง ตัวเลขลอย ขยับนุ่มขึ้นโดยไม่เพิ่มภาพใหม่ | `PlayerController.Visuals.cs`, `CameraFollow.cs`, `FloatingText.cs` |
| แก้ในหน้า Edit | กดเมนูแล้ว HUD/หน้าผล/ของในแม็พ มาอยู่ใน Scene ให้ลากแก้ได้ | `EditableTemplate.cs`, `EditableLayout.cs`, ดู `EDITABLE_UI_TH.md` |

ไฟล์ก่อนแก้สำรองไว้ในโฟลเดอร์ `Backup-before-*-20261006` ข้าง ๆ โปรเจกต์ ทุกชุด compile ผ่านแล้ว แต่ต้องทดสอบเล่นจริงใน Unity/มือถือ สองเครื่อง (โดยเฉพาะวาร์ป กระสุนเด้ง อีโมต และสียานที่ต้องส่งผ่าน Photon)

## วิธีแบ่งงานกับเพื่อนให้ไม่ชนกัน

ตัวอย่างการแบ่งที่ชัดเจน:

| ผู้รับผิดชอบ | ไฟล์หลักที่รับได้ | ขอบเขต |
|---|---|---|
| คนที่ดูแล Gameplay/แม็พ | `PlayerController*.cs`, `SkillController.cs`, `BulletController.cs`, `AutoTurret.cs`, `BattleBalance.cs`, `Managers/GameplayManager.Maps.cs`, `Managers/MapHazardManager.cs`, `Managers/HazardController.cs`, `PrismReflector.cs`, `WarpPad.cs`, `PrismArenaVisuals.cs`, `PrismBurst.cs` | การเล่น ยาน สกิล กระสุน แม็พ วาร์ป และคริสตัลสะท้อน |
| คนที่ดูแลเมนู/บัญชี/เสียง | `Managers/LoginManager.cs`, `Managers/LobbyManager*.cs`, `Managers/FirebaseManager.cs`, `BattleSettingsPanel.cs`, `Managers/AudioManager.cs`, `PolishSprites.cs`, `Managers/GameplayManager.HUD.cs` (อีโมต), `FloatingText.cs`, `EditableTemplate.cs` | ล็อกอิน/ออกจากระบบ Lobby คลัง สียาน อีโมต HUD การตั้งค่า เหรียญ และเสียง |
| คนที่ทดสอบ | ไม่จำเป็นต้องแก้โค้ด | ทดสอบสองเครื่อง/มือถือ ทำรายการขั้นตอนและส่งภาพ/อาการที่พบ |

ก่อนเริ่มตกลงกันว่าใครแก้ไฟล์เชื่อมระบบ เช่น `Managers/GameplayManager.cs`, `PlayerController.cs`, `BattleLoadoutCatalog.cs` และ `Managers/GameplayManager.HUD.cs` เพราะหลายระบบอ่าน/เรียกใช้ร่วมกัน หากทำคนละเครื่องให้ส่งไฟล์/ใช้ Git branch แล้วรวมทีละชุด อย่าให้สองคนส่งทับไฟล์เดียวกัน

## คำศัพท์สำคัญ: partial class

`PlayerController.cs` กับ `PlayerController.Movement.cs` เป็นคลาสเดียวกันที่แบ่งข้อความไว้หลายไฟล์ เช่นเดียวกับ `GameplayManager` และ `LobbyManager` การแยกนี้ช่วยให้หาโค้ดง่าย แต่ **ไม่ได้แยกเป็น Component หรือระบบที่คอมไพล์อิสระ**; สคริปต์ทีมอยู่ใน `Assembly-CSharp` เดียวกัน และไฟล์ย่อยยังเข้าถึงตัวแปรร่วมของคลาสได้

## ตัวอย่างเวลาครูให้แก้

- “เพิ่มความเร็วของยานสีแดง” → เปิด `Assets/Scripts/BattleLoadoutCatalog.cs` แล้วหา `Ships`; ความเร็วจริงถูกตั้งจาก catalog ตอนเริ่มเล่น ไม่ใช่แค่ค่าเริ่มต้นใน Prefab
- “เปลี่ยนความแรงกระสุน” → ตรวจค่าพลังโจมตีใน `BattleLoadoutCatalog.cs`; การสร้าง/ตรวจชนอยู่ `Assets/Scripts/PlayerController.cs` และ `Assets/Scripts/BulletController.cs`
- “ปรับเวลาคูลดาวน์สกิล” → `BattleLoadoutCatalog.cs` ส่วนชื่อ/คำอธิบายสกิล; ดาเมจและระยะสถานะอยู่ `BattleBalance.cs`; พฤติกรรมอยู่ `PlayerController.Skills.cs`/`SkillController.cs`
- “เพิ่ม/ลดบึงบนแผนที่ปริซึม” → การสุ่ม/ตำแหน่งที่ `Managers/MapHazardManager.cs`; การทำงานเมื่อชน/อยู่ในเขตที่ `Managers/HazardController.cs`
- “ย้ายหินหรือเสา” → `Managers/GameplayManager.Maps.cs`; หากเป็นตำแหน่งที่สร้างไว้ใน Editor ให้ตรวจ `Assets/Editor/MapLayoutSetup.cs` ด้วย
- “ให้กระสุนเด้งคริสตัลได้ 3 ครั้ง” → `PrismReflector.MaxBounces` ใน `Assets/Scripts/PrismReflector.cs`
- “ย้ายแท่นวาร์ป/เปลี่ยนคูลดาวน์วาร์ป” → ตำแหน่งที่ `AddWarpPads` ใน `Managers/GameplayManager.Maps.cs`; คูลดาวน์ `WarpCooldown` ใน `PlayerController.Warp.cs`
- “เพิ่มสียาน/ข้อความอีโมต” → `ShipPaint.Names/Colors` ใน `BattleLoadoutCatalog.cs` และ `Emotes` ใน `PlayerController.Cosmetics.cs` (เพิ่มต่อท้ายเท่านั้น ห้ามสลับลำดับ)
- “แก้ปุ่มบนหน้าเล่น” → UI ถูกสร้าง/จัดตำแหน่งใน `Managers/GameplayManager.HUD.cs`; การรับ pointer/ลากของปุ่มยิงอยู่ `UIButton.cs`
- “เหรียญไม่ตรงกับฐานข้อมูล” → การอ่าน/เขียนยอดอยู่ `Managers/FirebaseManager.cs`; การแสดงผลอยู่ `LobbyManager.Views.cs` หรือ `GameplayManager.HUD.cs`

## จุดที่ต้องระวังเมื่อแก้

- อย่าสลับลำดับยาน/สกิลใน `BattleLoadoutCatalog.cs` โดยไม่วางแผนย้ายข้อมูลผู้เล่นที่ Firebase เก็บไว้
- อย่าเปลี่ยนชื่อคลาส MonoBehaviour ที่ Scene/Prefab ใช้, ชื่อ public method ที่ปุ่มเรียก, หรือชื่อ `[PunRPC]` โดยไม่ตรวจการอ้างอิงทั้งหมด
- อย่าลบไฟล์ `.meta` หรือคัดลอกไฟล์ทับแบบทำให้ GUID เปลี่ยน เพราะ Scene/Prefab ใช้ GUID ผูกกับสคริปต์และภาพ
- ระบบ Photon ต้องตัดสินใจว่าใครเป็นเจ้าของการทำงาน: โดยทั่วไปโฮสต์สร้าง Hazard ร่วมกัน ส่วนเจ้าของยานควบคุมยานของตัวเอง
- การซื้อ/เพิ่มเหรียญใช้ Firebase transaction; อย่าเขียนยอดใหม่ทับยอดเดิมแบบไม่มี transaction
- โค้ดใน `Assets/Editor/` เป็นเครื่องมือ ไม่ใช่ไฟล์ที่ต้องเพิ่ม Component ใน Scene; Setup บางตัวอาจแก้ Scene/Prefab
- ไฟล์ `.slnx`, `.csproj`, `Library/`, `.utmp/` ส่วนใหญ่เป็นไฟล์/ข้อมูลของ Unity หรือเครื่องมือ ให้แก้เฉพาะเมื่อรู้ว่ากำลังแก้การตั้งค่า build ไม่ใช่ใช้แทนซอร์สโค้ด

## คู่มือและสถานะการทดสอบ

- คู่มือแผนที่โค้ดฉบับนี้: `Docs.คู่มือ/CODE_GUIDE_TH.md`
- แผนงานเก่า: `Docs.คู่มือ/GAME_POLISH_PLAN.md` (ลงวันที่ 24 ก.ย. 2026; เป็น backlog ณ เวลานั้น ไม่ใช่สถานะปัจจุบัน)
- บันทึกการตรวจ Lobby: `Docs.คู่มือ/Lobby-validation.md` (ลงวันที่ 5 ก.ย. 2026; รายการทดสอบในเอกสารอาจเก่ากว่าการทดสอบรอบล่าสุด)
- วิธีแก้ HUD/แม็พในหน้า Edit: `Docs.คู่มือ/EDITABLE_UI_TH.md`
- เจ้าของโปรเจกต์แจ้งเมื่อ 3 ต.ค. 2026 ว่าทดสอบเกมทั้งหมดแล้วและผ่าน; ระบบที่เพิ่มเมื่อ 6 ต.ค. 2026 ตรวจ compile แล้ว ต้องทดสอบเล่นจริงอีกรอบ

ก่อนส่งงาน/หลังแก้โค้ด ให้เปิด Unity รอ compile แล้วตรวจ Console ว่าไม่มี error หรือ Missing Script จากนั้นทดลองส่วนที่แก้จริง หากเปลี่ยน Photon/Firebase/UI ให้ทดสอบสองเครื่องและมือถือเมื่อเกี่ยวข้อง การ compile ผ่านเพียงอย่างเดียวไม่ยืนยันว่าการเล่นจริงถูกต้อง
