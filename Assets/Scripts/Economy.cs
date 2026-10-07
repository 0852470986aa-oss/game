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

[Serializable]
public class ShipUpgradeState
{
    public int ship;
    public int hp, spd, atk;          // เลเวลตีบวกแต่ละค่า
    public List<string> slots = new List<string>();   // uid ของไอเท็มที่ใส่ไว้
}

[Serializable]
public class OwnedItem
{
    public string uid;     // รหัสเฉพาะของไอเท็มชิ้นนี้
    public string item;    // ชนิด (ItemDef.id)
    public int level = 1;
}

[Serializable]
public class EconomyData
{
    public List<ShipUpgradeState> ships = new List<ShipUpgradeState>();
    public List<OwnedItem> items = new List<OwnedItem>();
    public int cratesOpened;
    public long savedAt;
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

public static class Economy
{
    public enum Rarity { Common, Rare, Epic }
    public enum Stat { Hp, Spd, Atk }
    public static readonly string[] StatNames = { "HULL (HP)", "ENGINE (SPD)", "WEAPON (ATK)" };
    public static readonly string[] RarityNames = { "COMMON", "RARE", "EPIC" };
    public static readonly Color[] RarityColors = { new Color(.7f, .78f, .85f), new Color(.35f, .7f, 1f), new Color(.8f, .45f, 1f) };

    public class ItemDef
    {
        public string id, name, effect;
        public Rarity rarity;
        public float perLevel;
        public Action<ShipBonus, float> apply;
        public ItemDef(string id, string name, Rarity rarity, string effect, float perLevel, Action<ShipBonus, float> apply)
        { this.id = id; this.name = name; this.rarity = rarity; this.effect = effect; this.perLevel = perLevel; this.apply = apply; }
        public string IconPath => "Images/Items/item_" + id.ToLowerInvariant();
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
    public const int ItemMaxLevel = 5;

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
    public static int ItemUpgradeChance(int level) => Mathf.Clamp(100 - (level) * 15, 35, 90);

    // ===== สถานะ =====
    public static EconomyData Data { get; private set; }
    public static event Action Changed;
    private static string loadedFor;
    private static string Uid => FirebaseManager.Instance != null && FirebaseManager.Instance.IsLoggedIn() ? FirebaseManager.Instance.GetUserId() : "local";
    private static string PrefsKey => "economy_" + Uid;

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

    public static void EnsureLoaded()
    {
        if (Data == null || loadedFor != Uid) LoadLocal();
    }

    private static void LoadLocal()
    {
        loadedFor = Uid;
        Data = Parse(PlayerPrefs.GetString(PrefsKey, "")) ?? new EconomyData();
        Normalize();
    }

    private static EconomyData Parse(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonUtility.FromJson<EconomyData>(json); } catch (Exception) { return null; }
    }

    private static void Normalize()
    {
        if (Data.ships == null) Data.ships = new List<ShipUpgradeState>();
        if (Data.items == null) Data.items = new List<OwnedItem>();
        foreach (var state in Data.ships) if (state.slots == null) state.slots = new List<string>();
    }

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

    public static ItemDef FindItem(string id)
    {
        foreach (var def in Items) if (def.id == id) return def;
        return null;
    }

    public static ShipUpgradeState ShipState(int ship)
    {
        EnsureLoaded();
        foreach (var state in Data.ships) if (state.ship == ship) return state;
        var created = new ShipUpgradeState { ship = ship };
        Data.ships.Add(created);
        return created;
    }

    public static int StatLevel(ShipUpgradeState state, Stat stat) => stat == Stat.Hp ? state.hp : stat == Stat.Spd ? state.spd : state.atk;
    private static void SetStatLevel(ShipUpgradeState state, Stat stat, int value)
    {
        if (stat == Stat.Hp) state.hp = value; else if (stat == Stat.Spd) state.spd = value; else state.atk = value;
    }

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

    // เปิดกล่อง: callback(สำเร็จ, ข้อความผลที่ได้, เหรียญคงเหลือ)
    public static void OpenCrate(Action<bool, string, long> done)
    {
        Spend(CratePrice, (paid, balance) =>
        {
            if (!paid) { done(false, "Not enough Astronium (" + CratePrice + " needed).", balance); return; }
            Data.cratesOpened++;
            int roll = UnityEngine.Random.Range(0, 100);
            string message;
            if (roll < CrateOdds[0])
            {
                int coins = UnityEngine.Random.Range(4, 11) * 50;
                if (FirebaseManager.Instance != null) FirebaseManager.Instance.AddCoins(coins);
                message = "Crate: +" + coins + " Astronium";
            }
            else
            {
                Rarity rarity = roll < CrateOdds[0] + CrateOdds[1] ? Rarity.Common : roll < CrateOdds[0] + CrateOdds[1] + CrateOdds[2] ? Rarity.Rare : Rarity.Epic;
                var pool = new List<ItemDef>();
                foreach (var def in Items) if (def.rarity == rarity) pool.Add(def);
                var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
                AddItem(pick.id);
                message = "Crate: " + RarityNames[(int)rarity] + " " + pick.name + "!";
            }
            Save();
            done(true, message, balance);
        });
    }

    private static void AddItem(string id)
    {
        Data.items.Add(new OwnedItem { uid = Guid.NewGuid().ToString("N").Substring(0, 12), item = id, level = 1 });
    }

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
