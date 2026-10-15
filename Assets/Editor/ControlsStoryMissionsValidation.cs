using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ตรวจ logic และหน้าจอจริงใน preview scene ไม่เขียน PlayerPrefs/บัญชีผู้ใช้
public static class ControlsStoryMissionsValidation
{
    public static void Run()
    {
        if(!Application.isBatchMode)throw new Exception("Batch only");
        int checks=0;
        Action<bool,string> check=(pass,message)=>{if(!pass)throw new Exception(message);checks++;};
        check(Campaign.Count==13,"Stage count");
        string[] old={"FIRST CONTACT","TWIN RAIDERS","AMBUSH","ELITE SQUADRON","THE DREADNOUGHT","STATION BREACH","EMBER PATROL","INFERNO CRUISER","VOID BLOCKADE","VOID MOTHERSHIP"};
        for(int i=0;i<10;i++)check(Campaign.Get(i+1).title==old[i]&&Campaign.Get(i+1).objective==StoryObjective.Eliminate,"Old stage ID "+i);
        var survive=new StoryObjectiveState();var survival=Campaign.Get(11);
        for(int i=0;i<80;i++)survive.Tick(.25f,false,true,true,StoryObjective.Survive);
        check(survive.Elapsed==0,"Dead player cannot advance survival");
        for(int i=0;i<280;i++)survive.Tick(.25f,true,true,true,StoryObjective.Survive);
        check(survive.Complete(survival,0),"Survival completes at duration");
        var escort=new StoryObjectiveState();var escortDef=Campaign.Get(13);
        escort.Tick(.25f,true,true,false,StoryObjective.Escort);check(escort.Elapsed==0,"Repair requires proximity");
        for(int i=0;i<240;i++)escort.Tick(.25f,true,true,true,StoryObjective.Escort);
        check(escort.Complete(escortDef,0),"Escort repair win");
        var lostEscort=new StoryObjectiveState();lostEscort.Tick(.25f,true,false,true,StoryObjective.Escort);
        check(lostEscort.Failed&&!lostEscort.Complete(escortDef,0),"Lost rescue ship fails");
        var cores=new StoryObjectiveState();check(!cores.Complete(Campaign.Get(12),2)&&cores.Complete(Campaign.Get(12),3),"Generator threshold");
        var report=new MatchReport{won=true,bossDefeated=true,mode="BOSS_COOP",newShipWin=true};
        check(Progression.FindMission("BOSS1").count(report)==1,"Boss mission");
        report.won=false;check(Progression.FindMission("BOSS1").count(report)==0,"Failed boss cannot count");
        report.won=true;report.mode="CAMPAIGN";check(Progression.FindMission("STORY1").count(report)==1,"Story mission");
        check(Progression.FindMission("VARIETY2").count(report)==1,"New ship win counts");
        report.newShipWin=false;check(Progression.FindMission("VARIETY2").count(report)==0,"Repeated ship win ignored");
        check(Progression.FindMission("PLAY2").goal==2&&Progression.DailyPool.Select(m=>m.id).Distinct().Count()==Progression.DailyPool.Length,"Unique missions");
        var progress=new PlayerProgress();
        check(!Progression.RegisterDailyShipWin(progress,0,false)&&Progression.RegisterDailyShipWin(progress,0,true),"Only wins register ships");
        check(!Progression.RegisterDailyShipWin(progress,0,true)&&Progression.RegisterDailyShipWin(progress,1,true)&&progress.dailyWinShips.Count==2,"Different ship wins persist once");
        for(int day=20261001;day<=20261031;day++)
        {
            var set=Progression.BuildDailySet(day);
            check(set.Count==3&&set[0].id=="PLAY2"&&set.Select(m=>m.id).Distinct().Count()==3,"Daily easy mission and distinct extras");
            check(set.Select(m=>m.id).SequenceEqual(Progression.BuildDailySet(day).Select(m=>m.id)),"Same day same missions");
        }
        var profile=ControlLayout.Defaults();check(ControlLayout.Valid(profile),"Default controls valid");
        check(ControlLayout.Valid(JsonUtility.FromJson<ControlLayout.Profile>(JsonUtility.ToJson(profile))),"Layout serialization");
        profile.slots[0].position=new Vector2(float.NaN,0);check(!ControlLayout.Valid(profile),"Corrupt layout rejected");
        profile=ControlLayout.Defaults();profile.slots[1].scale=99;check(!ControlLayout.Valid(profile),"Oversize rejected");
        Vector2 corner=ControlLayout.ClampPosition(Vector2.one,new Vector2(1280,720),new Vector2(150,150));
        check(corner.x<.9f&&corner.y<.8f,"Buttons stay inside edges");
        Preview(check);
        Debug.Log("CONTROLS_STORY_MISSIONS_PASS: "+checks+" checks. No profile writes.");
    }
    private static void Preview(Action<bool,string> check)
    {
        var scene=EditorSceneManager.NewPreviewScene();RenderTexture target=null;
        RenderTexture previous=RenderTexture.active;GameObject root=null;
        try
        {
            Directory.CreateDirectory("Builds/ControlsStoryMissionsPreviews");
            ControlLayoutEditor.Show();root=GameObject.Find("ControlLayoutEditor");
            SceneManager.MoveGameObjectToScene(root,scene);
            check(root.GetComponentsInChildren<UIJoystick>(true).Length==0&&root.GetComponentsInChildren<UIButton>(true).Length==0,"Preview cannot fire or move");
            check(root.GetComponentsInChildren<ControlLayoutDrag>(true).Length==3,"Three drag controls");
            check(root.transform.Find("Save").GetComponent<Button>()!=null&&root.transform.Find("Cancel").GetComponent<Button>()!=null,"Save and cancel available");
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            root.GetComponent<CanvasScaler>().enabled=false;root.GetComponent<RectTransform>().sizeDelta=new Vector2(1280,720);
            root.transform.position=Vector3.zero;root.transform.localScale=Vector3.one;
            var cameraObject=new GameObject("Camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.GetComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
            camera.enabled=false;camera.orthographic=true;camera.orthographicSize=360;camera.transform.position=new Vector3(0,0,-100);camera.farClipPlane=200;
            target=new RenderTexture(1600,900,24);target.Create();camera.targetTexture=target;canvas.worldCamera=camera;
            Canvas.ForceUpdateCanvases();
            foreach(var ring in root.GetComponentsInChildren<ControlRingGraphic>())
            {
                ring.SetAllDirty();ring.Rebuild(CanvasUpdate.PreRender);
                var mesh=ring.canvasRenderer.GetMesh();check(mesh!=null&&mesh.vertexCount>0,"Preview ring geometry");
            }
            camera.Render();RenderTexture.active=target;
            var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
            File.WriteAllBytes("Builds/ControlsStoryMissionsPreviews/controls.png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }
        finally
        {
            RenderTexture.active=previous;if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
