// Premium.cs — เงินพรีเมียม "คริสตัล" + ร้านเติมเงิน (ระบบชำระเงินจำลอง) + ข้อมูลร่วมของ Battle Pass / กาชาสกิน
// สวิตช์: FeatureFlags.PremiumShop (ร้านเติมเงิน/คริสตัล), FeatureFlags.PremiumPass, FeatureFlags.SkinGacha
//
// ภาพรวมระบบเศรษฐกิจแบบเกมขาย:
//   เติมเงิน (บาท) → ได้คริสตัล → ใช้คริสตัลซื้อ Battle Pass พรีเมียม / สุ่มกาชาสกิน
//   Astronium (เหรียญเดิม) ยังหาได้จากการเล่นเหมือนเดิม — คริสตัลแยกเป็นอีกสกุล
//
// การชำระเงิน: ใช้ IPaymentProvider (ตอนนี้ = SimulatedPayment ระบบจำลอง ไม่ตัดเงินจริง)
//   ถ้าวันหนึ่งจะขายจริง: เขียนคลาสใหม่ที่ทำ IPaymentProvider (เช่น Google Play Billing ผ่าน Unity IAP)
//   แล้วเปลี่ยน Premium.Payment = new คลาสนั้น — ระบบอื่น (คริสตัล/Pass/กาชา/ประวัติ) ไม่ต้องแก้
//
// ที่เก็บข้อมูล:
//   - คริสตัล: Firebase users/{uid}/crystal_balance (ตัวเลข ใช้ Transaction กันยอดติดลบ/หักซ้ำ เหมือนเหรียญ)
//   - อื่น ๆ (ประวัติเติมเงิน, Pass, กาชา, สกินที่มี): users/{uid}/premium_json + PlayerPrefs "premium_{uid}" (เลือกก้อนที่ใหม่กว่า)
//   - ยังไม่ล็อกอิน (ทดสอบใน Editor): คริสตัลเก็บในเครื่อง (PremiumData.localCrystals)
// ข้อจำกัด (เขียนในเล่มได้): การสุ่ม/การให้คริสตัลคำนวณในเครื่องผู้เล่น ถ้าขายจริงควรย้ายไปทำบนเซิร์ฟเวอร์ (Cloud Functions)
using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Database;
using Firebase.Extensions;

// ประวัติการเติมเงิน 1 รายการ
[Serializable]
public class TopupRecord
{
    public string id;       // เลขอ้างอิงรายการ (เช่น SIM-20261011-153012-4821)
    public string pack;     // รหัสแพ็กที่ซื้อ
    public int thb;         // ราคาเป็นบาท
    public int crystals;    // คริสตัลที่ได้ทั้งหมด (รวมโบนัส)
    public string method;   // ช่องทางชำระ (PROMPTPAY / TRUEMONEY / CARD)
    public long at;         // เวลา (ms)
}

// ประวัติการสุ่มกาชา 1 ครั้ง
[Serializable]
public class PullRecord
{
    public string skin;     // id สกินที่ได้ ("" = ได้ชิ้นส่วนดาว)
    public int rarity;      // ระดับ (SkinRarity)
    public int shards;      // ชิ้นส่วนที่ได้ (ของซ้ำ/ผลชิ้นส่วน)
    public long at;         // เวลา (ms)
}

// ข้อมูลพรีเมียมทั้งหมดของผู้เล่น (แปลงเป็น JSON)
[Serializable]
public class PremiumData
{
    public long savedAt;                                   // เวลาบันทึกล่าสุด ใช้เลือกก้อนที่ใหม่กว่า
    public long localCrystals;                             // คริสตัลตอนยังไม่ล็อกอิน (ทดสอบใน Editor)
    public List<string> firstBought = new List<string>();  // แพ็กที่เคยซื้อแล้ว (ครั้งแรกได้โบนัส x2)
    public bool starterBought;                             // ซื้อแพ็กเริ่มต้นแล้ว (ซื้อได้ครั้งเดียว)
    public List<TopupRecord> history = new List<TopupRecord>(); // ประวัติเติมเงิน (ล่าสุดอยู่หน้า)
    // ----- Battle Pass -----
    public string passSeason = "";                         // ฤดูกาลของข้อมูล Pass (yyyy-MM) เปลี่ยนเดือน = เริ่มใหม่
    public int passXp;                                     // XP ของ Pass ในฤดูกาลนี้
    public bool passPremium;                               // ซื้อ Pass พรีเมียมแล้ว
    public List<int> passFree = new List<int>();           // ขั้นที่รับรางวัลฟรีแล้ว
    public List<int> passPrem = new List<int>();           // ขั้นที่รับรางวัลพรีเมียมแล้ว
    // ----- กาชาสกิน -----
    public int tickets;                                    // ตั๋วสุ่มฟรี
    public int shards;                                     // ชิ้นส่วนดาว (แลกสกินได้)
    public int pity;                                       // จำนวนครั้งตั้งแต่ได้ LEGENDARY ครั้งล่าสุด
    public int epicPity;                                   // จำนวนครั้งตั้งแต่ได้ EPIC ขึ้นไปครั้งล่าสุด
    public bool lostFiftyFifty;                            // ได้ LEGENDARY ที่ไม่ใช่ตัวเด่นครั้งล่าสุด → ครั้งหน้าการันตีตัวเด่น
    public int totalPulls;                                 // สุ่มไปทั้งหมดกี่ครั้ง
    public List<string> skins = new List<string>();        // id สกินที่มี
    public List<PullRecord> pulls = new List<PullRecord>(); // ประวัติสุ่มล่าสุด
}

