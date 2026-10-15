using UnityEngine;

// เอฟเฟกต์บอสทุกโหมด: ออรา วงพลังงาน ประกาย และสายฟ้า ใช้วัตถุชุดเดิมทุกเฟรม ไม่มีตัวชน/ดาเมจเพิ่ม
public sealed class BossSpectacleFx : MonoBehaviour
{
    public const float BossScale=2.4f; // เดิม 1.6: ขนาดและตัวชนโตขึ้น 50% พร้อมตรวจจุดเกิดตามขนาดจริง
    private PlayerController owner;
    private SpriteRenderer body,aura,halo,orbit;
    private readonly SpriteRenderer[] sparks=new SpriteRenderer[24];
    private readonly LineRenderer[] arcs=new LineRenderer[3];
    private Material arcMaterial;
    private Vector3 center;
    private float height,charge,shot,previousHp,flash;
    private int phase;
    private bool deathPlayed;
    private Color theme;
    private void Start()
    {
        owner=GetComponent<PlayerController>();body=GetComponent<SpriteRenderer>();
        if(owner==null||body==null||body.sprite==null){enabled=false;return;}
        height=body.sprite.bounds.size.y;
        var reactor=transform.Find("StoryReactor")??transform.Find("RaidReactor");
        center=reactor!=null?reactor.localPosition:(Vector3)body.sprite.bounds.center;
        theme=body.sprite.name.EndsWith("5")?new Color(.68f,.25f,1f):new Color(1,.4f,.06f);
        aura=Sprite("BossAura",UiFx.Glow,.95f,-2);halo=Sprite("BossShieldHalo",UiFx.Ring,.9f,-1);
        orbit=Sprite("BossOrbitHalo",UiFx.Rays12,.58f,-1);
        for(int i=0;i<sparks.Length;i++)sparks[i]=Sprite("BossSpark"+i,UiFx.Spark,.018f,2);
        arcMaterial=new Material(Shader.Find("Sprites/Default"));
        for(int i=0;i<arcs.Length;i++)
        {
            var go=new GameObject("BossElectricalArc"+i);go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();arcs[i]=line;line.sharedMaterial=arcMaterial;line.useWorldSpace=false;
            line.positionCount=8;line.sortingLayerID=body.sortingLayerID;line.sortingOrder=body.sortingOrder+2;
            line.startWidth=line.endWidth=height*.006f;
        }
        previousHp=owner.currentHp;
    }
    private SpriteRenderer Sprite(string name,Sprite sprite,float fraction,int order)
    {
        var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.localPosition=center;
        go.transform.localScale=Vector3.one*height*fraction/sprite.bounds.size.y;
        var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingLayerID=body.sortingLayerID;sr.sortingOrder=body.sortingOrder+order;return sr;
    }
    public void Charge(float amount,bool firing){charge=Mathf.Clamp01(amount);shot=firing?1f:0f;}
    private void LateUpdate()
    {
        if(owner==null||aura==null)return;
        bool visible=body.enabled&&!owner.isDead&&!owner.HiddenFromLocalViewer;
        aura.enabled=halo.enabled=orbit.enabled=visible;
        float alpha=visible?body.color.a:0;
        int next=BossRaidRules.Phase(owner.currentHp,owner.maxHp);
        if(phase>0&&next>phase&&!owner.isDead)Burst(3);
        if(owner.isDead&&!deathPlayed){deathPlayed=true;Burst(5);}
        phase=next;
        if(owner.currentHp<previousHp)flash=.22f;
        previousHp=owner.currentHp;flash=Mathf.Max(0,flash-Time.deltaTime);
        Color color=phase==3?Color.Lerp(theme,new Color(1,.08f,.12f),.65f):theme;
        float power=.5f+.13f*phase+charge*.4f;
        aura.color=new Color(color.r,color.g,color.b,alpha*(.16f+.035f*Mathf.Sin(Time.time*3)+flash*.5f));
        halo.color=new Color(color.r,color.g,color.b,alpha*(.16f+charge*.2f));
        orbit.color=new Color(color.r,color.g,color.b,alpha*(.2f+shot*.15f));
        halo.transform.localRotation=Quaternion.Euler(0,0,-Time.time*12);
        orbit.transform.localRotation=Quaternion.Euler(0,0,Time.time*(14+phase*8));
        int count=GameSettings.LowGraphics?8:sparks.Length;
        for(int i=0;i<sparks.Length;i++)
        {
            var spark=sparks[i];spark.enabled=visible&&i<count;
            float age=Mathf.Repeat(Time.time*(.35f+phase*.08f)+i*.618f,1);
            float angle=i*2.39996f+Time.time*.35f;
            float radius=height*Mathf.Lerp(.09f,.5f,age);
            spark.transform.localPosition=center+new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius*.8f,0);
            spark.transform.localRotation=Quaternion.Euler(0,0,angle*Mathf.Rad2Deg+Time.time*40);
            spark.color=new Color(color.r,Mathf.Min(1,color.g+.25f),Mathf.Min(1,color.b+.2f),alpha*Mathf.Sin(age*Mathf.PI)*power*.6f);
        }
        for(int a=0;a<arcs.Length;a++)
        {
            var line=arcs[a];line.enabled=visible&&!GameSettings.LowGraphics;
            Vector3 endpoint=center+new Vector3((a-1)*height*.23f,height*(a==1?.23f:.02f),0);
            for(int i=0;i<8;i++)
            {
                float t=i/7f;Vector3 point=Vector3.Lerp(center,endpoint,t);
                point.x+=Mathf.Sin(Time.time*29+i*13+a*5)*height*.015f*Mathf.Sin(t*Mathf.PI);line.SetPosition(i,point);
            }
            line.startColor=line.endColor=new Color(color.r,.65f,color.b,alpha*(.12f+charge*.35f+flash));
        }
        charge=Mathf.MoveTowards(charge,0,Time.deltaTime*.8f);shot=Mathf.MoveTowards(shot,0,Time.deltaTime*4);
    }
    private void Burst(int count)
    {
        for(int i=0;i<count;i++)
        {
            var go=new GameObject("BossShockwave");go.transform.position=transform.TransformPoint(center);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,gameObject.scene);
            var sr=go.AddComponent<SpriteRenderer>();sr.sprite=UiFx.Ring;sr.sortingLayerID=body.sortingLayerID;sr.sortingOrder=body.sortingOrder+3;
            go.AddComponent<StoryBossPulse>().Initialize(sr,theme,height*transform.lossyScale.y*(.75f+i*.28f));
        }
    }
    private void OnDisable()
    {
        if(aura!=null)aura.enabled=false;if(halo!=null)halo.enabled=false;if(orbit!=null)orbit.enabled=false;
        foreach(var spark in sparks)if(spark!=null)spark.enabled=false;
        foreach(var line in arcs)if(line!=null)line.enabled=false;
    }
    private void OnDestroy(){if(arcMaterial!=null){if(Application.isPlaying)Destroy(arcMaterial);else DestroyImmediate(arcMaterial);}}
}
