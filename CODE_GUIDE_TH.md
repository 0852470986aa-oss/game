# คู่มือหาโค้ดและแก้เกม

เริ่มอ่านจากไฟล์นี้ก่อน ไม่จำเป็นต้องอ่านทั้งโปรเจกต์พร้อมกัน
เอกสารนี้อธิบายโค้ดปัจจุบัน ไม่ใช่รายการยืนยันว่าทุกระบบผ่านการทดสอบแล้ว

## อยากแก้อะไร เปิดที่ไหน

| งาน | ไฟล์ภายใต้ Assets/Scripts | จุดที่เริ่มอ่าน |
|---|---|---|
| เลือด ดาเมจ ความเร็ว จังหวะยิงแต่ละยาน | BattleLoadoutCatalog.cs | Ships |
| ชื่อ ไอคอน คูลดาวน์สกิล | BattleLoadoutCatalog.cs | Skills |
| การเคลื่อนที่และเล็ง | PlayerController.Movement.cs | HandleMovement, HandleAiming; FixedUpdate ยังอยู่ไฟล์หลัก |
| การยิงและอัปเดตยานแต่ละเฟรม | PlayerController.cs | Shoot, HandleShooting, Update, FixedUpdate |
| ดาเมจสกิล/แม็พและระยะเวลาสถานะ | BattleBalance.cs | ค่าคงที่ชื่อสื่อความหมาย; ไม่เปลี่ยน index ยาน/สกิล |
| การใช้สกิล โล่ สตั้น และบึง | PlayerController.Skills.cs | UseSkill, ActivateShieldRPC, ApplyStunRPC, UpdateEffectiveSpeed |
| เลือด ตาย เกิดใหม่ และผลกระทบเมื่อโดน | PlayerController.Health.cs | TakeDamage, Die, RespawnRoutine |
| ไอพ่นและภาพยาน | PlayerController.Visuals.cs | LateUpdate, ShipEffectSprite, PlaySheetBurst |
| วิถีจรวด ระเบิด และภาพสกิล | SkillController.cs | Update, DetonateNova, SkillSheetVisual |
| กระสุนชนหิน/ผู้เล่น | BulletController.cs | ProjectileSweep, OnTriggerEnter2D |
| จัดวางปริซึมและหุ่นยนต์ | Managers/GameplayManager.Maps.cs | DecoratePrism, PrepareMechCover |
| ขอบเขตแม็พและจุดเกิดปลอดภัย | Managers/GameplayManager.Maps.cs | ArenaHalfWidths, GetArenaMin/Max, TryFindSafeSpawn |
| แมงกะพรุน แรงดูด และวังวนตกแต่ง | Managers/MapHazardManager.cs | JellyArenaVisuals |
| สุ่มอุกกาบาต สายฟ้า และบึง | Managers/MapHazardManager.cs | SpawnHazardRoutine, SpawnPrismSwamp |
| ดาเมจ/ภาพอันตรายแม็พ | Managers/HazardController.cs | MoltenContactSurface, HazardRoutine, OnTriggerEnter2D |
| ป้อมยิง | AutoTurret.cs | detectionRadius, fireRate, damage, ClearSight |
| HUD เลือด ปุ่ม และหน้าสรุป | Managers/GameplayManager.HUD.cs | StyleBattleHUD, BuildResultUI, LateUpdate |
| ข้อความสถานะ สกิล และแจ้งฆ่า | Managers/GameplayManager.StatusUI.cs | UpdateCombatStatuses, UpdateSkillUI |
| ผลการแข่งขัน รางวัล กลับห้องเดิม | Managers/GameplayManager.Results.cs | ShowResultScreen, RequestSameRoom, CheckSameRoomReturn |
| ตั้งค่าเสียงและความไวเล็ง/ยืนยันออก | BattleSettingsPanel.cs | Show, Build, Row, Confirm |
| ภาพเหรียญ/ตัดภาพบึงขณะรัน | PolishSprites.cs | Coin, Swamp, AddCoinIcon |
| การกดและลากปุ่มยิง | UIButton.cs | OnPointerDown, OnDrag, OnPointerUp |
| วงแหวนปุ่มและวงคูลดาวน์ | ControlRingGraphic.cs | SetRing, OnPopulateMesh |
| เสียงและเพลงสังเคราะห์ | Managers/AudioManager.cs | CreateMusic, Placeholder, NormalizeGeneratedAudio |
| หน้าล็อกอิน | Managers/LoginManager.cs | BuildLoginStyle, LoginGoogle, LoginGuest |
| หน้าหลัก/คลัง และโหลดข้อมูลมาแสดง | Managers/LobbyManager.Views.cs | BuildHomeScreen, BuildHangarScreen, LoadLobbyProfile |
| ซื้อ เลือกยาน และติดตั้งสกิล | Managers/LobbyManager.Inventory.cs | OnInventoryActionClicked, OnInstallSkillClicked |
| เชื่อมต่อ สร้าง เข้าห้อง และ callback ห้อง | Managers/LobbyManager.Rooms.cs | OnJoinedRoom, OnCreateRoomConfirm, OnDisconnected |
| เริ่มต้นล็อบบี้ Ready และเลือกแม็พ | Managers/LobbyManager.cs | Start, OnReadyButtonClicked, TryLaunchConfirmedRoom |
| บัญชี เหรียญ ซื้อยาน และบันทึกผล | Managers/FirebaseManager.cs | GetCoinBalance, PurchaseShip, AddCoins, RecordMatchResult |

## ลำดับการทำงานที่ควรเข้าใจ

