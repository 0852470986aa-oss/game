// ไฟล์: BattleSettingsPanel.cs — หน้าต่าง SETTINGS (ปรับเสียง MASTER/MUSIC/EFFECTS และ AIM SPEED)
// ถูกเปิดจาก LobbyManager (ในล็อบบี้ มีปุ่ม LOG OUT) และ GameplayManager.HUD (ในสนามรบ มีปุ่ม LEAVE MATCH)
// ค่าที่ปรับเก็บใน PlayerPrefs (เฉพาะเครื่องนี้ ไม่ส่งผ่านเครือข่าย) และเรียก AudioManager ให้เสียงเปลี่ยนทันที
using UnityEngine;

// หน้าตั้งค่าที่ใช้ร่วมกันระหว่างล็อบบี้และสนามรบ; ไม่หยุดเวลาเกมออนไลน์
// ถ้าใน Scene มีต้นแบบ BattleSettingsTemplate (สร้างด้วยเมนู Editable) จะคัดลอกต้นแบบนั้นมาใช้
// หน้าตาจึงเป็นตามที่จัดในหน้า Edit; ถ้าไม่มีต้นแบบ จะสร้างด้วยโค้ดแบบเดิม
// คลาส MonoBehaviour ที่ติดกับ Canvas ของหน้าตั้งค่า เปิดได้ทีละหน้าเดียว (อ้างอิงผ่าน current)
public partial class BattleSettingsPanel : MonoBehaviour
{
    // ชื่อ GameObject ต้นแบบใน Scene ที่ FindTemplate ใช้ค้นหา
    public const string TemplateName = "BattleSettingsTemplate";
    // หน้าตั้งค่าที่เปิดอยู่ตอนนี้ (null = ปิดอยู่) ใช้กันเปิดซ้อน
    private static BattleSettingsPanel current;
    // true เมื่อหน้าตั้งค่าเปิดอยู่ PlayerController ใช้เช็คเพื่องดรับ input บางส่วนขณะเมนูเปิด
    public static bool IsOpen => current != null;
    // ความไวการหันยาน อ่านจาก PlayerPrefs (ค่าเริ่มต้น 0.7) จำกัดช่วง 0.25–1.5
    // PlayerController.Movement นำไปคูณกับความเร็วหมุนยาน (ค่ามาก = หันเร็วขึ้น)
    public static float AimSensitivity => Mathf.Clamp(PlayerPrefs.GetFloat("AimSensitivity", .7f), .25f, 1.5f);
    // callback ที่ผู้เรียก Show ส่งมา: leave = ยืนยันออกจากแมตช์, logout = ยืนยันออกจากระบบ
    private System.Action leave;
    private System.Action logout;
    // หน้ายืนยัน LOG OUT/LEAVE: แบบที่สร้างด้วยโค้ด (logoutConfirmation, confirmation) หรือแบบจากต้นแบบ (authored...)
    private GameObject logoutConfirmation;
    private GameObject authoredLogoutConfirmation;
    // กรอบหลักของหน้าตั้งค่า (SettingsPanel) ที่ปุ่มและสไลเดอร์ทั้งหมดอยู่ข้างใน
    private RectTransform panel;
    private GameObject confirmation;
    private GameObject authoredConfirmation;

    // เปิดหน้าตั้งค่า (static เรียกได้จากทุกที่) ถ้าเปิดอยู่แล้วจะไม่เปิดซ้ำ
    // ลองใช้ต้นแบบใน Scene ก่อน ถ้าไม่มีหรือต้นแบบไม่สมบูรณ์ จะสร้างหน้าด้วยโค้ด (Build)
    // onLeave = ปุ่ม LEAVE MATCH (ในสนามรบ), onLogout = ปุ่ม LOG OUT (ในล็อบบี้)
    public static void Show(System.Action onLeave = null, System.Action onLogout = null)
    {
        if (current != null) return;
        // 1) ลองหาต้นแบบที่จัดไว้ใน Scene แล้วคัดลอกมาใช้
        var template = FindTemplate();
        if (template != null)
        {
            var copy = EditableTemplate.Spawn(template, null);
            copy.name = "BattleSettings";
            current = copy.AddComponent<BattleSettingsPanel>();
            current.leave = onLeave;
            current.logout = onLogout;
            if (current.BindAuthored()) { current.MakeUnified(); return; }
            Debug.LogWarning("BattleSettingsTemplate is missing SettingsPanel; using the code-built settings panel.", template);
            current = null;
            Destroy(copy);
        }
        // 2) ไม่มีต้นแบบ (หรือผูกต้นแบบไม่สำเร็จ): สร้าง Canvas ใหม่และสร้าง UI ด้วยโค้ด
        var root = CreateRoot("BattleSettings");
        current = root.AddComponent<BattleSettingsPanel>();
        current.leave = onLeave;
        current.logout = onLogout;
        current.Build();
        current.MakeUnified();
    }

