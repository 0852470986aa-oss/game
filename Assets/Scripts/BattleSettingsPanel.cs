using UnityEngine;

// หน้าตั้งค่าที่ใช้ร่วมกันระหว่างล็อบบี้และสนามรบ; ไม่หยุดเวลาเกมออนไลน์
public class BattleSettingsPanel : MonoBehaviour
{
    private static BattleSettingsPanel current;
    public static bool IsOpen => current != null;
    public static float AimSensitivity => Mathf.Clamp(PlayerPrefs.GetFloat("AimSensitivity", .7f), .25f, 1.5f);
    private System.Action leave;
    private RectTransform panel;
    private GameObject confirmation;
    public static void Show(System.Action onLeave = null)
    {
        if (current != null) return;
        var root = new GameObject("BattleSettings", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280,720);
        scaler.matchWidthOrHeight = .5f;
        current = root.AddComponent<BattleSettingsPanel>();
        current.leave = onLeave;
        current.Build();
    }
    private RectTransform Rect(string name, Transform parent, float x,float y,float w,float h)
    {
        var item = new GameObject(name,typeof(RectTransform));
        var rect = item.GetComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.sizeDelta = new Vector2(w,h); rect.anchoredPosition = new Vector2(x,y); return rect;
    }
    private TMPro.TextMeshProUGUI Text(Transform parent,string value,float x,float y,float w=500)
    {
        var text=Rect("Label",parent,x,y,w,44).gameObject.AddComponent<TMPro.TextMeshProUGUI>();
        text.text=value; text.fontSize=23; text.alignment=TMPro.TextAlignmentOptions.Center;
        text.raycastTarget=false; return text;
    }
    private void Button(Transform parent,string value,float x,float y,System.Action action)
    {
        var rect=Rect(value,parent,x,y,220,48);
        rect.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.08f,.25f,.34f);
        rect.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(()=>action());
        Text(rect,value,0,0,210);
    }
    private void Row(string title,string key,float defaultValue,float min,float max,float y,System.Action<float> changed)
    {
        var label=Text(panel,title,-155,y,230);
        var track=Rect(title,panel,125,y,260,16);
        track.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.15f,.24f,.32f);
        var slider=track.gameObject.AddComponent<UnityEngine.UI.Slider>();
        var handle=Rect("Handle",track,0,0,26,34);
        var image=handle.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color=new Color(.3f,.85f,1);
        slider.handleRect=handle; slider.targetGraphic=image; slider.minValue=min; slider.maxValue=max;
        float value=Mathf.Clamp(PlayerPrefs.GetFloat(key,defaultValue),min,max);
        slider.SetValueWithoutNotify(value); label.text=title+"  "+Mathf.RoundToInt(value*100)+"%";
        slider.onValueChanged.AddListener(v=>{ PlayerPrefs.SetFloat(key,v); changed?.Invoke(v); label.text=title+"  "+Mathf.RoundToInt(v*100)+"%"; });
    }
    private void Build()
    {
        var shade=Rect("ModalShade",transform,0,0,0,0);
        shade.anchorMin=Vector2.zero; shade.anchorMax=Vector2.one; shade.offsetMin=shade.offsetMax=Vector2.zero;
        shade.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(0,0,0,.8f);
        panel=Rect("SettingsPanel",transform,0,0,640,560);
        panel.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.035f,.065f,.12f);
        Text(panel,"SETTINGS",0,225);
        Row("MASTER","MasterVolume",1,0,1,140,v=>AudioManager.Instance?.SetMasterVolume(v));
        Row("MUSIC","MusicVolume",.65f,0,1,75,v=>AudioManager.Instance?.SetMusicVolume(v));
        Row("EFFECTS","SFXVolume",.8f,0,1,10,v=>AudioManager.Instance?.SetSFXVolume(v));
        Row("AIM SPEED","AimSensitivity",.7f,.25f,1.5f,-55,null);
        Text(panel,leave == null ? "Settings are saved on this device." : "Online match continues while this menu is open.",0,-120,600).fontSize=18;
        Button(panel,"BACK",leave == null ? 0 : -125,-205,Close);
        if(leave != null) Button(panel,"LEAVE MATCH",125,-205,Confirm);
    }
    private void Confirm()
    {
        if(confirmation != null) return;
        var rect=Rect("ConfirmLeave",panel,0,0,640,560); confirmation=rect.gameObject;
        confirmation.AddComponent<UnityEngine.UI.Image>().color=new Color(.035f,.065f,.12f);
        Text(rect,"LEAVE THIS MATCH?",0,95);
        Text(rect,"The current match will end for both players.",0,25,610).fontSize=20;
        Button(rect,"CANCEL",-125,-95,()=>Destroy(confirmation));
        Button(rect,"LEAVE",125,-95,()=>{var action=leave; Close(); action?.Invoke();});
    }
    private void Close() { PlayerPrefs.Save(); Destroy(gameObject); }
    void OnDestroy() { if(current==this) current=null; }
}