// ผลการชำระเงิน
public class PaymentResult
{
    public bool success;    // จ่ายสำเร็จ
    public string reference; // เลขอ้างอิง
    public string message;  // ข้อความแสดงผู้เล่น
}

// ผู้ให้บริการชำระเงิน (เปลี่ยนตัวได้โดยไม่แก้ระบบอื่น)
public interface IPaymentProvider
{
    string Name { get; }                // ชื่อที่แสดงในหน้าชำระเงิน
    bool IsSimulated { get; }           // true = ระบบจำลอง (ต้องแสดงป้าย DEMO)
    void Pay(TopupPack pack, string method, Action<PaymentResult> done); // ชำระเงินแพ็กนี้ผ่านช่องทาง method
}

// ระบบชำระเงินจำลอง: ไม่ติดต่อธนาคาร/ไม่ตัดเงินจริง สร้างเลขอ้างอิงแล้วถือว่าสำเร็จ
public class SimulatedPayment : IPaymentProvider
{
    public string Name => "DEMO PAYMENT";
    public bool IsSimulated => true;
    public void Pay(TopupPack pack, string method, Action<PaymentResult> done)
    {
        string reference = "SIM-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + UnityEngine.Random.Range(1000, 9999);
        done?.Invoke(new PaymentResult { success = true, reference = reference, message = "PAYMENT SUCCESSFUL" });
    }
}

// แพ็กเติมเงิน
public class TopupPack
{
    public string id;        // รหัสแพ็ก (ใช้เก็บว่าซื้อครั้งแรกแล้ว)
    public int thb;          // ราคา (บาท)
    public int crystals;     // คริสตัลที่ได้
    public bool starter;     // แพ็กเริ่มต้น (ซื้อครั้งเดียว ได้ของหลายอย่าง)
}

// ตัวจัดการคริสตัล / เติมเงิน / ข้อมูลพรีเมียม
public static class Premium
{
    // ===== ค่าคงที่ (ปรับสมดุลได้ที่นี่) =====
    public static readonly TopupPack[] Packs =
    {
        new TopupPack { id = "C60",   thb = 35,   crystals = 60 },
        new TopupPack { id = "C140",  thb = 79,   crystals = 140 },
        new TopupPack { id = "C330",  thb = 179,  crystals = 330 },
        new TopupPack { id = "C680",  thb = 349,  crystals = 680 },
        new TopupPack { id = "C1400", thb = 699,  crystals = 1400 },
        new TopupPack { id = "C3000", thb = 1490, crystals = 3000 },
    };
    // แพ็กเริ่มต้น: 150 คริสตัล + ตั๋ว 3 ใบ + สกิน STARTER ACE + 2,000 Astronium (ซื้อได้ครั้งเดียว)
    public static readonly TopupPack Starter = new TopupPack { id = "STARTER", thb = 35, crystals = 150, starter = true };
    public const int StarterTickets = 3;
    public const int StarterCoins = 2000;
    public const string StarterSkin = "STARTER_ACE";
    public static readonly string[] Methods = { "PROMPTPAY", "TRUEMONEY", "CARD" }; // ช่องทางชำระ (จำลอง)

    // ผู้ให้บริการชำระเงินที่ใช้อยู่ (เปลี่ยนเป็นของจริงได้ที่บรรทัดนี้)
    public static IPaymentProvider Payment = new SimulatedPayment();

    // ===== สถานะ =====
    public static PremiumData Data { get; private set; }
    public static long Crystals { get; private set; } // ยอดคริสตัลล่าสุดที่รู้
    public static bool CrystalsKnown { get; private set; } // โหลดยอดคริสตัลจาก Firebase แล้ว
    public static event Action Changed; // ข้อมูลเปลี่ยน (ให้หน้าจอวาดใหม่)
    private static string loadedFor;

