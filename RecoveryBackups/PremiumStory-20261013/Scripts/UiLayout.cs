// UiLayout.cs — บันทึกตำแหน่ง/ขนาด UI ที่จัดเองใน Unity แล้วให้เกมใช้ตำแหน่งนั้นทุกครั้ง (FeatureFlags.SavedUiLayout)
// ปัญหาเดิม: UI ส่วนใหญ่โค้ดสร้าง/จัดตำแหน่งตอนรัน (หน้าเลือกโหมด หน้าเตรียมพร้อมรบ โรงเก็บยาน ฯลฯ) ลากแก้แล้วกด Play ใหม่ก็กลับที่เดิม
// วิธีใช้:
//   1) กด Play ไปหน้าที่อยากแก้ เลือกวัตถุ UI ใน Hierarchy แล้วลาก/ปรับขนาดใน Scene หรือ Inspector (เลือกได้หลายชิ้น)
//   2) คลิกขวาที่วัตถุนั้นใน Hierarchy > UI Layout > Save Position  (ทำก่อนกดหยุด Play)
//   3) ค่าถูกเขียนลง Assets/Resources/UILayoutOverrides.json — ครั้งต่อไปโค้ดจัดตำแหน่งเสร็จจะใช้ค่าที่บันทึกแทน
//   อยากกลับไปใช้ตำแหน่งจากโค้ด: คลิกขวา > UI Layout > Use Code Position Again
// การทำงาน: ทุกครั้งที่โค้ดวาง UI ชิ้นหนึ่ง (Placed) จะดูว่ามีค่าที่บันทึกไว้ของ "เส้นทาง" วัตถุนั้นไหม ถ้ามีก็ใช้ค่านั้น
// และ ApplyAll() ไล่ใส่ค่าที่บันทึกไว้ทั้งหมดอีกรอบหลังสร้างหน้าจอเสร็จ (สำหรับของที่โค้ดไม่ได้ย้าย)
// ไม่มีไฟล์บันทึก = ทำงานเหมือนเดิมทุกอย่าง
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// ตัวจัดการตำแหน่ง UI ที่บันทึกไว้ (static ใช้ได้ทุกฉาก)
public static class UiLayout
{
    // ค่าที่บันทึกของ UI 1 ชิ้น (path = "ชื่อฉาก:ชื่อวัตถุ/ลูก/หลาน")
    [System.Serializable]
    private class Entry
    {
        public string path; // ที่อยู่ของ UI ในรูป "ชื่อฉาก:ชื่อวัตถุ/ลูก" ใช้หาวัตถุตอนโหลด
        public Vector2 position, size, anchorMin, anchorMax, pivot; // ค่า RectTransform ที่บันทึก: ตำแหน่ง ขนาด anchor และ pivot
        public float scale = 1f; // สเกลของ UI ชิ้นนั้น (1 = ขนาดเดิม)
    }
    // รูปแบบไฟล์ JSON (JsonUtility อ่าน List ตรง ๆ ไม่ได้ จึงห่อไว้)
    [System.Serializable]
    private class EntryList { public List<Entry> items = new List<Entry>(); }

    // ชื่อไฟล์ใน Resources และที่อยู่ไฟล์จริงในโปรเจกต์
    private const string ResourceName = "UILayoutOverrides";
    private const string AssetPath = "Assets/Resources/UILayoutOverrides.json";
    // ค่าที่โหลดแล้ว (null = ยังไม่โหลด)
    private static Dictionary<string, Entry> saved;

    // โหลดไฟล์ครั้งแรกที่ใช้ (ไม่มีไฟล์ = ว่าง)
    private static Dictionary<string, Entry> Saved
    {
        get
        {
            if (saved != null) return saved;
            saved = new Dictionary<string, Entry>();
            var file = Resources.Load<TextAsset>(ResourceName);
            if (file != null && !string.IsNullOrEmpty(file.text))
            {
                var list = JsonUtility.FromJson<EntryList>(file.text);
                if (list != null && list.items != null)
                    foreach (var entry in list.items)
                        if (entry != null && !string.IsNullOrEmpty(entry.path)) saved[entry.path] = entry;
            }
            return saved;
        }
    }

