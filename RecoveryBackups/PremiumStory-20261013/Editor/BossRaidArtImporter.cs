// จำกัดการตั้งค่านำเข้าไว้ที่ภาพบอสใหม่เท่านั้น ไม่เปลี่ยน texture ของยานเดิม
using UnityEditor;
using UnityEngine;

public sealed class BossRaidArtImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Images/Bosses/") && !assetPath.StartsWith("Assets/Resources/Images/Skins/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Compressed;
        if (assetPath.StartsWith("Assets/Resources/Images/Skins/"))
        {
            importer.isReadable = true; // หา alpha bounds ของ sprite แต่ละเซลล์
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
