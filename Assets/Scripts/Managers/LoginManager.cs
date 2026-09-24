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
    private RectTransform loginSurface;

    private RectTransform MakeRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = size; rect.anchoredPosition = position;
        return rect;
    }

    private void Place(Transform item, Vector2 position, Vector2 size)
    {
        if (item == null) return;
        item.SetParent(loginSurface,false);
        var rect = item as RectTransform;
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
        rect.anchoredPosition=position; rect.sizeDelta=size; rect.localScale=Vector3.one;
    }

    private TMP_Text Label(string text, Vector2 position, Vector2 size, int fontSize, Color color)
    {
        var label=MakeRect("LoginLabel",loginSurface,position,size).gameObject.AddComponent<TextMeshProUGUI>();
        if(titleText != null) label.font=titleText.font;
        label.text=text; label.fontSize=fontSize; label.color=color;
        label.alignment=TextAlignmentOptions.Center; label.raycastTarget=false;
        return label;
    }

    private void StyleButton(UnityEngine.UI.Button button,string text,float y,bool primary)
    {
        if(button == null) return;
        Place(button.transform,new Vector2(310,y),new Vector2(380,64));
        var image=button.GetComponent<UnityEngine.UI.Image>();
        if(image != null) { image.sprite=null; image.color=primary ? new Color(.08f,.38f,.5f) : new Color(.06f,.12f,.2f); }
        var outline=button.GetComponent<UnityEngine.UI.Outline>();
        if(outline == null) outline=button.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor=new Color(.25f,.8f,1,primary ? .9f : .4f); outline.effectDistance=new Vector2(1,-1);
        var label=button.GetComponentInChildren<TMP_Text>();
        if(label != null)
        {
            label.text=text; label.fontSize=23; label.fontStyle=FontStyles.Bold;
            label.characterSpacing=1; label.color=Color.white;
            label.alignment=TextAlignmentOptions.Center;
            label.enableAutoSizing=true; label.fontSizeMin=18; label.fontSizeMax=23;
        }
        var colors=button.colors;
        colors.highlightedColor=new Color(.8f,1,1); colors.pressedColor=new Color(.45f,.7f,.8f);
        colors.disabledColor=new Color(.4f,.45f,.5f,.6f); button.colors=colors;
    }

    private void BuildLoginStyle()
    {
        var canvas=titleText != null ? titleText.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
        if(canvas == null) return;
        loginSurface=MakeRect("LoginPresentation",canvas.transform,Vector2.zero,new Vector2(1180,640));
        var card=MakeRect("PilotAccessFrame",loginSurface,new Vector2(310,0),new Vector2(520,510));
        var art=card.gameObject.AddComponent<UnityEngine.UI.Image>();
        var frames=Resources.LoadAll<Sprite>("Images/UI_Frames");
        art.sprite=System.Array.Find(frames,s=>s.name=="UI_Frames_5");
        art.color=art.sprite != null ? new Color(.65f,.8f,1,.95f) : new Color(.035f,.07f,.12f,.95f);
        art.raycastTarget=false;
        if(titleText != null)
        {
            Place(titleText.transform,new Vector2(-285,85),new Vector2(540,190));
            titleText.text="BATTLEFIELD\n<color=#65DFFF>OF THE STARS</color>";
            titleText.fontStyle=FontStyles.Bold; titleText.characterSpacing=2;
            titleText.fontSize=46; titleText.enableAutoSizing=true; titleText.fontSizeMin=32; titleText.fontSizeMax=46;
            titleText.alignment=TextAlignmentOptions.Center;
        }
        Label("CHOOSE YOUR SHIP. OWN THE ARENA.",new Vector2(-285,-65),new Vector2(530,50),19,new Color(.7f,.85f,.95f));
        Label("PILOT ACCESS",new Vector2(310,170),new Vector2(420,55),32,new Color(.5f,.9f,1));
        Label("Sign in and prepare for battle",new Vector2(310,122),new Vector2(420,40),19,new Color(.7f,.8f,.9f));
        StyleButton(googleButton,"CONTINUE WITH GOOGLE",45,true);
        StyleButton(guestButton,"PLAY AS GUEST",-40,false);
        Place(statusText != null ? statusText.transform : null,new Vector2(310,-110),new Vector2(410,45));
        Place(errorText != null ? errorText.transform : null,new Vector2(310,-174),new Vector2(410,75));
        if(statusText != null) { statusText.fontSize=18; statusText.alignment=TextAlignmentOptions.Center; statusText.color=new Color(.55f,.85f,.95f); }
        if(errorText != null) { errorText.fontSize=17; errorText.enableAutoSizing=true; errorText.fontSizeMin=13; errorText.fontSizeMax=17; errorText.alignment=TextAlignmentOptions.Center; errorText.color=new Color(1,.5f,.45f); }
        Label("BATTLEFIELD OF THE STARS  /  ONLINE ARENA",new Vector2(0,-295),new Vector2(1100,32),15,new Color(.5f,.65f,.75f));
        FitLogin();
    }

    private void LateUpdate() => FitLogin();
    private void FitLogin()
    {
        if(loginSurface == null || Screen.width <= 0 || Screen.height <= 0) return;
        var parent=loginSurface.parent as RectTransform;
        if(parent == null) return;
        var safe=Screen.safeArea;
        Vector2 units=new Vector2(parent.rect.width/Screen.width,parent.rect.height/Screen.height);
        loginSurface.localScale=Vector3.one*Mathf.Max(.01f,Mathf.Min(safe.width*units.x/1220,safe.height*units.y/680));
        loginSurface.anchoredPosition=Vector2.Scale(safe.center-new Vector2(Screen.width,Screen.height)*.5f,units);
    }

    void Start()
    {
        BuildLoginStyle();
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