    // เรียกหลังโค้ดวาง/สร้าง UI ชิ้นนี้เสร็จ: มีค่าที่บันทึกไว้ = ใช้ค่านั้นแทน
    public static void Placed(Transform item)
    {
        if (!FeatureFlags.SavedUiLayout) return;
        var rect = item as RectTransform;
        if (rect == null || Saved.Count == 0) return; // ไม่มีอะไรบันทึกไว้ = ไม่ต้องคำนวณเส้นทาง
        if (Saved.TryGetValue(PathOf(rect), out Entry entry)) Apply(rect, entry);
    }

    // ใส่ค่าที่บันทึกไว้ทั้งหมดของฉากที่เปิดอยู่ (เรียกหลังสร้างหน้าจอเสร็จ)
    public static void ApplyAll()
    {
        if (!FeatureFlags.SavedUiLayout || Saved.Count == 0) return;
        foreach (var entry in Saved.Values)
        {
            var rect = Find(entry.path);
            if (rect != null) Apply(rect, entry);
        }
    }

    // เขียนค่าที่บันทึกลง RectTransform
    private static void Apply(RectTransform rect, Entry entry)
    {
        rect.anchorMin = entry.anchorMin;
        rect.anchorMax = entry.anchorMax;
        rect.pivot = entry.pivot;
        rect.sizeDelta = entry.size;
        rect.anchoredPosition = entry.position;
        rect.localScale = new Vector3(entry.scale, entry.scale, rect.localScale.z);
    }

    // ===== เส้นทางของวัตถุ (ใช้เป็นชื่อเรียกในไฟล์) =====
    // "ชื่อฉาก:Root/Child/Child" ชื่อซ้ำในพ่อเดียวกันต่อท้าย #ลำดับ เช่น Item#2
    private static string PathOf(Transform item)
    {
        var parts = new List<string>();
        for (var t = item; t != null; t = t.parent) parts.Add(Segment(t));
        parts.Reverse();
        return item.gameObject.scene.name + ":" + string.Join("/", parts);
    }

    // ชื่อ 1 ชั้นของเส้นทาง
    private static string Segment(Transform t)
    {
        string name = CleanName(t.name);
        // ชื่อที่มีเลขรันต่อท้าย (เช่น หน้าโรงเก็บยาน WPage12) ไม่นับลำดับ เพราะหน้าเก่ากับหน้าใหม่อยู่ซ้อนกันชั่วคราว
        if (t.parent == null || name != t.name) return name;
        int index = 0;
        for (int i = 0; i < t.GetSiblingIndex(); i++)
            if (t.parent.GetChild(i).name == t.name) index++;
        return index == 0 ? name : name + "#" + index;
    }

    // ชื่อที่โค้ดใส่เลขรันต่อท้ายทุกครั้งที่วาดใหม่: ตัดเลขออก (WPage12 -> WPage)
    private static string CleanName(string name)
    {
        if (name.StartsWith("WPage"))
        {
            int end = name.Length;
            while (end > 5 && char.IsDigit(name[end - 1])) end--;
            if (end == 5) return "WPage";
        }
        return name;
    }

    // หาวัตถุจากเส้นทาง (รวมวัตถุที่ปิดอยู่) ในฉากที่เปิดอยู่ ไม่เจอคืน null
    private static RectTransform Find(string path)
    {
        int colon = path.IndexOf(':');
        if (colon < 0) return null;
        var scene = SceneManager.GetSceneByName(path.Substring(0, colon));
        if (!scene.IsValid() || !scene.isLoaded) return null;
        string[] parts = path.Substring(colon + 1).Split('/');
        Transform current = null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == parts[0]) { current = root.transform; break; }
        for (int i = 1; i < parts.Length && current != null; i++) current = Child(current, parts[i]);
        return current as RectTransform;
    }

    // หาลูกตามชื่อ 1 ชั้น ("ชื่อ" หรือ "ชื่อ#ลำดับ"; ชื่อที่ตัดเลขรันแล้วเอาตัวล่าสุด)
    private static Transform Child(Transform parent, string segment)
    {
        string name = segment;
        int wanted = 0, hash = segment.LastIndexOf('#');
        if (hash > 0 && int.TryParse(segment.Substring(hash + 1), out int n)) { name = segment.Substring(0, hash); wanted = n; }
        Transform found = null;
        int count = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (CleanName(child.name) != name) continue;
            if (child.name != name) { found = child; continue; } // ชื่อมีเลขรัน: เอาตัวท้ายสุด (หน้าที่วาดล่าสุด)
            if (count++ == wanted) return child;
        }
        return found;
    }

