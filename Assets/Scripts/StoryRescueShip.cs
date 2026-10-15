using UnityEngine;

// ยานกู้ภัยใช้ระบบ HP/กระสุน/ทีมเดิม รับดาเมจจากศัตรูได้ จอดรอให้ผู้เล่นคุ้มกันจนซ่อมสำเร็จ
public class StoryRescueShip : MonoBehaviour
{
    private PlayerController ship;
    private bool initialized;
    void Start()
    {
        ship=GetComponent<PlayerController>();
        var ai=GetComponent<BotController>();if(ai!=null)ai.enabled=false;
    }
    void LateUpdate()
    {
        if(ship==null)return;
        var ai=GetComponent<BotController>();if(ai!=null)ai.enabled=false;
        if(!initialized){ship.maxHp=ship.currentHp=320;ship.botAim=Vector2.up;initialized=true;}
        ship.botMove=Vector2.zero;ship.botFire=ship.botSkill=false;
        var image=GetComponent<SpriteRenderer>();if(image!=null)image.color=new Color(.35f,1,1);
        var body=GetComponent<Rigidbody2D>();if(body!=null)body.linearVelocity=Vector2.zero;
    }
}
