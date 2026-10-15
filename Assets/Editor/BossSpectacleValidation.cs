using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

public static class BossSpectacleValidation
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Run()
    {
        if(!Application.isBatchMode)throw new Exception("Batch only");
        var scene=EditorSceneManager.NewPreviewScene();var target=new RenderTexture(1600,900,24);var previous=RenderTexture.active;
        int checks=0;Action<bool,string> check=(pass,text)=>{if(!pass)throw new Exception(text);checks++;};
        try
        {
            Directory.CreateDirectory("Builds/BossSpectaclePreviews");
            var cameraGo=new GameObject("Camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraGo,scene);
            var camera=cameraGo.GetComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
            camera.enabled=false;camera.orthographic=true;camera.orthographicSize=7;camera.transform.position=new Vector3(0,0,-30);camera.farClipPlane=60;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.015f,.025f,.045f);
            target.Create();camera.targetTexture=target;
            for(int i=0;i<4;i++)
            {
                var go=new GameObject("Boss"+i,typeof(SpriteRenderer),typeof(PhotonView));SceneManager.MoveGameObjectToScene(go,scene);
                var body=go.GetComponent<SpriteRenderer>();body.sprite=i<3?StoryFleetArt.Get(i+3):BattleLoadoutCatalog.ShipSprite(1);
                var player=go.AddComponent<PlayerController>();player.enabled=false;player.maxHp=player.currentHp=100;
                typeof(PlayerController).GetField("spriteRenderer",Hidden).SetValue(player,body);
                typeof(PlayerController).GetProperty("IsBoss").SetValue(player,true);
                var collider=go.AddComponent<CircleCollider2D>();collider.radius=.2f;
                if(i==3)go.AddComponent<BossRaidAppearance>().Apply(body);
                go.transform.position=new Vector3((i-1.5f)*6.2f,0,0);go.transform.localScale=Vector3.one*5f/body.sprite.bounds.size.y;
                if(i<3){var story=go.AddComponent<StoryBossFx>();typeof(StoryBossFx).GetMethod("Start",Hidden).Invoke(story,null);typeof(StoryBossFx).GetMethod("LateUpdate",Hidden).Invoke(story,null);}
                else typeof(BossRaidAppearance).GetMethod("LateUpdate",Hidden).Invoke(go.GetComponent<BossRaidAppearance>(),null);
                var fx=go.AddComponent<BossSpectacleFx>();typeof(BossSpectacleFx).GetMethod("Start",Hidden).Invoke(fx,null);
                Vector3 scale=go.transform.localScale;
                fx.Charge(1,true);typeof(BossSpectacleFx).GetMethod("LateUpdate",Hidden).Invoke(fx,null);
                check(go.GetComponentsInChildren<Collider2D>().Length==1&&collider.radius==.2f&&go.transform.localScale==scale,"Effects preserve hull "+i);
                check(go.GetComponentsInChildren<LineRenderer>().Length==3,"Bounded arcs "+i);
                check(go.transform.Find("BossAura")!=null&&go.transform.Find("BossSpark23")!=null,"Full effect set "+i);
                player.currentHp=25;typeof(BossSpectacleFx).GetMethod("LateUpdate",Hidden).Invoke(fx,null);
                var material=new Material(Shader.Find("Sprites/Default")){mainTexture=body.sprite.texture};body.sharedMaterial=material;
                foreach(var sr in go.GetComponentsInChildren<SpriteRenderer>())if(sr!=body)sr.sharedMaterial=new Material(Shader.Find("Sprites/Default")){mainTexture=sr.sprite.texture};
                camera.Render();
                player.isDead=true;typeof(BossSpectacleFx).GetMethod("LateUpdate",Hidden).Invoke(fx,null);
                check(!go.transform.Find("BossAura").GetComponent<SpriteRenderer>().enabled,"Death hides aura "+i);
                foreach(var line in go.GetComponentsInChildren<LineRenderer>())check(!line.enabled,"Death hides arcs "+i);
                player.isDead=false;player.currentHp=100;typeof(BossSpectacleFx).GetMethod("LateUpdate",Hidden).Invoke(fx,null);
            }
            foreach(var root in scene.GetRootGameObjects())if(root.GetComponent<StoryBossPulse>()!=null)UnityEngine.Object.DestroyImmediate(root);
            var reference=new GameObject("PlayerSizeReference",typeof(SpriteRenderer));SceneManager.MoveGameObjectToScene(reference,scene);
            var referenceView=reference.GetComponent<SpriteRenderer>();referenceView.sprite=BattleLoadoutCatalog.ShipSprite(0);
            reference.transform.position=new Vector3(-9.3f,-4.8f,0);
            reference.transform.localScale=Vector3.one*(5f/BossSpectacleFx.BossScale)/referenceView.sprite.bounds.size.y;
            referenceView.sharedMaterial=new Material(Shader.Find("Sprites/Default")){mainTexture=referenceView.sprite.texture};
            // Child sprites use explicit texture materials for immediate edit-mode rendering.
            camera.Render();RenderTexture.active=target;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes("Builds/BossSpectaclePreviews/bosses.png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            check(BossSpectacleFx.BossScale==2.4f,"Boss scale 50 percent larger");
            Debug.Log("BOSS_SPECTACLE_PASS: "+checks+" checks; four boss styles rendered.");
        }
        finally{RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
