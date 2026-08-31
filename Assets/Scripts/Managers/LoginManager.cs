using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class LoginManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text titleText;
    public TMP_Text statusText;
    public TMP_Text errorText;
    public UnityEngine.UI.Button googleButton;
    public UnityEngine.UI.Button guestButton;

    private bool isLoggingIn = false;

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

    private void OnLoginSuccess(string username)
    {
        isLoggingIn = false;
        SetStatus($"Login successful! Welcome {username}");
        Invoke("LoadLobby", 0.5f);
    }

    private void OnLoginFailed(string errorMessage)
    {
        isLoggingIn = false;
        SetStatus("Login failed");
        SetError(errorMessage);
        EnableButtons(true);
    }

    private void LoadLobby() { SceneManager.LoadScene("LobbyScene"); }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    private void SetError(string msg)
    {
        if (errorText != null)
        {
            errorText.text = msg;
            errorText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
        }
    }

    private void EnableButtons(bool on)
    {
        if (googleButton != null) googleButton.interactable = on;
        if (guestButton != null) guestButton.interactable = on;
    }
}
