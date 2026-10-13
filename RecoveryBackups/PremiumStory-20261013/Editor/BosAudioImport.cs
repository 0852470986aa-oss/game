// BosAudioImport.cs (Editor เท่านั้น) — ตั้งค่านำเข้าไฟล์เสียงชุดใหม่ใน Assets/Resources/Audio/BOS/ ให้อัตโนมัติ (FeatureFlags.NewSounds)
// - เพลง (BGM_*): Streaming = อ่านจากไฟล์ทีละนิดระหว่างเล่น ไม่โหลดเพลงยาว 2 นาทีทั้งก้อนเข้าหน่วยความจำ (สำคัญบนมือถือ)
// - เสียงเอฟเฟกต์ (SFX_*): Decompress On Load + ADPCM = เล่นทันทีไม่สะดุด ใช้ CPU น้อย เหมาะกับเสียงสั้นที่เล่นถี่
// - เปิด Unity/คอมไพล์เสร็จ: ตรวจไฟล์ในโฟลเดอร์ ถ้าตั้งค่าไม่ตรง (เช่น นำเข้าก่อนสคริปต์นี้คอมไพล์) จะนำเข้าใหม่ให้ 1 ครั้ง
// - ทำเองได้ที่เมนู Tools > BOS > Reimport New Sounds / วางไฟล์ชื่อเดิมทับเพื่อเปลี่ยนเสียงได้เลย
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ตัวตั้งค่านำเข้าเสียงชุด BOS (Unity เรียกทุกครั้งที่นำเข้าไฟล์เสียง)
public class BosAudioImport : AssetPostprocessor
{
    private const string Folder = "Assets/Resources/Audio/BOS"; // โฟลเดอร์ไฟล์เสียงชุดใหม่ที่จะตั้งค่านำเข้าอัตโนมัติ

    // เลขรุ่นของกฎนำเข้า: เปลี่ยนเลขเมื่อแก้กฎ แล้ว Unity จะนำเข้าไฟล์เสียงใหม่ตามกฎล่าสุด
    public override uint GetVersion() => 1;

    // ก่อนนำเข้าไฟล์เสียง: ถ้าอยู่ในโฟลเดอร์ชุดใหม่ ใส่ค่าตามประเภท
    private void OnPreprocessAudio()
    {
        if (!InPack(assetPath)) return;
        Apply((AudioImporter)assetImporter, assetPath);
    }

    // ไฟล์อยู่ในโฟลเดอร์ชุดใหม่หรือไม่
    private static bool InPack(string path) => path.Replace('\\', '/').StartsWith(Folder + "/");

    // true = ไฟล์เพลง (ชื่อขึ้นต้น BGM_)
    private static bool IsMusic(string path) => System.IO.Path.GetFileName(path).StartsWith("BGM_");

    // ใส่ค่าที่ต้องการลง importer คืน true ถ้ามีค่าที่เปลี่ยน
    private static bool Apply(AudioImporter importer, string path)
    {
        bool music = IsMusic(path);
        var settings = importer.defaultSampleSettings;
        var wantLoad = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        var wantFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
        float wantQuality = music ? .7f : 1f;
        bool changed = settings.loadType != wantLoad || settings.compressionFormat != wantFormat
            || Mathf.Abs(settings.quality - wantQuality) > .001f || settings.preloadAudioData == music
            || importer.loadInBackground != music;
        settings.loadType = wantLoad;
        settings.compressionFormat = wantFormat;
        settings.quality = wantQuality;
        settings.preloadAudioData = !music;
        importer.defaultSampleSettings = settings;
        importer.loadInBackground = music;
        return changed;
    }

    // เปิด Unity/คอมไพล์เสร็จ: รอ Editor พร้อมแล้วตรวจไฟล์ในโฟลเดอร์ 1 ครั้งต่อการเปิด Unity
    [InitializeOnLoadMethod]
    private static void CheckOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetBool("BOS_AudioChecked", false)) return;
            SessionState.SetBool("BOS_AudioChecked", true);
            Fix(false);
        };
    }

    // เมนู Tools > BOS > Reimport New Sounds: นำเข้าเสียงชุดใหม่ทั้งหมดอีกครั้งตามกฎข้างบน
    [MenuItem("Tools/BOS/Reimport New Sounds")]
    private static void ReimportAll() => Fix(true);

    // ไล่ไฟล์เสียงในโฟลเดอร์: แก้ค่าที่ไม่ตรงแล้วนำเข้าใหม่ (all = นำเข้าใหม่ทุกไฟล์)
    private static void Fix(bool all)
    {
        if (!AssetDatabase.IsValidFolder(Folder)) return;
        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is AudioImporter importer && (Apply(importer, path) || all))
                paths.Add(path);
        }
        foreach (string path in paths) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (paths.Count > 0) Debug.Log("BOS audio: re-imported " + paths.Count + " sound file(s) with game settings.");
    }
}