    private static FirebaseManager Fb => FirebaseManager.Instance;
    private static bool Online => Fb != null && Fb.IsLoggedIn() && Fb.GetDbReference() != null;
    private static string Uid => Fb != null && Fb.IsLoggedIn() ? Fb.GetUserId() : "local";
    private static string PrefsKey => "premium_" + Uid;
    private static DatabaseReference UserRef => Fb.GetDbReference().Child("users").Child(Fb.GetUserId());

    public static void NotifyChanged() => Changed?.Invoke();

    // ===== โหลด / บันทึก =====
    public static void Load(Action done = null)
    {
        LoadLocal();
        Changed?.Invoke();
        if (!Online) { Crystals = Data.localCrystals; CrystalsKnown = true; Changed?.Invoke(); done?.Invoke(); return; }
        string uid = Uid;
        Fb.LoadUserJson("premium_json", json =>
        {
            if (uid != Uid) return;
            var remote = Parse(json);
            if (remote != null && remote.savedAt >= Data.savedAt) Data = remote;
            Normalize();
            BattlePass.RollSeason();
            Changed?.Invoke();
            RefreshCrystals(done);
        });
    }

    // โหลดจากเครื่องถ้ายังไม่มีข้อมูลหรือเปลี่ยนบัญชี
    public static void EnsureLoaded()
    {
        if (Data == null || loadedFor != Uid) LoadLocal();
    }

    private static void LoadLocal()
    {
        loadedFor = Uid;
        Data = Parse(PlayerPrefs.GetString(PrefsKey, "")) ?? new PremiumData();
        Normalize();
        BattlePass.RollSeason();
        if (!Online) { Crystals = Data.localCrystals; CrystalsKnown = true; }
    }

