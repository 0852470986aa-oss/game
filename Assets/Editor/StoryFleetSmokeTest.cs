using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Batch-only: verifies all stages without finishing reward screens or saving player progress.
[InitializeOnLoad]
public static class StoryFleetSmokeTest
{
    private const string Key = "StoryFleetSmoke.Running";
    private static int step, stage;
    private static double deadline;
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static StoryFleetSmokeTest()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            { step = 0; stage = 1; deadline = EditorApplication.timeSinceStartup + 240; }
        };
        EditorApplication.update += Tick;
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Batch only");
        SessionState.SetBool(Key, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
    private static void Check(bool pass, string message)
    { if (!pass) throw new Exception(message); Debug.Log("STORY_SMOKE_PASS: " + message); }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout stage " + stage + " step " + step);
            var gm = GameplayManager.Instance;
            if (step == 0)
            {
                PhotonNetwork.OfflineMode = true;
                PhotonNetwork.CreateRoom("STORY-SMOKE", new RoomOptions { MaxPlayers = 1, CustomRoomProperties = new Hashtable {
                    [MatchRules.GameModeKey] = MatchRules.ModeCampaign, [MatchRules.SoloKey] = true,
                    [MatchRules.HumansKey] = 1, [MatchRules.BotCountKey] = 0,
                    [MatchRules.MatchSecondsKey] = 480, [MatchRules.CampaignStageKey] = 1,
                    ["MapIndex"] = 0, ["StartTime"] = -1d, ["BattleToken"] = "story-smoke-1"
                }}); step = 1; return;
            }
            if (step == 1)
            {
                if (!PhotonNetwork.InRoom) return;
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { ["ShipType"] = 0 });
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [MatchRules.TeamPrefix + PhotonNetwork.LocalPlayer.ActorNumber] = 0 });
                SceneManager.LoadScene("SampleScene"); step = 2; return;
            }
            if (gm == null || gm.localPlayer == null) return;
            CheckAccount(gm);
            var ships = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            var bots = ships.Where(s => s.IsBot).ToArray();
            var def = Campaign.Get(stage);
            if (bots.Length != def.bots + (def.boss ? 1 : 0)) return;
            if (bots.Any(b => b.GetComponent<SpriteRenderer>().sprite == null || !b.GetComponent<SpriteRenderer>().sprite.name.StartsWith("StoryFleet_"))) return;
            Check(GameplayManager.GetCurrentMapIndex() == def.MapIndex, "Map for stage " + stage);
            Check(bots.All(b => b.GetComponent<Collider2D>() != null), "Enemy artwork and colliders stage " + stage);
            if (def.boss)
            {
                var boss = bots.FirstOrDefault(b => b.IsBoss);
                Check(boss != null && boss.GetComponent<StoryBossFx>() != null, "Boss effects stage " + stage);
                Check(!gm.CanRespawn(boss), "Boss cannot respawn stage " + stage);
                // Run objective once while alive, then once dead, without allowing result/reward UI to run.
                typeof(GameplayManager).GetMethod("UpdateCampaign", Private).Invoke(gm, null);
                boss.isDead = true; boss.currentHp = 0;
                typeof(GameplayManager).GetMethod("UpdateCampaign", Private).Invoke(gm, null);
                Check(Equals(PhotonNetwork.CurrentRoom.CustomProperties["ModeResult"], 1), "Boss defeat wins stage " + stage);
            }
            if (stage >= Campaign.Count) { Finish(true, "All ten stages spawn; all three bosses complete objectives. No reward screen or account writes."); return; }
            stage++;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable {
                [MatchRules.CampaignStageKey] = stage, ["MapIndex"] = Campaign.Get(stage).MapIndex,
                ["ModeResult"] = null, ["StartTime"] = -1d, ["Starting"] = false,
                ["BattleToken"] = "story-smoke-" + stage });
            PhotonNetwork.DestroyAll();
            SceneManager.LoadScene("SampleScene");
        }
        catch (Exception ex) { Finish(false, ex.ToString()); }
    }
    private static void CheckAccount(GameplayManager gm)
    {
        if (FirebaseManager.Instance != null) throw new Exception("Unexpected account manager");
        typeof(GameplayManager).GetField("progressReported", Private).SetValue(gm, true);
        gm.localPlayer.maxHp = gm.localPlayer.currentHp = 100000;
    }
    private static void Finish(bool ok, string message)
    {
        SessionState.SetBool(Key, false);
        Debug.Log("STORY_SMOKE_" + (ok ? "PASS: " : "FAIL: ") + message);
        EditorApplication.Exit(ok ? 0 : 1);
    }
}
