// FirebaseManager.cs — ตัวกลางเชื่อม Firebase Auth + Realtime Database ของทั้งเกม
// เป็น Singleton (DontDestroyOnLoad) วางไว้ใน LoginScene แล้วอยู่ต่อข้ามทุก Scene
// LoginManager เรียกล็อกอิน, LobbyManager เรียกอ่าน/บันทึกเหรียญ ยาน สกิล สถิติ
// GameplayManager.Results เรียก RecordMatchResult/RecordDrawMatch ตอนจบแมตช์
// เหรียญใช้ RunTransaction เพื่อกันการเขียนทับกัน (อธิบายละเอียดที่ AddCoins/PurchaseShip)
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using Google;

// เชื่อม Firebase Auth/Realtime Database: บัญชี โปรไฟล์ เหรียญ คลังยาน/สกิล สถิติ และประวัติแมตช์
public class FirebaseManager : MonoBehaviour
{
    // ตัวแปร static ให้สคริปต์อื่นเรียกได้ทันทีผ่าน FirebaseManager.Instance
    public static FirebaseManager Instance;

    // ตัวแปรสำหรับใช้งาน Firebase
    private FirebaseAuth auth;
    private FirebaseUser user;
    private DatabaseReference dbReference;
    private bool firebaseReady = false;
    private string currentUsername = "Unknown";
    // Web Client ID ของ Google OAuth ใช้ขอ IdToken จาก Google Sign-In เพื่อนำไปยืนยันกับ Firebase
    private string webClientId = "371326537675-e1kev9fitqvsqomdlhdbgp07kd300nbk.apps.googleusercontent.com";

