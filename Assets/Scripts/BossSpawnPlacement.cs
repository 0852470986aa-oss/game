using System.Collections.Generic;
using UnityEngine;

// ใช้รูปร่างตัวชนจริงเมื่อวงกลมครอบทั้งปีกใหญ่เกินช่องว่าง ไม่ย้ายหรือลบสิ่งกีดขวางของแม็พ
public static class BossSpawnPlacement
{
    public static bool Find(GameObject prefab,int map,out Vector2 position)
    {
        position=default;if(prefab==null)return false;
        var source=prefab.GetComponentsInChildren<PolygonCollider2D>(true);if(source.Length==0)return false;
        var probe=new GameObject("BossSpawnProbe");probe.transform.rotation=Quaternion.Euler(0,0,180);
        var shapes=new List<PolygonCollider2D>();
        var hits=new List<Collider2D>();var filter=new ContactFilter2D{useTriggers=true};
        Vector2 min=GameplayManager.GetArenaMin(map),max=GameplayManager.GetArenaMax(map);
        var players=Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        bool found=false;float best=float.NegativeInfinity;
        try
        {
            foreach(var original in source)
            {
                var shape=probe.AddComponent<PolygonCollider2D>();shape.isTrigger=true;shape.pathCount=original.pathCount;
                for(int p=0;p<original.pathCount;p++)
                {
                    var points=original.GetPath(p);
                    for(int i=0;i<points.Length;i++)points[i]=((Vector2)original.transform.TransformPoint(points[i]+original.offset)-(Vector2)prefab.transform.position)*BossSpectacleFx.BossScale;
                    shape.SetPath(p,points);
                }
                shapes.Add(shape);
            }
            for(int y=0;y<=20;y++)for(int x=0;x<=20;x++)
            {
                Vector2 candidate=new Vector2(Mathf.Lerp(min.x,max.x,x/20f),Mathf.Lerp(min.y,max.y,y/20f));
                float nearest=100;
                foreach(var player in players)if(!player.isDead)nearest=Mathf.Min(nearest,Vector2.Distance(candidate,player.transform.position));
                if(nearest<GameplayManager.SafeSpawnDistance)continue;
                probe.transform.position=candidate;Physics2D.SyncTransforms();bool blocked=false;
                foreach(var shape in shapes)
                {
                    Bounds bounds=shape.bounds;
                    if(bounds.min.x<min.x+.3f||bounds.max.x>max.x-.3f||bounds.min.y<min.y+.3f||bounds.max.y>max.y-.3f){blocked=true;break;}
                    hits.Clear();shape.Overlap(filter,hits);
                    foreach(var hit in hits)
                    {
                        if(hit==null||hit.gameObject==probe)continue;
                        if(!hit.isTrigger||hit.GetComponentInParent<HazardController>()!=null||hit.GetComponentInParent<PlayerController>()!=null){blocked=true;break;}
                    }
                    if(blocked)break;
                }
                if(blocked)continue;
                float score=nearest+(candidate.y>0?3:0);
                if(score>best){best=score;position=candidate;found=true;}
            }
            return found;
        }
        finally
        {
            foreach(var shape in shapes)shape.enabled=false;
            if(Application.isPlaying)Object.Destroy(probe);else Object.DestroyImmediate(probe);
        }
    }
}
