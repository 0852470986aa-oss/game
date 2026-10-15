using UnityEngine;

// One reusable visual per ship; death/expiry clears it and cloak hides it from enemies.
public sealed class OverdriveSkillFx : MonoBehaviour
{
    private SpriteRenderer ring;
    private PlayerController ship;
    private float until;
    public void Play(float duration)
    {
        ship=GetComponent<PlayerController>();
        if(ring==null)
        {
            var go=new GameObject("OverdriveRing");go.transform.SetParent(transform,false);
            ring=go.AddComponent<SpriteRenderer>();ring.sprite=UiFx.Rays12;
            var body=GetComponent<SpriteRenderer>();
            ring.sortingLayerID=body.sortingLayerID;ring.sortingOrder=body.sortingOrder-1;
            go.transform.localScale=Vector3.one*body.sprite.bounds.size.y*1.25f/ring.sprite.bounds.size.y;
        }
        until=Time.time+duration;
    }
    private void LateUpdate()
    {
        if(ring==null||ship==null)return;
        if(ship.isDead||ship.HasMatchEnded)until=0;
        ring.enabled=Time.time<until&&!ship.HiddenFromLocalViewer;
        ring.color=new Color(1,.55f,.08f,.4f+.12f*Mathf.Sin(Time.time*16));
        ring.transform.localRotation=Quaternion.Euler(0,0,Time.time*100);
    }
    private void OnDisable(){if(ring!=null)ring.enabled=false;until=0;}
}
