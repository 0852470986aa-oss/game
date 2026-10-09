# คู่มือการทำงานฉบับย่อ (อ่านก่อนสอบ / ก่อนแก้โค้ด)

> **ส่วนที่ 1** = กดอะไรแล้วโค้ดวิ่งไปไฟล์ไหน ทำอะไร ตั้งแต่ล็อกอินจนจบแมตช์
> **ส่วนที่ 2** = ถ้าอาจารย์สั่งเพิ่ม/แก้ (เช่น ปุ่มเปลี่ยนชื่อ ช่องกรอกชื่อ) ต้องไปเขียนที่ไหน พร้อมโค้ดตัวอย่างที่คอมไพล์ผ่านแล้ว
>
> ไฟล์โค้ดทั้งหมดอยู่ใต้ `My project/Assets/Scripts/` (ตัวจัดการหลักอยู่ใน `Managers/`) อัปเดต 9 ต.ค. 2026

---

# ส่วนที่ 1 — เส้นทางการทำงานของเกม

## ภาพรวม 3 ฉาก

```
LoginScene ──ล็อกอินสำเร็จ──▶ LobbyScene ──โฮสต์กดเริ่ม / เล่นกับบอท──▶ SampleScene (สนามรบ)
 LoginManager                 LobbyManager                               GameplayManager + PlayerController
     ▲                             ▲                                              │
     └──── กด LOG OUT ────────────┘◀──────────── จบแมตช์ กดกลับล็อบบี้ ────────────┘
```

ตัวที่อยู่ข้ามทุกฉาก (สร้างครั้งเดียว ไม่ถูกลบตอนเปลี่ยนฉาก): `FirebaseManager` (บัญชี/ฐานข้อมูล), `AudioManager` (เสียง), Photon (`PhotonNetwork`, ระบบออนไลน์)

## ขั้นที่ 1: หน้าล็อกอิน — `Managers/LoginManager.cs`

| ลำดับ | เกิดอะไรขึ้น | ฟังก์ชัน |
|---|---|---|
| 1 | เปิดเกม → ล็อกจอแนวนอน → รอ Firebase พร้อม (ไม่เกิน 10 วิ) แล้วเปิดปุ่ม | `Start` → `WaitForFirebase` |
| 2 | กดปุ่ม Google หรือ Guest | `LoginGoogle` / `LoginGuest` |
| 3 | ส่งต่อให้ Firebase ล็อกอินจริง | `FirebaseManager.LoginGoogle` / `LoginGuest` |
| 4 | อ่านโปรไฟล์จากฐานข้อมูล `users/{uid}` ถ้ามี = ใช้ชื่อเดิม / ไม่มี = สร้างบัญชีใหม่ (เหรียญเริ่ม 5000) | `FirebaseManager.EnsureUserRecord` |
| 5 | สำเร็จ → รอ 0.5 วิ → โหลด LobbyScene / ล้มเหลว → แสดงข้อความ error เปิดปุ่มใหม่ | `OnLoginSuccess` → `LoadLobby` / `OnLoginFailed` |

หน้านี้ **ปุ่มวางไว้ในฉาก** (ลากแก้ใน Unity ได้เลย) ต่างจากหน้าอื่นที่โค้ดสร้างให้

## ขั้นที่ 2: ล็อบบี้ — `Managers/LobbyManager*.cs` (คลาสเดียว แบ่งหลายไฟล์)