    // สร้าง Canvas แบบ Screen Space Overlay (sortingOrder 200 ให้อยู่บนสุด)
    // ปรับสเกลตามขนาดจอ โดยอ้างอิงความละเอียด 1280x720
    private static GameObject CreateRoot(string name)
    {
        var root = new GameObject(name, typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280,720);
        scaler.matchWidthOrHeight = .5f;
        return root;
    }

    // ค้นหา GameObject ต้นแบบชื่อ TemplateName ใน root ของ Scene ที่เปิดอยู่ ไม่เจอคืน null
    private static GameObject FindTemplate()
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == TemplateName) return root;
        return null;
    }

    // ตัวช่วยสร้าง RectTransform ลูกของ parent พร้อมกำหนดตำแหน่ง (x,y) และขนาด (w,h)
    private RectTransform Rect(string name, Transform parent, float x,float y,float w,float h)
    {
        var item = new GameObject(name,typeof(RectTransform));
        var rect = item.GetComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.sizeDelta = new Vector2(w,h); rect.anchoredPosition = new Vector2(x,y);
        if (Application.isPlaying) UiLayout.Placed(rect); // ตำแหน่งที่บันทึกเอง (UiLayout.cs)
        return rect;
    }
    // ตัวช่วยสร้างข้อความ TextMeshPro ขนาดตัวอักษร 23 จัดกึ่งกลาง (ไม่รับการคลิก)
    // ชื่อวัตถุไม่ซ้ำกัน เพื่อให้ต้นแบบใน Scene หาเจอได้ (หน้าตา/การทำงานเหมือนเดิม)
    private TMPro.TextMeshProUGUI Text(Transform parent,string value,float x,float y,float w=500,string name="Label")
    {
        var text=Rect(name,parent,x,y,w,44).gameObject.AddComponent<TMPro.TextMeshProUGUI>();
        text.text=value; text.fontSize=23; text.alignment=TMPro.TextAlignmentOptions.Center;
        text.raycastTarget=false; return text;
    }
    // ตัวช่วยสร้างปุ่มขนาด 220x48 พร้อมข้อความ เมื่อกดจะเรียก action
    private void Button(Transform parent,string value,float x,float y,System.Action action)
    {
        var rect=Rect(value,parent,x,y,220,48);
        rect.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.08f,.25f,.34f);
        rect.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(()=>action());
        Text(rect,value,0,0,210);
    }
    // สร้างสไลเดอร์หนึ่งแถว (ข้อความหัวข้อด้านซ้าย + แถบเลื่อนด้านขวา) แล้วผูกกับ PlayerPrefs ผ่าน HookSlider
    private void Row(string title,string key,float defaultValue,float min,float max,float y,System.Action<float> changed)
    {
        var label=Text(panel,title,-155,y,230,title+" Label");
        var track=Rect(title,panel,125,y,260,16);
        track.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.15f,.24f,.32f);
        var slider=track.gameObject.AddComponent<UnityEngine.UI.Slider>();
        var handle=Rect("Handle",track,0,0,26,34);
        var image=handle.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color=new Color(.3f,.85f,1);
        slider.handleRect=handle; slider.targetGraphic=image; slider.minValue=min; slider.maxValue=max;
        HookSlider(slider,label,title,key,defaultValue,min,max,changed);
    }
    // ตั้งค่าเริ่มของสไลเดอร์จาก PlayerPrefs[key] เมื่อผู้เล่นเลื่อน: บันทึกค่า, เรียก changed และแสดงค่าเป็น %
    // ใช้ร่วมกันทั้งหน้าที่สร้างด้วยโค้ด (Row) และหน้าจากต้นแบบ (BindRow)
    private static void HookSlider(UnityEngine.UI.Slider slider,TMPro.TMP_Text label,string title,string key,float defaultValue,float min,float max,System.Action<float> changed)
    {
        float value=Mathf.Clamp(PlayerPrefs.GetFloat(key,defaultValue),min,max);
        slider.SetValueWithoutNotify(value);
        if(label != null) label.text=title+"  "+Mathf.RoundToInt(value*100)+"%";
        slider.onValueChanged.AddListener(v=>{ PlayerPrefs.SetFloat(key,v); changed?.Invoke(v); if(label != null) label.text=title+"  "+Mathf.RoundToInt(v*100)+"%"; });
    }
    // สร้างหน้าตั้งค่าทั้งหมดด้วยโค้ด: พื้นหลังมืด, กรอบ SettingsPanel, สไลเดอร์ 4 แถว, ข้อความ และปุ่ม
    // ปุ่มขวาล่างเป็น LEAVE MATCH (ถ้ามี leave) หรือ LOG OUT (ถ้ามี logout) ถ้าไม่มีทั้งคู่ปุ่ม BACK อยู่กลาง
    private void Build()
    {
        var shade=Rect("ModalShade",transform,0,0,0,0);
        shade.anchorMin=Vector2.zero; shade.anchorMax=Vector2.one; shade.offsetMin=shade.offsetMax=Vector2.zero;
        shade.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(0,0,0,.8f);
        panel=Rect("SettingsPanel",transform,0,0,640,560);
        panel.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.035f,.065f,.12f);
        Text(panel,"SETTINGS",0,225,500,"Title");
        // แต่ละแถว: ชื่อ, key ใน PlayerPrefs, ค่าเริ่มต้น, ค่าต่ำสุด, ค่าสูงสุด, ตำแหน่ง y, ฟังก์ชันเมื่อค่าเปลี่ยน
        Row("MASTER","MasterVolume",1,0,1,140,v=>AudioManager.Instance?.SetMasterVolume(v));
        Row("MUSIC","MusicVolume",.65f,0,1,75,v=>AudioManager.Instance?.SetMusicVolume(v));
        Row("EFFECTS","SFXVolume",.8f,0,1,10,v=>AudioManager.Instance?.SetSFXVolume(v));
        Row("AIM SPEED","AimSensitivity",.7f,.25f,1.5f,-55,null);
        Text(panel,MessageFor(leave),0,-120,600,"Message").fontSize=18;
        AddOptionsButton();
        Button(panel,"BACK",leave == null && logout == null ? 0 : -125,-205,Close);
        if(leave != null) Button(panel,"LEAVE MATCH",125,-205,Confirm);
        else if(logout != null) Button(panel,"LOG OUT",125,-205,ConfirmLogout);
    }
    // true = ควรแสดงปุ่ม LOG OUT (อยู่ในล็อบบี้ ไม่มีปุ่มออกจากแมตช์)
    private bool ShowsLogout => leave == null && logout != null;
    // ข้อความใต้สไลเดอร์: ในล็อบบี้บอกว่าบันทึกในเครื่อง, ในสนามรบเตือนว่าแมตช์ออนไลน์ยังเดินต่อ
    private static string MessageFor(System.Action leave) =>
        leave == null ? "Settings are saved on this device." : "Online match continues while this menu is open.";

    // คืน false ถ้าไม่พบ SettingsPanel (Show จะลบสำเนาแล้วสร้างด้วยโค้ดแทน)
    // ผูกปุ่ม/สไลเดอร์ของต้นแบบที่คัดลอกมา (ไม่เปลี่ยนตำแหน่ง/สีที่จัดไว้)
    private bool BindAuthored()
    {
        panel = transform.Find("SettingsPanel") as RectTransform;
        if (panel == null) return false;
        // 1) ผูกสไลเดอร์ทั้ง 4 แถวกับค่าใน PlayerPrefs
        BindRow("MASTER","MasterVolume",1,0,1,v=>AudioManager.Instance?.SetMasterVolume(v));
        BindRow("MUSIC","MusicVolume",.65f,0,1,v=>AudioManager.Instance?.SetMusicVolume(v));
        BindRow("EFFECTS","SFXVolume",.8f,0,1,v=>AudioManager.Instance?.SetSFXVolume(v));
        BindRow("AIM SPEED","AimSensitivity",.7f,.25f,1.5f,null);
        // 2) ตั้งข้อความ และผูกปุ่ม BACK / LEAVE MATCH (ซ่อน LEAVE MATCH ถ้าไม่ได้อยู่ในสนามรบ)
        var message = FindText(panel,"Message");
        if(message != null) message.text = MessageFor(leave);
        BindButton(panel,"BACK",Close);
        var leaveButton = panel.Find("LEAVE MATCH");
        if(leaveButton != null)
        {
            leaveButton.gameObject.SetActive(leave != null);
            if(leave != null) BindButton(panel,"LEAVE MATCH",Confirm);
        }
        // 3) ปุ่ม LOG OUT: แสดงเฉพาะเมื่อ ShowsLogout
        var logoutButton = panel.Find("LOG OUT");
        if(logoutButton == null && ShowsLogout)
        {
            // ต้นแบบเก่าที่ยังไม่มีปุ่ม LOG OUT: เพิ่มให้ และเลื่อน BACK ไปซ้ายไม่ให้ทับกัน
            var back = panel.Find("BACK") as RectTransform;
            if(back != null && Mathf.Abs(back.anchoredPosition.x) < 1f) back.anchoredPosition = new Vector2(-125, back.anchoredPosition.y);
            Button(panel,"LOG OUT",125,-205,ConfirmLogout);
        }
        else if(logoutButton != null)
        {
            logoutButton.gameObject.SetActive(ShowsLogout);
            if(ShowsLogout) BindButton(panel,"LOG OUT",ConfirmLogout);
        }
        // เฟส 9: ปุ่ม MORE OPTIONS (ต้นแบบเก่าไม่มี = เพิ่มด้วยโค้ด)
        var moreOptions = panel.Find("MORE OPTIONS");
        if(moreOptions == null) AddOptionsButton();
        else
        {
            moreOptions.gameObject.SetActive(FeatureFlags.PlayerOptions);
            if(FeatureFlags.PlayerOptions) BindButton(panel,"MORE OPTIONS",ShowOptions);
        }
        // 4) ผูกหน้ายืนยัน ConfirmLogout / ConfirmLeave ของต้นแบบ (ซ่อนไว้ก่อน แสดงเมื่อกดปุ่ม)
        var confirmLogout = panel.Find("ConfirmLogout");
        if(confirmLogout != null)
        {
            authoredLogoutConfirmation = confirmLogout.gameObject;
            authoredLogoutConfirmation.SetActive(false);
            BindButton(confirmLogout,"CANCEL",()=>authoredLogoutConfirmation.SetActive(false));
            BindButton(confirmLogout,"LOG OUT",()=>{var action=logout; Close(); action?.Invoke();});
        }
        var confirm = panel.Find("ConfirmLeave");
        if(confirm != null)
        {
            authoredConfirmation = confirm.gameObject;
            authoredConfirmation.SetActive(false);
            BindButton(confirm,"CANCEL",()=>authoredConfirmation.SetActive(false));
            BindButton(confirm,"LEAVE",()=>{var action=leave; Close(); action?.Invoke();});
        }
        return true;
    }
    // หาสไลเดอร์ชื่อ title ในต้นแบบ แล้วผูกกับ PlayerPrefs ด้วย HookSlider (ไม่เจอก็ข้าม)
    private void BindRow(string title,string key,float defaultValue,float min,float max,System.Action<float> changed)
    {
        var track = panel.Find(title);
        var slider = track != null ? track.GetComponent<UnityEngine.UI.Slider>() : null;
        if(slider == null) return;
        // ช่วงค่าเป็นกติกาเกม จึงตั้งจากโค้ดเสมอ
        slider.minValue=min; slider.maxValue=max;
        HookSlider(slider,FindText(panel,title+" Label"),title,key,defaultValue,min,max,changed);
    }
    // หา TMP_Text ลูกของ parent ตามชื่อ ไม่เจอคืน null
    private static TMPro.TMP_Text FindText(Transform parent,string name)
    {
        var item = parent.Find(name);
        return item != null ? item.GetComponent<TMPro.TMP_Text>() : null;
    }
    // หา Button ลูกของ parent ตามชื่อ แล้วเพิ่ม listener ให้เรียก action เมื่อกด
    private static void BindButton(Transform parent,string name,System.Action action)
    {
        var item = parent.Find(name);
        var button = item != null ? item.GetComponent<UnityEngine.UI.Button>() : null;
        if(button != null) button.onClick.AddListener(()=>action());
    }
    // กดปุ่ม LEAVE MATCH: แสดงหน้ายืนยัน "LEAVE THIS MATCH?" (ของต้นแบบ หรือสร้างใหม่ด้วยโค้ด)
    // กด LEAVE = ปิดหน้านี้แล้วเรียก leave (ผู้เรียก Show จัดการออกจากแมตช์), กด CANCEL = ปิดหน้ายืนยัน
    private void Confirm()
    {
        // SetAsLastSibling: หน้าตั้งค่าแบบหน้าเดียวสร้างกล่องเลื่อน/แท็บทีหลัง ถ้าไม่ยกขึ้นบนสุด กล่องเลื่อนจะบังปุ่ม LEAVE/CANCEL จนกดไม่ได้
        if(authoredConfirmation != null) { authoredConfirmation.transform.SetAsLastSibling(); authoredConfirmation.SetActive(true); return; }
        if(confirmation != null) return;
        var rect=Rect("ConfirmLeave",panel,0,0,640,560); confirmation=rect.gameObject;
        confirmation.AddComponent<UnityEngine.UI.Image>().color=new Color(.035f,.065f,.12f);
        Text(rect,"LEAVE THIS MATCH?",0,95,500,"Title");
        Text(rect,"The current match will end for both players.",0,25,610,"Message").fontSize=20;
        Button(rect,"CANCEL",-125,-95,()=>Destroy(confirmation));
        Button(rect,"LEAVE",125,-95,()=>{var action=leave; Close(); action?.Invoke();});
    }
    // กดปุ่ม LOG OUT: แสดงหน้ายืนยัน "LOG OUT?" แบบเดียวกับ Confirm แต่เรียก logout เมื่อยืนยัน
    private void ConfirmLogout()
    {
        if(authoredLogoutConfirmation != null) { authoredLogoutConfirmation.transform.SetAsLastSibling(); authoredLogoutConfirmation.SetActive(true); return; }
        if(logoutConfirmation != null) return;
        var rect=Rect("ConfirmLogout",panel,0,0,640,560); logoutConfirmation=rect.gameObject;
        logoutConfirmation.AddComponent<UnityEngine.UI.Image>().color=new Color(.035f,.065f,.12f);
        Text(rect,"LOG OUT?",0,95,500,"Title");
        Text(rect,"You will return to the login screen.",0,25,610,"Message").fontSize=20;
        Button(rect,"CANCEL",-125,-95,()=>Destroy(logoutConfirmation));
        Button(rect,"LOG OUT",125,-95,()=>{var action=logout; Close(); action?.Invoke();});
    }
    // ===================== เฟส 9: หน้า MORE OPTIONS =====================
    // ปุ่มมุมขวาบนของหน้าตั้งค่า (ซ่อนเมื่อปิด FeatureFlags.PlayerOptions)
    private GameObject optionsPage;
    // สร้างปุ่ม MORE OPTIONS มุมขวาบนของ panel กดแล้วเรียก ShowOptions (ไม่สร้างถ้าปิด FeatureFlags.PlayerOptions)
    private void AddOptionsButton()
    {
        if(!FeatureFlags.PlayerOptions || panel == null) return;
        var rect=Rect("MORE OPTIONS",panel,205,222,190,44);
        rect.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.12f,.2f,.36f);
        rect.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(ShowOptions);
        Text(rect,"MORE OPTIONS",0,0,185).fontSize=17;
    }

    // แถวตั้งค่า 1 แถว: ชื่อ, ฟังก์ชันอ่านค่าเป็นข้อความ, ฟังก์ชันเมื่อกด (เปลี่ยนเป็นค่าถัดไป)
    private struct OptionRow { public string title; public System.Func<string> value; public System.Action next; }

    // แปลงค่า bool เป็นข้อความ "ON"/"OFF" สำหรับแสดงในแถวตั้งค่า
    private static string OnOff(bool on) => on ? "ON" : "OFF";
    // สลับค่าเปิด/ปิดของคีย์ใน PlayerPrefs (บันทึกเป็นค่าตรงข้ามกับค่าปัจจุบัน) ผ่าน GameSettings.SetFlag
    private static void Toggle(string key, bool current) => GameSettings.SetFlag(key, !current);
    // เลื่อนตัวเลือกของคีย์ไปค่าถัดไปแบบวนรอบ (current + 1) % count แล้วบันทึกผ่าน GameSettings.SetInt
    private static void Cycle(string key, int current, int count) => GameSettings.SetInt(key, (current + 1) % count);

    // สร้างรายการแถวตั้งค่าทั้งหมด (ชื่อ, ค่าที่แสดง, การเปลี่ยนค่าเมื่อกด) ใช้ทั้งหน้า MORE OPTIONS และหน้าแบบหน้าเดียว
    // แถว LANGUAGE เพิ่มเฉพาะเมื่อเปิด FeatureFlags.Language
    private OptionRow[] BuildOptionRows()
    {
        var rows = new System.Collections.Generic.List<OptionRow>
        {
            new OptionRow{ title="KILL FEED", value=()=>OnOff(GameSettings.KillFeed), next=()=>Toggle("Opt_KillFeed",GameSettings.KillFeed) },
            new OptionRow{ title="DAMAGE NUMBERS", value=()=>OnOff(GameSettings.DamageNumbers), next=()=>Toggle("Opt_DamageNumbers",GameSettings.DamageNumbers) },
            new OptionRow{ title="EMOTES", value=()=>OnOff(GameSettings.Emotes), next=()=>Toggle("Opt_Emotes",GameSettings.Emotes) },
            new OptionRow{ title="CAMERA SHAKE", value=()=>GameSettings.ShakeNames[GameSettings.ShakeLevel], next=()=>Cycle("Opt_Shake",GameSettings.ShakeLevel,3) },
            new OptionRow{ title="SHOW FPS", value=()=>OnOff(GameSettings.ShowFps), next=()=>Toggle("Opt_ShowFps",GameSettings.ShowFps) },
            new OptionRow{ title="FPS LIMIT", value=()=>GameSettings.FpsValues[GameSettings.FpsIndex].ToString(), next=()=>Cycle("Opt_Fps",GameSettings.FpsIndex,GameSettings.FpsValues.Length) },
            new OptionRow{ title="GRAPHICS", value=()=>GameSettings.GraphicsNames[GameSettings.Graphics], next=()=>Cycle("Opt_Graphics",GameSettings.Graphics,3) },
            new OptionRow{ title="BUTTON SIZE", value=()=>Mathf.RoundToInt(GameSettings.ButtonScale*100)+"%", next=()=>Cycle("Opt_ButtonSize",GameSettings.ButtonSizeIndex,GameSettings.ButtonSizes.Length) },
            new OptionRow{ title="BUTTON OPACITY", value=()=>Mathf.RoundToInt(GameSettings.ButtonOpacity*100)+"%", next=()=>Cycle("Opt_ButtonOpacity",GameSettings.ButtonOpacityIndex,GameSettings.ButtonOpacities.Length) },
            new OptionRow{ title="CONTROLS", value=()=>GameSettings.LeftHanded ? "LEFT-HANDED" : "RIGHT-HANDED", next=()=>Toggle("Opt_LeftHanded",GameSettings.LeftHanded) },
        };
        if(FeatureFlags.Language)
            rows.Add(new OptionRow{ title="LANGUAGE", value=()=>Lang.CurrentName, next=()=>Lang.SetThai(!Lang.Thai) });
        return rows.ToArray();
    }

    // เปิดหน้า MORE OPTIONS ทับหน้าตั้งค่า (สร้างด้วยโค้ดเสมอ)
    private void ShowOptions()
    {
        if(optionsPage != null) { optionsPage.SetActive(true); return; }
        var page=Rect("OptionsPage",panel,0,0,640,560); optionsPage=page.gameObject;
        optionsPage.AddComponent<UnityEngine.UI.Image>().color=new Color(.035f,.065f,.12f);
        Text(page,"MORE OPTIONS",0,238,500,"Title");
        var rows=BuildOptionRows();
        for(int i=0;i<rows.Length;i++)
        {
            var row=rows[i];
            float x=i<6 ? -152 : 152;
            float y=172-(i%6)*58;
            var rect=Rect("Opt_"+row.title,page,x,y,296,50);
            rect.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.08f,.2f,.3f);
            var title=Text(rect,row.title,-52,0,180,"Name"); title.fontSize=16; title.alignment=TMPro.TextAlignmentOptions.Left;
            var value=Text(rect,row.value(),88,0,110,"Value"); value.fontSize=16; value.color=new Color(.45f,.9f,1f);
            value.alignment=TMPro.TextAlignmentOptions.Right;
            rect.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(()=>
            {
                row.next();
                value.text=row.value();
                AudioManager.Instance?.PlaySFX("SFX_Click");
            });
        }
        Text(page,"Tap a row to change it. Saved on this device.",0,-178,600,"Hint").fontSize=16;
        Button(page,"BACK",0,-232,()=>optionsPage.SetActive(false));
    }

    // ปุ่มย้อนกลับมือถือ/Esc: ปิดชั้นบนสุดของหน้าตั้งค่า (หน้า MORE OPTIONS > หน้ายืนยัน > หน้าตั้งค่า) คืน false ถ้าไม่ได้เปิดอยู่
    public static bool HandleBack()
    {
        if (current == null) return false;
        if (current.optionsPage != null && current.optionsPage.activeSelf) { current.optionsPage.SetActive(false); return true; }
        if (current.authoredConfirmation != null && current.authoredConfirmation.activeSelf) { current.authoredConfirmation.SetActive(false); return true; }
        if (current.authoredLogoutConfirmation != null && current.authoredLogoutConfirmation.activeSelf) { current.authoredLogoutConfirmation.SetActive(false); return true; }
        if (current.confirmation != null) { Destroy(current.confirmation); current.confirmation = null; return true; }
        if (current.logoutConfirmation != null) { Destroy(current.logoutConfirmation); current.logoutConfirmation = null; return true; }
        current.Close();
        return true;
    }

    // ปิดหน้าตั้งค่า: บันทึก PlayerPrefs ลงเครื่อง แล้วทำลาย GameObject ของหน้านี้
    private void Close() { PlayerPrefs.Save(); Destroy(gameObject); }
    // Unity เรียกเมื่อถูกทำลาย: เคลียร์ current เพื่อให้เปิดหน้าใหม่ได้อีก
    void OnDestroy() { if(current==this) current=null; }

