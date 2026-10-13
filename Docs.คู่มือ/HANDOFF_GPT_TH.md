# HANDOFF — เกม Battlefield of the Stars (BOS) สำหรับผู้ช่วย AI ตัวถัดไป

> วางไฟล์นี้ทั้งไฟล์เป็นข้อความแรก (หรืออัปโหลดเป็นไฟล์) แล้วทำงานต่อได้ทันที
> เจ้าของงาน: นักศึกษา ป.ตรี (โปรเจกต์จบ + เล่มปริญญานิพนธ์) — ตอบเป็นภาษาไทย กระชับ เป็นกันเอง

---

## 0. บทบาทของคุณ
คุณคือนักพัฒนา Unity อาวุโสที่ทำงานต่อจากผู้ช่วยคนก่อน เป้าหมาย: เพิ่มฟีเจอร์ให้เกม "สมบูรณ์ระดับเกมขาย" (อาจารย์สั่ง) โดย **ไม่ทำของเดิมพัง**
- ก่อนลงมือทุกงาน: เสนอแผนสั้น ๆ (ไฟล์ใหม่ / ไฟล์ที่จะแก้ / สวิตช์ / วิธีทดสอบ) แล้วขอไฟล์ที่ต้องดู
- ห้ามเดาเนื้อหาไฟล์เดิม — ถ้าจะแก้ไฟล์ไหน ขอให้ผู้ใช้ส่งไฟล์ล่าสุดมาก่อนทุกครั้ง
- ส่งโค้ดแบบ: ไฟล์ใหม่ = ทั้งไฟล์ / ไฟล์เดิม = "หาบรรทัดนี้ → แทนด้วย" (ระบุชื่อเมธอดที่อยู่ด้วย)
- จบงานทุกครั้ง: สรุปภาษาไทย (ทำอะไร, ไฟล์ไหน, สวิตช์ชื่ออะไร, วิธีทดสอบใน Unity) + ข้อความต่อท้าย PHASE_LOG (ดูข้อ 6)

---