    private static PremiumData Parse(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonUtility.FromJson<PremiumData>(json); } catch (Exception) { return null; }
    }

    private static void Normalize()
    {
        if (Data.firstBought == null) Data.firstBought = new List<string>();
        if (Data.history == null) Data.history = new List<TopupRecord>();
        if (Data.passFree == null) Data.passFree = new List<int>();
        if (Data.passPrem == null) Data.passPrem = new List<int>();
        if (Data.skins == null) Data.skins = new List<string>();
        if (Data.pulls == null) Data.pulls = new List<PullRecord>();
        if (Data.passSeason == null) Data.passSeason = "";
    }

    public static void Save()
    {
        if (Data == null) return;
        Data.savedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        while (Data.history.Count > 30) Data.history.RemoveAt(Data.history.Count - 1);
        while (Data.pulls.Count > 40) Data.pulls.RemoveAt(Data.pulls.Count - 1);
        string json = JsonUtility.ToJson(Data);
        PlayerPrefs.SetString(PrefsKey, json);
        PlayerPrefs.Save();
        if (Online) Fb.SaveUserJson("premium_json", json);
        Changed?.Invoke();
    }

    // ===== คริสตัล =====
    // อ่านยอดคริสตัลล่าสุดจาก Firebase (ไม่มีค่า = 0)
    public static void RefreshCrystals(Action done = null)
    {
        if (!Online) { EnsureLoaded(); Crystals = Data.localCrystals; CrystalsKnown = true; Changed?.Invoke(); done?.Invoke(); return; }
        string uid = Uid;
        UserRef.Child("crystal_balance").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (uid != Uid) return;
            if (!task.IsFaulted && !task.IsCanceled && task.Result != null)
            {
                Crystals = task.Result.Exists && TryRead(task.Result.Value, out long value) ? value : 0;
                CrystalsKnown = true;
            }
            Changed?.Invoke();
            done?.Invoke();
        });
    }

    private static bool TryRead(object value, out long result)
        => long.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out result) && result >= 0;

    // เพิ่มคริสตัล (Transaction) done(สำเร็จ)
    public static void AddCrystals(int amount, Action<bool> done = null)
    {
        if (amount <= 0) { done?.Invoke(amount == 0); return; }
        if (!Online)
        {
            EnsureLoaded();
            Data.localCrystals += amount;
            Crystals = Data.localCrystals;
            Save();
            done?.Invoke(true);
            return;
        }
        bool applied = false;
        long after = 0;
        UserRef.Child("crystal_balance").RunTransaction(data =>
        {
            applied = false;
            long balance = 0;
            if (data.Value != null && !TryRead(data.Value, out balance)) return TransactionResult.Abort();
            if (balance > long.MaxValue - amount) return TransactionResult.Abort();
            after = balance + amount;
            data.Value = after;
            applied = true;
            return TransactionResult.Success(data);
        }, false).ContinueWithOnMainThread(task =>
        {
            bool ok = !task.IsFaulted && !task.IsCanceled && applied;
            if (ok) { Crystals = after; CrystalsKnown = true; }
            Changed?.Invoke();
            done?.Invoke(ok);
        });
    }

    // หักคริสตัล (Transaction) ไม่พอ = ไม่หัก done(สำเร็จ)
    public static void SpendCrystals(int amount, Action<bool> done)
    {
        if (amount <= 0) { done?.Invoke(amount == 0); return; }
        if (!Online)
        {
            EnsureLoaded();
            if (Data.localCrystals < amount) { done?.Invoke(false); return; }
            Data.localCrystals -= amount;
            Crystals = Data.localCrystals;
            Save();
            done?.Invoke(true);
            return;
        }
        bool applied = false;
        long after = 0;
        UserRef.Child("crystal_balance").RunTransaction(data =>
        {
            applied = false;
            // แคชในเครื่องยังว่าง: ส่งค่าเดิมกลับไปให้เซิร์ฟเวอร์เรียกซ้ำด้วยค่าจริง (ถ้าเซิร์ฟเวอร์ว่างจริง = ไม่มีคริสตัล)
            if (data.Value == null) return TransactionResult.Success(data);
            if (!TryRead(data.Value, out long balance) || balance < amount) return TransactionResult.Abort();
            after = balance - amount;
            data.Value = after;
            applied = true;
            return TransactionResult.Success(data);
        }, false).ContinueWithOnMainThread(task =>
        {
            bool ok = !task.IsFaulted && !task.IsCanceled && applied;
            if (ok) { Crystals = after; CrystalsKnown = true; }
            else RefreshCrystals();
            Changed?.Invoke();
            done?.Invoke(ok);
        });
    }

    // ===== เติมเงิน =====
    // ครั้งแรกของแพ็กนี้ได้โบนัส x2 ไหม
    public static bool FirstBonus(TopupPack pack) { EnsureLoaded(); return !pack.starter && !Data.firstBought.Contains(pack.id); }
    // คริสตัลที่จะได้ (รวมโบนัส)
    public static int CrystalsFor(TopupPack pack) => FirstBonus(pack) ? pack.crystals * 2 : pack.crystals;
    public static bool StarterAvailable { get { EnsureLoaded(); return !Data.starterBought; } }

    // ซื้อแพ็ก: ชำระผ่าน Payment แล้วให้คริสตัล/ของ + บันทึกประวัติ done(ผล, คริสตัลที่ได้)
    public static void Purchase(TopupPack pack, string method, Action<PaymentResult, int> done)
    {
        EnsureLoaded();
        if (pack.starter && Data.starterBought) { done?.Invoke(new PaymentResult { success = false, message = "Starter pack already purchased." }, 0); return; }
        int crystals = CrystalsFor(pack);
        Payment.Pay(pack, method, result =>
        {
            if (result == null || !result.success) { done?.Invoke(result ?? new PaymentResult { message = "Payment failed." }, 0); return; }
            AddCrystals(crystals, ok =>
            {
                if (!ok) { done?.Invoke(new PaymentResult { success = false, reference = result.reference, message = "Payment received but crystals could not be added. Ref " + result.reference }, 0); return; }
                EnsureLoaded();
                if (pack.starter)
                {
                    Data.starterBought = true;
                    Data.tickets += StarterTickets;
                    if (!Data.skins.Contains(StarterSkin)) Data.skins.Add(StarterSkin);
                    if (Fb != null && Fb.IsLoggedIn()) Fb.AddCoins(StarterCoins);
                }
                else if (!Data.firstBought.Contains(pack.id)) Data.firstBought.Add(pack.id);
                Data.history.Insert(0, new TopupRecord
                {
                    id = result.reference, pack = pack.id, thb = pack.thb, crystals = crystals, method = method,
                    at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
                Save();
                done?.Invoke(result, crystals);
            });
        });
    }

    // ยอดเติมเงินรวมทั้งหมด (บาท)
    public static int TotalSpentThb()
    {
        EnsureLoaded();
        int total = 0;
        foreach (var record in Data.history) total += record.thb;
        return total;
    }

    // ===== สกินที่มี =====
    public static bool OwnsSkin(string id) { EnsureLoaded(); return Data.skins.Contains(id); }
    public static void GrantSkin(string id) { EnsureLoaded(); if (!Data.skins.Contains(id)) Data.skins.Add(id); }
}
