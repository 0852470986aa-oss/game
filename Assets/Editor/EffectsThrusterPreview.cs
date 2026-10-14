using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

// Isolated visual regression: invokes the real thruster builder without entering gameplay or touching accounts.
public static class EffectsThrusterPreview
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static void Set(PlayerController p, string field, object value) => typeof(PlayerController).GetField(field,F).SetValue(p,value);
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Batch-only preview");
        var scene = EditorSceneManager.NewPreviewScene();
        var oldTarget = RenderTexture.active;
        var target = new RenderTexture(2100,1200,24);
        try
        {
            Directory.CreateDirectory("Builds/EffectsPreviews");
            var cameraObject = new GameObject("EffectPreviewCamera",typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 8;
            camera.transform.position = new Vector3(0,0,-30); camera.farClipPlane = 60;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.015f,.025f,.045f);
            target.Create(); camera.targetTexture = target;
            int anchors = 0;
            for (int i=0;i<28;i++)
            {
                Sprite art = i<15 ? VisualSkin.SpriteFor(SkinCatalog.All[i]) : i<21 ? StoryFleetArt.Get(i-15)
                    : i==21 ? BattleLoadoutCatalog.ShipSprite(0) : BattleLoadoutCatalog.ShipSprite(i-22);
                if (art==null) throw new Exception("Missing ship art " + i);
                var go = new GameObject("Preview_"+i,typeof(SpriteRenderer),typeof(PhotonView));
                SceneManager.MoveGameObjectToScene(go,scene);
                var body = go.GetComponent<SpriteRenderer>(); body.sprite = art;
                var player = go.AddComponent<PlayerController>(); player.enabled = false;
                Set(player,"spriteRenderer",body);
                typeof(PlayerController).GetProperty("IsBot").SetValue(player,true);
                Set(player,"botShip",i>=22 ? i-22 : 0);
                player.currentHp = player.maxHp = 100;
                if(i==21) go.AddComponent<BossRaidAppearance>().Apply(body);
                go.transform.position = new Vector3((i%7-3)*4,5.8f-i/7*4,0);
                go.transform.localScale = Vector3.one * 2.8f/body.sprite.bounds.size.y;
                Set(player,"previousVfxPosition",go.transform.position);
                Set(player,"displayedThrust",1f);
                typeof(PlayerController).GetMethod("LateUpdateVisuals",F).Invoke(player,null);
                var nozzles = (SpriteRenderer[])typeof(PlayerController).GetField("sheetThrusters",F).GetValue(player);
                if(nozzles==null || nozzles.Length==0) throw new Exception("Missing thrusters " + i);
                var points = (Vector2[])typeof(PlayerController).GetField("exhaustAnchors",F).GetValue(player);
                foreach(var point in points)
                    if(point.x<0 || point.x>1 || point.y<0 || point.y>1) throw new Exception("Anchor outside sprite " + i + ": " + point);
                foreach(var nozzle in nozzles)
                    if(!nozzle.enabled || nozzle.sprite==null) throw new Exception("Invisible thruster " + i);
                anchors += points.Length;
                if(i>=18 && i<=20)
                {
                    var boss = go.AddComponent<StoryBossFx>();
                    typeof(StoryBossFx).GetMethod("Start",F).Invoke(boss,null);
                    typeof(StoryBossFx).GetMethod("LateUpdate",F).Invoke(boss,null);
                }
                if(i==21) typeof(BossRaidAppearance).GetMethod("LateUpdate",F).Invoke(go.GetComponent<BossRaidAppearance>(),null);
            }
            // Immediate editor rendering has no player-loop sprite batching update. Bind each texture explicitly.
            foreach(var root in scene.GetRootGameObjects())
                foreach(var renderer in root.GetComponentsInChildren<SpriteRenderer>())
                {
                    if(renderer.sprite == null) continue;
                    var material = new Material(Shader.Find("Sprites/Default"));
                    material.mainTexture = renderer.sprite.texture;
                    renderer.sharedMaterial = material;
                }
            camera.Render(); RenderTexture.active = target;
            var image = new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply();
            File.WriteAllBytes("Builds/EffectsPreviews/thrusters-all.png",image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            foreach(var root in scene.GetRootGameObjects())
            {
                var player = root.GetComponent<PlayerController>();
                if(player==null) continue;
                var body = root.GetComponent<SpriteRenderer>();
                body.flipX = body.flipY = true;
                typeof(PlayerController).GetMethod("LateUpdateVisuals",F).Invoke(player,null);
                var nozzles = (SpriteRenderer[])typeof(PlayerController).GetField("sheetThrusters",F).GetValue(player);
                var points = (Vector2[])typeof(PlayerController).GetField("exhaustAnchors",F).GetValue(player);
                for(int n=0;n<nozzles.Length;n++)
                {
                    Vector2 expected = (Vector2)body.sprite.bounds.min + Vector2.Scale(body.sprite.bounds.size,Vector2.one-points[n]);
                    if(Vector2.Distance(expected,nozzles[n].transform.localPosition)>.0001f) throw new Exception("Mirrored nozzle mismatch "+root.name);
                }
                player.isDead = true;
                typeof(PlayerController).GetMethod("LateUpdateVisuals",F).Invoke(player,null);
                foreach(var nozzle in nozzles) if(nozzle.enabled) throw new Exception("Dead ship still emits "+root.name);
            }
            Debug.Log("EFFECTS_PREVIEW_PASS: 28 ships, "+anchors+" nozzles, real runtime thruster builder; no gameplay started.");
            Debug.Log("EFFECTS_STATE_PASS: mirrored anchors and death hiding verified for all 28 ships.");
        }
        finally
        {
            RenderTexture.active = oldTarget;
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