| ลำดับ | เกิดอะไรขึ้น | ฟังก์ชัน / ไฟล์ |
|---|---|---|
| 1 | **สร้าง UI ทั้งหมดด้วยโค้ด** (หน้าหลัก คลัง ห้องรอ ฯลฯ) | `Awake` → `BuildLobbyUI` (`LobbyManager.Views.cs`) |
| 2 | เปิดเพลงล็อบบี้ | `Start` → `AudioManager.PlayBGM("BGM_Lobby")` |
| 3 | โหลดข้อมูลผู้เล่น: เลเวล/ภารกิจ, ตีบวก/ไอเท็ม, แรงค์, ชื่อ, เหรียญ, ยาน, สกิล | `LoadLobbyProfile` (`Views.cs`) → `Progression`, `Economy.Load`, `Ranked.Load`, `FirebaseManager.GetCoinBalance` … |
| 4 | ตั้งชื่อที่คนอื่นเห็นในเกมออนไลน์ = ชื่อบัญชี | `PhotonNetwork.NickName = GetUsername()` |
| 5 | ต่อเซิร์ฟเวอร์ Photon → เข้า Lobby → ขึ้น "Ready!" | `ConnectUsingSettings` → `OnConnectedToMaster` → `OnJoinedLobby` (`LobbyManager.Rooms.cs`) |

**ปุ่มหน้าหลัก → โค้ดที่ทำงาน**

| ปุ่ม (ไทย / อังกฤษ) | ฟังก์ชันที่เรียก | ไฟล์ |
|---|---|---|
| จับคู่ด่วน / QUICK MATCH | `OnPlayButtonClicked` | `LobbyManager.Rooms.cs` |
| เลือกโหมด / CHOOSE MODE | `OpenPlayMenu` (หน้าการ์ดโหมด) | `LobbyManager.Navigation.cs`, `LobbyManager.NewLayout.cs` |
| โรงเก็บยาน / HANGAR | `ShowInventoryPanel` (ยาน สกิล ตีบวก ไอเท็ม ร้านค้า) | `LobbyManager.Inventory.cs`, `LobbyManager.Workshop.cs` |
| แรงค์ / RANKED | `OpenRanked` | `LobbyManager.Ranked.cs`, `Ranked.cs` |
| ภารกิจ / MISSIONS, โปรไฟล์ / PROFILE | `OpenProgressPage` | `LobbyManager.Progress.cs`, `Progression.cs` |
| สังคม / SOCIAL (เพื่อน แชท กิลด์) | `OpenSocial` | `LobbyManager.Social.cs`, `Social.cs` |
| ตั้งค่า / SETTINGS | `OnSettingsClicked` | `BattleSettingsPanel.cs`, `BattleSettingsPanel.Unified.cs` |

## ขั้นที่ 3: เข้าสนามรบ (2 ทาง)

**ทาง A — เล่นกับบอท (ออฟไลน์)** `LobbyManager.Solo.cs`
`StartSolo` → ตัดเน็ต Photon → รอ 1 เฟรม `EnterOfflineSoloNextFrame` → สร้างห้องออฟไลน์ `CreateSoloRoom` → `LaunchSoloRoom` → `PhotonNetwork.LoadLevel("SampleScene")`

**ทาง B — ออนไลน์กับคนจริง** `LobbyManager.Rooms.cs` + `LobbyManager.cs`
สร้างห้อง `OnCreateRoomConfirm` หรือเข้าห้อง → `OnJoinedRoom` (ไปหน้าห้องรอ) → ทุกคนกด **READY** `OnReadyButtonClicked` → โฮสต์กด **START BATTLE** `OnStartGameClicked` → `TryLaunchConfirmedRoom` (เช็คความพร้อม แบ่งทีม เติมบอท) → `PhotonNetwork.LoadLevel("SampleScene")` → **ทุกเครื่องเปลี่ยนฉากพร้อมกัน** (เพราะตั้ง `AutomaticallySyncScene = true` ใน `Awake`)

ค่าที่ส่งไปสนามรบผ่าน "ข้อมูลห้อง/ผู้เล่น" ของ Photon: ยาน `ShipType`, สกิล `SkillType`, แม็พ `MapIndex`, กติกา (Kill/เวลา/โหมด)

## ขั้นที่ 4: สนามรบ — `Managers/GameplayManager*.cs` + `PlayerController*.cs`

