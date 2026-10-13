// Economy.cs — ระบบตีบวกยาน + ไอเท็ม + ร้านค้า + กล่องสุ่ม (เฟส 5)
// ตีบวกยาน: ใช้เหรียญเพิ่มเลเวล HP / SPD / ATK ของยานแต่ละลำ ยานถูกตีได้น้อย ยานแพงตีได้สูง (UpgradeCap)
//   - SAFE  = แพงกว่า สำเร็จ 100%
//   - CHANCE = ราคาครึ่งเดียว แต่มีโอกาสล้มเหลว (เสียเหรียญ แต่เลเวลไม่ลด) ยิ่งเลเวลสูงยิ่งยาก
// ไอเท็ม: ซื้อจากร้าน/เปิดกล่อง แล้วใส่ช่องของยาน (ยานถูก 1 ช่อง ยานกลาง 2 ช่อง ยานแพง 3 ช่อง) ตีบวกไอเท็มได้ถึง +5
// ข้อมูลเก็บเป็น JSON: Firebase users/{uid}/economy_json + PlayerPrefs "economy_{uid}" (เลือกก้อนที่บันทึกล่าสุด)
// เหรียญหักผ่าน FirebaseManager.SpendCoins (Transaction กันเหรียญติดลบ/หักซ้ำ)
// ค่าพลังที่ได้ใช้ในเกมผ่าน Economy.BuildBonus → PlayerController.InitializeStats (เฉพาะยานของเรา และห้องเปิด UPGRADES)
// รูปไอคอนไอเท็ม: Resources/Images/Items/item_{id ตัวเล็ก}.png (ยังไม่มีรูป = ใช้กล่องสีจากโค้ดแทน) ดู Docs.คู่มือ/ART_TODO_TH.md
using System;
using System.Collections.Generic;
using UnityEngine;

// สถานะตีบวกของยานหนึ่งลำ: เลเวล HP/SPD/ATK และไอเท็มที่ใส่ในช่อง (เก็บใน EconomyData.ships)
[Serializable]
public class ShipUpgradeState
{
    public int ship; // index ยานใน BattleLoadoutCatalog.Ships ที่ข้อมูลนี้เป็นของ
    public int hp, spd, atk;          // เลเวลตีบวกแต่ละค่า
    public List<string> slots = new List<string>();   // uid ของไอเท็มที่ใส่ไว้
}

// ไอเท็มหนึ่งชิ้นที่ผู้เล่นมี (ซื้อจากร้าน/ได้จากกล่อง) พร้อมเลเวลตีบวก 1-5
[Serializable]
public class OwnedItem
{
    public string uid;     // รหัสเฉพาะของไอเท็มชิ้นนี้
    public string item;    // ชนิด (ItemDef.id)
    public int level = 1; // เลเวลตีบวกของไอเท็ม (1 ถึง ItemMaxLevel)
}

// ข้อมูลเศรษฐกิจทั้งหมดของผู้เล่น แปลงเป็น JSON เก็บใน Firebase economy_json และ PlayerPrefs
// savedAt ใช้เทียบว่าก้อนไหนบันทึกล่าสุดตอนโหลด
[Serializable]
public class EconomyData
{
    public List<ShipUpgradeState> ships = new List<ShipUpgradeState>(); // สถานะตีบวกและช่องไอเท็มของยานแต่ละลำ
    public List<OwnedItem> items = new List<OwnedItem>(); // ไอเท็มทั้งหมดที่ผู้เล่นมีในคลัง
    public int cratesOpened; // จำนวนกล่องสุ่มที่เปิดไปแล้วทั้งหมด
    public long savedAt; // เวลาที่บันทึกล่าสุด (ms) ใช้เลือกก้อนข้อมูลที่ใหม่กว่า
    public string shopDay = "";                          // วันของร้านรายวันที่ซื้อไป (yyyy-MM-dd)
    public List<int> shopBought = new List<int>();       // ช่องร้านรายวันที่ซื้อไปแล้วในวันนั้น (ช่องละ 1 ชิ้นต่อวัน)
}

