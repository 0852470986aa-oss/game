using UnityEngine;
using Photon.Pun;

// เครื่องกำเนิดใน Campaign คนเดียวเท่านั้น: รับดาเมจจากคนจริง ไม่รับดาเมจฝ่ายศัตรู
public class StoryGenerator : MonoBehaviour
{
    public const float MaxHp = 100;
    public float Hp { get; private set; } = MaxHp;
    public bool Destroyed => Hp<=0;
    private SpriteRenderer core;
    private TMPro.TextMeshPro label;
    private Vector3 original;
    public void Build(int index)
    {
        core=gameObject.AddComponent<SpriteRenderer>();core.sprite=UiFx.Glow;core.color=new Color(1,.2f,.15f);core.sortingOrder=25;
        transform.localScale=Vector3.one*3f;original=transform.localScale;
        var collider=gameObject.AddComponent<CircleCollider2D>();collider.radius=.3f;
        var text=new GameObject("Label");text.transform.SetParent(transform,false);text.transform.localPosition=new Vector3(0,.8f,0);
        label=text.AddComponent<TMPro.TextMeshPro>();label.fontSize=3.5f;label.alignment=TMPro.TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta=new Vector2(5,1);label.text="CORE "+index+" / 100";label.GetComponent<MeshRenderer>().sortingOrder=30;
    }
    public void Hit(float damage,int shooter)
    {
        if (Destroyed || !PhotonNetwork.OfflineMode || !PhotonNetwork.InRoom
            || MatchRules.GameMode(PhotonNetwork.CurrentRoom)!=MatchRules.ModeCampaign
            || shooter!=PhotonNetwork.LocalPlayer.ActorNumber || damage<=0 || float.IsNaN(damage)||float.IsInfinity(damage))return;
        Hp=Mathf.Max(0,Hp-damage);
        if(label!=null)label.text=Destroyed?"CORE DOWN": "CORE / "+Mathf.CeilToInt(Hp);
        if(!Destroyed)return;
        GetComponent<Collider2D>().enabled=false;core.color=new Color(.15f,.5f,.35f,.3f);
        var fx=GameplayManager.GetPrefab("DeathExplosion");if(fx!=null)Instantiate(fx,transform.position,Quaternion.identity);
    }
    private void Update(){if(core!=null&&!Destroyed)transform.localScale=original*(1+Mathf.Sin(Time.time*3)*.06f);}
}
