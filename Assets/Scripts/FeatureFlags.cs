// FeatureFlags.cs — สวิตช์เปิด/ปิดฟีเจอร์ทั้งเกมรวมไว้ที่เดียว
// ถ้าฟีเจอร์ไหนมีปัญหา ตั้งเป็น false แล้วเกมกลับไปทำงานแบบเดิม (ปุ่มของฟีเจอร์นั้นจะซ่อน)
// ค่าเริ่มต้นอยู่ในไฟล์นี้ และแอดมินเขียนทับได้จาก Firebase: feature_flags/{ชื่อ} = true/false
// FirebaseManager โหลดค่าจาก Firebase ครั้งเดียวหลังล็อกอิน (LoadFeatureFlags)
using System.Collections.Generic;

// คลาส static เก็บสวิตช์ฟีเจอร์ทั้งหมดเป็นตัวแปร bool (FirebaseManager เขียนทับได้ตอนโหลดหลังล็อกอิน)
public static class FeatureFlags
{
    // ระบบยิงแบบเบา: ส่งแค่คำสั่งยิงผ่าน RPC แล้วแต่ละเครื่องสร้างกระสุนเอง (false = กลับไปใช้ PhotonNetwork.Instantiate แบบเดิม)
    public static bool LightBullets = true;
    // ให้ Host ตั้งค่าห้องได้ (จำนวน Kill / เวลา / อันตรายในแม็พ) ในห้องรอ
    public static bool RoomSettings = true;
    // บอท / เล่นคนเดียว (เฟส 1)
    public static bool Bots = true;
    // ตีบวกยาน / ไอเท็ม ร้านค้า กล่องสุ่ม (เฟส 5)
    public static bool Upgrades = true;
    public static bool Items = true;
    // เลเวล + XP + หน้าโปรไฟล์ + ประวัติแมตช์ (เฟส 4)
    public static bool Progression = true;
    // แมตช์แรงค์ + MMR (เฟส 6)
    public static bool Ranked = true;
    // เฟส 2: ห้องหลายคน 2-10 คน (Free-for-all) / ตารางคะแนน / ข้อความใครฆ่าใคร
    public static bool MultiPlayer = true;
    public static bool Scoreboard = true;
    public static bool KillFeed = true;
    // เฟส 3: โหมดทีม 2v2 - 5v5 (ยิงเพื่อนไม่โดน)
    public static bool Teams = true;
    // เฟส 4: ภารกิจ / รางวัลล็อกอินรายวัน / Achievement (เลเวล+XP+โปรไฟล์ใช้สวิตช์ Progression)
    public static bool Missions = true;
    public static bool DailyLogin = true;
    public static bool Achievements = true;
    // เฟส 6: ตารางอันดับ / ฤดูกาลรายเดือน (แมตช์แรงค์ใช้สวิตช์ Ranked)
    public static bool Leaderboard = true;
    public static bool Seasons = true;
    // เฟส 7: ยานใหม่ / สกิลใหม่ / ไอเท็มเกิดในแม็พ / ประกาศ Kill Streak
    public static bool NewShips = true;
    public static bool NewSkills = true;
    public static bool PowerUps = true;
    public static bool KillStreaks = true;
    // เฟส 7B: โหมดเกมใหม่ (ยึดจุด/เก็บดาว/Survival/Battle Royale/Campaign) / หินลอยรอบเสาแม็พปริซึม
    public static bool GameModes = true;
    public static bool PillarRocks = true;
    // เฟส 8: เพื่อน+สถานะออนไลน์+ชวนเข้าห้อง / แชท / กิลด์
    public static bool Social = true;
    public static bool Chat = true;
    public static bool Guilds = true;
    // เฟส 9: หน้าตั้งค่าเพิ่มเติม (MORE OPTIONS) / ภาษาไทย-อังกฤษ / อ่านค่ายาน-สกิลจาก Firebase
    // กลับเข้าแมตช์เดิมเมื่อเน็ตหลุด (รอ 25 วิ) / Host ตรวจและตัดสินดาเมจแทนคนยิง (กันโกงครึ่งทาง)
    public static bool PlayerOptions = true;
    public static bool Language = true;
    // RemoteCatalog ค่าเริ่มต้นปิด: ข้อมูล spacecraft/skills ใน Firebase ถูกสร้างไว้นานแล้วอาจเป็นค่าเก่า
    // แอดมินตรวจ/แก้ค่าใน Console ให้ถูกก่อน แล้วตั้ง feature_flags/RemoteCatalog = true เพื่อเปิดใช้
    public static bool RemoteCatalog = false;
    public static bool BattleReconnect = true;
    public static bool HostDamage = true;
    // แม็พใหม่จากรูปชุดใหม่: แม็พ 4 สถานีอวกาศ / แม็พ 5 เนบิวลาลาวา (GameplayManager.NewMaps.cs)
    public static bool NewMaps = true;
    // จัดหน้าใหม่ (เปลี่ยนแค่การจัดวาง ปุ่ม/การทำงานเหมือนเดิม) ปิด = กลับไปหน้าเดิม
    // หน้าตั้งค่า: รวม MORE OPTIONS มาไว้หน้าเดียว เลื่อนขึ้นลงได้ (BattleSettingsPanel.Unified.cs)
    public static bool UnifiedSettings = true;
    // หน้าเลือกโหมดแบบการ์ด + หน้าเตรียมพร้อมรบแบบช่อง VS (LobbyManager.NewLayout.cs)
    public static bool NewLobbyLayout = true;
    // อนิเมชันเปิดกล่องเสบียง (LobbyManager.CrateReveal.cs) ปิด = ขึ้นข้อความผลเฉย ๆ
    public static bool CrateAnimation = true;
    // เงื่อนไขชนะเพิ่ม: MOST KILLS / NET SCORE / MOST DAMAGE / BOUNTY HUNT (MatchRules.WinRules.cs) ปิด = ฆ่าครบก่อนชนะอย่างเดียว
    public static bool WinRules = true;
    // ไอคอนหน้าข้อความปุ่ม/ป้าย (UiIcon.cs, รูปที่ Resources/Images/UI) ปิด = ปุ่มเป็นตัวหนังสืออย่างเดียว
    public static bool UiIcons = true;
    // สถิติหลังแมตช์ + ป้ายเกียรติยศ (MatchStats.cs, GameplayManager.Stats.cs) ปิด = ไม่มีปุ่ม MATCH STATS
    public static bool PostMatchStats = true;
    // ฉากฉลองเลเวลอัป/แรงค์อัปในหน้าผล (CelebrationOverlay.cs)
    public static bool Celebrations = true;
    // ดาวพื้นหลัง (เม็ดขาว) กระจายทั่วทั้งสนามทุกแม็พ (false = ใช้ค่าที่ตั้งใน Scene)
    public static bool FullStarfield = true;
    // ล็อกห้อง Photon ไว้ที่ region เดียว (asia) ทุกเครื่องเจอห้องกันเสมอ จอยด้วยเลขห้องได้ (false = ให้ Photon เลือก region ที่ ping ดีสุดเอง)
    public static bool FixedRegion = true;
    // สกิลปลดล็อกตามเลเวล (3 อันแรกฟรี) (false = ใช้ได้ทุกสกิลเหมือนเดิม)
    public static bool SkillLevelLock = true;
    // แชทเก็บแค่ 50 ข้อความล่าสุด + แชทรวมข้อความหายเองหลัง 2 นาที
    public static bool ChatLimits = true;
    // แชทส่วนตัวกับเพื่อน (ปุ่ม CHAT ในรายชื่อเพื่อน)
    public static bool FriendChat = true;
    // แท็กกิลด์สร้างให้อัตโนมัติจากชื่อกิลด์ (false = ให้ผู้เล่นพิมพ์เอง)
    public static bool AutoGuildTag = true;
    // แรงค์ขึ้นยาก: ขั้นแรงค์กว้างขึ้น + ได้คะแนนต่อแมตช์น้อยลง (Ranked.cs) (false = แบบเดิม ขึ้นเร็ว)
    public static bool HardRanked = true;
    // ห้องแรงค์ชวนเพื่อน/จอยด้วยรหัสไม่ได้ หาคู่แบบสุ่มเท่านั้น (false = ชวนได้เหมือนห้องปกติ)
    public static bool RankedNoInvite = true;
    // ขายไอเท็มได้ (ปุ่ม SELL ในแท็บไอเท็ม ได้เหรียญคืนบางส่วน) (false = ขายไม่ได้)
    public static bool SellItems = true;
    // ร้านค้ารายวัน: สุ่มไอเท็มมาขายวันละ 4 ช่อง รีเซ็ตเที่ยงคืน ราคาสูง (false = ร้านเดิม ขายทุกชิ้น COMMON/RARE ราคาปกติ)
    public static bool DailyShop = true;
    // ปุ่มตั้งค่า + อีโมตในสนามรบ จัดเป็นแถวเดียวมุมขวาบน ขนาด/สีเดียวกัน (false = ตำแหน่งเดิม)
    public static bool TidyBattleButtons = true;
    // แม็พแมงกะพรุนใช้ภาพที่ตัดใหม่ (Images/Maps/Jelly/) ไม่มีเศษภาพข้างเคียงติดขอบ (false = ใช้ภาพตัดจากแผ่นรวม Props_Jellyfish แบบเดิม)
    public static bool CleanJellyArt = true;
    // ค่าพลังในล็อบบี้แยกสี: ส่วนตีบวกสีเขียว ตามด้วยส่วนจากไอเท็มสีม่วง เช่น "175 (+30) (+20)" (false = รวมเป็นสีเขียวอันเดียว)
    public static bool ItemStatColor = true;
    // ห้องรอเห็นค่าพลังที่ตีบวก/ใส่ไอเท็มของคู่แข่งด้วย (ส่งผ่าน Photon) (false = เห็นแต่ของตัวเอง)
    public static bool ShowRivalStats = true;
    // แท็บไอเท็ม: รูปยาน + ปุ่มเลือกยานอยู่ข้างช่องใส่ไอเท็ม (false = ปุ่มเลือกยานด้านบนแบบเดิม ไม่มีรูป)
    public static bool ItemsShipCard = true;
    // ปุ่ม "ไอเท็มทั้งหมด" ในร้านค้า (รายการไอเท็มเรียงจากหายากสุด) + คลังไอเท็มเรียงจากหายากลงมา (false = ไม่มีปุ่ม เรียงตามเลเวลแบบเดิม)
    public static bool ItemCatalog = true;
    // หมุนยานผ่าน Rigidbody2D แทน Transform (แก้ยานสั่นไปมาตอนเล็ง) + ยานคนอื่นไม่ใช้ Interpolate (false = แบบเดิม)
    public static bool SmoothShipRotation = true;
    // ใช้ตำแหน่ง/ขนาด UI ที่บันทึกไว้ด้วยเมนู UI Layout > Save Position (UiLayout.cs) (false = ตำแหน่งจากโค้ดแบบเดิม)
    public static bool SavedUiLayout = true;
    // เมนูทดสอบ Test/ บน LobbyManager (ตั้งเลเวล) ใช้ได้เฉพาะใน Unity Editor ไม่มีในเกมจริง (false = ซ่อนการทำงาน)
    public static bool TestTools = true;
    // สกิลล่องหน: ภาพออร่าระหว่างล่องหน (VFX_CloakLoop) + แสงแตกตอนหลุด (VFX_CloakReveal) (false = มีแค่วังวนตอนเริ่มแบบเดิม)
    public static bool CloakFx = true;
    // แม็พใหม่ 3/4 มีของขยับบนพื้นหลัง: สถานี = เศษหินลอย ไฟสัญญาณกะพริบ ดาวตก / ลาวา = ประกายไฟลอยขึ้น หินลาวาลอย แสงร้อนเรือง (false = พื้นหลังนิ่ง)
    public static bool MapAmbience = true;
    // ชื่อแอป "BOS" + ไอคอนแอป ตั้งให้อัตโนมัติใน Editor (Assets/Editor/BosBranding.cs) (false = ไม่ตั้งเอง ใช้เมนู Tools > BOS ได้)
    public static bool AppBranding = true;
    // กดเล่นกับบอทครั้งแรกแล้วแวบไม่เข้าเกม: กันระบบต่อเน็ตอัตโนมัติแทรกตอนกำลังเข้าโหมดออฟไลน์ + ค้างเกิน 8 วิ ให้กดใหม่ได้ (false = แบบเดิม)
    public static bool SoloStartFix = true;
    // แม็พปริซึมรุ่น 3: พื้นหลังเนบิวลามุมบน + คริสตัล/เสาโอเบลิสก์/ปริซึมสะท้อนกระสุน/แท่นวาร์ปภาพใหม่ + ลำแสงหมุนและเศษคริสตัลลอย (false = แม็พปริซึมรุ่น 2 แบบเดิม)
    public static bool PrismMapV3 = true;
    // เสียงชุดใหม่ทั้งเกม (Resources/Audio/BOS: เสียงเอฟเฟกต์ 21 เสียง + เพลงล็อบบี้ + เพลงต่อสู้แยก 5 แม็พ) และเสียงเฉพาะของวาร์ป/พาวเวอร์อัป/ฮีล/กล่องสุ่ม ฯลฯ (false = เสียงสังเคราะห์เดิมในโค้ด)
    public static bool NewSounds = true;
    // ทุกฉากไม่มี AudioListener ทำให้ใน Unity Editor ไม่มีเสียงและเตือนซ้ำใน Console: ให้ AudioManager สร้างตัวรับเสียงให้เองเมื่อหาไม่เจอ (false = แบบเดิม)
    public static bool AudioListenerGuard = true;

