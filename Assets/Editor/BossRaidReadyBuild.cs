// สร้างตัวเกมแยกโฟลเดอร์ ไม่แก้ Build Settings หรือเขียนทับ build เดิม
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BossRaidReadyBuild
{
    public static void WindowsPremiumStory()
    {
        PremiumStoryPreview.Run();
        BuildWindows("Builds/PremiumStory-20261014-Windows");
    }
    public static void Windows()
        => BuildWindows("Builds/BossCoop-20261013-Windows");

    public static void WindowsSkinsRanked()
    {
        VisualRankedValidation.Run();
        BuildWindows("Builds/SkinsRanked-20261013-Windows");
    }

    private static void BuildWindows(string folder)
    {
        BossRaidValidation.Run();
        if (File.Exists(Path.Combine(folder, "BattlefieldOfTheStars.exe")))
            throw new Exception("Build already exists; choose a new output folder instead of overwriting.");
        Directory.CreateDirectory(folder);
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length < 3 || !scenes[0].EndsWith("LoginScene.unity"))
            throw new Exception("Expected LoginScene first and all three game scenes enabled.");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = scenes, locationPathName = folder + "/BattlefieldOfTheStars.exe",
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
        });
        string result = report.summary.result + ": " + report.summary.totalErrors + " errors, "
            + report.summary.totalWarnings + " warnings, " + report.summary.totalSize + " bytes";
        File.WriteAllText("Temp/BossRaidBuildResult.txt", result);
        File.WriteAllText(Path.Combine(folder, "BuildResult.txt"), result);
        Debug.Log("RAID_BUILD: " + result);
        if (report.summary.result != BuildResult.Succeeded) throw new Exception(result);
    }
}
