using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using Google;

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
        dbReference.Child("users").Child(uid).Child("username").SetValueAsync(username);
        dbReference.Child("users").Child(uid).Child("callsign").SetValueAsync(username); // ตาม ERD
        dbReference.Child("users").Child(uid).Child("coin_balance").SetValueAsync(5000); 
        dbReference.Child("users").Child(uid).Child("high_score").SetValueAsync(0); // ตาม ERD
        dbReference.Child("users").Child(uid).Child("last_login").SetValueAsync(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        dbReference.Child("users").Child(uid).Child("unlocked_ships").SetValueAsync("0"); 
        dbReference.Child("users").Child(uid).Child("selected_ship").SetValueAsync(0);
        dbReference.Child("users").Child(uid).Child("selected_skill").SetValueAsync(0);
        
        // สำหรับระบบสถิติ
        dbReference.Child("users").Child(uid).Child("total_wins").SetValueAsync(0);
        dbReference.Child("users").Child(uid).Child("total_losses").SetValueAsync(0);
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

            if (task.Result.Exists && task.Result.HasChild("username"))
            {
                string savedName = task.Result.Child("username").Value?.ToString();
                currentUsername = string.IsNullOrEmpty(savedName) ? fallbackUsername : savedName;
                dbReference.Child("users").Child(uid).Child("last_login")
                    .SetValueAsync(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            else
            {
                currentUsername = fallbackUsername;
                SaveInitialUserData(uid, currentUsername);
            }

            onComplete?.Invoke(currentUsername);
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
        dbReference.Child("users").Child(uid).RunTransaction(data =>
        {
            completed = false;
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
            }
            completed = true;
            return TransactionResult.Success(data);
        }, false).ContinueWithOnMainThread(task =>
        {
            bool success = !task.IsFaulted && !task.IsCanceled && completed && user != null && user.UserId == uid;
            long balance = 0;
            if (success) success = task.Result != null && TryReadCoins(task.Result.Child("coin_balance").Value, out balance);
            onResult?.Invoke(success, balance);
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
        if (user == null || dbReference == null) return;
        dbReference.Child("users").Child(user.UserId).Child("selected_ship").SetValueAsync(shipIndex);
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
        if (user == null || dbReference == null) return;
        dbReference.Child("users").Child(user.UserId).Child("selected_skill").SetValueAsync(skillIndex);
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
        dbReference.Child("users").Child(user.UserId).Child("username")
            .SetValueAsync(newName).ContinueWithOnMainThread(task =>
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

    // === บันทึกผลการแข่งขัน ===
    public void RecordMatchResult(bool isWinner, string opponentName, int rewardCoins, Action<bool> onComplete = null)
    {
        if (user == null || dbReference == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        string uid = user.UserId;

        // 1. อัปเดตสถิติแพ้ชนะ
        string statKey = isWinner ? "total_wins" : "total_losses";
        dbReference.Child("users").Child(uid).Child(statKey).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (!task.IsFaulted && !task.IsCanceled && task.Result.Exists)
            {
                int.TryParse(task.Result.Value.ToString(), out int currentStat);
                dbReference.Child("users").Child(uid).Child(statKey).SetValueAsync(currentStat + 1);
            }
            else
            {
                dbReference.Child("users").Child(uid).Child(statKey).SetValueAsync(1);
            }
        });

        // Report success only when both the reward and match history have completed.
        int pendingSaves = 2;
        bool savesSucceeded = true;
        Action<bool> saved = success =>
        {
            savesSucceeded &= success;
            if (--pendingSaves == 0) onComplete?.Invoke(savesSucceeded);
        };

        // 2. อัปเดตเหรียญ
        AddCoins(rewardCoins, success =>
        {
            if (!success) Debug.LogWarning("Match reward could not be saved. Coin balance was not overwritten.");
            saved(success);
        });

        // 3. บันทึก Match History (อิงตาม ER Diagram)
        string matchId = dbReference.Child("match_history").Push().Key;
        
        string currentRoom = Photon.Pun.PhotonNetwork.CurrentRoom != null ? Photon.Pun.PhotonNetwork.CurrentRoom.Name : "QuickMatch";
        
        Dictionary<string, object> matchData = new Dictionary<string, object>
        {
            { "user_a", currentUsername },
            { "user_b", opponentName },
            { "status", "Completed" },
            { "Result", isWinner ? "Win" : "Loss" },
            { "reward_a", rewardCoins },
            { "reward_b", isWinner ? 10 : 190 }, // รางวัลฝั่งตรงข้าม
            { "room_code", currentRoom },
            { "map_name", "Arena" },
            { "play_date", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
        };

        dbReference.Child("match_history").Child(matchId).SetValueAsync(matchData).ContinueWithOnMainThread(task =>
        {
            saved(!task.IsFaulted && !task.IsCanceled);
        });
    }
}