// ค่าพลังเสริมรวม (คำนวณจากตีบวก + ไอเท็มที่ใส่)
public class ShipBonus
{
    public float hp, atk, spd;             // เพิ่มเป็นสัดส่วน (0.2 = +20%)
    public float fireRate, cooldown;       // ลดเวลาเป็นสัดส่วน
    public float regen;                    // HP/วินาที เมื่อไม่โดนยิง 3 วินาที
    public float killHeal;                 // ฟื้นเลือดเป็นสัดส่วนของ HP สูงสุดเมื่อฆ่าได้
    public float protection;               // วินาทีกันตัวหลังเกิดเพิ่ม
}

// ตัวจัดการระบบตีบวกยาน/ไอเท็ม/ร้านค้า/กล่องสุ่ม แบบ static เรียกได้จากทุกฉาก
public static class Economy
{
    // ความหายากของไอเท็ม (ใช้เป็น index ของ RarityNames / RarityColors)
    public enum Rarity { Common, Rare, Epic }
    // ค่าพลังยานที่ตีบวกได้ (ใช้เป็น index ของ StatNames / StatPerLevel)
    public enum Stat { Hp, Spd, Atk }
    public static readonly string[] StatNames = { "HULL (HP)", "ENGINE (SPD)", "WEAPON (ATK)" }; // ชื่อค่าพลังที่ตีบวกได้ แสดงในหน้า Workshop ตามลำดับ enum Stat
    public static readonly string[] RarityNames = { "COMMON", "RARE", "EPIC" }; // ชื่อระดับความหายากที่แสดงบน UI ตามลำดับ enum Rarity
    public static readonly Color[] RarityColors = { new Color(.7f, .78f, .85f), new Color(.35f, .7f, 1f), new Color(.8f, .45f, 1f) }; // สีประจำความหายาก ใช้ระบายกรอบ/ชื่อไอเท็ม

    // นิยามชนิดไอเท็ม: ชื่อ ความหายาก ข้อความผล ค่าที่เพิ่มต่อเลเวล และฟังก์ชันใส่ผลลง ShipBonus
    public class ItemDef
    {
        public string id, name, effect; // รหัสไอเท็ม ชื่อที่แสดง และข้อความผล (มี {0} แทนค่า)
        public Rarity rarity; // ระดับความหายากของไอเท็ม (กำหนดราคาร้านและสี)
        public float perLevel; // ค่าที่เพิ่มต่อเลเวลตีบวก 1 ขั้น
        public Action<ShipBonus, float> apply; // ฟังก์ชันใส่ผลไอเท็มลง ShipBonus ตามค่ารวมของเลเวล
        // สร้างนิยามไอเท็ม (ใช้ในรายการ Items ด้านล่าง)
        public ItemDef(string id, string name, Rarity rarity, string effect, float perLevel, Action<ShipBonus, float> apply)
        { this.id = id; this.name = name; this.rarity = rarity; this.effect = effect; this.perLevel = perLevel; this.apply = apply; }
        public string IconPath => "Images/Items/item_" + id.ToLowerInvariant(); // path รูปไอคอนไอเท็มใน Resources
        // ข้อความอธิบายผลไอเท็มที่เลเวล level เช่น "Damage +15%" (แทน {0} ด้วย perLevel x level)
        public string Describe(int level) => string.Format(effect, Mathf.RoundToInt(perLevel * level * 100) / 100f);
    }

    // ===== รายการไอเท็ม (แก้ตัวเลขได้ที่นี่) =====
    public static readonly ItemDef[] Items =
    {
        new ItemDef("ARMOR", "Armor Plating", Rarity.Common, "Hull HP +{0}%", 6, (b, v) => b.hp += v / 100f),
        new ItemDef("THRUST", "Ion Thruster", Rarity.Common, "Speed +{0}%", 4, (b, v) => b.spd += v / 100f),
        new ItemDef("CANNON", "Plasma Cannon", Rarity.Rare, "Damage +{0}%", 5, (b, v) => b.atk += v / 100f),
        new ItemDef("LOADER", "Rapid Loader", Rarity.Rare, "Fire interval -{0}%", 4, (b, v) => b.fireRate += v / 100f),
        new ItemDef("CAPACITOR", "Skill Capacitor", Rarity.Rare, "Skill cooldown -{0}%", 6, (b, v) => b.cooldown += v / 100f),
        new ItemDef("NANO", "Nano Repair", Rarity.Epic, "Regenerate {0} HP/sec out of combat", .6f, (b, v) => b.regen += v),
        new ItemDef("AEGIS", "Aegis Core", Rarity.Epic, "Spawn shield +{0}s", .5f, (b, v) => b.protection += v),
        new ItemDef("SIPHON", "Siphon Module", Rarity.Epic, "Heal {0}% HP on kill", 4, (b, v) => b.killHeal += v / 100f),
    };
    public const int ItemMaxLevel = 5; // เลเวลตีบวกสูงสุดของไอเท็ม

