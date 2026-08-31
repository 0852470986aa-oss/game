using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class MapLayoutSetup : EditorWindow
{
    private const string GameplayScenePath = "Assets/Scenes/SampleScene.unity";
    private const float TargetBackgroundHeight = 75f;

    private struct RockPlacement
    {
        public Vector2 position;
        public Vector2 collisionSize;
        public float rotation;

        public RockPlacement(float x, float y, float width, float height, float angle)
        {
            position = new Vector2(x, y);
            collisionSize = new Vector2(width, height);
            rotation = angle;
        }
    }

    private static int SpriteNumber(Sprite sprite)
    {
        if (sprite == null) return 0;
        int underscore = sprite.name.LastIndexOf('_');
        return underscore >= 0 && int.TryParse(sprite.name.Substring(underscore + 1), out int value) ? value : 0;
    }

    [MenuItem("Battlefield/Setup Map Layouts")]
    public static void SetupMaps()
    {
        // 1. ????? BackgroundMap ???????????
        GameObject oldBg = GameObject.Find("BackgroundMap");
        if (oldBg != null)
        {
            DestroyImmediate(oldBg);
        }

        // ?????????????? 3
        string[] mapNames = { "Map0_Layout", "Map1_Layout", "Map2_Layout" };
        string[] bgImages = { "Images/Map_ThunderJellyfish", "Images/Map_ObeliskPlains", "Images/Map_AncientMech" };

        for (int i = 0; i < 3; i++)
        {
            // ??????????????????????????????? ????????????????????
            GameObject mapLayout = GameObject.Find(mapNames[i]);
            if (mapLayout == null)
            {
                mapLayout = new GameObject(mapNames[i]);
            }
            
            // ???????????????????????????
            mapLayout.transform.position = Vector3.zero;
            mapLayout.transform.rotation = Quaternion.identity;
            mapLayout.transform.localScale = Vector3.one;

            // ?????????????????????????????
            Transform bgTransform = mapLayout.transform.Find("Background");
            GameObject bgObj;
            if (bgTransform == null)
            {
                bgObj = new GameObject("Background");
                bgObj.transform.SetParent(mapLayout.transform);
            }
            else
            {
                bgObj = bgTransform.gameObject;
            }

            // ?????? SpriteRenderer ?????????????
            SpriteRenderer sr = bgObj.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = bgObj.AddComponent<SpriteRenderer>();
            }

            // ?????????????????
            Sprite bgSprite = Resources.Load<Sprite>(bgImages[i]);
            if (bgSprite != null)
            {
                sr.sprite = bgSprite;
            }
            
            sr.sortingOrder = -10; // ??????????????
            
            // ????????????????????
            bgObj.transform.position = Vector3.zero;
            if (bgSprite != null)
            {
                float uniformScale = TargetBackgroundHeight / Mathf.Max(bgSprite.bounds.size.y, 0.01f);
                bgObj.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
            }

            // ???????????? ?????????? 2 ??????????????????????
            mapLayout.SetActive(i == 2);
        }

        Debug.Log("?? ???????????????????????????????????! ???????????????????????????????!");
    }

    [MenuItem("Battlefield/Setup Map 2 Full Arena Obstacles")]
    public static void SetupMap2RockObstacles()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != GameplayScenePath)
        {
            scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }

        GameObject mapLayout = GameObject.Find("Map2_Layout");
        if (mapLayout == null)
        {
            Debug.LogError("Map2_Layout was not found in " + GameplayScenePath);
            return;
        }

        Transform oldContainer = mapLayout.transform.Find("RockObstacles");
        if (oldContainer != null)
        {
            DestroyImmediate(oldContainer.gameObject);
        }

        GameObject container = new GameObject("RockObstacles");
        container.transform.SetParent(mapLayout.transform, false);

        Transform oldTurretContainer = mapLayout.transform.Find("TurretObstacles");
        if (oldTurretContainer != null)
        {
            DestroyImmediate(oldTurretContainer.gameObject);
        }

        Transform oldRedCoreContainer = mapLayout.transform.Find("RedCoreObstacles");
        if (oldRedCoreContainer != null)
        {
            DestroyImmediate(oldRedCoreContainer.gameObject);
        }

        Transform oldDecorationContainer = mapLayout.transform.Find("RockDecorations");
        if (oldDecorationContainer != null)
        {
            DestroyImmediate(oldDecorationContainer.gameObject);
        }

        Object[] loadedAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Images/Obs_Asteroids.png");
        List<Sprite> sprites = new List<Sprite>();
        foreach (Object asset in loadedAssets)
        {
            Sprite sprite = asset as Sprite;
            if (sprite != null && !sprite.name.EndsWith("_9") && !sprite.name.EndsWith("_10") && !sprite.name.EndsWith("_11"))
            {
                sprites.Add(sprite);
            }
        }

        if (sprites.Count == 0)
        {
            Debug.LogError("No asteroid sprites were found at Assets/Resources/Images/Obs_Asteroids.png");
            DestroyImmediate(container);
            return;
        }
        sprites.Sort((left, right) => SpriteNumber(left).CompareTo(SpriteNumber(right)));

        RockPlacement[] rocks =
        {
            new RockPlacement(-14f, 10f, 9f, 7f, -18f),
            new RockPlacement(14f, -10f, 9f, 7f, 162f),
            new RockPlacement(15f, 11f, 8f, 6f, 28f),
            new RockPlacement(-15f, -11f, 8f, 6f, 208f),
            new RockPlacement(-34f, 30f, 16f, 12f, 14f),
            new RockPlacement(34f, 30f, 16f, 12f, -14f),
            new RockPlacement(-34f, -30f, 16f, 12f, 24f),
            new RockPlacement(34f, -30f, 16f, 12f, -24f),
            new RockPlacement(-11f, 32f, 11f, 8f, 35f),
            new RockPlacement(11f, -32f, 11f, 8f, 215f),
            new RockPlacement(-36f, 6f, 10f, 8f, -12f),
            new RockPlacement(36f, -6f, 10f, 8f, 168f)
        };

        for (int i = 0; i < rocks.Length; i++)
        {
            RockPlacement placement = rocks[i];
            // Prefer the cracked/lava asteroid cuts so Map 2 matches the orange mech background.
            Sprite sprite = sprites[(i + 6) % sprites.Count];
            GameObject rock = new GameObject(string.Format("RockObstacle_{0:00}", i + 1));
            rock.transform.SetParent(container.transform, false);
            rock.transform.localPosition = new Vector3(placement.position.x, placement.position.y, 0f);
            rock.transform.localRotation = Quaternion.Euler(0f, 0f, placement.rotation);

            Vector2 spriteSize = sprite.bounds.size;
            float scale = Mathf.Min(
                placement.collisionSize.x / Mathf.Max(spriteSize.x, 0.01f),
                placement.collisionSize.y / Mathf.Max(spriteSize.y, 0.01f));
            rock.transform.localScale = new Vector3(scale, scale, 1f);

            SpriteRenderer renderer = rock.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -1;

            GameObject rim = new GameObject("LavaRim");
            rim.transform.SetParent(rock.transform, false);
            rim.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
            SpriteRenderer rimRenderer = rim.AddComponent<SpriteRenderer>();
            rimRenderer.sprite = sprite;
            rimRenderer.color = new Color(1f, 0.25f, 0.04f, 0.22f);
            rimRenderer.sortingOrder = -2;
        }

        // Small visual-only debris fills the full composition without making flight frustrating.
        GameObject decorationContainer = new GameObject("RockDecorations");
        decorationContainer.transform.SetParent(mapLayout.transform, false);
        RockPlacement[] decorations =
        {
            new RockPlacement(-27f, 23f, 3.8f, 3f, 18f),
            new RockPlacement(-18f, 27f, 2.4f, 2f, -24f),
            new RockPlacement(4f, 29f, 2.2f, 1.8f, 12f),
            new RockPlacement(18f, 25f, 3.5f, 2.8f, -32f),
            new RockPlacement(28f, 20f, 2.4f, 2f, 35f),
            new RockPlacement(-31f, 15f, 2.2f, 1.8f, -18f),
            new RockPlacement(-21f, 15f, 3.2f, 2.6f, 42f),
            new RockPlacement(-5f, 17f, 2f, 1.7f, -12f),
            new RockPlacement(8f, 18f, 2.5f, 2f, 28f),
            new RockPlacement(25f, 13f, 3.2f, 2.5f, -16f),
            new RockPlacement(-29f, 5f, 2.2f, 1.8f, 24f),
            new RockPlacement(-19f, 3f, 1.8f, 1.5f, -30f),
            new RockPlacement(-4f, 7f, 1.7f, 1.4f, 15f),
            new RockPlacement(18f, 5f, 2.2f, 1.8f, 38f),
            new RockPlacement(30f, 3f, 2.7f, 2.1f, -20f),
            new RockPlacement(-30f, -7f, 3f, 2.4f, -36f),
            new RockPlacement(-19f, -18f, 2.5f, 2f, 22f),
            new RockPlacement(-5f, -17f, 2f, 1.7f, -18f),
            new RockPlacement(19f, -17f, 3.2f, 2.5f, 32f),
            new RockPlacement(29f, -14f, 2.2f, 1.8f, -25f),
            new RockPlacement(-26f, -25f, 2.6f, 2.1f, 18f),
            new RockPlacement(-15f, -28f, 3.5f, 2.8f, -28f),
            new RockPlacement(1f, -28f, 2.2f, 1.8f, 12f),
            new RockPlacement(25f, -26f, 3.5f, 2.8f, 28f)
        };

        for (int i = 0; i < decorations.Length; i++)
        {
            RockPlacement placement = decorations[i];
            Sprite sprite = sprites[(i * 3 + 2) % sprites.Count];
            GameObject rock = new GameObject(string.Format("RockDecoration_{0:00}", i + 1));
            rock.transform.SetParent(decorationContainer.transform, false);
            rock.transform.localPosition = new Vector3(placement.position.x, placement.position.y, 0f);
            rock.transform.localRotation = Quaternion.Euler(0f, 0f, placement.rotation);
            Vector2 spriteSize = sprite.bounds.size;
            float scale = Mathf.Min(
                placement.collisionSize.x / Mathf.Max(spriteSize.x, 0.01f),
                placement.collisionSize.y / Mathf.Max(spriteSize.y, 0.01f));
            scale *= 1.45f;
            rock.transform.localScale = new Vector3(scale, scale, 1f);
            SpriteRenderer renderer = rock.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -3;
            float depthAlpha = i % 3 == 0 ? 0.68f : 0.86f;
            renderer.color = new Color(0.72f, 0.78f, 0.9f, depthAlpha);
        }

        Object[] loadedTurretAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Images/Obs_Turrets.png");
        List<Sprite> turretSprites = new List<Sprite>();
        foreach (Object asset in loadedTurretAssets)
        {
            Sprite sprite = asset as Sprite;
            if (sprite != null) turretSprites.Add(sprite);
        }

        if (turretSprites.Count > 0)
        {
            GameObject turretContainer = new GameObject("TurretObstacles");
            turretContainer.transform.SetParent(mapLayout.transform, false);

            RockPlacement[] turrets =
            {
                new RockPlacement(-31f, 29f, 6f, 4.8f, -18f),
                new RockPlacement(31f, 29f, 6f, 4.8f, 198f)
            };

            for (int i = 0; i < turrets.Length; i++)
            {
                RockPlacement placement = turrets[i];
                Sprite sprite = turretSprites[(i + 2) % turretSprites.Count];
                GameObject turret = new GameObject(string.Format("TurretObstacle_{0:00}", i + 1));
                turret.transform.SetParent(turretContainer.transform, false);
                turret.transform.localPosition = new Vector3(placement.position.x, placement.position.y, 0f);
                turret.transform.localRotation = Quaternion.Euler(0f, 0f, placement.rotation);

                Vector2 spriteSize = sprite.bounds.size;
                float scale = Mathf.Min(
                    placement.collisionSize.x / Mathf.Max(spriteSize.x, 0.01f),
                    placement.collisionSize.y / Mathf.Max(spriteSize.y, 0.01f));
                turret.transform.localScale = new Vector3(scale, scale, 1f);

                SpriteRenderer renderer = turret.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 0;

                GameObject warningGlow = new GameObject("WarningGlow");
                warningGlow.transform.SetParent(turret.transform, false);
                warningGlow.transform.localScale = new Vector3(1.12f, 1.12f, 1f);
                SpriteRenderer glowRenderer = warningGlow.AddComponent<SpriteRenderer>();
                glowRenderer.sprite = sprite;
                glowRenderer.color = new Color(1f, 0.08f, 0.02f, 0.24f);
                glowRenderer.sortingOrder = -1;
            }
        }
        else
        {
            Debug.LogWarning("No turret sprites were found at Assets/Resources/Images/Obs_Turrets.png");
        }

        Object[] loadedCoreAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Images/Obs_RedCores.png");
        List<Sprite> coreSprites = new List<Sprite>();
        foreach (Object asset in loadedCoreAssets)
        {
            Sprite sprite = asset as Sprite;
            if (sprite != null && !sprite.name.EndsWith("_8") && !sprite.name.EndsWith("_9") &&
                !sprite.name.EndsWith("_10") && !sprite.name.EndsWith("_11"))
            {
                coreSprites.Add(sprite);
            }
        }

        if (coreSprites.Count > 0)
        {
            GameObject coreContainer = new GameObject("RedCoreObstacles");
            coreContainer.transform.SetParent(mapLayout.transform, false);
            RockPlacement[] cores =
            {
                new RockPlacement(-9f, -3f, 3.8f, 3.8f, -10f),
                new RockPlacement(9f, 3f, 3.8f, 3.8f, 10f),
                new RockPlacement(0f, 20f, 3.5f, 3.5f, 0f),
                new RockPlacement(0f, -20f, 3.5f, 3.5f, 180f),
                new RockPlacement(-24f, 0f, 3.2f, 3.2f, -20f),
                new RockPlacement(24f, 0f, 3.2f, 3.2f, 20f)
            };

            for (int i = 0; i < cores.Length; i++)
            {
                RockPlacement placement = cores[i];
                Sprite sprite = coreSprites[i % coreSprites.Count];
                GameObject core = new GameObject(string.Format("RedCoreObstacle_{0:00}", i + 1));
                core.transform.SetParent(coreContainer.transform, false);
                core.transform.localPosition = new Vector3(placement.position.x, placement.position.y, 0f);
                core.transform.localRotation = Quaternion.Euler(0f, 0f, placement.rotation);
                Vector2 spriteSize = sprite.bounds.size;
                float scale = Mathf.Min(
                    placement.collisionSize.x / Mathf.Max(spriteSize.x, 0.01f),
                    placement.collisionSize.y / Mathf.Max(spriteSize.y, 0.01f));
                core.transform.localScale = new Vector3(scale, scale, 1f);
                SpriteRenderer renderer = core.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 1;

                GameObject halo = new GameObject("CoreHalo");
                halo.transform.SetParent(core.transform, false);
                halo.transform.localScale = new Vector3(1.55f, 1.55f, 1f);
                SpriteRenderer haloRenderer = halo.AddComponent<SpriteRenderer>();
                haloRenderer.sprite = sprite;
                haloRenderer.color = new Color(1f, 0.04f, 0.02f, 0.2f);
                haloRenderer.sortingOrder = 0;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Map2 full composition created: " + rocks.Length + " obstacle rocks, " + decorations.Length + " debris rocks, 2 turrets and 6 red cores.");
    }
}

