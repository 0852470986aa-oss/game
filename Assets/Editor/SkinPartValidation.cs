using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SkinPartValidation
{
    public static void Run()
    {
        if(!Application.isBatchMode) throw new Exception("Batch-only isolated visual validation");
        int checks=0;
        void Check(bool condition,string message) { if(!condition)throw new Exception(message); checks++; }
        var scene=EditorSceneManager.NewPreviewScene();
        var active=RenderTexture.active;
        var target=new RenderTexture(1600,960,24); target.Create();
        var models=new List<GameObject>();
        try
        {
            Directory.CreateDirectory("Builds/AnimatedSkinPreviews");
            var cameraObject=new GameObject("RigCamera",typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.GetComponent<Camera>(); camera.scene=scene;
            camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
            camera.orthographic=true; camera.orthographicSize=6; camera.enabled=false;
            camera.transform.position=new Vector3(0,0,-30); camera.farClipPlane=60;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.015f,.025f,.045f); camera.targetTexture=target;
            for(int i=0;i<15;i++)
            {
                var art=VisualSkin.SpriteFor(SkinCatalog.All[i]);
                var go=new GameObject(SkinCatalog.All[i].id,typeof(SpriteRenderer),typeof(CircleCollider2D));
                SceneManager.MoveGameObjectToScene(go,scene); models.Add(go);
                go.transform.position=new Vector3((i%5-2)*4,3.8f-i/5*4,0);
                var body=go.GetComponent<SpriteRenderer>(); body.sprite=art;
                go.AddComponent<Photon.Pun.PhotonView>();
                var player=go.AddComponent<PlayerController>(); player.enabled=false;
                var collider=go.GetComponent<CircleCollider2D>(); collider.radius=.43f;
                go.AddComponent<ShipSkinAppearance>().Apply(body,art,3.2f);
                var rig=go.GetComponent<SkinPartMesh>();
                Check(rig!=null,"Rig installed "+i);
                var mesh=go.GetComponentInChildren<MeshFilter>().sharedMesh;
                rig.Sample(0); var first=mesh.vertices; var uv=mesh.uv;
                rig.Sample(.9f); var second=mesh.vertices;
                bool moves=false; for(int v=0;v<first.Length;v++) if(Vector3.Distance(first[v],second[v])>.001f)moves=true;
                Check(moves,"Actual mesh parts move "+i);
                int core=18*(SkinPartMotion.Columns+1)+10;
                Check(Vector3.Distance(first[core],second[core])<.00001f,"Fuselage stays fixed "+i);
                Check(mesh.vertexCount==525,"Dense continuous rig "+i);
                Check(go.transform.localScale==Vector3.one && Mathf.Approximately(collider.radius,.43f),"Gameplay geometry unchanged "+i);
                Check(go.GetComponentsInChildren<Collider2D>().Length==1,"No extra colliders "+i);
                var nextUV=mesh.uv; bool fixedUV=true;
                for(int v=0;v<uv.Length;v++) if(uv[v]!=nextUV[v]) fixedUV=false;
                Check(fixedUV,"Texture coordinates fixed during motion "+i);
                body.enabled=false; rig.Sample(1);
                Check(!go.GetComponentInChildren<MeshRenderer>().enabled,"Hidden hull hides mesh "+i);
                body.enabled=true; rig.Sample(0);
                player.isDead=true; rig.Sample(0);
                Check(!go.GetComponentInChildren<MeshRenderer>().enabled,"Dead ship hides all parts "+i);
                player.isDead=false;
                const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                typeof(PlayerController).GetField("cloaked",flags).SetValue(player,true);
                typeof(PlayerController).GetField("cloakUntil",flags).SetValue(player,Time.time+100);
                rig.Sample(0);
                Check(!go.GetComponentInChildren<MeshRenderer>().enabled,"Enemy cloak hides all parts "+i);
                typeof(PlayerController).GetField("cloaked",flags).SetValue(player,false);
                rig.Sample(0);
            }
            Save(camera,target,"combat-pose-a");
            foreach(var go in models) go.GetComponent<SkinPartMesh>().Sample(.9f);
            Save(camera,target,"combat-pose-b");
            foreach(var go in models)
            {
                go.GetComponent<SkinPartMesh>().Deactivate();
                Check(!go.GetComponent<SpriteRenderer>().forceRenderingOff,"Disable restores source");
                UnityEngine.Object.DestroyImmediate(go);
            }
            var canvasObject=new GameObject("PreviewCanvas",typeof(RectTransform),typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasObject,scene);
            var canvas=canvasObject.GetComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace; canvas.worldCamera=camera;
            canvasObject.GetComponent<RectTransform>().sizeDelta=new Vector2(20,12);
            for(int i=0;i<15;i++)
            {
                var go=new GameObject("SkinUI"+i,typeof(RectTransform),typeof(Image)); go.transform.SetParent(canvasObject.transform,false);
                var rect=go.GetComponent<RectTransform>(); rect.sizeDelta=new Vector2(3.4f,3.4f); rect.anchoredPosition=new Vector2((i%5-2)*4,3.8f-i/5*4);
                var image=go.GetComponent<Image>(); VisualSkin.Preview(image,0,SkinCatalog.All[i]);
                var rig=go.GetComponent<SkinPartUI>(); rig.previewTime=.9f;
                using(var vh=new VertexHelper()) { rig.ModifyMesh(vh); Check(vh.currentVertCount==525,"UI part mesh "+i); }
                Check(rect.anchoredPosition==new Vector2((i%5-2)*4,3.8f-i/5*4),"UI root stays fixed "+i);
                Check(!image.raycastTarget,"UI does not block clicks "+i);
            }
            Canvas.ForceUpdateCanvases(); Save(camera,target,"ui-pose-b");
            Debug.Log("SKIN_PART_PASS: "+checks+" checks; 15 combat rigs and 15 UI rigs; two combat poses rendered.");
        }
        finally { RenderTexture.active=active; target.Release(); UnityEngine.Object.DestroyImmediate(target); EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static void Save(Camera camera,RenderTexture target,string name)
    {
        camera.Render(); RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply();
        File.WriteAllBytes("Builds/AnimatedSkinPreviews/"+name+".png",image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
    }
}
