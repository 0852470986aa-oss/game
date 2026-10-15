// Batch-only Play Mode smoke test: no Firebase login, no profile rewards, no scene saves.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

[InitializeOnLoad]
public static class BossRaidSmokeTest
{
    private const string Running = "BossRaidSmoke.Running";
    private static int step;
    private static double deadline;
    private static GameplayManager previous;
    private static string oldToken;
    private static int oldMap;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static BossRaidSmokeTest()
    {
        EditorApplication.playModeStateChanged += state => {
            if (!SessionState.GetBool(Running, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) { step = 0; deadline = EditorApplication.timeSinceStartup + 180; }
        };
        EditorApplication.update += Tick;
    }

    // Run via Unity -batchmode -executeMethod BossRaidSmokeTest.Run (without -quit).
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("This test is batch-only to protect open user scenes.");
        BossRaidValidation.Run();
        BossSpectacleValidation.Run();
        SessionState.SetBool(Running, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        Debug.Log("RAID_SMOKE_PASS: " + message);
    }

    private static PlayerController Boss() => UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).FirstOrDefault(s => s.IsBoss);
    private static bool FieldBool(GameplayManager gm, string name) => (bool)typeof(GameplayManager).GetField(name, Private).GetValue(gm);
    private static void SuppressRewards(GameplayManager gm)
    {
        Check(FirebaseManager.Instance == null, "No Firebase account in isolated test");
        typeof(GameplayManager).GetField("progressReported", Private).SetValue(gm, true);
        FeatureFlags.SkinGacha = false;
        if (gm.localPlayer != null) { gm.localPlayer.maxHp = 100000; gm.localPlayer.currentHp = 100000; }
    }

