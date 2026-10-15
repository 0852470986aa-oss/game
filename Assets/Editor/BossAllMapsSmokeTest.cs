using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using Hashtable=ExitGames.Client.Photon.Hashtable;

[InitializeOnLoad]
public static class BossAllMapsSmokeTest
{
    private const string Key="BossAllMaps.Running";
    private static int step,map;
    private static double deadline;
    private static GameplayManager previous;
    private const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static BossAllMapsSmokeTest()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){step=map=0;deadline=EditorApplication.timeSinceStartup+150;previous=null;}};
    }
    public static void Run()
    {
        if(!Application.isBatchMode)throw new Exception("Batch only");
        SessionState.SetBool(Key,true);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout map "+map);
            if(step==0)
            {
                PhotonNetwork.OfflineMode=true;PhotonNetwork.CreateRoom("BOSS-MAPS",new RoomOptions{MaxPlayers=1,CustomRoomProperties=new Hashtable{
                    [MatchRules.GameModeKey]=MatchRules.ModeBossRaid,[MatchRules.SoloKey]=true,[MatchRules.HumansKey]=1,
                    [MatchRules.BotCountKey]=3,[MatchRules.BotDifficultyKey]=2,[MatchRules.MatchSecondsKey]=480,
                    ["MapIndex"]=map,["StartTime"]=-1d,["BattleToken"]="boss-map-0"}});step=1;return;
            }
            if(step==1){if(!PhotonNetwork.InRoom)return;SceneManager.LoadScene("SampleScene");step=2;return;}
            var gm=GameplayManager.Instance;if(gm==null||gm==previous||gm.localPlayer==null)return;
            if(FirebaseManager.Instance!=null)throw new Exception("Unexpected account manager");
            gm.localPlayer.maxHp=gm.localPlayer.currentHp=100000;
            typeof(GameplayManager).GetField("progressReported",Hidden).SetValue(gm,true);FeatureFlags.SkinGacha=false;
            PlayerController boss=null;foreach(var ship in UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))if(ship.IsBoss)boss=ship;
            if(boss==null||boss.GetComponent<BossSpectacleFx>()==null)return;
            if(GameplayManager.GetCurrentMapIndex()!=map)throw new Exception("Wrong map");
            Debug.Log("BOSS_ALL_MAPS_PASS: enlarged boss spawned on map "+map+" with three allied bots");
            if(++map>=GameplayManager.MapCount){SessionState.SetBool(Key,false);Debug.Log("BOSS_ALL_MAPS_PASS: All five maps passed. No accounts or rewards written.");EditorApplication.Exit(0);return;}
            previous=gm;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable{
                ["MapIndex"]=map,["ModeResult"]=null,["StartTime"]=-1d,["Starting"]=false,
                [BossRaidRules.Spawned]=false,[BossRaidRules.PhaseKey]=null,[BossRaidRules.MinionsKey]=null,
                ["BattleToken"]="boss-map-"+map});
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable{["Deaths"]=0});
            PhotonNetwork.DestroyAll();SceneManager.LoadScene("SampleScene");
        }
        catch(Exception error){SessionState.SetBool(Key,false);Debug.LogError("BOSS_ALL_MAPS_FAIL: "+error);EditorApplication.Exit(1);}
    }
}
