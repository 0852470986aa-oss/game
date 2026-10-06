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
    public static FirebaseManager Instance;

    // ตัวแปรสำหรับใช้งาน Firebase
    private FirebaseAuth auth;
    private FirebaseUser user;
    private DatabaseReference dbReference;
    private bool firebaseReady = false;
    private string currentUsername = "Unknown";
    private string webClientId = "371326537675-e1kev9fitqvsqomdlhdbgp07kd300nbk.apps.googleusercontent.com";

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

    public bool IsFirebaseReady()
    {
        return firebaseReady;
    }

    public bool IsLoggedIn()
    {
        return user != null;
    }

    public string GetUsername()
    {
        return currentUsername;
    }

    public string GetUserId()
    {
        return user != null ? user.UserId : "";
    }

    public DatabaseReference GetDbReference()
    {
        return dbReference;
    }

    // === Login Google ===

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

    private static void AddMissing(Dictionary<string, object> fields, DataSnapshot snapshot, string key, object value)
    {
        if (!snapshot.HasChild(key)) fields[key] = value;
    }

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

    private static int ReadInt(object value, int fallback)
    {
        return int.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out int parsed)
            ? parsed : fallback;
    }

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
    private void EnsureGameCatalog()
    {
        EnsureCatalogBranch(dbReference.Child("spacecraft"), BuildSpacecraftCatalog());
        EnsureCatalogBranch(dbReference.Child("skills"), BuildSkillCatalog());
    }

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

    private static Dictionary<string, object> BuildSkillCatalog()
    {
        var catalog = new Dictionary<string, object>();
        int[] damage = { (int)BattleBalance.StunDamage, 0, (int)BattleBalance.NovaDamage, (int)BattleBalance.SeekerDamage };
        float[] duration = { BattleBalance.StunSeconds, BattleBalance.ShieldSeconds, 1.5f, 0f };
        for (int i = 0; i < BattleLoadoutCatalog.Skills.Length; i++)
        {
            SkillData skill = BattleLoadoutCatalog.Skills[i];
            catalog[(i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)] = new Dictionary<string, object>
            {
                { "skill_id", i + 1 },
                { "skill_name", skill.name },
                { "damage", damage[i] },
                { "cooldown", skill.cooldown },
                { "duration", duration[i] },
                { "skill_price", 0 }
            };
        }
        return catalog;
    }

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

    private static bool TryReadCoins(object value, out long coins)
    {
        return long.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out coins)
            && coins >= 0;
    }

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
    public void UpdateCoinBalance(long newBalance, Action<bool> onResult = null)
    {
        if (newBalance < 0 || user == null || dbReference == null) { onResult?.Invoke(false); return; }
        dbReference.Child("users").Child(user.UserId).Child("coin_balance")
            .SetValueAsync(newBalance).ContinueWithOnMainThread(task =>
                onResult?.Invoke(!task.IsFaulted && !task.IsCanceled));
    }

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

    public void RecordDrawMatch(string opponentUid, int score, string mapName)
    {
        if (user == null || dbReference == null || !Photon.Pun.PhotonNetwork.IsMasterClient) return;
        UpdateHighScore(score);
        WriteMatchRecord(user.UserId, opponentUid, "Draw", 0, 0, mapName, null);
    }

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