    private static void Replay(GameplayManager gm)
    {
        previous = gm;
        oldToken = PhotonNetwork.CurrentRoom.CustomProperties["BattleToken"] as string;
        oldMap = GameplayManager.GetCurrentMapIndex();
        typeof(GameplayManager).GetMethod("RequestSameRoom", Private).Invoke(gm, null);
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (deadline <= 0) deadline = EditorApplication.timeSinceStartup + 180;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout at smoke step " + step);
            var gm = GameplayManager.Instance;
            switch (step)
            {
                case 0:
                    PhotonNetwork.OfflineMode = true;
                    PhotonNetwork.CreateRoom("RAID-SMOKE", new RoomOptions { MaxPlayers = 1, CustomRoomProperties = new Hashtable {
                        [MatchRules.GameModeKey] = 6, [MatchRules.SoloKey] = true, [MatchRules.HumansKey] = 1,
                        [MatchRules.BotCountKey] = 3, [MatchRules.BotDifficultyKey] = 2,
                        [MatchRules.MatchSecondsKey] = 480, ["MapIndex"] = 2, ["StartTime"] = -1d, ["BattleToken"] = "smoke-first"
                    }});
                    step++; break;
                case 1:
                    if (!PhotonNetwork.InRoom) return;
                    PhotonNetwork.LocalPlayer.NickName = "SMOKE TEST";
                    PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { ["ShipType"] = 0 });
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [MatchRules.TeamPrefix + PhotonNetwork.LocalPlayer.ActorNumber] = 0 });
                    SceneManager.LoadScene("SampleScene"); step++; break;
                case 2:
                    if (gm == null || gm.localPlayer == null) return;
                    CheckOverdrive(gm.localPlayer);
                    SuppressRewards(gm); step++; break;
                case 3:
                    var boss = Boss();
                    if (boss == null) return;
                    Check(boss.GetComponent<BossRaidAppearance>() != null && boss.GetComponent<SpriteRenderer>().sprite.name == "DreadnoughtRaidVisual", "New boss art loaded");
                    Check(UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Count(s => s.IsBot && MatchRules.TeamOf(s.CombatantId) == 0) == 3, "Three allied bots");
                    Check(!gm.CanRespawn(boss), "Boss cannot respawn");
                    float expectedScale=GameplayManager.GetPrefab(BattleLoadoutCatalog.PrefabName(boss.ShipIndex)).transform.localScale.x*BossSpectacleFx.BossScale;
                    Check(boss.GetComponent<BossSpectacleFx>() != null && Mathf.Abs(boss.transform.localScale.x - expectedScale) < .01f, "Enlarged boss and spectacle effects loaded");
                    boss.currentHp = boss.maxHp * .5f; step++; break;
                case 4:
                    if (!Equals(PhotonNetwork.CurrentRoom.CustomProperties[BossRaidRules.MinionsKey], 2)) return;
                    Check(GameplayManager.GetCurrentMapIndex() == GameplayManager.BossArenaMapIndex, "Dedicated citadel selected");
                    Check(UnityEngine.Object.FindFirstObjectByType<BossCitadelVisuals>() != null, "Citadel scenery loaded");
                    CaptureCitadel(Boss());
                    Check(UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Count(s => s.IsBot && !s.IsBoss && MatchRules.TeamOf(s.CombatantId) == 1) == 2, "Phase 2 adds two minions");
                    Boss().currentHp = Boss().maxHp * .3f; step++; break;
                case 5:
                    if (!Equals(PhotonNetwork.CurrentRoom.CustomProperties[BossRaidRules.MinionsKey], 3)) return;
                    Check(UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Count(s => s.IsBot && !s.IsBoss && MatchRules.TeamOf(s.CombatantId) == 1) == 5, "Phase 3 adds three minions once");
                    Boss().isDead = true; Boss().currentHp = 0; step++; break;
                case 6:
                    if (!FieldBool(gm, "resultShown")) return;
                    Check(Equals(PhotonNetwork.CurrentRoom.CustomProperties["ModeResult"], 1), "Boss death produces victory screen");
                    Replay(gm); step++; break;
                case 7:
                case 10:
                    if (gm == null || gm == previous || gm.localPlayer == null) return;
                    SuppressRewards(gm);
                    Check((string)PhotonNetwork.CurrentRoom.CustomProperties["BattleToken"] != oldToken, "Replay creates new round token");
                    Check(GameplayManager.GetCurrentMapIndex() == GameplayManager.BossArenaMapIndex, "Replay stays in dedicated boss arena");
                    step++; break;
                case 8:
                    if (!FieldBool(gm, "matchStarted") || Boss() == null) return;
                    Check(!Equals(PhotonNetwork.CurrentRoom.CustomProperties["ModeResult"], 1), "Replay cleared prior victory");
                    PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { ["Deaths"] = 3 });
                    Check(!gm.CanRespawn(gm.localPlayer), "Human exhausted all three lives");
                    step++; break;
                case 9:
                    if (!FieldBool(gm, "resultShown")) return;
                    Check(Equals(PhotonNetwork.CurrentRoom.CustomProperties["ModeResult"], -1), "Life exhaustion produces defeat screen");
                    Replay(gm); step++; break;
                case 11:
                    if (!FieldBool(gm, "matchStarted") || Boss() == null) return;
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { ["StartTime"] = PhotonNetwork.Time - 1000d });
                    step++; break;
                case 12:
                    if (!FieldBool(gm, "resultShown")) return;
                    Check(Equals(PhotonNetwork.CurrentRoom.CustomProperties["ModeResult"], -1), "Timeout produces defeat screen");
                    Finish(true, "Spawn, art, allies, phases, victory, actual replay, life defeat, timeout defeat passed."); break;
            }
        }
        catch (Exception ex) { Finish(false, "Step " + step + ": " + ex); }
    }

    private static void Finish(bool passed, string detail)
    {
        SessionState.SetBool(Running, false);
        Directory.CreateDirectory("Temp");
        File.WriteAllText("Temp/BossRaidSmokeResult.txt", (passed ? "PASS" : "FAIL") + "\n" + detail);
        Debug.Log("RAID_SMOKE_" + (passed ? "PASS" : "FAIL") + ": " + detail);
        EditorApplication.Exit(passed ? 0 : 1);
    }
    private static void CaptureCitadel(PlayerController boss)
    {
        var fx=boss.GetComponent<BossWeaponSpectacle>();
        Check(fx != null, "Boss weapon effect pool created");
        int colliders=boss.GetComponentsInChildren<Collider2D>().Length;
        fx.Draw(boss.transform.position,Vector2.down,1.7f,1.25f,true);
        Check(colliders==boss.GetComponentsInChildren<Collider2D>().Length,"Weapon spectacle adds no damage colliders");
        var camera=Camera.main;
        if(camera==null)throw new Exception("Preview camera missing");
        var position=camera.transform.position;float size=camera.orthographicSize;
        var previous=camera.targetTexture;var active=RenderTexture.active;
        var target=new RenderTexture(1600,1000,24);
        try
        {
            target.Create();camera.targetTexture=target;
            camera.transform.position=new Vector3(0,0,position.z);camera.orthographicSize=40;
            camera.Render();RenderTexture.active=target;
            var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();
            Directory.CreateDirectory("Builds/BossCitadelPreviews");
            File.WriteAllBytes("Builds/BossCitadelPreviews/arena.png",pixels.EncodeToPNG());
            UnityEngine.Object.Destroy(pixels);
        }
        finally
        {
            camera.transform.position=position;camera.orthographicSize=size;camera.targetTexture=previous;
            RenderTexture.active=active;target.Release();UnityEngine.Object.Destroy(target);fx.Hide();
        }
    }
    private static void CheckOverdrive(PlayerController player)
    {
        Check(BattleLoadoutCatalog.ValidSkill(7)==7 && BattleLoadoutCatalog.Skills[7].cooldown==18,"Overdrive catalog and cooldown");
        Check(Resources.Load<Sprite>(BattleLoadoutCatalog.Skills[7].iconPath)!=null,"Overdrive icon available");
        var timer=typeof(PlayerController).GetField("overdriveUntil",Private);
        int oldSkill=player.skillType;float oldTimer=(float)timer.GetValue(player);
        try
        {
            player.skillType=7;
            typeof(PlayerController).GetMethod("UseSkill",Private).Invoke(player,null);
            Check(player.PowerFireFactor==.5f && player.GetComponent<OverdriveSkillFx>()!=null,"Overdrive RPC activates fire rate and visual");
            timer.SetValue(player,Time.time+8);
            float pickup=(float)timer.GetValue(player);
            typeof(PlayerController).GetMethod("UseSkill",Private).Invoke(player,null);
            Check((float)timer.GetValue(player)>=pickup && player.PowerFireFactor==.5f,"Pickup duration preserved and rate does not stack");
            timer.SetValue(player,Time.time-1);
            Check(player.PowerFireFactor==1,"Expired overdrive restores normal fire rate");
        }
        finally{player.skillType=oldSkill;timer.SetValue(player,oldTimer);}
    }
}
