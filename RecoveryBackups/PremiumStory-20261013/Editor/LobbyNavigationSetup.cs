using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Rebuilds the authored Lobby UI and checks repeated builds preserve layout.
public static class LobbyNavigationSetup
{
    // เมนู Editor: สำรองฉากล็อบบี้ สร้าง UI ใหม่ ตรวจว่าสร้างซ้ำไม่พัง แล้วบันทึกฉาก
    [MenuItem("Tools/Battlefield/Build Lobby Navigation")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play Mode before building Lobby navigation.");
            return;
        }
        const string scenePath = "Assets/Scenes/LobbyScene.unity"; // path ฉากล็อบบี้ที่จะสร้าง UI และสำรองไฟล์
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string backupDirectory = "RecoveryBackups/Navigation-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backupDirectory);
        File.Copy(scenePath, Path.Combine(backupDirectory, "LobbyScene.unity"));
        var scene = EditorSceneManager.OpenScene(scenePath);
        var manager = UnityEngine.Object.FindFirstObjectByType<LobbyManager>();
        if (manager == null) throw new InvalidOperationException("LobbyManager missing from scene.");
        var build = typeof(LobbyManager).GetMethod("BuildEditableLobbyScreens", BindingFlags.Instance | BindingFlags.NonPublic);
        if (build == null) throw new InvalidOperationException("Lobby UI builder missing.");
        build.Invoke(manager, null);
        var root = manager.mainPanel.transform.Find("LobbySurface");
        var quick = (RectTransform)root.Find("QuickMatch");
        var originalPosition = quick.anchoredPosition;
        int canvasCount = manager.mainPanel.transform.root.GetComponentsInChildren<Button>(true).Length;
        quick.anchoredPosition += new Vector2(7, 0);
        build.Invoke(manager, null);
        if (quick.anchoredPosition != originalPosition + new Vector2(7, 0))
            throw new InvalidOperationException("Rebuild overwrote authored Quick Match position.");
        quick.anchoredPosition = originalPosition;
        if (canvasCount != manager.mainPanel.transform.root.GetComponentsInChildren<Button>(true).Length)
            throw new InvalidOperationException("Repeated UI build created duplicate buttons.");
        if (root.Find("PlayMenu/CreateJoin") == null || root.Find("PlayMenu/Ranked") == null)
            throw new InvalidOperationException("Play menu is missing its online actions.");
        if (manager.waitingRoomPanel.transform.Find("LobbySurface/RoomRulesMenu/RoomKills") == null)
            throw new InvalidOperationException("Room rules were not migrated.");
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Lobby scene could not be saved.");
        Debug.Log("NAVIGATION_CHECK_PASSED: saved Lobby scene; repeat build does not duplicate buttons; authored position preserved. Backup: " + backupDirectory);
    }
}