// ส่วนนี้คอมไพล์เฉพาะใน Unity Editor: เมนูคลิกขวาใน Hierarchy (ใช้ได้ทั้งตอนกด Play และหน้า Edit)
#if UNITY_EDITOR
    // กันเมนูทำงานซ้ำ (Unity เรียกเมนูครั้งละ 1 ชิ้นที่เลือก แต่เราจัดการทุกชิ้นที่เลือกในครั้งแรกแล้ว)
    private static double lastMenuTime;

    // คลิกขวา > UI Layout > Save Position: บันทึกตำแหน่ง/ขนาดปัจจุบันของทุกชิ้นที่เลือก
    [UnityEditor.MenuItem("GameObject/UI Layout/Save Position", false, 10)]
    private static void SaveSelected()
    {
        if (UnityEditor.EditorApplication.timeSinceStartup - lastMenuTime < .3) return;
        lastMenuTime = UnityEditor.EditorApplication.timeSinceStartup;
        int count = 0;
        foreach (Transform item in UnityEditor.Selection.transforms)
        {
            var rect = item as RectTransform;
            if (rect == null) continue;
            string path = PathOf(rect);
            Saved[path] = new Entry
            {
                path = path, position = rect.anchoredPosition, size = rect.sizeDelta,
                anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, pivot = rect.pivot, scale = rect.localScale.x
            };
            count++;
        }
        if (count == 0) { Debug.LogWarning("UI Layout: select UI objects (RectTransform) in the Hierarchy first."); return; }
        WriteFile();
        Debug.Log("UI Layout: saved " + count + " position(s) to " + AssetPath);
    }

    // คลิกขวา > UI Layout > Use Code Position Again: ลบค่าที่บันทึกของชิ้นที่เลือก (กด Play ใหม่จะกลับไปใช้ตำแหน่งจากโค้ด)
    [UnityEditor.MenuItem("GameObject/UI Layout/Use Code Position Again", false, 11)]
    private static void ForgetSelected()
    {
        if (UnityEditor.EditorApplication.timeSinceStartup - lastMenuTime < .3) return;
        lastMenuTime = UnityEditor.EditorApplication.timeSinceStartup;
        int count = 0;
        foreach (Transform item in UnityEditor.Selection.transforms)
            if (item is RectTransform rect && Saved.Remove(PathOf(rect))) count++;
        WriteFile();
        Debug.Log("UI Layout: removed " + count + " saved position(s). Press Play again to see the code layout.");
    }

    // คลิกขวา > UI Layout > Clear ALL Saved Positions: ลบค่าที่บันทึกทั้งหมด (ถามยืนยันก่อน)
    [UnityEditor.MenuItem("GameObject/UI Layout/Clear ALL Saved Positions", false, 12)]
    private static void ClearAll()
    {
        if (UnityEditor.EditorApplication.timeSinceStartup - lastMenuTime < .3) return;
        lastMenuTime = UnityEditor.EditorApplication.timeSinceStartup;
        if (!UnityEditor.EditorUtility.DisplayDialog("UI Layout", "Remove ALL saved UI positions?", "Remove", "Cancel")) return;
        Saved.Clear();
        WriteFile();
    }

    // เขียนค่าทั้งหมดลงไฟล์ JSON ใน Assets/Resources แล้วให้ Unity นำเข้าใหม่ (ทำได้ระหว่าง Play)
    private static void WriteFile()
    {
        var list = new EntryList();
        list.items.AddRange(Saved.Values);
        list.items.Sort((a, b) => string.CompareOrdinal(a.path, b.path));
        System.IO.Directory.CreateDirectory("Assets/Resources");
        System.IO.File.WriteAllText(AssetPath, JsonUtility.ToJson(list, true));
        UnityEditor.AssetDatabase.ImportAsset(AssetPath);
    }
#endif
}
