using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Photon.Pun;
using Photon.Realtime;
using Hashtable=ExitGames.Client.Photon.Hashtable;

// เล่นในสำเนาทดสอบ ใช้โปรไฟล์ในหน่วยความจำ ตรวจคืนค่า/ยกเลิกโดยไม่บันทึก PlayerPrefs
[InitializeOnLoad]
public static class ControlLayoutSmokeTest
{
    private const string Key="ControlLayoutSmoke.Running";
    private static int step;
    private static double deadline;
    private static string original;
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static ControlLayoutSmokeTest()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){step=0;deadline=EditorApplication.timeSinceStartup+90;}};
    }
    public static void Run()
    {
        if(!Application.isBatchMode)throw new Exception("Batch only");
        ControlsStoryMissionsValidation.Run();
        SessionState.SetBool(Key,true);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;
    }
    private static void Check(bool pass,string message){if(!pass)throw new Exception(message);Debug.Log("CONTROL_SMOKE_PASS: "+message);}
    private static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout "+step);
            if(step==0)
            {
                PhotonNetwork.OfflineMode=true;
                PhotonNetwork.CreateRoom("CONTROL-SMOKE",new RoomOptions{MaxPlayers=1,CustomRoomProperties=new Hashtable{
                    [MatchRules.GameModeKey]=MatchRules.ModeCampaign,[MatchRules.CampaignStageKey]=1,[MatchRules.SoloKey]=true,
                    [MatchRules.HumansKey]=1,[MatchRules.BotCountKey]=0,["MapIndex"]=0,["StartTime"]=-1d,["BattleToken"]="control-smoke"}});
                step=1;return;
            }
            if(step==1){if(!PhotonNetwork.InRoom)return;SceneManager.LoadScene("SampleScene");step=2;return;}
            var gm=GameplayManager.Instance;if(gm==null||gm.localPlayer==null)return;
            if(FirebaseManager.Instance!=null)throw new Exception("Unexpected account manager");gm.localPlayer.maxHp=gm.localPlayer.currentHp=100000;
            typeof(GameplayManager).GetField("progressReported",Hidden).SetValue(gm,true);
            if(step==2)
            {
                if(!gm.MatchInputAllowed)return;
                original=PlayerPrefs.GetString(ControlLayout.Key,"");
                var profile=ControlLayout.Defaults();profile.slots[0].position=new Vector2(.4f,.35f);profile.slots[0].scale=1.3f;
                var control=(RectTransform)gm.joystick.transform;
                var method=typeof(GameplayManager).GetMethod("LayoutEditableControl",Hidden);
                var basePosition=control.anchoredPosition;var baseMin=control.anchorMin;var baseScale=control.localScale;
                method.Invoke(gm,new object[]{control,0,profile,1f,false});
                Check(control.anchorMin==profile.slots[0].position&&Mathf.Approximately(control.localScale.x,1.3f),"Custom position and size applied to actual joystick");
                method.Invoke(gm,new object[]{control,0,null,GameSettings.ButtonScale,GameSettings.LeftHanded});
                Check(control.anchorMin==baseMin&&control.anchoredPosition==basePosition&&control.localScale==baseScale,"Default layout restored without accumulating transforms");
                ControlLayoutEditor.Show();step=3;return;
            }
            if(step==3)
            {
                Canvas.ForceUpdateCanvases();
                Check(!gm.MatchInputAllowed,"Editing blocks ship input");
                var editor=UnityEngine.Object.FindFirstObjectByType<ControlLayoutEditor>();
                Check(editor!=null&&editor.GetComponentsInChildren<ControlLayoutDrag>().Length==3,"Runtime editor has three draggable controls");
                foreach(var ring in editor.GetComponentsInChildren<ControlRingGraphic>())
                {var mesh=ring.canvasRenderer.GetMesh();Check(mesh!=null&&mesh.vertexCount>0,"Runtime control ring rendered");}
                var area=(RectTransform)editor.transform.Find("SafeArea");
                Vector3 goal=area.TransformPoint(new Vector3((.3f-.5f)*area.rect.width,(.4f-.5f)*area.rect.height,0));
                var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,goal),pointerId=-1};
                var drag=area.Find(ControlLayout.Names[0]).GetComponent<ControlLayoutDrag>();drag.OnPointerDown(pointer);drag.OnDrag(pointer);
                var moved=(RectTransform)drag.transform;
                Check(Vector2.Distance(moved.anchorMin,new Vector2(.3f,.4f))<.01f,"Actual pointer drag moves selected control");
                editor.transform.Find("Reset").GetComponent<Button>().onClick.Invoke();
                editor.transform.Find("Cancel").GetComponent<Button>().onClick.Invoke();step=4;return;
            }
            Check(!ControlLayoutEditor.IsOpen,"Cancel closes editor");
            Check(PlayerPrefs.GetString(ControlLayout.Key,"")==original,"Reset then cancel preserves saved layout");
            SessionState.SetBool(Key,false);Debug.Log("CONTROL_SMOKE_PASS: All control checks passed; no saved layout or account changed.");EditorApplication.Exit(0);
        }
        catch(Exception error){SessionState.SetBool(Key,false);Debug.LogError("CONTROL_SMOKE_FAIL: "+error);EditorApplication.Exit(1);}
    }
}