| ลำดับ | เกิดอะไรขึ้น | ฟังก์ชัน / ไฟล์ |
|---|---|---|
| 1 | อ่านกติกาจากห้อง เปิดเพลงต่อสู้ ติดกล้อง/มินิแม็พ | `GameplayManager.Start` |
| 2 | สร้าง HUD (เลือด เวลา ปุ่มยิง จอย) และหน้าผล | `CreateMatchUI` (`GameplayManager.HUD.cs`) |
| 3 | จัดแม็พตาม `MapIndex` (0 แมงกะพรุน / 1 ปริซึม / 2 หุ่นยนต์ / 3 สถานี / 4 ลาวา) | `ApplySelectedMapLayout` (`GameplayManager.Maps.cs`, `PrismV3.cs`, `NewMaps.cs`) |
| 4 | สร้างยานของเรา (ทุกเครื่องเห็น) + บอท | `PhotonNetwork.Instantiate` ใน `GameplayManager.cs` / `Multi.cs`, `SpawnBotsIfNeeded` (`Bots.cs`) |
| 5 | ยานอ่านค่าพลังจาก catalog แล้วรับการบังคับ: เดิน/เล็ง ยิง สกิล | `PlayerController.cs`, `.Movement.cs`, `.Shooting.cs`, `.Skills.cs` / ค่าพลัง `BattleLoadoutCatalog.cs` |
| 6 | กระสุน/สกิลโดน → ลดเลือด → ตาย → นับ Kill → เกิดใหม่ 3 วิ | `BulletController.cs`, `SkillController.cs` → `PlayerController.Health.cs` (`TakeDamage`, `Die`, `RespawnRoutine`) |
| 7 | ทุกเฟรมเช็คเวลา/คะแนน ครบเงื่อนไข = จบ | `Update` → `UpdateMatchTimerAndScore` → `EndMatch` (`GameplayManager.cs`) |
| 8 | หน้าผล + ให้เหรียญ (ชนะ 190 / แพ้ 10, เล่นกับบอท 60 / 5) + XP/ภารกิจ | `ShowResultScreen` (`Results.cs`) → `FirebaseManager.RecordMatchResult`, `Progression.RecordMatch` (`GameplayManager.Progress.cs`) |
| 9 | กดเล่นห้องเดิมอีกรอบ / กลับล็อบบี้ | `RequestSameRoom` / โหลด `LobbyScene` (`Results.cs`) |

## ข้อมูลถูกเก็บที่ไหน

| ข้อมูล | ที่เก็บ | อ่าน/เขียนด้วย |
|---|---|---|
| ชื่อ เหรียญ ยานที่ปลดล็อก ยาน/สกิลที่ใช้ ชนะ/แพ้ | Firebase `users/{uid}/...` | `FirebaseManager.cs` (`GetUsername`, `UpdateUsername`, `AddCoins`, `SaveSelectedShip` …) |
| เลเวล XP ภารกิจ Achievement ประวัติแมตช์ ฉายา | คลาส `PlayerProgress` → แปลงเป็น JSON → เครื่อง + Firebase | `Progression.cs` (`Progression.Data`, `Progression.Save()`) |
| ตีบวก ไอเท็ม ร้านรายวัน | JSON ของ `Economy` | `Economy.cs` |
| ตั้งค่าเสียง ภาษา ปุ่ม | `PlayerPrefs` (ในเครื่องเท่านั้น) | `AudioManager.cs`, `GameSettings.cs`, `Lang.cs` |
| ข้อมูลระหว่างแมตช์ (Kill ทีม ยาน) | Photon Custom Properties ของห้อง/ผู้เล่น | `MatchRules.cs`, `LobbyManager.Rooms.cs` |
| เปิด/ปิดฟีเจอร์ | `FeatureFlags.cs` (+ Firebase `feature_flags/`) | ทุกไฟล์เช็ค `FeatureFlags.ชื่อ` |

---

# ส่วนที่ 2 — ถ้าต้องเพิ่ม / แก้โค้ด

## หลัก 3 ข้อที่ต้องรู้ก่อน

