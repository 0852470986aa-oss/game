using UnityEngine;

// Fixed pool per boss. Uses the same network attack clock as the damage/telegraph.
public sealed class BossWeaponSpectacle : MonoBehaviour
{
    private readonly SpriteRenderer[] motes = new SpriteRenderer[20];
    private SpriteRenderer ring, flare;
    private void Awake()
    {
        ring = Make("WeaponChargeRing", UiFx.Ring, 62);
        flare = Make("WeaponMuzzleFlare", UiFx.Rays12, 63);
        for (int i=0;i<motes.Length;i++) motes[i]=Make("WeaponParticle"+i,UiFx.Spark,62);
        Hide();
    }
    private SpriteRenderer Make(string label, Sprite sprite, int order)
    {
        var go=new GameObject(label);go.transform.SetParent(transform,false);
        var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=order;return sr;
    }
    private void Place(SpriteRenderer sr, Vector2 position, float size, Color color, float angle)
    {
        sr.enabled=true;sr.transform.position=position;
        sr.transform.rotation=Quaternion.Euler(0,0,angle);
        sr.transform.localScale=Vector3.one*size/(sr.sprite.bounds.size.y*Mathf.Max(.01f,Mathf.Abs(transform.lossyScale.x)));
        sr.color=color;
    }
    public void Draw(Vector2 origin, Vector2 direction, float age, float windup, bool laser)
    {
        bool warning=age<windup;
        float progress=Mathf.Clamp01(age/windup);
        Color energy=warning?new Color(1,.65f,.15f,.8f):new Color(1,.28f,.08f,.75f);
        Place(ring,origin,warning?Mathf.Lerp(5,2,progress):3.5f,energy,age*140);
        Place(flare,origin,warning?Mathf.Lerp(.4f,2,progress):4.2f, new Color(1,.85f,.5f,warning?.3f:.65f),-age*200);
        int count=GameSettings.LowGraphics?6:motes.Length;
        Vector2 side=new Vector2(-direction.y,direction.x);
        for(int i=0;i<motes.Length;i++)
        {
            if(i>=count){motes[i].enabled=false;continue;}
            float cycle=Mathf.Repeat(age*(warning?1.2f:2f)+i/(float)count,1);
            float angle=i*2.39996f+age;
            Vector2 position=warning
                ?origin+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*Mathf.Lerp(5,.3f,cycle)
                :laser?origin+direction*(cycle*45)+side*(Mathf.Sin(age*20+i)*.45f)
                :origin+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*cycle*7;
            Place(motes[i],position,warning?.3f:.5f,new Color(1,.7f,.3f,Mathf.Sin(cycle*Mathf.PI)*.8f),angle*Mathf.Rad2Deg);
        }
    }
    public void Hide()
    {
        if(ring!=null)ring.enabled=false;if(flare!=null)flare.enabled=false;
        foreach(var sr in motes)if(sr!=null)sr.enabled=false;
    }
    private void OnDisable()=>Hide();
}