    // ราคาในร้าน (Epic ได้จากกล่องเท่านั้น)
    public static int ShopPrice(Rarity rarity) => rarity == Rarity.Common ? 400 : rarity == Rarity.Rare ? 900 : 0;
    public const int CratePrice = 600;
    // โอกาสในกล่อง (แสดงในร้านให้ผู้เล่นเห็น): เหรียญ 45% / Common 35% / Rare 15% / Epic 5%
    public static readonly int[] CrateOdds = { 45, 35, 15, 5 };

    // ===== ตีบวกยาน =====
    // ยานถูก (ฟรี) ตีได้ +5, ยานราคาไม่ถึง 3000 ได้ +8, ยานแพงกว่านั้นได้ +10
    public static int UpgradeCap(int ship)
    {
        int price = BattleLoadoutCatalog.Ships[BattleLoadoutCatalog.ValidShip(ship)].price;
        return price <= 0 ? 5 : price < 3000 ? 8 : 10;
    }
    // ช่องไอเท็ม: 1 / 2 / 3 ตามราคายาน
    public static int SlotCount(int ship)
    {
        int price = BattleLoadoutCatalog.Ships[BattleLoadoutCatalog.ValidShip(ship)].price;
        return price <= 0 ? 1 : price < 3000 ? 2 : 3;
    }
    // ผลต่อเลเวล: HP +4%, SPD +2.5%, ATK +4%
    public static readonly float[] StatPerLevel = { .04f, .025f, .04f };
    // ราคาตีบวกจากเลเวล level ไป level+1 (แบบ SAFE) / CHANCE ราคาครึ่งเดียว
    public static int UpgradeCost(int level, bool safe) => (safe ? 2 : 1) * 100 * (level + 1);
    // โอกาสสำเร็จแบบ CHANCE (%)
    public static int UpgradeChance(int level) => Mathf.Clamp(95 - level * 7, 30, 95);
    // ตีบวกไอเท็ม: ราคา / โอกาส (ไอเท็มใช้แบบ CHANCE อย่างเดียว)
    public static int ItemUpgradeCost(OwnedItem item) => (int)(FindItem(item.item)?.rarity ?? Rarity.Common) * 150 + 200 * item.level;
    // โอกาสสำเร็จ (%) ตอนตีบวกไอเท็มจากเลเวล level: ลดลงเลเวลละ 15% อยู่ในช่วง 35-90%
    public static int ItemUpgradeChance(int level) => Mathf.Clamp(100 - (level) * 15, 35, 90);

    // ===== สถานะ =====
    public static EconomyData Data { get; private set; }
    public static event Action Changed; // ยิงเมื่อข้อมูลเศรษฐกิจโหลดหรือเปลี่ยน ให้หน้าจอวาดใหม่
    private static string loadedFor; // uid ที่โหลดข้อมูลไว้ล่าสุด ใช้เช็กว่าเปลี่ยนบัญชีแล้วต้องโหลดใหม่
    private static string Uid => FirebaseManager.Instance != null && FirebaseManager.Instance.IsLoggedIn() ? FirebaseManager.Instance.GetUserId() : "local"; // uid ผู้เล่นที่ล็อกอิน หรือ "local" ถ้ายังไม่ล็อกอิน
    private static string PrefsKey => "economy_" + Uid; // คีย์ PlayerPrefs สำหรับเก็บข้อมูลเศรษฐกิจของบัญชีนี้ในเครื่อง