1. **UI ล็อบบี้และสนามรบสร้างด้วยโค้ด** ไม่ได้ลากวางในฉาก → เพิ่มปุ่ม = เพิ่มโค้ด 1 บรรทัด (ยกเว้นหน้าล็อกอิน ใช้ของในฉาก)
2. **ตำแหน่ง x, y นับจากกลางจอ** (พื้นที่ 1280×720) x บวก = ขวา, y บวก = ขึ้น เช่น (0,0) กลางจอ, (425,270) มุมขวาบน
3. **ข้อความในโค้ดเขียนภาษาอังกฤษ** แล้วเกมแปลไทยให้จากตาราง `Lang.cs` → ข้อความใหม่ต้องเพิ่มคู่คำแปลด้วย ไม่งั้นจะขึ้นอังกฤษตอนเลือกไทย

## ฟังก์ชันสำเร็จรูปสำหรับสร้าง UI (เรียกใช้ได้เลย)

| ใช้ใน | ฟังก์ชัน | สร้างอะไร |
|---|---|---|
| ล็อบบี้ (`LobbyManager` ทุกไฟล์) | `UIButton(ชื่อ, พ่อ, "ข้อความ", x, y, กว้าง, สูง, ฟังก์ชันตอนกด)` | ปุ่ม (มีเสียงคลิกให้เอง) |
| | `UILabel(ชื่อ, พ่อ, "ข้อความ", x, y, กว้าง, สูง, ขนาดตัวอักษร, สี)` | ตัวหนังสือ |
| | `UIPanel(ชื่อ, พ่อ, x, y, กว้าง, สูง, สี)` | กล่องสี/พื้นหลัง |
| | `CreateInput(ชื่อ, พ่อ, x, y, กว้าง, สูง, "ข้อความจาง", จำนวนตัวอักษรสูงสุด)` | **ช่องพิมพ์** (คืนค่า `TMP_InputField` อ่านค่าด้วย `.text`) |
| สนามรบ (`GameplayManager.HUD.cs`) | `BattlePanel`, `BattleLabel`, `BattleRect` | กล่อง / ตัวหนังสือ / พื้นที่ บน HUD |
| หน้าตั้งค่า | แถวในตาราง `UnifiedLayout` (`BattleSettingsPanel.Unified.cs`) | แถวตั้งค่าใหม่ในหมวด |

## จะเพิ่มของที่หน้าไหน → เปิดไฟล์ไหน

| อยากเพิ่ม/แก้ที่ | ไฟล์ | ฟังก์ชันที่สร้างหน้านั้น |
|---|---|---|
| หน้าล็อกอิน | ลากใน `Assets/Scenes/LoginScene.unity` + โค้ด `Managers/LoginManager.cs` | (ของในฉาก) |
| หน้าหลักล็อบบี้ (ปุ่มใหญ่ฝั่งขวา) | `Managers/LobbyManager.Navigation.cs` | `BuildHomeNavigation` (ปุ่มแรกสร้างใน `BuildHomeScreen` ของ `Views.cs` แล้วถูกย้ายตำแหน่งที่นี่) |
| หน้าโปรไฟล์ | `Managers/LobbyManager.Progress.cs` | `BuildProfilePage` |
| หน้าภารกิจ | `Managers/LobbyManager.Progress.cs` | `BuildMissionsPage` |
| โรงเก็บยาน / ร้านค้า / ไอเท็ม | `Managers/LobbyManager.Workshop.cs`, `Inventory.cs` | แท็บต่าง ๆ ใน Workshop |
| ห้องรอ (ปุ่ม READY / START) | `Managers/LobbyManager.Views.cs` (สร้าง) + `NewLayout.cs` `LayoutPrepRoom` (จัดตำแหน่ง) | |
| หน้าเลือกโหมด | `Managers/LobbyManager.Navigation.cs`, `NewLayout.cs` | `BuildHomeNavigation`, `ApplyModeCardLayout` |
| หน้าตั้งค่า | `BattleSettingsPanel.cs`, `BattleSettingsPanel.Unified.cs` | `UnifiedLayout` |
| HUD ระหว่างเล่น | `Managers/GameplayManager.HUD.cs` | `CreateMatchUI` |
| หน้าผลการแข่ง | `Managers/GameplayManager.Results.cs` | `ShowResultScreen` |
| ค่าพลังยาน/สกิล/ราคา | `BattleLoadoutCatalog.cs`, `BattleBalance.cs` | ตาราง `Ships`, `Skills` |
| เหรียญรางวัลชนะ/แพ้ | `Managers/GameplayManager.Bots.cs` | `WinReward`, `LoseReward` |

