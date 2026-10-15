using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// เปิดได้จากตั้งค่าทั้งล็อบบี้และสนาม ตัวอย่างไม่มีสคริปต์ยิงหรือจอยจริง
public class ControlLayoutEditor : MonoBehaviour
{
    public static bool IsOpen => current != null;
    private static ControlLayoutEditor current;
    private ControlLayout.Profile draft;
    private RectTransform area;
    private readonly RectTransform[] buttons = new RectTransform[3];
    private int selected;
    private Slider size;
    private TMP_Text selectedLabel;
    private bool reset;
    public static void Show()
    {
        if (current != null) return;
        Lang.EnsureThaiFont();
        var root=new GameObject("ControlLayoutEditor",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=350;
        var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1280,720); scaler.matchWidthOrHeight=.5f;
        current=root.AddComponent<ControlLayoutEditor>(); current.Build();
    }
    private RectTransform Rect(string name,Transform parent)
    {
        var rect=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer)).GetComponent<RectTransform>(); rect.SetParent(parent,false); return rect;
    }
    private void Stretch(RectTransform rect) { rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero; }
    private TMP_Text Label(string name,Transform parent,string text,float font=20)
    {
        var rect=Rect(name,parent); Stretch(rect); var label=rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text=Lang.T(text);label.fontSize=font;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false; return label;
    }
    private void Action(string name,string text,float x,System.Action action)
    {
        var rect=Rect(name,transform);rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);
        rect.anchoredPosition=new Vector2(x,-66);rect.sizeDelta=new Vector2(180,44);
        rect.gameObject.AddComponent<Image>().color=new Color(.08f,.36f,.45f);
        rect.gameObject.AddComponent<Button>().onClick.AddListener(()=>action()); Label("Label",rect,text);
    }
    private void Build()
    {
        draft=ControlLayout.Load()??ControlLayout.Defaults();
        var background=Rect("Background",transform);Stretch(background); background.gameObject.AddComponent<Image>().color=new Color(.015f,.025f,.06f,.98f);
        area=Rect("SafeArea",transform);Stretch(area);
        var heading=Rect("Heading",transform);heading.anchorMin=new Vector2(0,1);heading.anchorMax=Vector2.one;
        heading.pivot=new Vector2(.5f,1);heading.sizeDelta=new Vector2(0,48);heading.anchoredPosition=new Vector2(0,-8);
        Label("Title",heading,"CUSTOMIZE CONTROLS / DRAG A BUTTON",24);
        for(int i=0;i<3;i++)
        {
            int index=i;var rect=Rect(ControlLayout.Names[i],area);rect.sizeDelta=Vector2.one*ControlLayout.Sizes[i];buttons[i]=rect;
            var ring=rect.gameObject.AddComponent<ControlRingGraphic>();ring.SetRing(.92f);ring.color=i==0?new Color(.2f,.85f,1):i==1?new Color(1,.65f,.2f):new Color(.7f,.5f,1);
            rect.gameObject.AddComponent<ControlLayoutDrag>().Bind(this,index);Label("Label",rect,ControlLayout.Names[i]);
        }
        Action("Save","SAVE",330,()=>{if(reset)ControlLayout.Reset();else ControlLayout.Save(draft);Close();});
        Action("Cancel","CANCEL",530,Close);
        Action("Reset","RESET",130,()=>{draft=ControlLayout.Defaults();reset=true;Refresh();Select(selected);});
        var sizeRect=Rect("Size",transform);sizeRect.anchorMin=sizeRect.anchorMax=new Vector2(0,1);sizeRect.pivot=new Vector2(0,1);
        sizeRect.anchoredPosition=new Vector2(30,-72);sizeRect.sizeDelta=new Vector2(250,28);
        sizeRect.gameObject.AddComponent<Image>().color=new Color(.1f,.2f,.3f);
        size=sizeRect.gameObject.AddComponent<Slider>();size.minValue=.65f;size.maxValue=1.6f;
        var handle=Rect("Handle",sizeRect);handle.sizeDelta=new Vector2(24,36);handle.gameObject.AddComponent<Image>().color=Color.cyan;
        size.handleRect=handle;size.targetGraphic=handle.GetComponent<Image>();
        size.onValueChanged.AddListener(v=>{draft.slots[selected].scale=v;reset=false;Refresh();SelectLabel();});
        var caption=Rect("Selected",transform);caption.anchorMin=caption.anchorMax=new Vector2(0,1);caption.pivot=new Vector2(0,1);
        caption.anchoredPosition=new Vector2(24,-112);caption.sizeDelta=new Vector2(280,34);selectedLabel=Label("Label",caption,"",17);
        var hint=Rect("Hint",transform);hint.anchorMin=new Vector2(0,0);hint.anchorMax=new Vector2(1,0);hint.pivot=new Vector2(.5f,0);
        hint.sizeDelta=new Vector2(0,40);hint.anchoredPosition=new Vector2(0,8);
        Label("Label",hint,"Saved on this device. Online matches continue while editing.",16);
        Refresh();Select(0);
    }
    private void Update()
    {
        if (LobbyManager.BackKeyPressed()) Close();
        Rect safe=Screen.safeArea;
        var parent=(RectTransform)transform;
        Vector2 units=new Vector2(parent.rect.width/Screen.width,parent.rect.height/Screen.height);
        area.anchorMin=area.anchorMax=area.pivot=new Vector2(.5f,.5f);
        area.sizeDelta=new Vector2(1280,720);
        area.localScale=Vector3.one*Mathf.Max(.01f,Mathf.Min((safe.width-24)*units.x/1280,(safe.height-24)*units.y/720));
        area.anchoredPosition=Vector2.Scale(safe.center-new Vector2(Screen.width,Screen.height)*.5f,units);Refresh();
    }
    private void Refresh(){for(int i=0;i<3;i++)ControlLayout.Apply(buttons[i],draft.slots[i]);}
    internal void Select(int index){selected=index;size.SetValueWithoutNotify(draft.slots[index].scale);SelectLabel();}
    private void SelectLabel(){selectedLabel.text=Lang.T(ControlLayout.Names[selected])+"  "+Mathf.RoundToInt(draft.slots[selected].scale*100)+"%";}
    internal void Drag(int index,PointerEventData data)
    {
        Select(index);
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(area,data.position,data.pressEventCamera,out Vector2 point))return;
        Vector2 position=new Vector2((point.x-area.rect.xMin)/area.rect.width,(point.y-area.rect.yMin)/area.rect.height);
        // แถวเครื่องมือด้านบนไม่ควรมีปุ่มเล่นทับ
        position.y=Mathf.Min(position.y,.76f);
        draft.slots[index].position=ControlLayout.ClampPosition(position,area.rect.size,buttons[index].rect.size*draft.slots[index].scale*.5f);
        reset=false;Refresh();
    }
    public static bool HandleBack(){if(current==null)return false;current.Close();return true;}
    private void Close(){Destroy(gameObject);}
    private void OnDestroy(){if(current==this)current=null;}
}
public class ControlLayoutDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private ControlLayoutEditor owner;private int index;
    public void Bind(ControlLayoutEditor editor,int slot){owner=editor;index=slot;}
    public void OnPointerDown(PointerEventData data){owner.Select(index);}
    public void OnDrag(PointerEventData data){owner.Drag(index,data);}
}