    // เขียนทับค่าจาก Firebase (key ต้องตรงชื่อ field เช่น "LightBullets") ค่าที่ไม่ใช่ bool จะถูกข้าม
    public static void Apply(IDictionary<string, object> values)
    {
        if (values == null) return;
        foreach (var pair in values)
        {
            if (!(pair.Value is bool on)) continue;
            switch (pair.Key)
            {
                case nameof(LightBullets): LightBullets = on; break;
                case nameof(RoomSettings): RoomSettings = on; break;
                case nameof(Bots): Bots = on; break;
                case nameof(Upgrades): Upgrades = on; break;
                case nameof(Items): Items = on; break;
                case nameof(Progression): Progression = on; break;
                case nameof(Ranked): Ranked = on; break;
                case nameof(MultiPlayer): MultiPlayer = on; break;
                case nameof(Scoreboard): Scoreboard = on; break;
                case nameof(KillFeed): KillFeed = on; break;
                case nameof(Teams): Teams = on; break;
                case nameof(Missions): Missions = on; break;
                case nameof(DailyLogin): DailyLogin = on; break;
                case nameof(Achievements): Achievements = on; break;
                case nameof(Leaderboard): Leaderboard = on; break;
                case nameof(Seasons): Seasons = on; break;
                case nameof(NewShips): NewShips = on; break;
                case nameof(NewSkills): NewSkills = on; break;
                case nameof(PowerUps): PowerUps = on; break;
                case nameof(KillStreaks): KillStreaks = on; break;
                case nameof(GameModes): GameModes = on; break;
                case nameof(PillarRocks): PillarRocks = on; break;
                case nameof(Social): Social = on; break;
                case nameof(Chat): Chat = on; break;
                case nameof(Guilds): Guilds = on; break;
                case nameof(PlayerOptions): PlayerOptions = on; break;
                case nameof(Language): Language = on; break;
                case nameof(RemoteCatalog): RemoteCatalog = on; break;
                case nameof(BattleReconnect): BattleReconnect = on; break;
                case nameof(HostDamage): HostDamage = on; break;
                case nameof(NewMaps): NewMaps = on; break;
                case nameof(UnifiedSettings): UnifiedSettings = on; break;
                case nameof(NewLobbyLayout): NewLobbyLayout = on; break;
                case nameof(CrateAnimation): CrateAnimation = on; break;
                case nameof(WinRules): WinRules = on; break;
                case nameof(UiIcons): UiIcons = on; break;
                case nameof(PostMatchStats): PostMatchStats = on; break;
                case nameof(Celebrations): Celebrations = on; break;
                case nameof(FullStarfield): FullStarfield = on; break;
                case nameof(FixedRegion): FixedRegion = on; break;
                case nameof(SkillLevelLock): SkillLevelLock = on; break;
                case nameof(ChatLimits): ChatLimits = on; break;
                case nameof(FriendChat): FriendChat = on; break;
                case nameof(AutoGuildTag): AutoGuildTag = on; break;
                case nameof(HardRanked): HardRanked = on; break;
                case nameof(RankedNoInvite): RankedNoInvite = on; break;
                case nameof(SellItems): SellItems = on; break;
                case nameof(DailyShop): DailyShop = on; break;
                case nameof(TidyBattleButtons): TidyBattleButtons = on; break;
                case nameof(CleanJellyArt): CleanJellyArt = on; break;
                case nameof(ItemStatColor): ItemStatColor = on; break;
                case nameof(ShowRivalStats): ShowRivalStats = on; break;
                case nameof(ItemsShipCard): ItemsShipCard = on; break;
                case nameof(ItemCatalog): ItemCatalog = on; break;
                case nameof(SmoothShipRotation): SmoothShipRotation = on; break;
                case nameof(SavedUiLayout): SavedUiLayout = on; break;
                case nameof(TestTools): TestTools = on; break;
                case nameof(CloakFx): CloakFx = on; break;
                case nameof(MapAmbience): MapAmbience = on; break;
                case nameof(AppBranding): AppBranding = on; break;
                case nameof(SoloStartFix): SoloStartFix = on; break;
                case nameof(PrismMapV3): PrismMapV3 = on; break;
                case nameof(NewSounds): NewSounds = on; break;
                case nameof(AudioListenerGuard): AudioListenerGuard = on; break;
            }
        }
    }
}