    // โหลดข้อมูลจากเครื่องทันที แล้วโหลด economy_json จาก Firebase (ถ้า savedAt ใหม่กว่าใช้ของ Firebase)
    // เสร็จแล้วยิง event Changed และเรียก done
    public static void Load(Action done = null)
    {
        LoadLocal();
        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsLoggedIn()) { Changed?.Invoke(); done?.Invoke(); return; }
        string uid = Uid;
        FirebaseManager.Instance.LoadUserJson("economy_json", json =>
        {
            if (uid != Uid) return;
            var remote = Parse(json);
            if (remote != null && remote.savedAt >= Data.savedAt) Data = remote;
            Normalize();
            Changed?.Invoke();
            done?.Invoke();
        });
    }

    // โหลดจากเครื่องเฉพาะเมื่อยังไม่มีข้อมูล หรือบัญชีที่ล็อกอินเปลี่ยนไปจากที่โหลดไว้
    public static void EnsureLoaded()
    {
        if (Data == null || loadedFor != Uid) LoadLocal();
    }

    // อ่านข้อมูลจาก PlayerPrefs ของบัญชีปัจจุบัน (ไม่มี/อ่านไม่ได้ = เริ่มข้อมูลใหม่)
    private static void LoadLocal()
    {
        loadedFor = Uid;
        Data = Parse(PlayerPrefs.GetString(PrefsKey, "")) ?? new EconomyData();
        Normalize();
    }

    // แปลง JSON เป็น EconomyData (สตริงว่างหรือ JSON เสีย = คืน null)
    private static EconomyData Parse(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonUtility.FromJson<EconomyData>(json); } catch (Exception) { return null; }
    }

    // เติม List ที่เป็น null หลังโหลด JSON กัน NullReferenceException
    private static void Normalize()
    {
        if (Data.ships == null) Data.ships = new List<ShipUpgradeState>();
        if (Data.items == null) Data.items = new List<OwnedItem>();
        if (Data.shopBought == null) Data.shopBought = new List<int>();
        if (Data.shopDay == null) Data.shopDay = "";
        foreach (var state in Data.ships) if (state.slots == null) state.slots = new List<string>();
    }

    // บันทึกข้อมูล: ประทับเวลา savedAt แล้วเขียนลง PlayerPrefs และ Firebase (ถ้าล็อกอิน) จากนั้นยิง Changed
    public static void Save()
    {
        if (Data == null) return;
        Data.savedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string json = JsonUtility.ToJson(Data);
        PlayerPrefs.SetString(PrefsKey, json);
        PlayerPrefs.Save();
        if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsLoggedIn())
            FirebaseManager.Instance.SaveUserJson("economy_json", json);
        Changed?.Invoke();
    }

    // หานิยามไอเท็มจาก id (ไม่พบ = null)
    public static ItemDef FindItem(string id)
    {
        foreach (var def in Items) if (def.id == id) return def;
        return null;
    }

    // คืนสถานะตีบวกของยาน ship ถ้ายังไม่เคยมีจะสร้างใหม่และเพิ่มเข้า Data.ships
    public static ShipUpgradeState ShipState(int ship)
    {
        EnsureLoaded();
        foreach (var state in Data.ships) if (state.ship == ship) return state;
        var created = new ShipUpgradeState { ship = ship };
        Data.ships.Add(created);
        return created;
    }

    // อ่านเลเวลตีบวกของค่าพลัง stat ในยานนั้น
    public static int StatLevel(ShipUpgradeState state, Stat stat) => stat == Stat.Hp ? state.hp : stat == Stat.Spd ? state.spd : state.atk;
    // ตั้งเลเวลตีบวกของค่าพลัง stat (ใช้หลังตีบวกสำเร็จ)
    private static void SetStatLevel(ShipUpgradeState state, Stat stat, int value)
    {
        if (stat == Stat.Hp) state.hp = value; else if (stat == Stat.Spd) state.spd = value; else state.atk = value;
    }

    // หาไอเท็มที่ผู้เล่นมีจาก uid (ยังไม่โหลดข้อมูล/ไม่พบ = null)
    public static OwnedItem FindOwned(string uid)
    {
        if (Data == null) return null;
        foreach (var item in Data.items) if (item.uid == uid) return item;
        return null;
    }

    // ไอเท็มชิ้นนี้ใส่อยู่ในยานลำไหน (-1 = ยังไม่ได้ใส่)
    public static int EquippedOn(string uid)
    {
        foreach (var state in Data.ships) if (state.slots.Contains(uid)) return state.ship;
        return -1;
    }

    // ===== ค่าพลังรวมของยาน =====
    public static ShipBonus BuildBonus(int ship)
    {
        var bonus = new ShipBonus();
        if (!FeatureFlags.Upgrades && !FeatureFlags.Items) return bonus;
        var state = ShipState(ship);
        if (FeatureFlags.Upgrades)
        {
            bonus.hp += Mathf.Min(state.hp, UpgradeCap(ship)) * StatPerLevel[0];
            bonus.spd += Mathf.Min(state.spd, UpgradeCap(ship)) * StatPerLevel[1];
            bonus.atk += Mathf.Min(state.atk, UpgradeCap(ship)) * StatPerLevel[2];
        }
        if (FeatureFlags.Items)
            for (int i = 0; i < state.slots.Count && i < SlotCount(ship); i++)
            {
                var owned = FindOwned(state.slots[i]);
                var def = owned != null ? FindItem(owned.item) : null;
                if (def != null) def.apply(bonus, def.perLevel * Mathf.Clamp(owned.level, 1, ItemMaxLevel));
            }
        bonus.fireRate = Mathf.Min(bonus.fireRate, .5f);
        bonus.cooldown = Mathf.Min(bonus.cooldown, .5f);
        return bonus;
    }

    // โบนัสแยกส่วน (ใช้แสดงผลในล็อบบี้): upgrades = true คืนเฉพาะส่วนตีบวก, false คืนเฉพาะส่วนจากไอเท็มที่ใส่อยู่
    public static ShipBonus PartBonus(int ship, bool upgrades)
    {
        var bonus = new ShipBonus();
        var state = ShipState(ship);
        if (upgrades)
        {
            if (!FeatureFlags.Upgrades) return bonus;
            bonus.hp = Mathf.Min(state.hp, UpgradeCap(ship)) * StatPerLevel[0];
            bonus.spd = Mathf.Min(state.spd, UpgradeCap(ship)) * StatPerLevel[1];
            bonus.atk = Mathf.Min(state.atk, UpgradeCap(ship)) * StatPerLevel[2];
            return bonus;
        }
        if (!FeatureFlags.Items) return bonus;
        for (int i = 0; i < state.slots.Count && i < SlotCount(ship); i++)
        {
            var owned = FindOwned(state.slots[i]);
            var def = owned != null ? FindItem(owned.item) : null;
            if (def != null) def.apply(bonus, def.perLevel * Mathf.Clamp(owned.level, 1, ItemMaxLevel));
        }
        return bonus;
    }

    // ===== การกระทำ (ทุกอย่างหักเหรียญก่อน สำเร็จแล้วจึงเปลี่ยนข้อมูล) =====
    // หักเหรียญ: ไม่มี Firebase (ทดสอบใน Editor) = ถือว่าสำเร็จ
    private static void Spend(int cost, Action<bool, long> done)
    {
        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsLoggedIn()) { done(true, -1); return; }
        FirebaseManager.Instance.SpendCoins(cost, done);
    }

    // ตีบวกค่าพลังยาน: callback(สำเร็จจ่ายเงิน, ตีติด, ข้อความ, เหรียญคงเหลือ)
    public static void UpgradeStat(int ship, Stat stat, bool safe, Action<bool, bool, string, long> done)
    {
        var state = ShipState(ship);
        int level = StatLevel(state, stat);
        if (level >= UpgradeCap(ship)) { done(false, false, "This ship is already at its upgrade limit (+" + UpgradeCap(ship) + ").", -1); return; }
        int cost = UpgradeCost(level, safe);
        Spend(cost, (paid, balance) =>
        {
            if (!paid) { done(false, false, "Not enough Astronium (" + cost + " needed).", balance); return; }
            bool success = safe || UnityEngine.Random.Range(0, 100) < UpgradeChance(level);
            if (success) SetStatLevel(state, stat, level + 1);
            Save();
            done(true, success, success ? StatNames[(int)stat] + " upgraded to +" + (level + 1) + "!" : "Upgrade failed... (coins spent, level kept)", balance);
        });
    }

    // ซื้อไอเท็มจากร้าน: หักเหรียญตามความหายาก (Epic ซื้อไม่ได้) แล้วเพิ่มไอเท็มเลเวล 1
    // callback(สำเร็จ, ข้อความ, เหรียญคงเหลือ)
    public static void BuyItem(ItemDef def, Action<bool, string, long> done)
    {
        int price = ShopPrice(def.rarity);
        if (price <= 0) { done(false, "This item is only found in Supply Crates.", -1); return; }
        Spend(price, (paid, balance) =>
        {
            if (!paid) { done(false, "Not enough Astronium (" + price + " needed).", balance); return; }
            AddItem(def.id);
            Save();
            done(true, "Bought " + def.name + "!", balance);
        });
    }

    // ผลกล่องล่าสุด (ใช้แสดงอนิเมชันเปิดกล่อง LobbyManager.CrateReveal.cs): ได้เหรียญ = LastCrateCoins, ได้ไอเท็ม = LastCrateItem
    public static int LastCrateCoins;
    public static ItemDef LastCrateItem;

    // เปิดกล่อง: callback(สำเร็จ, ข้อความผลที่ได้, เหรียญคงเหลือ)
    public static void OpenCrate(Action<bool, string, long> done)
    {
        Spend(CratePrice, (paid, balance) =>
        {
            if (!paid) { done(false, "Not enough Astronium (" + CratePrice + " needed).", balance); return; }
            Data.cratesOpened++;
            LastCrateCoins = 0; LastCrateItem = null;
            int roll = UnityEngine.Random.Range(0, 100);
            string message;
            if (roll < CrateOdds[0])
            {
                int coins = UnityEngine.Random.Range(4, 11) * 50;
                if (FirebaseManager.Instance != null) FirebaseManager.Instance.AddCoins(coins);
                LastCrateCoins = coins;
                message = "Crate: +" + coins + " Astronium";
            }
            else
            {
                Rarity rarity = roll < CrateOdds[0] + CrateOdds[1] ? Rarity.Common : roll < CrateOdds[0] + CrateOdds[1] + CrateOdds[2] ? Rarity.Rare : Rarity.Epic;
                var pool = new List<ItemDef>();
                foreach (var def in Items) if (def.rarity == rarity) pool.Add(def);
                var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
                AddItem(pick.id);
                LastCrateItem = pick;
                message = "Crate: " + RarityNames[(int)rarity] + " " + pick.name + "!";
            }
            Save();
            done(true, message, balance);
        });
    }

    // เพิ่มไอเท็มชนิด id เข้าคลัง สร้าง uid สุ่ม 12 ตัวอักษร เลเวลเริ่มที่ 1
    private static void AddItem(string id)
    {
        Data.items.Add(new OwnedItem { uid = Guid.NewGuid().ToString("N").Substring(0, 12), item = id, level = 1 });
    }

    // ตีบวกไอเท็ม (แบบ CHANCE อย่างเดียว): หักเหรียญแล้วสุ่มตามโอกาส ล้มเหลวเลเวลไม่ลด
    // callback(สำเร็จจ่ายเงิน, ตีติด, ข้อความ, เหรียญคงเหลือ)
    public static void UpgradeItem(OwnedItem item, Action<bool, bool, string, long> done)
    {
        if (item == null || item.level >= ItemMaxLevel) { done(false, false, "Item is already at max level.", -1); return; }
        int cost = ItemUpgradeCost(item);
        int chance = ItemUpgradeChance(item.level);
        Spend(cost, (paid, balance) =>
        {
            if (!paid) { done(false, false, "Not enough Astronium (" + cost + " needed).", balance); return; }
            bool success = UnityEngine.Random.Range(0, 100) < chance;
            if (success) item.level++;
            Save();
            done(true, success, success ? "Item upgraded to +" + item.level + "!" : "Item upgrade failed (level kept).", balance);
        });
    }

    // ===== ร้านค้ารายวัน (FeatureFlags.DailyShop) =====
    // ทุกวันสุ่มไอเท็ม DailyShopSlots ชิ้นมาขาย (รีเซ็ตเที่ยงคืนเวลาไทย ทุกคนเห็นของชุดเดียวกัน) ช่องละ 1 ชิ้นต่อวัน
    // โอกาสออกแต่ละช่อง: COMMON 65% / RARE 27% / EPIC 8% (ยิ่งหายากยิ่งออกน้อย) — ราคาสูงมาก: 3,000 / 8,000 / 20,000
    // เวลาอ้างอิงเวลา server (Social.ServerTimeMs) ถ้ามี — แก้นาฬิกาเครื่องเพื่อสุ่มร้านใหม่ไม่ได้
    public const int DailyShopSlots = 4;
    public static readonly int[] DailyRarityOdds = { 65, 27, 8 };
    // ราคาขายในร้านรายวันตามความหายาก
    public static int DailyPrice(Rarity rarity) => rarity == Rarity.Common ? 3000 : rarity == Rarity.Rare ? 8000 : 20000;
    // เวลาปัจจุบันแบบเวลาไทย (UTC+7)
    private static DateTime ShopNow => DateTimeOffset.FromUnixTimeMilliseconds(Social.ServerTimeMs).UtcDateTime.AddHours(7);
    // วันของร้าน เช่น "2026-10-08"
    public static string ShopDay => ShopNow.ToString("yyyy-MM-dd");
    // เหลือเวลาอีกเท่าไรร้านจะสุ่มใหม่
    public static TimeSpan ShopResetIn => ShopNow.Date.AddDays(1) - ShopNow;

    // ไอเท็มที่ขายวันนี้ (สุ่มจากวันที่ = ทุกเครื่องได้ชุดเดียวกัน ไม่ซ้ำกันในวันเดียว)
    public static ItemDef[] DailyItems()
    {
        string day = ShopDay;
        uint hash = 2166136261; // FNV-1a ของวันที่ (string.GetHashCode ไม่คงที่ข้ามเครื่อง)
        foreach (char c in day) { hash ^= c; hash *= 16777619; }
        var random = new System.Random((int)(hash & 0x7fffffff));
        var picked = new List<ItemDef>();
        for (int slot = 0; slot < DailyShopSlots; slot++)
        {
            int roll = random.Next(100);
            Rarity rarity = roll < DailyRarityOdds[0] ? Rarity.Common : roll < DailyRarityOdds[0] + DailyRarityOdds[1] ? Rarity.Rare : Rarity.Epic;
            var pool = new List<ItemDef>();
            foreach (var def in Items) if (def.rarity == rarity && !picked.Contains(def)) pool.Add(def);
            if (pool.Count == 0) foreach (var def in Items) if (!picked.Contains(def)) pool.Add(def); // ความหายากนั้นหมดแล้ว
            picked.Add(pool[random.Next(pool.Count)]);
        }
        return picked.ToArray();
    }

    // ช่องนี้ซื้อไปแล้ววันนี้หรือยัง
    public static bool DailyBought(int slot)
    {
        EnsureLoaded();
        return Data.shopDay == ShopDay && Data.shopBought.Contains(slot);
    }

    // ซื้อไอเท็มช่อง slot ของร้านวันนี้ (ช่องละ 1 ครั้งต่อวัน): callback(สำเร็จ, ข้อความ, เหรียญคงเหลือ)
    public static void BuyDaily(int slot, Action<bool, string, long> done)
    {
        EnsureLoaded();
        var items = DailyItems();
        if (slot < 0 || slot >= items.Length) { done(false, "This offer is no longer available.", -1); return; }
        string day = ShopDay;
        if (DailyBought(slot)) { done(false, "Already bought today. The shop refreshes at midnight.", -1); return; }
        var def = items[slot];
        int price = DailyPrice(def.rarity);
        Spend(price, (paid, balance) =>
        {
            if (!paid) { done(false, "Not enough Astronium (" + price + " needed).", balance); return; }
            if (Data.shopDay != day) { Data.shopDay = day; Data.shopBought.Clear(); }
            Data.shopBought.Add(slot);
            AddItem(def.id);
            Save();
            done(true, "Bought " + def.name + "!", balance);
        });
    }

    // ===== ขายไอเท็ม (FeatureFlags.SellItems) =====
    // ราคาขาย = 40% ของมูลค่าไอเท็ม (COMMON 400 / RARE 900 / EPIC 1600) + 25% ของค่าตีบวกที่ใส่ไปแล้ว ปัดเป็นหลักสิบ
    // เช่น COMMON +1 = 160, RARE +1 = 360, EPIC +1 = 640 (ขายได้น้อยกว่าราคาซื้อ/ราคากล่องเสมอ ปั๊มเหรียญไม่ได้)
    // มูลค่าไอเท็มตามความหายาก (ใช้คิดราคาขาย)
    public static int ItemValue(Rarity rarity) => rarity == Rarity.Common ? 400 : rarity == Rarity.Rare ? 900 : 1600;
    // ราคาขายของไอเท็มชิ้นนี้ (ตามสูตรด้านบน)
    public static int SellPrice(OwnedItem item)
    {
        var def = item != null ? FindItem(item.item) : null;
        if (def == null) return 0;
        float price = ItemValue(def.rarity) * .4f;
        for (int level = 1; level < item.level; level++) // ค่าตีบวกจาก +1 ถึงเลเวลปัจจุบัน
            price += ((int)def.rarity * 150 + 200 * level) * .25f;
        return Mathf.RoundToInt(price / 10f) * 10;
    }

    // ขายไอเท็ม: ถอดออกจากยานทุกลำ ลบออกจากคลัง แล้วเพิ่มเหรียญ (เพิ่มเหรียญไม่สำเร็จ = คืนไอเท็มให้)
    // callback(สำเร็จ, ข้อความ, เหรียญคงเหลือ -1 = ไม่ทราบ)
    public static void SellItem(OwnedItem item, Action<bool, string, long> done)
    {
        EnsureLoaded();
        if (!FeatureFlags.SellItems || item == null || !Data.items.Contains(item)) { done(false, "This item can't be sold.", -1); return; }
        var def = FindItem(item.item);
        int price = SellPrice(item);
        string name = (def != null ? def.name : item.item) + " +" + item.level;
        // ลบออกก่อนค่อยเพิ่มเหรียญ (กันกดขายซ้ำได้เงินสองรอบ)
        foreach (var state in Data.ships) state.slots.Remove(item.uid);
        Data.items.Remove(item);
        Save();
        var firebase = FirebaseManager.Instance;
        if (firebase == null || !firebase.IsLoggedIn()) { done(true, "Sold " + name + " for " + price + " Astronium.", -1); return; } // ทดสอบใน Editor ไม่มีบัญชี
        firebase.AddCoins(price, ok =>
        {
            if (!ok)
            {
                Data.items.Add(item); // เพิ่มเหรียญไม่สำเร็จ: คืนไอเท็ม
                Save();
                done(false, "Could not sell (connection). Your item was returned.", -1);
                return;
            }
            firebase.GetCoinBalance(balance => done(true, "Sold " + name + " for " + price + " Astronium.", balance),
                error => done(true, "Sold " + name + " for " + price + " Astronium.", -1));
        });
    }

    // ใส่/ถอดไอเท็ม (ฟรี): ใส่ในยาน ship ถ้าช่องเต็มจะไม่ใส่ / ไอเท็มที่อยู่ยานอื่นจะถูกย้ายมา
    public static string ToggleEquip(int ship, OwnedItem item)
    {
        var state = ShipState(ship);
        if (state.slots.Contains(item.uid)) { state.slots.Remove(item.uid); Save(); return "Removed " + FindItem(item.item)?.name; }
        if (state.slots.Count >= SlotCount(ship)) return "All " + SlotCount(ship) + " slots are full. Remove an item first.";
        int other = EquippedOn(item.uid);
        if (other >= 0) ShipState(other).slots.Remove(item.uid);
        state.slots.Add(item.uid);
        Save();
        return "Equipped " + FindItem(item.item)?.name;
    }
}