## ขั้นตอนทุกครั้งที่แก้ (เช็คลิสต์)

1. **สำรองไฟล์** ที่จะแก้ก่อน (คัดลอกไปโฟลเดอร์ `Backup-before-...`)
2. **เพิ่มสวิตช์** ใน `FeatureFlags.cs` (2 จุด: ตัวแปร `public static bool ชื่อ = true;` และบรรทัด `case nameof(ชื่อ)` ใน `Apply`) แล้วครอบโค้ดใหม่ด้วย `if (FeatureFlags.ชื่อ)` → พังเมื่อไหร่ปิดได้ทันที
3. เขียนโค้ด + **คอมเมนต์ภาษาไทย** บรรทัดบนของเมธอด/ท้ายบรรทัดตัวแปร
4. ข้อความใหม่ → เพิ่มคำแปลใน `Lang.cs` ตาราง `Table` เช่น `{ "SAVE NAME", "บันทึกชื่อ" },`
5. กลับไป Unity รอคอมไพล์ → Console ต้องไม่มีสีแดง → กด Play ทดสอบจริง
6. จดใน `Docs.คู่มือ/PHASE_LOG_TH.md` ว่าแก้อะไร ไฟล์ไหน สวิตช์ชื่ออะไร

---

## ตัวอย่างที่ 1: "เพิ่มช่องเปลี่ยนชื่อ + ปุ่มบันทึก" ในหน้าโปรไฟล์

ระบบเขียนชื่อลงฐานข้อมูล **มีอยู่แล้ว** คือ `FirebaseManager.UpdateUsername(ชื่อใหม่, callback)` แต่ยังไม่มีปุ่มเรียก → งานนี้แค่สร้าง UI แล้วเรียกใช้

**ไฟล์: `Managers/LobbyManager.Progress.cs` ในฟังก์ชัน `BuildProfilePage`** ต่อจากบรรทัดปุ่ม `ChangeTitle`:

```csharp
        UIButton("ChangeTitle", page, "CHANGE TITLE", 225, 270, 200, 54, Progression.CycleTitle); // (บรรทัดเดิม)
        // ช่องกรอกชื่อใหม่ + ปุ่มบันทึก (สวิตช์ RenamePilot)
        if (FeatureFlags.RenamePilot)
        {
            var nameInput = CreateInput("NewNameInput", page, 225, 225, 200, 40, "New name (3-16)", 16);
            UIButton("SaveName", page, "SAVE NAME", 425, 225, 170, 40, () => SaveNewName(nameInput.text));
        }
```

**เพิ่มเมธอดใหม่ไว้ท้ายไฟล์เดียวกัน** (ก่อนปีกกา `}` ตัวสุดท้าย):

```csharp
    // บันทึกชื่อใหม่: ตรวจความยาว -> เขียน Firebase -> ตั้งชื่อใน Photon -> อัปเดตชื่อบนจอ
    private void SaveNewName(string newName)
    {
        newName = (newName ?? "").Trim();
        if (newName.Length < 3 || newName.Length > 16) { UpdateStatus("Name must be 3-16 characters."); return; }
        if (FirebaseManager.Instance == null) return;
        FirebaseManager.Instance.UpdateUsername(newName, ok =>
        {
            if (!ok) { UpdateStatus("Could not change name. Try again."); return; }
            Photon.Pun.PhotonNetwork.NickName = newName;          // ชื่อที่คนอื่นเห็นในห้อง/ในสนาม
            if (playerNameText != null) playerNameText.text = newName; // ชื่อบนหน้าหลัก
            UpdateStatus("Name changed!");
            BuildPage(ProgressPage.Profile);                       // วาดหน้าโปรไฟล์ใหม่ให้เห็นชื่อใหม่
        });
    }
```