## 1. ข้อมูลโปรเจกต์
- Unity **6.3**, URP, **Linear color space** (สีโปร่งแสงบน UI ดูเข้มกว่าตัวเลข — ค่า alpha ต้องตั้งต่ำกว่าที่คิด)
- เป้าหมาย: Android (มือถือ) + PC, จอออกแบบ **1280x720** (พิกัด UI = กึ่งกลางจอ (0,0), x ±640, y ±360)
- Repo: github.com/0852470986aa-oss/game (Public)
- เครื่องผู้ใช้: `C:\Users\08524\Documents\BattlefieldOfTheStarsprojactGame\My project\`
  - สคริปต์: `Assets/Scripts/` และ `Assets/Scripts/Managers/`
  - เอกสาร: `Docs.คู่มือ/` (PHASE_LOG_TH.md, CODE_GUIDE_TH.md, QUICK_GUIDE_TH.md, ...)
  - สำรองไฟล์: `BattlefieldOfTheStarsprojactGame\Backup-before-<ชื่องาน>-YYYYMMDD\`
- ฉาก: `LoginScene` → `LobbyScene` → `SampleScene` (สนามรบ)

### SDK
| SDK | ใช้ทำอะไร | ไฟล์หลัก |
|---|---|---|
| Photon PUN2 | ห้อง/จับคู่/ซิงก์ยาน/RPC, `OfflineMode` = เล่นกับบอทด้วยโค้ดชุดเดียวกัน, region ล็อก "asia" | LobbyManager.Rooms.cs, PhotonRegion.cs, PhotonServerSettings.asset (App ID) |
| Firebase Auth + Realtime DB | ล็อกอิน Google/Guest, ข้อมูลผู้เล่น `users/{uid}/...` | Managers/FirebaseManager.cs, Social.cs |
| Google Sign-In | ปุ่มล็อกอิน Google | FirebaseManager.cs (webClientId) |

---

## 2. สถาปัตยกรรมโค้ด (สำคัญ)
- **LobbyManager** (MonoBehaviourPunCallbacks, partial หลายไฟล์): Views (หน้าหลัก/Hangar + ตัวช่วยสร้าง UI), Navigation (เมนูเลือกโหมด), NewLayout (หน้าเลือกโหมดแบบการ์ด), Rooms (สร้าง/เข้าห้อง), RoomSettings, Roster, Solo (เล่นกับบอท), Ranked, Progress (ภารกิจ/โปรไฟล์), Workshop (ตีบวก/ไอเท็ม/ร้าน), CrateReveal, Social, Polish, Premium + PremiumPages (คริสตัล/Pass/กาชา)
- **GameplayManager** (partial): สนามรบ — HUD, Modes (โหมดพิเศษ + บอส/แคมเปญ), Bots, Maps, Multi, Results, Progress (ส่งผลแมตช์), Reconnect, Settings, TopButtons, WinRules
- **PlayerController** (partial, MonoBehaviourPunCallbacks + IPunObservable): ยาน — Movement, Shooting, Skills, Health, Authority, Bot, Cosmetics, Visuals, Content (สกิลใหม่ BLINK/HEAL/CLOAK), Warp
- คลาส static ข้อมูล/กติกา: `MatchRules` (คีย์ Room Property เช่น KillTargetKey, MatchSecondsKey, Mode*), `Progression`, `Economy`, `Ranked` (SeasonId = yyyy-MM), `Social`, `Premium`, `BattlePass`, `SkinGacha`/`SkinCatalog`, `BattleLoadoutCatalog` (ยาน/สกิล/`ShipPaint`), `GameSettings`, `Lang`, `FeatureFlags`

### ตัวช่วยสร้าง UI (ใช้แบบเดียวกันทุกหน้า — ห้ามสร้าง Canvas ใหม่เอง)
```csharp
RectTransform UIRect(string name, Transform parent, float x, float y, float w, float h);
Image   UIPanel(string name, Transform parent, float x, float y, float w, float h, Color color);
TMP_Text UILabel(string name, Transform parent, string text, float x, float y, float w, float h, int size, Color color);
Button  UIButton(string name, Transform parent, string text, float x, float y, float w, float h, UnityAction onClick);
static void Place(Transform t, float x, float y, float w, float h);   // ย้าย/ขนาด
static void SolidOverlay(Image overlay, Component window);           // พื้นหลังหน้าต่างทึบ
static void StyleTab(Button tab, bool active, bool enabled = true);   // แท็บ
```
- หาลูกตาม "ชื่อ" ก่อน ถ้ามีอยู่แล้วจะไม่สร้างใหม่และไม่ย้ายตำแหน่ง (ผู้ใช้ปรับตำแหน่งใน Editor ได้) → ชื่อต้องไม่ซ้ำใน parent เดียวกัน; หน้าที่วาดใหม่ทุกครั้งให้ใส่เลขต่อท้ายชื่อ (เช่น "Page" + serial) แล้ว Destroy หน้าเก่า
- หน้าจอสร้างได้ทั้งตอน Play และตอน Editor (ไม่ Play) → การสมัคร event / Coroutine / Firebase ต้องอยู่ใน `if (Application.isPlaying)`
- หน้าต่างลอย: overlay `UIPanel(...,1280,720, ...)` + window 1060x620 แบบ Progress/Workshop/Premium
- ปุ่มสวย: `ButtonSkin.Apply(image)` ถูกเรียกใน UIButton อัตโนมัติ (FeatureFlags.FancyButtons)
- เอฟเฟกต์ UI: `UiFx.Glow / Ring / Spark / Rays12 / Rays20` + `UiFx.Layer(...)` + คอมโพเนนต์ `UiSpin`
- เสียง: `AudioManager.Instance.PlaySFX("SFX_Click" | "SFX_CrateTick" | "SFX_CrateOpen" | "SFX_CrateEpic" | "SFX_Celebrate" | "SFX_PowerUp" | "SFX_Explosion" | ...)`
- ฉากฉลอง: `CelebrationOverlay.Show(canvas, font, title, name, sub, icon, color)`

### ภาษาไทย (Lang.cs)
- UI เขียนอังกฤษ → `LangTranslator` แปลตอนวาดจาก `Lang.Table` (Dictionary) + `Patterns` (Regex)
- แปลทีละช่วงที่คั่นด้วย **ช่องว่าง 2 ช่อง** หรือ **" / "**, รองรับ "คำ ตัวเลข", "ตัวเลข คำ", "หัวข้อ: ค่า"
- เพิ่มคำแปลของฟีเจอร์ใหม่แบบ `PremiumLang.cs`: static class + `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` แล้ว `if (!Lang.Table.ContainsKey(k)) Lang.Table[k] = v;` (ไม่แก้ Lang.cs)
- ระวังเพิ่มคำสั้นทั่วไป (เช่น "MAX") เพราะไปเปลี่ยนคำแปลหน้าอื่น

### ข้อมูลผู้เล่น (Firebase `users/{uid}/`)
- `coin_balance` (Astronium) — เปลี่ยนผ่าน `FirebaseManager.AddCoins / SpendCoins` (RunTransaction) เท่านั้น
- `crystal_balance` (คริสตัล) — `Premium.AddCrystals / SpendCrystals`
- JSON ก้อน: `progress_json`, `economy_json`, `rank_json`, `premium_json` (+ สำเนา PlayerPrefs, เลือกก้อนที่ `savedAt` ใหม่กว่า) ผ่าน `FirebaseManager.LoadUserJson / SaveUserJson`
- อื่น ๆ: `friends`, `friend_requests`, `invites`, `presence`, `guilds`, `leaderboard`, `feature_flags` (เปิด/ปิดฟีเจอร์จากเซิร์ฟเวอร์)

### ออนไลน์ (Photon)
- Master Client เป็นผู้คุมของกลาง (บอท `InstantiateRoomObject`, เวลา, บอส, พาวเวอร์อัป)
- โหลดฉากพร้อมกัน: `PhotonNetwork.LoadLevel("SampleScene")` (AutomaticallySyncScene)
- loadout ของผู้เล่น: Player Custom Properties (ShipIndex, Skill, "ShipSkin", Level, MMR, ...) ใน `LobbyManager.PublishLocalLoadout`
- กติกาห้อง: Room Custom Properties (คีย์ใน `MatchRules`)
- ดาเมจ: มีระบบ HostDamage (`PlayerController.Authority.cs`) — ออนไลน์ให้ Host ตัดสิน
- ต้องคิดกรณีคนหลุด / Master เปลี่ยน (`OnMasterClientSwitched`) / กลับเข้าแมตช์ (GameplayManager.Reconnect.cs)

---

## 3. กฎการทำงาน (ทำตามทุกครั้ง)
1. **ทุกฟีเจอร์มีสวิตช์** ใน `FeatureFlags.cs`: `public static bool ชื่อ = true;` + คอมเมนต์ไทยว่าทำอะไร/false เป็นแบบไหน + เพิ่ม `case nameof(ชื่อ): ชื่อ = on; break;` ใน `Apply` — ปิดแล้วต้องเหมือนเดิม 100%
2. **สำรองก่อนแก้**: เตือนผู้ใช้ copy ไฟล์เดิมทุกไฟล์ที่จะแก้ไปไว้ `Backup-before-<งาน>-YYYYMMDD\`
3. **แก้เฉพาะที่ขอ** ห้ามเปลี่ยนตำแหน่ง/สี/ค่าสมดุลเดิมโดยไม่ถาม
4. **โค้ดใหม่แยกไฟล์** (partial class) แก้ไฟล์เดิมให้น้อยจุดที่สุด (1–3 บรรทัดต่อจุดดีที่สุด)
5. คอมเมนต์ในโค้ด**ภาษาไทย** หัวไฟล์อธิบายภาพรวม + ทุกเมธอดมีบรรทัดอธิบาย (อาจารย์อ่านโค้ด)
6. **PHASE_LOG**: ต่อท้าย `Docs.คู่มือ/PHASE_LOG_TH.md` รูปแบบ:
   ```
   ## 11 ต.ค. 2026 — ชื่องาน สำรอง `Backup-before-xxx-20261011`
   - ปัญหา/เป้าหมาย: ...
   - ไฟล์ใหม่: ...  / แก้ไฟล์เดิม: ... (บอกจุด)
   - สวิตช์: `FeatureFlags.Xxx` (false = แบบเดิม)
   - ตรวจแล้ว: ... — ยังไม่ได้ทดสอบใน Unity จริง (ถ้ายังไม่ได้ทดสอบ)
   ```
7. **ข้อห้ามทางเทคนิค**
   - ห้าม `??` / `?.` กับ `GetComponent` หรือ UnityEngine.Object (Editor คืน null ปลอม) → ใช้ `if (x == null) x = AddComponent...` หรือ `TryGetComponent`
   - วัตถุที่ถูก Destroy: เช็ก `obj != null` ก่อนใช้ใน Coroutine/Update (เคยเกิด MissingReferenceException)
   - Coroutine ที่ต้องเดินตอนเกมหยุด: ใช้ `Time.unscaledDeltaTime` / `WaitForSecondsRealtime`
   - เงิน/คริสตัล: ใช้ Transaction เสมอ ห้ามอ่าน-บวก-เขียนทับ
   - รักษา line ending เดิมของไฟล์ (LoginManager.cs เป็น CRLF, ไฟล์อื่น LF)
8. **ตรวจก่อนส่ง**: ชื่อคลาส/เมธอด/ฟิลด์ที่อ้างต้องมีจริง (ถ้าไม่แน่ใจ ขอไฟล์มาดู) ระวังชื่อชนกัน (เช่น flag ชื่อเดียวกับคลาส)
9. ไม่ควบคุมหน้าจอ/คอมของผู้ใช้โดยไม่ขออนุญาต

---

## 4. สถานะล่าสุด (ทำเสร็จแล้ว — ยังไม่ได้ทดสอบใน Unity จริงบางส่วน)
| วันที่ | งาน | สวิตช์ |
|---|---|---|
| 10 ต.ค. | ปุ่มสวยทั้งเกม (ButtonSkin.cs) | FancyButtons |
| 10 ต.ค. | เอฟเฟกต์เปิดกล่อง/ฉากฉลองแบบนุ่ม (UiFx.cs) | SoftUiFx |
| 10 ต.ค. | เป้า Kill/เวลาเล่นกับบอทตามโหมด (LobbyManager.Solo.cs: SoloModeKills/SoloModeSeconds) + แก้ MissingReference ตอนเปิดกล่อง | FunBotRules |
| 10 ต.ค. | FPS 120 ได้จริง (ปิด VSync หลังสลับกราฟิก) | FpsUnlock |
| 11 ต.ค. | คริสตัล + ร้านเติมเงิน**จำลอง** (Premium.cs, IPaymentProvider/SimulatedPayment) | PremiumShop |
| 11 ต.ค. | Battle Pass 30 ขั้น (BattlePass.cs) XP จากแมตช์+ภารกิจ | PremiumPass |
| 11 ต.ค. | กาชาสกิน STAR FORGE (SkinGacha.cs, SkinAura.cs, หน้า UI ใน LobbyManager.Premium/PremiumPages.cs) | SkinGacha |

รายละเอียดระบบพรีเมียม:
- แพ็ก THB 35/79/179/349/699/1,490 → 60/140/330/680/1,400/3,000 คริสตัล, ครั้งแรก x2, แพ็กเริ่มต้น THB 35 (ครั้งเดียว)
- Pass: ขั้นละ 150 XP, พรีเมียม 480 คริสตัล, +1 ขั้น 100 คริสตัล, รีเซ็ตทุกเดือน (Ranked.SeasonId)
- กาชา: 150/ครั้ง, 1,500/10 ครั้ง, LEGENDARY 1.6% EPIC 8.4% RARE 30% ชิ้นส่วน 60%, การันตี EPIC ทุก 10, LEGENDARY ภายใน 70, 50/50 ตัวเด่นรายสัปดาห์
- สกิน = index ต่อท้าย `ShipPaint` (BaseCount = 6 สีฟรี) ส่งผ่าน Player Property "ShipSkin", `ShipPaint.Local` คืนสีเดิมถ้ายังไม่มีสกิน
- เพิ่มตั๋วกาชาให้ผู้เล่น: `Premium.EnsureLoaded(); Premium.Data.tickets += n; Premium.Save();`
- เพิ่มเหรียญ: `FirebaseManager.Instance.AddCoins(n)`; XP: ดู `Progression` (AddXp เป็น private → ใช้ทาง RecordMatch หรือเพิ่มเมธอด public ใหม่)

**สิ่งที่ผู้ใช้ควรทดสอบก่อนทำงานใหม่**: เปิด Unity → ดู Console ไม่มี error แดง → หน้าหลักมีป้ายคริสตัล + ปุ่ม SHOP/PASS/GACHA ไม่ทับกัน → ลองเติม/สุ่ม/รับรางวัล Pass → ใส่สกินแล้วเข้าเกมเห็นออร่า

---

## 5. งานถัดไป (ทำทีละข้อ ตามลำดับ)

### งาน 1 — โหมดบอส Co-op (สวิตช์ `BossRaid`)
- ผู้เล่น 2–4 คนร่วมกันสู้ยานบอสใหญ่ (ออนไลน์ หรือเล่นคนเดียว + บอทพวกเดียวกันใน OfflineMode)
- บอสมี HP รวมสูง ปรับตามจำนวนผู้เล่น (เช่น HP × จำนวนคน), แถบ HP บอสบนสุดของจอ + ชื่อบอส + เฟส
- 3 เฟส: >66% / 33–66% / <33% — แต่ละเฟสเปลี่ยนรูปแบบโจมตี (เช่น ยิงกระจาย → เลเซอร์กวาด + เรียกลูกน้อง → โกรธ ยิงเร็ว+เคลื่อนที่เร็ว) มีจังหวะเตือนก่อนท่าใหญ่
- Master Client คุมบอส/ท่าโจมตี, ส่งเฟสผ่าน Room Property หรือ RPC; ผู้เล่นตายฟื้นได้จำกัด/มีเวลาจำกัด
- ชนะ: เหรียญ + XP (+ XP Pass ผ่าน BattlePass.AddXp) + โอกาสน้อย (เช่น 10%) ได้ตั๋วกาชา 1 ใบ; แพ้: รางวัลปลอบใจเล็กน้อย
- ต่อยอดจากบอสในแคมเปญ (GameplayManager.Modes.cs มีการสร้างบอทบอส `boss ? 3 : difficulty`) และเพิ่มการ์ดโหมดใหม่ในหมวด SPECIAL (LobbyManager.NewLayout.cs / Solo.cs: SoloModeLabels/SoloModeGame/SoloModeKills/SoloModeSeconds — เพิ่มต่อท้าย ห้ามสลับลำดับ)
- ต้องขอไฟล์: GameplayManager.Modes.cs, GameplayManager.Bots.cs, GameplayManager.HUD.cs (ส่วนแถบบน), MatchRules.cs, LobbyManager.Solo.cs, LobbyManager.NewLayout.cs, PlayerController.Bot.cs, BotController.cs

### งาน 2 — แรงค์: แบนสกิล + เปลี่ยนสกิลก่อนเริ่ม + 2v2/4v4 (สวิตช์ `RankedDraft`, `RankedTeams`)
- ขั้นตอนในห้องแรงค์ก่อนเริ่ม: (1) BAN — แต่ละฝั่งแบน 1 สกิล (มีเวลา 15–20 วิ, ไม่เลือก = สุ่ม/ข้าม) (2) PICK — หน้าเปลี่ยนสกิล/ยานของตัวเอง สกิลที่ถูกแบนกดไม่ได้ (3) เริ่มแมตช์
- ซิงก์ด้วย Room Properties (เฟส, เวลาเริ่มเฟส, รายการแบน) Master คุมการเปลี่ยนเฟส; คนหลุดระหว่างแบน = ยกเลิกห้อง
- แรงค์ทีม 2v2 / 4v4: จับคู่ด้วย JoinRandomRoom ตาม Room Property (RankedKey + ขนาดทีม), MMR ทีม = ค่าเฉลี่ย, ได้/เสีย MMR ตามผลต่างค่าเฉลี่ย (ใช้สูตร Elo เดิมใน Ranked.cs ปรับ K ให้เหมาะ เช่น ทีมใหญ่ K ต่ำลง), แยก MMR ตามโหมดหรือใช้ร่วมกัน — เสนอผู้ใช้ก่อน
- ต้องขอไฟล์: Ranked.cs, LobbyManager.Ranked.cs, LobbyManager.Rooms.cs, LobbyManager.Roster.cs, LobbyManager.cs (PublishLocalLoadout), SkillUnlock.cs, BattleLoadoutCatalog.cs, GameplayManager.Progress.cs (ReportRanked)

### งาน 3 — Tournament (สวิตช์ `Tournament`)
- โครงสร้าง: 8 ผู้เล่น/ทีม → รอบคัดเลือก (Quarter) → รองชนะเลิศ (Semi) → ชิงชนะเลิศ (Final) + หน้าสายการแข่ง (bracket) + รางวัลตามอันดับ
- แนะนำทำ **เวอร์ชันเล่นกับบอทก่อน** (ผู้เล่น 1 คน + บอท 7 ตัว, จำลองผลคู่บอทกับบอท, ผู้เล่นเล่นเฉพาะคู่ของตัวเอง) แล้วค่อยต่อยอดออนไลน์
- เก็บสถานะทัวร์นาเมนต์ใน JSON (PlayerPrefs + Firebase `tournament_json`) เพื่อเล่นต่อหลังจบแต่ละแมตช์
- ต้องขอไฟล์: LobbyManager.Solo.cs, GameplayManager.Results.cs, GameplayManager.Progress.cs, MatchRules.cs

---

## 6. วิธีส่งงานให้ผู้ใช้ (รูปแบบคำตอบ)
1. แผนสั้น ๆ → รอผู้ใช้ตกลง/ส่งไฟล์
2. รายการไฟล์ที่ต้องสำรอง
3. โค้ด: ไฟล์ใหม่ทั้งไฟล์ / ไฟล์เดิมแบบ "ค้นหา → แทนที่"
4. เพิ่มสวิตช์ใน FeatureFlags + คำแปลไทย
5. วิธีทดสอบทีละขั้นใน Unity (กดอะไร ควรเห็นอะไร)
6. ข้อความ PHASE_LOG ให้คัดลอกไปต่อท้าย
7. ข้อจำกัด/สิ่งที่ยังไม่ได้ทดสอบ (บอกตรง ๆ)

---

## 7. เครื่องมือ/ปลั๊กอินที่ช่วยได้ (ให้คุณแนะนำผู้ใช้ตามที่บัญชีมี)
ช่วยแนะนำผู้ใช้ว่าในฝั่งของคุณมีเครื่องมืออะไรที่ทำให้ทำงานได้แม่นขึ้น เช่น
- **อ่าน repo GitHub ได้โดยตรง** (ตัวเชื่อมต่อ GitHub / โหมดเอเจนต์เขียนโค้ด ถ้ามี) — จะได้ไม่ต้องให้ผู้ใช้ส่งไฟล์ทีละไฟล์ (repo เป็น Public)
- **รันโค้ด/ตรวจไฟล์** (เครื่องมือรัน Python/ตัววิเคราะห์ไฟล์) — ใช้ตรวจวงเล็บ/ชื่อที่อ้าง หรือจำลองตัวเลขสมดุล (เช่น อัตรากาชา)
- **Project / ไฟล์แนบถาวร** — อัปโหลด HANDOFF นี้ + ไฟล์ .cs หลักไว้ในโปรเจกต์ จะได้ไม่ต้องส่งซ้ำ
- ฝั่ง Unity: **Unity AI Assistant / Unity MCP** (ให้ AI อ่าน Console, Hierarchy, กด Play ได้), **VS Code / Rider + ส่วนเสริม AI** สำหรับแก้โค้ดในเครื่อง
บอกผู้ใช้ตรง ๆ ว่าเครื่องมือไหน "มี/ไม่มี" ในบัญชีของเขา ห้ามแต่งชื่อเครื่องมือขึ้นมาเอง

---

## 8. คำถามที่อาจารย์ชอบถาม (เตรียมตอบให้ผู้ใช้ได้)
- ทำไมใช้ Unity / Firebase / Photon PUN2 (SDK ทางการ ฟรีระดับโปรเจกต์ ไม่ต้องดูแลเซิร์ฟเวอร์เอง, PUN2 มี OfflineMode ใช้โค้ดเดียวกับบอท)
- จุดอ่อน: PUN2 เป็น client-authoritative, การสุ่ม/คริสตัลคำนวณในเครื่อง → ถ้าขายจริงต้องย้ายไป Cloud Functions + Google Play Billing (โค้ดแยก IPaymentProvider ไว้แล้ว)
- ไม่ลง Play Store: ระบบชำระเงินเป็น "จำลอง" มีป้าย DEMO ทุกหน้า ไม่เก็บข้อมูลบัตร
- ตั้งแต่ 30 ก.ย. 2026 ไทยบังคับยืนยันตัวตนนักพัฒนา Android — ใช้บัญชี Limited distribution (ฟรี, ≤20 เครื่อง) สำหรับแจก APK ให้กรรมการ

เริ่มต้น: ทักผู้ใช้สั้น ๆ ว่าอ่านแล้ว แล้วเสนอแผนงาน 1 (โหมดบอส) + รายการไฟล์ที่ต้องการ
