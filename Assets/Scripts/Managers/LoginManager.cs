// LoginManager.cs — ควบคุมหน้าจอล็อกอิน (LoginScene) ซึ่งเป็นหน้าแรกของเกม
// รอให้ FirebaseManager (Singleton) พร้อมก่อน แล้วเปิดให้กดปุ่ม Google หรือ Guest
// การล็อกอินจริงทำใน FirebaseManager.LoginGoogle/LoginGuest แล้วเรียก callback กลับมาที่ไฟล์นี้
// ล็อกอินสำเร็จจะโหลด LobbyScene (LobbyManager) ต่อ
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// หน้าล็อกอิน: รับการกด Google/Guest แสดงสถานะ และเปลี่ยนไปล็อบบี้เมื่อยืนยันตัวตนสำเร็จ
public class LoginManager : MonoBehaviour
{
    // ช่อง UI ที่ลากมาใส่ใน Inspector: ข้อความหัวเรื่อง/สถานะ/ข้อผิดพลาด และปุ่มล็อกอินสองแบบ
    [Header("UI References")]
    public TMP_Text titleText;
    public TMP_Text statusText; // ข้อความสถานะ เช่น กำลังเชื่อมต่อ/กำลังล็อกอิน
    public TMP_Text errorText; // ข้อความแจ้งข้อผิดพลาดเมื่อล็อกอินไม่สำเร็จ
    public UnityEngine.UI.Button googleButton; // ปุ่มล็อกอินด้วยบัญชี Google
    public UnityEngine.UI.Button guestButton; // ปุ่มเข้าเล่นแบบ Guest (ไม่ผูกบัญชี)

    // true ระหว่างรอผลล็อกอิน ใช้กันผู้เล่นกดปุ่มซ้ำหลายครั้ง
    private bool isLoggingIn = false;


    // Unity เรียกตอนเริ่ม Scene: ล็อกจอแนวนอน ล้างข้อความ error แล้วเริ่ม Coroutine รอ Firebase
    // (ทั้งสองกรณีเรียก WaitForFirebase เหมือนกัน ต่างกันแค่ข้อความสถานะ)
    void Start()
    {
        // ล็อคหน้าจอเป็นแนวนอน
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.LandscapeLeft;

        SetError("");

        if (FirebaseManager.Instance != null)
        {
            SetStatus("System Initializing...");
            StartCoroutine(WaitForFirebase());
        }
        else
        {
            SetStatus("Loading Firebase...");
            StartCoroutine(WaitForFirebase());
        }
    }

    // Coroutine วนเช็คทุกเฟรมจนกว่า FirebaseManager จะพร้อม (IsFirebaseReady)
    // ถ้าเกิน timeout 10 วินาทีจะแสดงข้อความเชื่อมต่อไม่ได้และหยุด (ปุ่มยังกดไม่ได้)
    // พร้อมแล้วจึงเปิดปุ่มล็อกอิน
    private System.Collections.IEnumerator WaitForFirebase()
    {
        float timeout = 10f;
        float elapsed = 0f;

        while (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsFirebaseReady())
        {
            elapsed += Time.deltaTime;
            if (elapsed > timeout)
            {
                SetStatus("Firebase connection failed");
                SetError("Cannot connect to server\nPlease check your internet connection");
                yield break;
            }
            yield return null;
        }

        SetStatus("Ready to login");
        EnableButtons(true);
    }

    // เรียกจากปุ่ม Google: ปิดปุ่ม ตั้ง isLoggingIn แล้วสั่ง FirebaseManager ล็อกอินด้วย Google
    // ผลลัพธ์จะกลับมาที่ OnLoginSuccess หรือ OnLoginFailed
    public void LoginGoogle()
    {
        if (isLoggingIn) return;
        SetError("");
        SetStatus("Logging in with Google...");
        EnableButtons(false);
        isLoggingIn = true;

        // เรียกใช้งาน Google Login
        if (FirebaseManager.Instance != null)
            FirebaseManager.Instance.LoginGoogle(OnLoginSuccess, OnLoginFailed);
        else
            OnLoginFailed("FirebaseManager not found!");
    }

    // เรียกจากปุ่ม Guest: ล็อกอินแบบไม่ระบุตัวตน (Anonymous) ผ่าน FirebaseManager
    public void LoginGuest()
    {
        if (isLoggingIn) return;
        SetError("");
        SetStatus("Logging in as Guest...");
        EnableButtons(false);
        isLoggingIn = true;

        if (FirebaseManager.Instance != null)
            FirebaseManager.Instance.LoginGuest(OnLoginSuccess, OnLoginFailed);
        else
            OnLoginFailed("ไม่พบ FirebaseManager!");
    }

    // callback เมื่อล็อกอินสำเร็จ: แสดงชื่อผู้เล่น แล้วหน่วง 0.5 วินาทีค่อยไปหน้าล็อบบี้
    private void OnLoginSuccess(string username)
    {
        isLoggingIn = false;
        SetStatus($"Login successful! Welcome {username}");
        Invoke("LoadLobby", 0.5f);
    }

    // callback เมื่อล็อกอินล้มเหลว: แสดงข้อความ error และเปิดปุ่มให้ลองใหม่
    private void OnLoginFailed(string errorMessage)
    {
        isLoggingIn = false;
        SetStatus("Login failed");
        SetError(errorMessage);
        EnableButtons(true);
    }

    // โหลด LobbyScene (ถูกเรียกผ่าน Invoke จาก OnLoginSuccess)
    private void LoadLobby() { SceneManager.LoadScene("LobbyScene"); }

    // แสดงข้อความสถานะบนจอ
    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    // แสดงข้อความ error และซ่อนกล่องข้อความถ้าไม่มี error
    private void SetError(string msg)
    {
        if (errorText != null)
        {
            errorText.text = msg;
            errorText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
        }
    }

    // เปิด/ปิดการกดปุ่มล็อกอินทั้งสองปุ่มพร้อมกัน
    private void EnableButtons(bool on)
    {
        if (googleButton != null) googleButton.interactable = on;
        if (guestButton != null) guestButton.interactable = on;
    }
}