**สวิตช์: `FeatureFlags.cs`** (ใต้ `AudioListenerGuard`)

```csharp
    // ช่องเปลี่ยนชื่อในหน้าโปรไฟล์
    public static bool RenamePilot = true;
    // ...และใน Apply:
                case nameof(RenamePilot): RenamePilot = on; break;
```

**คำแปล: `Lang.cs` ตาราง `Table`** → `{ "SAVE NAME", "บันทึกชื่อ" }, { "Name changed!", "เปลี่ยนชื่อแล้ว!" }, { "Name must be 3-16 characters.", "ชื่อต้องยาว 3-16 ตัวอักษร" }, { "Could not change name. Try again.", "เปลี่ยนชื่อไม่สำเร็จ ลองใหม่" },`

ผลที่ได้: ชื่อใหม่ถูกเก็บใน `users/{uid}/username` → ล็อกอินครั้งหน้าก็ยังเป็นชื่อใหม่ (`EnsureUserRecord` อ่านชื่อนี้) / ตารางอันดับเปลี่ยนชื่อตามเมื่อ MMR อัปเดตครั้งถัดไป (`PublishLeaderboard` ใช้ `GetUsername()`)

## ตัวอย่างที่ 2: "เพิ่มช่องกรอกชื่อในหน้าล็อกอิน" (ก่อนกด Guest)

หน้าล็อกอินใช้ของในฉาก → ทำ 2 ส่วน

**ส่วน Unity (ลากวาง):** เปิด `LoginScene` → คลิกขวาที่ `Canvas` → **UI > Input Field - TextMeshPro** → ตั้งชื่อ `GuestNameInput` วางเหนือปุ่ม PLAY AS GUEST → ช่อง Placeholder พิมพ์ "Your name"

**ส่วนโค้ด: `Managers/LoginManager.cs`**

```csharp
    public UnityEngine.UI.Button guestButton; // (บรรทัดเดิม)
    public TMP_InputField guestNameInput; // ช่องกรอกชื่อก่อนเข้าเล่น (ลากใส่ใน Inspector)
```

ในฟังก์ชัน `OnLoginSuccess` ใส่ก่อนบรรทัด `SetStatus(...)`:

```csharp
        string typed = guestNameInput != null ? guestNameInput.text.Trim() : "";
        if (typed.Length >= 3 && FirebaseManager.Instance != null)
        {
            FirebaseManager.Instance.UpdateUsername(typed); // เขียนชื่อที่พิมพ์ลงฐานข้อมูล
            username = typed;
        }
```

แล้วกลับ Unity → เลือก `LoginManager` ใน Hierarchy → ลาก `GuestNameInput` ใส่ช่อง **Guest Name Input** ใน Inspector (ไม่ลาก = ช่องเป็น null โค้ดข้ามไปเอง ไม่ error)

## ตัวอย่างที่ 3: "เพิ่มข้อมูลใหม่ให้ผู้เล่น" (เช่น คำแนะนำตัว / สีโปรด)

เลือกที่เก็บตามความต้องการ:

| ต้องการ | ทำแบบนี้ | ไฟล์ |
|---|---|---|
| เก็บในเครื่องอย่างเดียว (เช่น ตั้งค่า) | `PlayerPrefs.SetString("Bio", ค่า); PlayerPrefs.Save();` อ่าน `PlayerPrefs.GetString("Bio", "")` | ที่ไหนก็ได้ |
| เก็บถาวรไปกับบัญชี **ง่ายสุด** | เพิ่มตัวแปรใน `class PlayerProgress` เช่น `public string bio = ""; // คำแนะนำตัว` แล้วตั้งค่า `Progression.Data.bio = ...; Progression.Save();` → บันทึกลงเครื่อง + Firebase ให้เอง | `Progression.cs` |
| เก็บเป็นช่องแยกในฐานข้อมูล | เพิ่มเมธอดใน `FirebaseManager` แบบเดียวกับ `UpdateUsername`: `dbReference.Child("users").Child(user.UserId).Child("bio").SetValueAsync(ค่า)` | `Managers/FirebaseManager.cs` |
| ให้คนอื่นในห้องเห็น | ตั้ง Photon Custom Property ของผู้เล่น (ดูตัวอย่าง `StatBonus` ใน `LobbyManager.UpgradeStats.cs`) | `LobbyManager.*.cs` |

> เพิ่มตัวแปรใน `PlayerProgress` ได้ แต่ **อย่าลบ/เปลี่ยนชื่อตัวแปรเดิม** เพราะข้อมูลผู้เล่นเก่าใน Firebase อ้างชื่อเดิมอยู่

## ตัวอย่างงานสั้น ๆ ที่เจอบ่อย

| อาจารย์สั่ง | แก้ตรงไหน |
|---|---|
| เปลี่ยนข้อความบนปุ่ม | ข้อความอังกฤษในบรรทัด `UIButton(..., "ข้อความ", ...)` + คำแปลไทยใน `Lang.cs` |
| ย้าย/ขยายปุ่ม | เลข `x, y, กว้าง, สูง` ในบรรทัด `UIButton` (หรือกด Play แล้วลาก → คลิกขวา **UI Layout > Save Position**) |
| เพิ่มปุ่มบนหน้าหลักเปิดหน้าใหม่ | `BuildHomeNavigation` (`LobbyManager.Navigation.cs`) เพิ่ม `UIButton("MyPage", root, "MY PAGE", x, y, w, h, OpenMyPage);` แล้วเขียนเมธอด `OpenMyPage` (ดูแบบจาก `OpenProgressPage`) |
| ชนะได้เหรียญเพิ่ม | `WinReward` ใน `Managers/GameplayManager.Bots.cs` |
| ยานเร็วขึ้น / เลือดเยอะขึ้น | ตาราง `Ships` ใน `BattleLoadoutCatalog.cs` |
| สกิลคูลดาวน์สั้นลง / ปลดล็อกเร็วขึ้น | `Skills` ใน `BattleLoadoutCatalog.cs` / `RequiredLevels` ใน `SkillUnlock.cs` |
| แมตช์ยาวขึ้น / Kill ชนะเปลี่ยน | ค่าเริ่มต้นใน `MatchRules.cs` (`DefaultMatchSeconds`) และปุ่มตั้งค่าห้อง `LobbyManager.RoomSettings.cs` |
| เพิ่มปุ่มบน HUD ตอนเล่น | `CreateMatchUI` ใน `Managers/GameplayManager.HUD.cs` |
| เปลี่ยนรูป/เสียง | วางไฟล์ชื่อเดิมทับ ดู `ART_TODO_TH.md` (ไม่ต้องแก้โค้ด) |

## ข้อห้าม (แก้แล้วเกมพังง่าย)

- อย่าสลับลำดับยาน/สกิล/แม็พในรายการ (ข้อมูลผู้เล่นเก็บเป็นเลขลำดับ)
- อย่าเปลี่ยนชื่อฟังก์ชันที่มี `[PunRPC]` หรือชื่อคลาสที่ติดอยู่กับฉาก/Prefab
- อย่าลบไฟล์ `.meta`
- เพิ่มเหรียญต้องผ่าน `FirebaseManager.AddCoins` / `SpendCoins` (มี Transaction กันยอดเพี้ยน) อย่าเขียนยอดทับตรง ๆ

รายละเอียดเต็มของทุกระบบ: `CODE_GUIDE_TH.md` / ประวัติการแก้และสวิตช์ทั้งหมด: `PHASE_LOG_TH.md`