    // Unity เรียกตอนสร้าง Object: ตั้งตัวเองเป็น Singleton ตัวเดียว ถ้ามีอยู่แล้ว (เช่นกลับมา LoginScene) ให้ทำลายตัวซ้ำทิ้ง
    void Awake()
    {
        // ทำเป็น Singleton เพื่อไม่ให้ถูกทำลายเมื่อเปลี่ยน Scene
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeFirebase();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ตรวจและแก้ dependency ของ Firebase SDK แบบ async; ถ้าพร้อมจึงเริ่มบริการต่าง ๆ
    // ContinueWithOnMainThread ทำให้ callback กลับมารันบน Main Thread ของ Unity (แตะ GameObject ได้)
    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("ไม่สามารถตรวจสอบ Firebase dependencies ได้");
                return;
            }
            Firebase.DependencyStatus dependencyStatus = task.Result;
            if (dependencyStatus == Firebase.DependencyStatus.Available)
            {
                InitializeFirebaseServices();
            }
            else
            {
                Debug.LogError($"ไม่สามารถเชื่อมต่อ Firebase ได้: {dependencyStatus}");
            }
        });
    }

    // เก็บ reference ของ Auth และ Database root, ตั้ง firebaseReady = true และตั้งค่า Google Sign-In ให้ขอ IdToken
    private void InitializeFirebaseServices()
    {
        Debug.Log("Firebase พร้อมใช้งานแล้ว!");
        auth = FirebaseAuth.DefaultInstance;
        dbReference = FirebaseDatabase.DefaultInstance.RootReference;
        firebaseReady = true;

        GoogleSignIn.Configuration = new GoogleSignInConfiguration
        {
            RequestIdToken = true,
            WebClientId = webClientId
        };
    }

    // === ตรวจสอบสถานะ ===

    // LoginManager ใช้เช็คว่า Firebase พร้อมหรือยังก่อนเปิดปุ่มล็อกอิน
    public bool IsFirebaseReady()
    {
        return firebaseReady;
    }

    // true ถ้ามีผู้ใช้ล็อกอินอยู่
    public bool IsLoggedIn()
    {
        return user != null;
    }

    // ชื่อผู้เล่นปัจจุบัน (LobbyManager ใช้ตั้ง PhotonNetwork.NickName)
    public string GetUsername()
    {
        return currentUsername;
    }

    // UID ของ Firebase (ว่างถ้ายังไม่ล็อกอิน) ใช้เป็น key ของข้อมูลผู้เล่นใน Database และส่งต่อให้ Photon
    public string GetUserId()
    {
        return user != null ? user.UserId : "";
    }

    // === Logout ===
    // ออกจากระบบทั้ง Firebase และ Google เพื่อให้ครั้งหน้าเลือกบัญชีใหม่ได้
    public void Logout()
    {
        try
        {
            if (GoogleSignIn.Configuration != null) GoogleSignIn.DefaultInstance.SignOut();
        }
        catch (Exception e)
        {
            // ใน Unity Editor ปลั๊กอิน Google อาจใช้งานไม่ได้ ไม่เป็นไร ออกจาก Firebase ต่อได้
            Debug.LogWarning("Google sign-out skipped: " + e.Message);
        }
        if (auth != null) auth.SignOut();
        user = null;
        currentUsername = "Unknown";
        Debug.Log("Logged out.");
    }

    // คืน reference ราก (root) ของ Realtime Database ให้สคริปต์อื่นใช้
    public DatabaseReference GetDbReference()
    {
        return dbReference;
    }

    // === Login Google ===

    // ล็อกอินด้วย Google (เรียกจาก LoginManager.LoginGoogle) ขั้นตอน:
    // 1) Google Sign-In ให้ผู้ใช้เลือกบัญชี ได้ IdToken  2) แปลงเป็น Credential แล้ว SignInWithCredentialAsync กับ Firebase
    // 3) ตั้งชื่อผู้เล่นจาก DisplayName (ไม่มีก็สุ่ม Player_xxxx)  4) EnsureUserRecord สร้าง/เติมข้อมูลผู้เล่น แล้วเรียก onSuccess
    public void LoginGoogle(Action<string> onSuccess = null, Action<string> onFailed = null)
    {
        if (auth == null)
        {
            onFailed?.Invoke("Firebase ยังไม่พร้อมใช้งาน");
            return;
        }

        GoogleSignIn.DefaultInstance.SignIn().ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                onFailed?.Invoke("ผู้ใช้ยกเลิกการล็อกอิน");
                return;
            }
            if (task.IsFaulted)
            {
                onFailed?.Invoke("เกิดข้อผิดพลาดในการเรียก Google Login");
                return;
            }

            Credential credential = GoogleAuthProvider.GetCredential(task.Result.IdToken, null);
            auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(authTask =>
            {
                if (authTask.IsCanceled)
                {
                    onFailed?.Invoke("ยกเลิกการล็อกอินกับ Firebase");
                    return;
                }
                if (authTask.IsFaulted)
                {
                    onFailed?.Invoke("ยืนยันตัวตนกับ Firebase ล้มเหลว");
                    return;
                }

                user = auth.CurrentUser;
                currentUsername = user != null ? user.DisplayName : "Player_" + UnityEngine.Random.Range(1000, 9999);
                if (string.IsNullOrEmpty(currentUsername))
                    currentUsername = "Player_" + UnityEngine.Random.Range(1000, 9999);

                string uid = user != null ? user.UserId : "unknown_uid";
                Debug.Log($"Login Google สำเร็จ! UID: {uid}, Username: {currentUsername}");
                
                EnsureUserRecord(uid, currentUsername, onSuccess);
            });
        });
    }

    // === Login Guest (พร้อม Callback) ===

    // ล็อกอินแบบ Guest (Anonymous Auth) เรียกจาก LoginManager.LoginGuest
    // สำเร็จแล้วตั้งชื่อสุ่ม Guest_xxxx แล้วไป EnsureUserRecord เหมือนกรณี Google; ถ้าล้มเหลวแปลง error เป็นข้อความอ่านง่าย
    public void LoginGuest(Action<string> onSuccess = null, Action<string> onFailed = null)
    {
        if (auth == null)
        {
            onFailed?.Invoke("Firebase ยังไม่พร้อมใช้งาน");
            return;
        }

        auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                onFailed?.Invoke("การเข้าสู่ระบบถูกยกเลิก");
                return;
            }

            if (task.IsFaulted)
            {
                string errorMsg = "เกิดข้อผิดพลาดในการเข้าสู่ระบบ";
                if (task.Exception != null)
                {
                    foreach (var inner in task.Exception.InnerExceptions)
                    {
                        if (inner is FirebaseException firebaseEx)
                        {
                            errorMsg = GetFirebaseErrorMessage(firebaseEx);
                        }
                    }
                }
                onFailed?.Invoke(errorMsg);
                return;
            }

            // ล็อกอินสำเร็จ
            user = task.Result.User;
            currentUsername = "Guest_" + UnityEngine.Random.Range(1000, 9999);
            Debug.Log($"Login Guest สำเร็จ! UID: {user.UserId}, Username: {currentUsername}");
            EnsureUserRecord(user.UserId, currentUsername, onSuccess);
        });
    }

    // === บันทึกข้อมูลผู้เล่น ===

    // สร้างข้อมูลผู้เล่นใหม่ครั้งแรกที่ users/{uid}: เหรียญเริ่มต้น 5000, ยานเริ่มต้น index 0, สกิล 0, สถิติเป็น 0
    // แล้วสร้าง loadouts, user_spacecraft ของยานลำแรก และ catalog ของเกม
    private void SaveInitialUserData(string uid, string username)
    {
        string now = DateTime.UtcNow.ToString("o");
        var initialProfile = new Dictionary<string, object>
        {
            { "username", username },
            { "callsign", username },
            { "coin_balance", 5000L },
            { "high_score", 0 },
            { "last_login", now },
            { "unlocked_ships", "0" },
            { "selected_ship", 0 },
            { "selected_skill", 0 },
            { "total_wins", 0 },
            { "total_losses", 0 }
        };
        dbReference.Child("users").Child(uid).UpdateChildrenAsync(initialProfile);

        EnsureLoadoutRecord(uid, 0, 0);
        EnsureUserSpacecraftRecord(uid, 0, now, true);
        EnsureGameCatalog();
    }

    // อ่าน users/{uid} หลังล็อกอิน: ถ้ามีอยู่แล้วใช้ชื่อเดิมและเติม field ที่ขาด (ไม่เขียนทับ)
    // ถ้ายังไม่มีจึงสร้างใหม่ด้วย SaveInitialUserData; ถ้าอ่านไม่ได้จะไม่เขียนอะไรเลยเพื่อกันข้อมูลเดิมหาย
    private void EnsureUserRecord(string uid, string fallbackUsername, Action<string> onComplete)
    {
        dbReference.Child("users").Child(uid).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogWarning("ไม่สามารถอ่านข้อมูลผู้เล่นได้ จะไม่เขียนทับข้อมูลเดิม");
                onComplete?.Invoke(currentUsername);
                return;
            }

            if (task.Result.Exists)
            {
                string savedName = task.Result.Child("username").Value?.ToString();
                if (string.IsNullOrEmpty(savedName))
                    savedName = task.Result.Child("callsign").Value?.ToString();
                currentUsername = string.IsNullOrEmpty(savedName) ? fallbackUsername : savedName;
                BackfillExistingProfile(uid, task.Result, fallbackUsername);
            }
            else
            {
                currentUsername = fallbackUsername;
                SaveInitialUserData(uid, currentUsername);
            }

            EnsureGameCatalog();
            EnsureUserERRecords(uid);

            onComplete?.Invoke(currentUsername);
        });
    }

    // เติมเฉพาะ field ที่ยังไม่มีในโปรไฟล์ผู้เล่นเก่า (เช่นบัญชีที่สร้างก่อนมี field ใหม่) และอัปเดต last_login
    private void BackfillExistingProfile(string uid, DataSnapshot snapshot, string fallbackUsername)
    {
        var missing = new Dictionary<string, object>();
        AddMissing(missing, snapshot, "username", fallbackUsername);
        AddMissing(missing, snapshot, "callsign", fallbackUsername);
        AddMissing(missing, snapshot, "coin_balance", 5000L);
        AddMissing(missing, snapshot, "high_score", 0);
        AddMissing(missing, snapshot, "unlocked_ships", "0");
        AddMissing(missing, snapshot, "selected_ship", 0);
        AddMissing(missing, snapshot, "selected_skill", 0);
        AddMissing(missing, snapshot, "total_wins", 0);
        AddMissing(missing, snapshot, "total_losses", 0);
        missing["last_login"] = DateTime.UtcNow.ToString("o");
        dbReference.Child("users").Child(uid).UpdateChildrenAsync(missing);
    }

    // ใส่ key ลง dictionary เฉพาะเมื่อ snapshot ยังไม่มี key นั้น
    private static void AddMissing(Dictionary<string, object> fields, DataSnapshot snapshot, string key, object value)
    {
        if (!snapshot.HasChild(key)) fields[key] = value;
    }

    // ซ่อมข้อมูลตารางเสริม (loadouts และ user_spacecraft) ให้ตรงกับโปรไฟล์ใน users/{uid} ทุกครั้งที่ล็อกอิน
    private void EnsureUserERRecords(string uid)
    {
        dbReference.Child("users").Child(uid).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled || task.Result == null || !task.Result.Exists) return;
            DataSnapshot profile = task.Result;
            int shipIndex = ReadInt(profile.Child("selected_ship").Value, 0);
            int skillIndex = ReadInt(profile.Child("selected_skill").Value, 0);
            EnsureLoadoutRecord(uid, shipIndex, skillIndex);

            string unlocked = profile.Child("unlocked_ships").Value?.ToString() ?? "0";
            foreach (string part in unlocked.Split(','))
                if (int.TryParse(part, out int index) && index >= 0 && index < BattleLoadoutCatalog.Ships.Length)
                    EnsureUserSpacecraftRecord(uid, index, DateTime.UtcNow.ToString("o"), false);
        });
    }

    // แปลงค่าจาก Database (อาจเป็น long/string) เป็น int ถ้าแปลงไม่ได้ใช้ค่า fallback
    private static int ReadInt(object value, int fallback)
    {
        return int.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out int parsed)
            ? parsed : fallback;
    }

    // เขียน loadouts/{uid}: ยานและสกิลที่ใส่อยู่ (ใน Database เก็บเป็นเลขเริ่มที่ 1 คือ index + 1)
    private void EnsureLoadoutRecord(string uid, int shipIndex, int skillIndex)
    {
        shipIndex = BattleLoadoutCatalog.ValidShip(shipIndex);
        skillIndex = BattleLoadoutCatalog.ValidSkill(skillIndex);
        var loadout = new Dictionary<string, object>
        {
            { "loadout_id", uid },
            { "user_id", uid },
            { "spacecraft_model", shipIndex + 1 },
            { "skill_id", skillIndex + 1 }
        };
        dbReference.Child("loadouts").Child(uid).UpdateChildrenAsync(loadout);
    }

    // สร้างบันทึกการครอบครองยาน user_spacecraft/{uid}/{เลขยาน}
    // overwrite = true เขียนทับเลย, false จะเขียนเฉพาะเมื่อยังไม่มีบันทึก (ไม่ทับ buy_date เดิม)
    private void EnsureUserSpacecraftRecord(string uid, int shipIndex, string acquiredAt, bool overwrite)
    {
        if (shipIndex < 0 || shipIndex >= BattleLoadoutCatalog.Ships.Length) return;
        string spacecraftId = (shipIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        DatabaseReference record = dbReference.Child("user_spacecraft").Child(uid).Child(spacecraftId);
        if (overwrite)
        {
            WriteUserSpacecraftRecord(record, uid, shipIndex, acquiredAt);
            return;
        }
        record.GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled || (task.Result != null && task.Result.Exists)) return;
            WriteUserSpacecraftRecord(record, uid, shipIndex, acquiredAt);
        });
    }

    // เขียนข้อมูลยานที่ครอบครอง (เจ้าของ, รุ่นยาน, วันที่ได้มา)
    private static void WriteUserSpacecraftRecord(DatabaseReference record, string uid, int shipIndex, string acquiredAt)
    {
        record.SetValueAsync(new Dictionary<string, object>
        {
            { "user_id", uid },
            { "spacecraft_model", shipIndex + 1 },
            { "buy_date", acquiredAt }
        });
    }

    // Catalog entries are seeded once from the same values used by gameplay. Keep these paths
    // read-only for normal clients in Firebase Database Rules after the initial seed.
    // สร้าง catalog ยาน (spacecraft) และสกิล (skills) ใน Database ถ้ายังไม่มี
    private void EnsureGameCatalog()
    {
        LoadFeatureFlags();
        // เฟส 9: เติมทีละรายการ (ยาน/สกิลใหม่เฟส 7 จะถูกเพิ่มให้แอดมินเห็นใน Console ด้วย) ไม่เขียนทับค่าที่มีอยู่
        EnsureCatalogEntries(dbReference.Child("spacecraft"), BuildSpacecraftCatalog());
        EnsureCatalogEntries(dbReference.Child("skills"), BuildSkillCatalog());
    }

    private static void EnsureCatalogEntries(DatabaseReference reference, Dictionary<string, object> entries)
    {
        foreach (var entry in entries) EnsureCatalogBranch(reference.Child(entry.Key), entry.Value as Dictionary<string, object>);
    }

    // เฟส 9: อ่านค่ายาน/สกิลที่แอดมินแก้ใน Firebase มาใช้ในเกม (RemoteCatalog.cs) — ทำหลังโหลดสวิตช์ฟีเจอร์แล้ว
    private void LoadRemoteCatalog()
    {
        if (dbReference == null || !FeatureFlags.RemoteCatalog) return;
        dbReference.Child("spacecraft").GetValueAsync().ContinueWithOnMainThread(ships =>
        {
            if (ships.IsFaulted || ships.IsCanceled || ships.Result == null) return;
            object shipValue = ships.Result.Value;
            dbReference.Child("skills").GetValueAsync().ContinueWithOnMainThread(skills =>
            {
                object skillValue = skills.IsFaulted || skills.IsCanceled || skills.Result == null ? null : skills.Result.Value;
                RemoteCatalog.Apply(shipValue, skillValue);
            });
        });
    }

    // โหลดสวิตช์ฟีเจอร์จาก feature_flags/{ชื่อ} = true/false (แอดมินแก้ใน Firebase Console ได้)
    // ไม่มีข้อมูลหรือโหลดไม่สำเร็จ = ใช้ค่าเริ่มต้นใน FeatureFlags.cs
    private void LoadFeatureFlags()
    {
        if (dbReference == null) return;
        dbReference.Child("feature_flags").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (!task.IsFaulted && !task.IsCanceled && task.Result != null && task.Result.Exists
                && task.Result.Value is IDictionary<string, object> values) FeatureFlags.Apply(values);
            LoadRemoteCatalog();
        });
    }

    // สร้างข้อมูล catalog ยานจาก BattleLoadoutCatalog.Ships (HP, ความเร็ว, อัตรายิง, ดาเมจ, ราคา)
    private static Dictionary<string, object> BuildSpacecraftCatalog()
    {
        var catalog = new Dictionary<string, object>();
        for (int i = 0; i < BattleLoadoutCatalog.Ships.Length; i++)
        {
            ShipData ship = BattleLoadoutCatalog.Ships[i];
            catalog[(i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)] = new Dictionary<string, object>
            {
                { "spacecraft_model", i + 1 },
                { "spacecraft_name", ship.name },
                { "base_hp", ship.hp },
                { "speed", ship.spd },
                { "fire_rate", ship.shotInterval },
                { "bullet_damage", ship.atk },
                { "spacecraft_price", ship.price }
            };
        }
        return catalog;
    }

    // สร้างข้อมูล catalog สกิลจาก BattleLoadoutCatalog.Skills และค่าใน BattleBalance (ดาเมจ, cooldown, ระยะเวลา)
    private static Dictionary<string, object> BuildSkillCatalog()
    {
        var catalog = new Dictionary<string, object>();
        // เฟส 9 แก้บั๊ก: สกิลมี 7 แบบแล้ว (เดิมอาร์เรย์มี 4 ช่อง ทำให้ index เกินตอนล็อกอิน)
        int[] damage = { (int)BattleBalance.StunDamage, 0, (int)BattleBalance.NovaDamage, (int)BattleBalance.SeekerDamage, 0, 0, 0 };
        float[] duration = { BattleBalance.StunSeconds, BattleBalance.ShieldSeconds, 1.5f, 0f, 0f, 0f, 3f };
        for (int i = 0; i < BattleLoadoutCatalog.Skills.Length; i++)
        {
            SkillData skill = BattleLoadoutCatalog.Skills[i];
            catalog[(i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)] = new Dictionary<string, object>
            {
                { "skill_id", i + 1 },
                { "skill_name", skill.name },
                { "damage", i < damage.Length ? damage[i] : 0 },
                { "cooldown", skill.cooldown },
                { "duration", i < duration.Length ? duration[i] : 0f },
                { "skill_price", 0 }
            };
        }
        return catalog;
    }

    // ใช้ Transaction เขียน catalog เฉพาะเมื่อ path ยังว่าง (data.Value == null) ถ้ามีอยู่แล้วคืนค่าเดิมไม่แก้
    // Transaction ทำให้ถึงผู้เล่นหลายคนล็อกอินพร้อมกันก็ไม่เขียนทับกัน
    private static void EnsureCatalogBranch(DatabaseReference reference, Dictionary<string, object> initialValue)
    {
        reference.RunTransaction(data =>
        {
            if (data.Value == null) data.Value = initialValue;
            return TransactionResult.Success(data);
        }, false).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled) Debug.LogWarning("Firebase game catalog could not be initialized: " + reference.Key);
        });
    }

    // === อ่านข้อมูลเหรียญ ===

    // อ่านค่าเหรียญจาก Database เป็น long; คืน false ถ้าไม่ใช่ตัวเลขหรือติดลบ (ข้อมูลเสีย)
    private static bool TryReadCoins(object value, out long coins)
    {
        return long.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out coins)
            && coins >= 0;
    }

    // อ่านยอดเหรียญปัจจุบัน (LobbyManager เรียกตอนโหลดโปรไฟล์) แบบอ่านอย่างเดียว
    // ถ้าระหว่างรอผลผู้ใช้เปลี่ยนบัญชี หรืออ่านไม่สำเร็จ จะเรียก onError และไม่แก้ยอดเหรียญ
    public void GetCoinBalance(Action<long> onResult, Action<string> onError = null)
    {
        if (user == null || dbReference == null)
        {
            onError?.Invoke("Account is not ready.");
            return;
        }
        string uid = user.UserId;
        dbReference.Child("users").Child(uid).Child("coin_balance")
            .GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (user == null || user.UserId != uid) { onError?.Invoke("Account changed."); return; }
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning("Coin balance read failed; existing balance was not changed.");
                    onError?.Invoke("Could not load coins. Check connection.");
                    return;
                }
                if (!task.Result.Exists || !TryReadCoins(task.Result.Value, out long coins))
                {
                    onError?.Invoke("Coin balance is missing or invalid.");
                    return;
                }
                onResult?.Invoke(coins);
            });
    }

    // Explicit absolute writes only; gameplay rewards and purchases use transactions below.
    // เขียนยอดเหรียญแบบกำหนดค่าตรง ๆ (SetValueAsync) ไม่ได้ใช้ Transaction
    // จึงไม่ควรใช้กับการบวก/หักเหรียญ เพราะอาจทับค่าที่เพิ่งเปลี่ยนจากที่อื่น
    public void UpdateCoinBalance(long newBalance, Action<bool> onResult = null)
    {
        if (newBalance < 0 || user == null || dbReference == null) { onResult?.Invoke(false); return; }
        dbReference.Child("users").Child(user.UserId).Child("coin_balance")
            .SetValueAsync(newBalance).ContinueWithOnMainThread(task =>
                onResult?.Invoke(!task.IsFaulted && !task.IsCanceled));
    }

    // เพิ่มเหรียญ (เช่นรางวัลจบแมตช์จาก RecordMatchResult) ด้วย RunTransaction
    // ทำไมต้องใช้ Transaction: ถ้าใช้ "อ่านค่า -> บวก -> เขียนกลับ" ธรรมดา เมื่อมีการเปลี่ยนเหรียญสองครั้งพร้อมกัน
    // (เช่นรับรางวัลขณะซื้อยาน หรือเล่นสองเครื่อง) ค่าหนึ่งจะเขียนทับอีกค่าแล้วเหรียญหาย/เพิ่มผิด (lost update)
    // Transaction จะส่งค่าปัจจุบันจริงให้ฟังก์ชันคำนวณ ถ้าบน server ค่าเปลี่ยนไประหว่างนั้น Firebase จะเรียกฟังก์ชันใหม่ด้วยค่าล่าสุดเอง
    // ฟังก์ชันอาจถูกเรียกหลายรอบ จึงรีเซ็ต applied ทุกรอบ; ค่าไม่ถูกต้องหรือบวกแล้วล้น long จะ Abort (ไม่เขียนอะไร)
    public void AddCoins(int amount, Action<bool> onResult = null)
    {
        if (amount < 0 || user == null || dbReference == null) { onResult?.Invoke(false); return; }
        bool applied = false;
        var reference = dbReference.Child("users").Child(user.UserId).Child("coin_balance");
        reference.RunTransaction(data =>
        {
            applied = false;
            // Firebase may first call with an empty local cache; let the server retry with its actual value.
            if (data.Value == null) return TransactionResult.Success(data);
            if (!TryReadCoins(data.Value, out long balance) || balance > long.MaxValue - amount)
                return TransactionResult.Abort();
            data.Value = balance + amount;
            applied = true;
            return TransactionResult.Success(data);
        }, false).ContinueWithOnMainThread(task =>
            onResult?.Invoke(!task.IsFaulted && !task.IsCanceled && applied));
    }

    // ซื้อยาน (เรียกจาก LobbyManager.OnInventoryActionClicked) ทำใน Transaction เดียวบน users/{uid}
    // เพื่อให้ "เช็คเหรียญพอ + หักเหรียญ + เพิ่มยานใน unlocked_ships" เกิดพร้อมกันแบบ atomic
    // กันกดซื้อซ้ำ/ซื้อจากสองเครื่องพร้อมกันแล้วหักเงินซ้ำ หรือเหรียญติดลบ; ถ้ามียานอยู่แล้วจะไม่หักเงิน
    // ขั้นตอนใน Transaction: อ่านเหรียญและรายการยาน -> ข้อมูลเสียให้ Abort -> ยังไม่มียานและเงินพอจึงหักเงินและเพิ่มยาน
    // หลัง Transaction สำเร็จ ถ้าเป็นยานใหม่จะเขียน user_spacecraft เพิ่ม แล้วเรียก onResult(สำเร็จ, ยอดเหรียญใหม่)
    // completed/purchasedNewShip ถูกรีเซ็ตทุกรอบเพราะ Firebase อาจเรียกฟังก์ชันซ้ำหลายรอบ
    public void PurchaseShip(int shipIndex, int price, Action<bool, long> onResult)
    {
        if (user == null || dbReference == null || price < 0 || shipIndex < 0)
        { onResult?.Invoke(false, 0); return; }
        string uid = user.UserId;
        bool completed = false;
        bool purchasedNewShip = false;
        dbReference.Child("users").Child(uid).RunTransaction(data =>
        {
            completed = false;
            purchasedNewShip = false;
            if (data.Value == null) return TransactionResult.Success(data);
            if (!TryReadCoins(data.Child("coin_balance").Value, out long balance)) return TransactionResult.Abort();
            string stored = data.Child("unlocked_ships").Value as string;
            if (stored == null) return TransactionResult.Abort();
            var owned = new HashSet<int>();
            foreach (string part in stored.Split(','))
            {
                if (!int.TryParse(part, out int index)) return TransactionResult.Abort();
                owned.Add(index);
            }
            if (!owned.Contains(shipIndex))
            {
                if (balance < price) return TransactionResult.Abort();
                owned.Add(shipIndex);
                var ordered = new List<int>(owned);
                ordered.Sort();
                data.Child("coin_balance").Value = balance - price;
                data.Child("unlocked_ships").Value = string.Join(",", ordered);
                purchasedNewShip = true;
            }
            completed = true;
            return TransactionResult.Success(data);
        }, false).ContinueWithOnMainThread(task =>
        {
            bool success = !task.IsFaulted && !task.IsCanceled && completed && user != null && user.UserId == uid;
            long balance = 0;
            if (success) success = task.Result != null && TryReadCoins(task.Result.Child("coin_balance").Value, out balance);
            if (!success || !purchasedNewShip)
            {
                onResult?.Invoke(success, balance);
                return;
            }

            int purchasedIndex = shipIndex;
            string spacecraftId = (purchasedIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            DatabaseReference shipRecord = dbReference.Child("user_spacecraft").Child(uid).Child(spacecraftId);
            shipRecord.SetValueAsync(new Dictionary<string, object>
            {
                { "user_id", uid },
                { "spacecraft_model", purchasedIndex + 1 },
                { "buy_date", DateTime.UtcNow.ToString("o") }
            }).ContinueWithOnMainThread(recordTask =>
                {
                    if (recordTask.IsFaulted || recordTask.IsCanceled)
                        Debug.LogWarning("Ship is unlocked, but its ownership record will be repaired on next login.");
                    onResult?.Invoke(true, balance);
                });
        });
    }

    // เพิ่มจำนวนชนะใน users/{uid}/wins แบบอ่านแล้วเขียน (ไม่ใช่ Transaction)
    // หมายเหตุ: ระบบสถิติปัจจุบันใช้ total_wins ผ่าน IncrementUserCounter แทน
    public void AddWin(Action<bool> onResult = null)
    {
        if (user == null || dbReference == null)
        {
            onResult?.Invoke(false);
            return;
        }

        dbReference.Child("users").Child(user.UserId).Child("wins")
            .GetValueAsync().ContinueWithOnMainThread(task =>
            {
                int currentWins = 0;
                if (!task.IsFaulted && !task.IsCanceled && task.Result.Exists)
                {
                    int.TryParse(task.Result.Value.ToString(), out currentWins);
                }

                dbReference.Child("users").Child(user.UserId).Child("wins")
                    .SetValueAsync(currentWins + 1).ContinueWithOnMainThread(updateTask =>
                    {
                        onResult?.Invoke(updateTask.IsCompleted && !updateTask.IsFaulted);
                    });
            });
    }

    // === ระบบคลังยาน (Inventory) ===

    // อ่านรายการยานที่ปลดล็อกแล้ว (เก็บเป็น string คั่นด้วยจุลภาค เช่น "0,2") แปลงเป็น List<int>
    // ถ้าอ่านไม่ได้หรือไม่ได้ล็อกอินคืน [0] คือมีแค่ยานเริ่มต้น
    public void GetUnlockedShips(Action<List<int>> onResult)
    {
        if (user == null || dbReference == null)
        {
            onResult?.Invoke(new List<int> { 0 });
            return;
        }

        dbReference.Child("users").Child(user.UserId).Child("unlocked_ships")
            .GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (!task.IsFaulted && !task.IsCanceled && task.Result.Exists)
                {
                    string data = task.Result.Value.ToString();
                    List<int> unlocked = new List<int>();
                    foreach (string s in data.Split(','))
                    {
                        if (int.TryParse(s, out int idx)) unlocked.Add(idx);
                    }
                    onResult?.Invoke(unlocked);
                }
                else
                {
                    onResult?.Invoke(new List<int> { 0 });
                }
            });
    }

    // เพิ่มยานเข้า unlocked_ships แบบอ่านแล้วเขียนทับ ไม่หักเหรียญ (การซื้อจริงใช้ PurchaseShip)
    public void UnlockShip(int shipIndex, Action<bool> onResult = null)
    {
        GetUnlockedShips(unlocked =>
        {
            if (!unlocked.Contains(shipIndex))
            {
                unlocked.Add(shipIndex);
            }
            string newData = string.Join(",", unlocked);
            
            dbReference.Child("users").Child(user.UserId).Child("unlocked_ships")
                .SetValueAsync(newData).ContinueWithOnMainThread(task =>
                {
                    onResult?.Invoke(task.IsCompleted && !task.IsFaulted);
                });
        });
    }

    // อ่าน index ยานที่ผู้เล่นใส่อยู่ (selected_ship) ไม่มีข้อมูลคืน 0
    public void GetSelectedShip(Action<int> onResult)
    {
        if (user == null || dbReference == null)
        {
            onResult?.Invoke(0);
            return;
        }

        dbReference.Child("users").Child(user.UserId).Child("selected_ship")
            .GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (!task.IsFaulted && !task.IsCanceled && task.Result.Exists)
                {
                    int.TryParse(task.Result.Value.ToString(), out int shipIndex);
                    onResult?.Invoke(shipIndex);
                }
                else
                {
                    onResult?.Invoke(0);
                }
            });
    }

    // บันทึกยานที่เลือกลง users/{uid}/selected_ship และ loadouts/{uid} (เรียกเมื่อกด Equip ในคลังยาน)
    public void SaveSelectedShip(int shipIndex)
    {
        if (user == null || dbReference == null || shipIndex < 0 || shipIndex >= BattleLoadoutCatalog.Ships.Length) return;
        string uid = user.UserId;
        dbReference.Child("users").Child(uid).Child("selected_ship").SetValueAsync(shipIndex);
        dbReference.Child("loadouts").Child(uid).UpdateChildrenAsync(new Dictionary<string, object>
        {
            { "loadout_id", uid }, { "user_id", uid }, { "spacecraft_model", shipIndex + 1 }
        });
    }

    // อ่าน index สกิลที่ผู้เล่นใส่อยู่ (selected_skill) ไม่มีข้อมูลคืน 0
    public void GetSelectedSkill(Action<int> onResult)
    {
        if (user == null || dbReference == null)
        {
            onResult?.Invoke(0);
            return;
        }

        dbReference.Child("users").Child(user.UserId).Child("selected_skill")
            .GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (!task.IsFaulted && !task.IsCanceled && task.Result.Exists)
                {
                    int.TryParse(task.Result.Value.ToString(), out int skillIndex);
                    onResult?.Invoke(skillIndex);
                }
                else
                {
                    onResult?.Invoke(0);
                }
            });
    }

    // บันทึกสกิลที่เลือกลง users/{uid}/selected_skill และ loadouts/{uid} (เรียกเมื่อกด Install)
    public void SaveSelectedSkill(int skillIndex)
    {
        if (user == null || dbReference == null || skillIndex < 0 || skillIndex >= BattleLoadoutCatalog.Skills.Length) return;
        string uid = user.UserId;
        dbReference.Child("users").Child(uid).Child("selected_skill").SetValueAsync(skillIndex);
        dbReference.Child("loadouts").Child(uid).UpdateChildrenAsync(new Dictionary<string, object>
        {
            { "loadout_id", uid }, { "user_id", uid }, { "skill_id", skillIndex + 1 }
        });
    }

    // อ่านคะแนนสูงสุด จำนวนชนะ และแพ้ ให้หน้าล็อบบี้แสดง; ผิดพลาดหรือเปลี่ยนบัญชีระหว่างรอคืน 0 ทั้งหมด
    public void GetPlayerStats(Action<int, int, int> onResult)
    {
        if (user == null || dbReference == null) { onResult?.Invoke(0, 0, 0); return; }
        string uid = user.UserId;
        dbReference.Child("users").Child(uid).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (user == null || user.UserId != uid || task.IsFaulted || task.IsCanceled || task.Result == null)
            {
                onResult?.Invoke(0, 0, 0);
                return;
            }
            DataSnapshot profile = task.Result;
            onResult?.Invoke(ReadInt(profile.Child("high_score").Value, 0),
                ReadInt(profile.Child("total_wins").Value, 0),
                ReadInt(profile.Child("total_losses").Value, 0));
        });
    }

    // อัปเดตคะแนนสูงสุดด้วย Transaction: เขียนเฉพาะเมื่อคะแนนใหม่มากกว่าค่าบน server ตอนนั้นจริง ๆ
    private void UpdateHighScore(int score)
    {
        if (score < 0 || user == null || dbReference == null) return;
        DatabaseReference reference = dbReference.Child("users").Child(user.UserId).Child("high_score");
        reference.RunTransaction(data =>
        {
            int current = ReadInt(data.Value, 0);
            if (score > current) data.Value = score;
            return TransactionResult.Success(data);
        }, false).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled) Debug.LogWarning("High score could not be saved.");
        });
    }

    // === อัปเดตชื่อผู้เล่น ===

    // เปลี่ยนชื่อผู้เล่น (username และ callsign) ในเครื่องและใน Database
    public void UpdateUsername(string newName, Action<bool> onResult = null)
    {
        if (user == null || dbReference == null)
        {
            onResult?.Invoke(false);
            return;
        }

        currentUsername = newName;
        dbReference.Child("users").Child(user.UserId).UpdateChildrenAsync(new Dictionary<string, object>
            { { "username", newName }, { "callsign", newName } }).ContinueWithOnMainThread(task =>
            {
                onResult?.Invoke(task.IsCompleted && !task.IsFaulted);
            });
    }

    // === แปลง Error ให้อ่านง่าย ===

    // แปลงรหัส error ของ Firebase เป็นข้อความภาษาไทยให้ผู้เล่นอ่านเข้าใจ
    private string GetFirebaseErrorMessage(FirebaseException ex)
    {
        switch (ex.ErrorCode)
        {
            case 17020: return "ไม่มีการเชื่อมต่ออินเทอร์เน็ต";
            case 17999: return "เกิดข้อผิดพลาดภายใน กรุณาลองใหม่";
            default: return $"เกิดข้อผิดพลาด (รหัส: {ex.ErrorCode})";
        }
    }

    // === สถิติและประวัติการแข่งขัน ===
    // บวก 1 ให้ตัวนับสถิติ (total_wins หรือ total_losses) ด้วย Transaction กันการนับหายเมื่อเขียนพร้อมกัน
    private void IncrementUserCounter(string field)
    {
        if (user == null || dbReference == null) return;
        string uid = user.UserId;
        dbReference.Child("users").Child(uid).Child(field).RunTransaction(data =>
        {
            int current = ReadInt(data.Value, 0);
            data.Value = current + 1;
            return TransactionResult.Success(data);
        }, false).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled) Debug.LogWarning("Could not update player statistic: " + field);
        });
    }

    // บันทึกผลแมตช์ของผู้เล่นเครื่องนี้ (GameplayManager.Results เรียกทุกเครื่องตอนจบเกม: ชนะได้ 190 แพ้ได้ 10 เหรียญ)
    // ขั้นตอน: เพิ่มตัวนับชนะ/แพ้ -> อัปเดตคะแนนสูงสุด -> เพิ่มเหรียญด้วย AddCoins (Transaction)
    // แล้วเฉพาะ Master Client เท่านั้นที่เขียน match_history เพื่อไม่ให้แมตช์เดียวถูกบันทึกซ้ำสองแถว
    public void RecordMatchResult(bool isWinner, string opponentUid,
        int rewardCoins, int score, string mapName, Action<bool> onComplete = null)
    {
        if (user == null || dbReference == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        string uid = user.UserId;
        IncrementUserCounter(isWinner ? "total_wins" : "total_losses");
        UpdateHighScore(score);
        AddCoins(rewardCoins, success =>
        {
            if (!success) Debug.LogWarning("Match reward could not be saved. Coin balance was not overwritten.");
            if (!Photon.Pun.PhotonNetwork.IsMasterClient)
            {
                onComplete?.Invoke(success);
                return;
            }
            WriteMatchRecord(uid, opponentUid, isWinner ? "Win_A" : "Win_B", rewardCoins,
                isWinner ? 10 : 190, mapName, saved => onComplete?.Invoke(success && saved));
        });
    }

    // ===== ความก้าวหน้าของผู้เล่น (เฟส 4: เลเวล ภารกิจ Achievement ประวัติแมตช์) =====
    // เก็บเป็น JSON ก้อนเดียวที่ users/{uid}/progress_json (โครงสร้างดูที่ Progression.cs) อ่าน/เขียนครั้งเดียวต่อการเปลี่ยนแปลง
    // และเก็บ level / xp แยกไว้ที่ users/{uid} ด้วย (เผื่อทำตารางอันดับเลเวลภายหลัง)
    public void LoadProgressJson(Action<string> onResult)
    {
        if (user == null || dbReference == null) { onResult?.Invoke(null); return; }
        dbReference.Child("users").Child(user.UserId).Child("progress_json").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled || task.Result == null || !task.Result.Exists) { onResult?.Invoke(null); return; }
            onResult?.Invoke(task.Result.Value as string);
        });
    }

    public void SaveProgressJson(string json, int level, int xp)
    {
        if (user == null || dbReference == null || string.IsNullOrEmpty(json)) return;
        dbReference.Child("users").Child(user.UserId).UpdateChildrenAsync(new Dictionary<string, object>
        {
            { "progress_json", json },
            { "level", level },
            { "xp", xp }
        });
    }

    // ===== ข้อมูล JSON ทั่วไปของผู้เล่น (เฟส 5: economy_json = ตีบวก/ไอเท็ม) =====
    public void LoadUserJson(string key, Action<string> onResult)
    {
        if (user == null || dbReference == null || string.IsNullOrEmpty(key)) { onResult?.Invoke(null); return; }
        dbReference.Child("users").Child(user.UserId).Child(key).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled || task.Result == null || !task.Result.Exists) { onResult?.Invoke(null); return; }
            onResult?.Invoke(task.Result.Value as string);
        });
    }

    public void SaveUserJson(string key, string json)
    {
        if (user == null || dbReference == null || string.IsNullOrEmpty(key) || json == null) return;
        dbReference.Child("users").Child(user.UserId).Child(key).SetValueAsync(json);
    }

    // หักเหรียญแบบ Transaction (เงินไม่พอ = ไม่หักและคืน false) — ใช้กับตีบวก / ร้านค้า / กล่องสุ่ม
    // onResult(สำเร็จ, ยอดเหรียญหลังหัก)
    public void SpendCoins(int amount, Action<bool, long> onResult)
    {
        if (amount < 0 || user == null || dbReference == null) { onResult?.Invoke(false, 0); return; }
        bool applied = false;
        long after = 0;
        var reference = dbReference.Child("users").Child(user.UserId).Child("coin_balance");
        reference.RunTransaction(data =>
        {
            applied = false;
            if (data.Value == null) return TransactionResult.Success(data);
            if (!TryReadCoins(data.Value, out long balance) || balance < amount) return TransactionResult.Abort();
            after = balance - amount;
            data.Value = after;
            applied = true;
            return TransactionResult.Success(data);
        }, false).ContinueWithOnMainThread(task =>
            onResult?.Invoke(!task.IsFaulted && !task.IsCanceled && applied, after));
    }

    // ===== แรงค์ (เฟส 6) =====
    // mmr เก็บเป็นตัวเลขแยก (ใช้เรียงตารางอันดับ) + rank_json รายละเอียดทั้งหมด
    public void SaveRank(int mmr, string json)
    {
        if (user == null || dbReference == null) return;
        dbReference.Child("users").Child(user.UserId).UpdateChildrenAsync(new Dictionary<string, object>
        {
            { "mmr", mmr },
            { "rank_json", json }
        });
    }

    // ตารางอันดับ: ผู้เล่นที่ mmr สูงสุด count คน (เรียงมาก -> น้อย)
    // แนะนำเพิ่มใน Firebase Rules: "users": { ".indexOn": ["mmr"] } เพื่อให้ query เร็ว (ไม่ใส่ก็ทำงานได้)
    public void GetLeaderboard(int count, Action<List<LeaderboardEntry>> onResult)
    {
        if (dbReference == null) { onResult?.Invoke(new List<LeaderboardEntry>()); return; }
        dbReference.Child("users").OrderByChild("mmr").LimitToLast(count).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            var list = new List<LeaderboardEntry>();
            if (!task.IsFaulted && !task.IsCanceled && task.Result != null)
                foreach (var child in task.Result.Children)
                {
                    if (child.Child("mmr").Value == null) continue;
                    list.Add(new LeaderboardEntry
                    {
                        uid = child.Key,
                        name = child.Child("username").Value?.ToString() ?? "PILOT",
                        mmr = ReadInt(child.Child("mmr").Value, 0),
                        level = ReadInt(child.Child("level").Value, 1)
                    });
                }
            list.Sort((a, b) => b.mmr.CompareTo(a.mmr));
            onResult?.Invoke(list);
        });
    }

    // บันทึกกรณีเสมอ: อัปเดตคะแนนสูงสุด และ Master Client เขียนประวัติผล "Draw" (ไม่มีเหรียญรางวัล)
    public void RecordDrawMatch(string opponentUid, int score, string mapName)
    {
        if (user == null || dbReference == null || !Photon.Pun.PhotonNetwork.IsMasterClient) return;
        UpdateHighScore(score);
        WriteMatchRecord(user.UserId, opponentUid, "Draw", 0, 0, mapName, null);
    }

    // เขียนประวัติแมตช์หนึ่งแถวลง match_history/{matchId} (Push() สร้าง key ไม่ซ้ำ)
    // user_a คือผู้เล่นเครื่อง Master, ผลเป็น Win_A/Win_B/Draw พร้อมรางวัลแต่ละฝั่ง รหัสห้อง ชื่อแม็พ และเวลาแข่ง
    private void WriteMatchRecord(string userA, string userB, string result,
        int rewardA, int rewardB, string mapName, Action<bool> onComplete)
    {
        string matchId = dbReference.Child("match_history").Push().Key;
        string currentRoom = Photon.Pun.PhotonNetwork.CurrentRoom != null
            ? Photon.Pun.PhotonNetwork.CurrentRoom.Name : "QuickMatch";
        var matchData = new Dictionary<string, object>
        {
            { "match_id", matchId },
            { "user_a", userA },
            { "user_b", string.IsNullOrWhiteSpace(userB) ? "" : userB },
            { "status", "Completed" },
            { "Result", result },
            { "reward_a", rewardA },
            { "reward_b", rewardB },
            { "room_code", currentRoom },
            { "map_name", mapName },
            { "play_date", DateTime.UtcNow.ToString("o") }
        };
        dbReference.Child("match_history").Child(matchId).SetValueAsync(matchData).ContinueWithOnMainThread(task =>
        {
            bool success = !task.IsFaulted && !task.IsCanceled;
            if (!success) Debug.LogWarning("Match history could not be saved.");
            onComplete?.Invoke(success);
        });
    }

}

// แถวหนึ่งในตารางอันดับแรงค์ (เฟส 6)
public class LeaderboardEntry
{
    public string uid;
    public string name;
    public int mmr;
    public int level;
}