1. LoginManager รับการกดเข้าเกม → FirebaseManager ตรวจบัญชี
2. LobbyManager โหลดข้อมูลผู้เล่น → เลือกยาน/สกิล → สร้างหรือเข้าห้อง Photon
3. ผู้เล่น Ready → โฮสต์เริ่มเกม → GameplayManager เลือกแม็พและสร้างยาน
4. PlayerController อ่าน BattleLoadoutCatalog แล้วควบคุมยานของผู้เล่นเจ้าของ
5. กระสุน สกิล และอันตรายตรวจการชน → ลด HP → ตายและเกิดใหม่
6. GameplayManager สรุปผล → FirebaseManager บันทึก → กลับห้องเดิมหรือออกล็อบบี้

## ตัวอย่างโจทย์อาจารย์

- ให้ยานเร็วขึ้น: แก้ spd ของยานใน Ships ไม่ใช่ค่า speed ใน Prefab เพราะตอนเริ่มเกมถูกแทนด้วยค่าจาก catalog
- ให้ยิงแรงขึ้น: แก้ atk; คิดดาเมจต่อวินาทีคร่าว ๆ ด้วย atk / shotInterval
- ให้สกิลพร้อมไวขึ้น: ลด cooldown ใน BattleLoadoutCatalog.Skills; ดาเมจสกิลอยู่ใน BattleBalance
- เปลี่ยนปริมาณบึง: แก้เพดาน count และช่วงสุ่มใน SpawnPrismSwamp/SpawnHazardRoutine
- เปลี่ยนชื่อปุ่ม: ดูโค้ดสร้าง UI ก่อน เพราะหลายหน้าสร้างใหม่ตอนรัน ไม่ได้ใช้ข้อความ Scene โดยตรง

## จุดที่ต้องระวัง

- ลำดับแม็พในโค้ด: 0 แมงกะพรุน, 1 ปริซึม, 2 หุ่นยนต์ ไม่ตรงลำดับในเล่ม
- อย่าเปลี่ยนชื่อ public method ที่ผูกกับปุ่มหรือชื่อ RPC โดยไม่ตรวจจุดเรียกทั้งหมด
- อย่าลบไฟล์ .meta ของสคริปต์/ภาพเดิม: Scene และ Prefab อ้างอิงด้วย GUID
- Photon: โฮสต์สร้างอันตรายร่วมกัน; เจ้าของยานควบคุมการเคลื่อนที่ ไม่ให้ทุกเครื่องทำรายการเดียวกันซ้ำ
- Firebase: ซื้อยานและเพิ่มเหรียญใช้ transaction ไม่เขียนยอดเก่าทับยอดใหม่
- ไม่สลับ index ยาน/สกิล เพราะเกี่ยวข้องกับข้อมูลผู้เล่นที่บันทึกแล้ว
- คอมไพล์ผ่านไม่ได้แปลว่าภาพ/ระบบออนไลน์ถูกต้อง ต้องทดสอบ Scene และสองเครื่องด้วย

## สถานะการจัดโค้ด

- แยก ShipData, SkillData และ BattleLoadoutCatalog ออกจาก LobbyManager แล้ว โดยคงชื่อคลาสและค่าทั้งหมด
- แยก BattleSettingsPanel, PolishSprites และ ControlRingGraphic ออกจาก UIButton แล้ว; คง UIButton.cs และ GUID เดิมเพื่อรักษาปุ่มใน Scene/Prefab
- แยกการจัดวางแม็พและหาจุดเกิดไป GameplayManager.Maps.cs โดยใช้ partial class: ยังเป็น GameplayManager ตัวเดิม ไม่ต้องลากสคริปต์ใหม่ลง Scene

### partial class คืออะไร

เป็นการแบ่งคลาสเดียวไว้หลายไฟล์เพื่ออ่านง่าย ไม่ใช่สร้างระบบหรือ Component ตัวใหม่
GameplayManager.cs คุมการแข่งขัน ส่วน GameplayManager.Maps.cs คุมแม็พ ทั้งสองไฟล์เข้าถึงสมาชิกของคลาสเดียวกันได้
ถ้าอาจารย์ให้แก้ตำแหน่งหิน เปิดไฟล์ Maps; ถ้าให้แก้ผลการแข่งขัน เปิดไฟล์หลัก
- ยังไม่ย้าย MonoBehaviour ที่ผูก Scene/Prefab เพื่อไม่ให้การอ้างอิงหลุด
- แยกส่วนหลักของ PlayerController, GameplayManager และ LobbyManager ตามตารางด้านบนแล้ว
- รวมค่าดาเมจและสถานะสำคัญใน BattleBalance โดยใช้ค่าเดิม ไม่ปรับเกมในรอบจัดโค้ด
- ไฟล์หลักเก็บ Component เดิมและจุดเริ่มต้นไว้; ไม่ต้องเปลี่ยนการผูกใน Inspector
- ไฟล์ย่อยบางไฟล์ยังใช้ข้อมูลร่วมกับไฟล์หลักผ่าน partial โดยตั้งใจ ไม่ใช่ระบบอิสระ

## ตรวจหลังจัดโค้ดก่อนส่งอาจารย์

1. เปิด Unity รอ import และตรวจว่าไม่มี Missing Script/compile error
2. ทดสอบล็อกอิน คลัง ซื้อยาน และตั้งค่า
3. ทดสอบเคลื่อนที่ ยิง ทุกสกิล และอันตรายทั้งสามแม็พ
4. ทดสอบสองเครื่อง: Ready, เริ่มเกม, ตาย/เกิดใหม่, จบเกม, กลับห้องเดิมและหลุดเชื่อมต่อ
5. บิลด์ Android แล้วตรวจปุ่มและเสียงบนมือถือ

การตรวจ build ด้วย dotnet และเทียบโค้ดที่ย้ายช่วยลดความเสี่ยง แต่ไม่แทนการตรวจ Unity/มือถือจริง