#if UNITY_EDITOR
    // (ใช้ใน Unity Editor เท่านั้น) inBattle=true สร้างแบบสนามรบ (LEAVE MATCH), false สร้างแบบล็อบบี้ (LOG OUT)
    // สร้างหน้ายืนยันไว้แต่ซ่อน แล้วถอดสคริปต์นี้ออก ใส่ EditableTemplate และปิด GameObject ไว้
    // ใช้จากเมนู Editable บน GameplayManager / LobbyManager: สร้างต้นแบบหน้าตั้งค่าไว้ใน Scene ให้แก้ได้
    public static void BuildEditableTemplate(bool inBattle)
    {
        if (Application.isPlaying) return;
        if (FindTemplate() != null) { Debug.LogWarning(TemplateName + " already exists in this scene."); return; }
        var root = CreateRoot(TemplateName);
        UnityEditor.Undo.RegisterCreatedObjectUndo(root, "Build Settings Template");
        var builder = root.AddComponent<BattleSettingsPanel>();
        builder.leave = inBattle ? () => { } : (System.Action)null;
        builder.logout = inBattle ? (System.Action)null : () => { };
        builder.Build();
        if (inBattle)
        {
            builder.Confirm();
            if (builder.confirmation != null) builder.confirmation.SetActive(false);
        }
        else
        {
            builder.ConfirmLogout();
            if (builder.logoutConfirmation != null) builder.logoutConfirmation.SetActive(false);
        }
        DestroyImmediate(builder);
        root.AddComponent<EditableTemplate>();
        root.SetActive(false);
        UnityEditor.Selection.activeGameObject = root;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log(TemplateName + " created. Tick it on to edit, the game hides it automatically when you press Play.", root);
    }
#endif
}
