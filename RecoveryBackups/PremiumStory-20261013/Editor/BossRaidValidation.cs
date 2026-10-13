// ตรวจค่ากติกาแบบไม่เข้าเกม ไม่ล็อกอิน และไม่เขียนโปรไฟล์ผู้เล่น
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Photon.Realtime;

public static class BossRaidValidation
{
    [MenuItem("Tools/Battlefield/Validate Boss Co-op Rules")]
    public static void Run()
    {
        int checks = 0;
        Action<bool, string> check = (ok, name) => { if (!ok) throw new Exception("Boss Raid: " + name); checks++; };
        check(MatchRules.ModeCampaign == 5 && MatchRules.ModeBossRaid == 6, "append mode without changing existing ids");
        check(MatchRules.ModeTitles.Length == 7, "mode title count");
        check(BossRaidRules.Phase(100, 100) == 1, "phase 1");
        check(BossRaidRules.Phase(66, 100) == 2, "66% threshold");
        check(BossRaidRules.Phase(33, 100) == 3, "33% threshold");
        check(BossRaidRules.HealthMultiplier(2) < BossRaidRules.HealthMultiplier(4), "HP scales with team");
        check(BossRaidRules.HealthMultiplier(10) == BossRaidRules.HealthMultiplier(4), "HP upper bound");
        check(BossRaidRules.Reward(false) < BossRaidRules.Reward(true), "loss reward smaller");
        var premium = new PremiumData();
        check(!BossRaidRules.TryGrantTicket(premium, "round-a", false) && premium.tickets == 0, "no bonus on ineligible result");
        check(!BossRaidRules.TryGrantTicket(premium, "", true), "no bonus without round token");
        check(BossRaidRules.TryGrantTicket(premium, "round-a", true) && premium.tickets == 1, "one ticket on eligible win");
        check(!BossRaidRules.TryGrantTicket(premium, "round-a", true) && premium.tickets == 1, "duplicate result does not duplicate ticket");
        check(BossRaidRules.TryGrantTicket(premium, "round-b", true) && premium.tickets == 2, "next round may earn a new ticket");
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        foreach (string field in new[] { "SoloModeBots", "SoloModeTeams", "SoloModeGame", "SoloModeLabels", "SoloModeKills", "SoloModeSeconds" })
        {
            var array = (Array)typeof(LobbyManager).GetField(field, flags).GetValue(null);
            check(array.Length == BossRaidRules.SoloChoice + 1, "solo array " + field);
        }
        var bots = (int[])typeof(LobbyManager).GetField("SoloModeBots", flags).GetValue(null);
        check(bots[BossRaidRules.SoloChoice] == 3, "three allied bots offline");
        bool gameModes = FeatureFlags.GameModes;
        try
        {
            FeatureFlags.GameModes = true;
            var room = new Room("rules-only", new RoomOptions());
            room.CustomProperties[MatchRules.GameModeKey] = MatchRules.ModeBossRaid;
            check(MatchRules.IsCoop(room) && MatchRules.UsesTeams(room), "raid cooperative team");
            check(MatchRules.Lives(room) == 3, "raid lives");
            room.CustomProperties[MatchRules.GameModeKey] = MatchRules.ModeDeathmatch;
            check(!MatchRules.IsCoop(room) && MatchRules.Lives(room) == 0, "deathmatch unchanged");
            room.CustomProperties[MatchRules.GameModeKey] = MatchRules.ModeSurvival;
            check(MatchRules.IsCoop(room) && MatchRules.Lives(room) == 3, "survival unchanged");
            room.CustomProperties[MatchRules.GameModeKey] = MatchRules.ModeRoyale;
            check(!MatchRules.IsCoop(room) && MatchRules.Lives(room) == 1, "royale unchanged");
        }
        finally { FeatureFlags.GameModes = gameModes; }
        Debug.Log("Boss Raid validation passed: " + checks + " checks. No profile data written.");
    }
}
