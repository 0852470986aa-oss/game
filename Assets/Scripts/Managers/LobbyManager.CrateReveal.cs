// LobbyManager.CrateReveal.cs — อนิเมชันเปิดกล่องเสบียง (Supply Crate)
// ลำดับ: พื้นหลังมืด → กล่องเด้งขึ้นกลางจอ → กล่องสั่นแรงขึ้นเรื่อย ๆ (แสงรอบกล่องค่อย ๆ เป็นสีความหายาก)
//        → แสงวาบ → รางวัลเด้งออกมา พร้อมแสงหมุนและประกายกระจาย → ชื่อรางวัล → "แตะเพื่อไปต่อ"
// แตะระหว่างกล่องสั่น = ข้ามไปเปิดเลย / ปุ่มย้อนกลับ (Esc/Android) = ปิด
// ใช้ผลจาก Economy.OpenCrate (LastCrateCoins / LastCrateItem) ไม่เปลี่ยนการสุ่มหรือการให้รางวัล
// ปิด FeatureFlags.CrateAnimation = เปิดกล่องแล้วขึ้นข้อความเฉย ๆ แบบเดิม
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ส่วนแอนิเมชันเปิดกล่องของ LobbyManager (partial)
public partial class LobbyManager
{
    private RectTransform crateRevealRoot;
    private bool crateRevealSkip, crateRevealCanClose;

    // เริ่มอนิเมชัน (เรียกหลังเปิดกล่องสำเร็จ)
    private void PlayCrateReveal()
    {
        if (!FeatureFlags.CrateAnimation || workshopOverlay == null || !Application.isPlaying) return;
        CloseCrateReveal();
        StartCoroutine(CrateRevealRoutine(Economy.LastCrateCoins, Economy.LastCrateItem));
    }

    // ปิดอนิเมชัน (แตะหลังโชว์รางวัล / ปุ่มย้อนกลับ) คืน true ถ้าปิดจริง
    private bool CloseCrateReveal()
    {
        if (crateRevealRoot == null) return false;
        Destroy(crateRevealRoot.gameObject);
        crateRevealRoot = null;
        return true;
    }

    // Coroutine ลำดับแอนิเมชันเปิดกล่อง (เขย่า → แตก → แสงรางวัล → ป้ายรางวัล) แตะเพื่อข้าม/ปิด
    private IEnumerator CrateRevealRoutine(int coins, Economy.ItemDef item)
    {
        Color rarity = item != null ? Economy.RarityColors[(int)item.rarity] : new Color(1f, .82f, .35f);
        bool epic = item != null && item.rarity == Economy.Rarity.Epic;
        var canvas = workshopOverlay.canvas != null ? workshopOverlay.canvas.rootCanvas.transform : workshopOverlay.transform.parent;
        crateRevealSkip = crateRevealCanClose = false;

        // ชั้นเต็มจอ + พื้นหลังมืด (แตะ = ข้าม/ปิด)
        crateRevealRoot = RevealRect("CrateReveal", canvas, Vector2.zero, Vector2.zero);
        crateRevealRoot.anchorMin = Vector2.zero; crateRevealRoot.anchorMax = Vector2.one;
        crateRevealRoot.offsetMin = crateRevealRoot.offsetMax = Vector2.zero;
        crateRevealRoot.SetAsLastSibling();
        var root = crateRevealRoot;
        var shade = RevealImage("Shade", root, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
        Stretch(shade.rectTransform);
        shade.raycastTarget = true;
        shade.gameObject.AddComponent<Button>().onClick.AddListener(() =>
        {
            if (crateRevealCanClose) CloseCrateReveal(); else crateRevealSkip = true;
        });
        var stage = RevealRect("Stage", root, Vector2.zero, Vector2.zero); // จุดกลางจอ (ทุกอย่างหมุน/สั่นรอบจุดนี้)

        // แสงรัศมี 16 เส้น (หมุนตลอด)
        var rays = RevealRect("Rays", stage, Vector2.zero, Vector2.zero);
        var rayImages = new List<Image>();
        for (int i = 0; i < 16; i++)
        {
            var ray = RevealImage("Ray" + i, rays, Vector2.zero, new Vector2(i % 2 == 0 ? 26 : 14, 900), new Color(1, 1, 1, 0));
            ray.rectTransform.localRotation = Quaternion.Euler(0, 0, i * 11.25f);
            rayImages.Add(ray);
        }
        // กล่อง
        var crate = RevealImage("Crate", stage, Vector2.zero, new Vector2(230, 230), Color.white);
        var crateSprite = Resources.Load<Sprite>("Images/Items/crate");
        if (crateSprite != null) { crate.sprite = crateSprite; crate.preserveAspect = true; }
        else crate.color = new Color(.55f, .4f, .2f);
        crate.rectTransform.localScale = Vector3.zero;
        var title = RevealText("Title", stage, "SUPPLY CRATE", new Vector2(0, 205), 30, Color.white);

        // 1) พื้นหลังมืด + กล่องเด้งขึ้น
        for (float t = 0; t < .35f && crateRevealRoot == root; t += Time.unscaledDeltaTime)
        {
            float k = t / .35f;
            shade.color = new Color(0, 0, 0, .9f * k);
            crate.rectTransform.localScale = Vector3.one * EaseOutBack(k);
            yield return null;
        }
        if (crateRevealRoot != root) yield break; // ถูกปิดกลางทาง (ปุ่มย้อนกลับ)
        shade.color = new Color(0, 0, 0, .9f);
        crate.rectTransform.localScale = Vector3.one;

        // 2) กล่องสั่นแรงขึ้น แสงรอบกล่องค่อย ๆ เป็นสีความหายาก
        float shake = 1.3f, nextTick = 0;
        for (float t = 0; t < shake && !crateRevealSkip && crateRevealRoot == root; t += Time.unscaledDeltaTime)
        {
            float k = t / shake;
            float amp = Mathf.Lerp(2f, 16f, k * k);
            crate.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 42f) * amp);
            crate.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(t * 57f) * amp * .6f, 0);
            crate.rectTransform.localScale = Vector3.one * (1f + .12f * k + .03f * Mathf.Sin(t * 30f));
            Color glow = Color.Lerp(Color.white, rarity, Mathf.Clamp01((k - .45f) / .55f));
            glow.a = .05f + .3f * k;
            foreach (var ray in rayImages) ray.color = glow;
            rays.localRotation = Quaternion.Euler(0, 0, t * 25f);
            if (t >= nextTick) { nextTick = t + Mathf.Lerp(.28f, .09f, k); AudioManager.Instance?.PlaySFX("SFX_Click"); }
            yield return null;
        }

        if (crateRevealRoot != root) yield break;
        // 3) แสงวาบ กล่องหาย
        crate.gameObject.SetActive(false);
        title.gameObject.SetActive(false);
        AudioManager.Instance?.PlaySFX(epic ? "SFX_Explosion" : "SFX_ShieldBreak");
        var flash = RevealImage("Flash", root, Vector2.zero, Vector2.zero, Color.white);
        Stretch(flash.rectTransform);
        flash.raycastTarget = false;

        // 4) รางวัลเด้งออกมา + ประกายกระจาย
        Sprite rewardSprite = item != null ? Resources.Load<Sprite>(item.IconPath) : PolishSprites.Coin();
        var reward = RevealImage("Reward", stage, Vector2.zero, new Vector2(210, 210), Color.white);
        if (rewardSprite != null) { reward.sprite = rewardSprite; reward.preserveAspect = true; }
        else reward.color = rarity;
        reward.rectTransform.localScale = Vector3.zero;
        var sparks = new List<KeyValuePair<Image, Vector2>>();
        int sparkCount = epic ? 36 : 20;
        for (int i = 0; i < sparkCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float speed = Random.Range(260f, epic ? 720f : 520f);
            var spark = RevealImage("Spark" + i, stage, Vector2.zero, Vector2.one * Random.Range(6f, 14f), Color.Lerp(rarity, Color.white, Random.value * .5f));
            spark.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            sparks.Add(new KeyValuePair<Image, Vector2>(spark, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed));
        }
        string rarityName = item != null ? Economy.RarityNames[(int)item.rarity] : "ASTRONIUM";
        var rarityLabel = RevealText("Rarity", stage, rarityName, new Vector2(0, -150), 26, rarity);
        var nameLabel = RevealText("Name", stage, item != null ? item.name : "+" + coins + " Astronium", new Vector2(0, -192), 34, Color.white);
        var detail = RevealText("Detail", stage, item != null ? item.Describe(1) : "", new Vector2(0, -230), 18, new Color(.75f, .85f, .92f));
        foreach (var text in new[] { rarityLabel, nameLabel, detail }) text.alpha = 0;
        Vector2 basePos = stage.anchoredPosition;
        for (float t = 0; t < .9f && crateRevealRoot == root; t += Time.unscaledDeltaTime)
        {
            flash.color = new Color(1, 1, 1, Mathf.Clamp01(1f - t / .35f));
            reward.rectTransform.localScale = Vector3.one * EaseOutBack(Mathf.Clamp01(t / .45f)) * 1.05f;
            rays.localRotation = Quaternion.Euler(0, 0, 32f + t * 40f);
            Color glow = rarity; glow.a = Mathf.Lerp(.55f, .3f, t / .9f);
            foreach (var ray in rayImages) ray.color = glow;
            foreach (var spark in sparks)
            {
                var rt = spark.Key.rectTransform;
                rt.anchoredPosition = spark.Value * t * (1f - t * .45f);
                var c = spark.Key.color; c.a = Mathf.Clamp01(1f - t / .9f); spark.Key.color = c;
            }
            float textK = Mathf.Clamp01((t - .3f) / .4f);
            foreach (var text in new[] { rarityLabel, nameLabel, detail }) text.alpha = textK;
            // EPIC: จอสั่นเล็กน้อย
            stage.anchoredPosition = epic && t < .4f ? basePos + Random.insideUnitCircle * 10f * (1f - t / .4f) : basePos;
            yield return null;
        }
        if (crateRevealRoot != root) yield break;
        stage.anchoredPosition = basePos;
        Destroy(flash.gameObject);
        foreach (var spark in sparks) Destroy(spark.Key.gameObject);
        foreach (var text in new[] { rarityLabel, nameLabel, detail }) text.alpha = 1;

        // 5) แตะเพื่อไปต่อ (แสงหมุนช้า ๆ รางวัลลอยขึ้นลง)
        crateRevealCanClose = true;
        var tap = RevealText("Tap", root, "TAP TO CONTINUE", new Vector2(0, -300), 20, new Color(.8f, .9f, 1f));
        for (float t = 0; crateRevealRoot == root; t += Time.unscaledDeltaTime)
        {
            rays.localRotation = Quaternion.Euler(0, 0, 68f + t * 18f);
            reward.rectTransform.anchoredPosition = new Vector2(0, Mathf.Sin(t * 2.4f) * 8f);
            tap.alpha = .45f + .55f * Mathf.Abs(Mathf.Sin(t * 2.5f));
            yield return null;
        }
    }

    // ===== ตัวช่วยสร้าง UI ของอนิเมชัน (สร้างใหม่ทุกครั้ง แล้วทำลายทิ้งตอนปิด) =====
    private static RectTransform RevealRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        return rect;
    }

    // สร้าง Image ในฉากเปิดกล่อง (ไม่รับการกด)
    private static Image RevealImage(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var image = RevealRect(name, parent, pos, size).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // สร้างข้อความกึ่งกลางในฉากเปิดกล่อง
    private TMP_Text RevealText(string name, Transform parent, string value, Vector2 pos, float size, Color color)
    {
        var text = RevealRect(name, parent, pos, new Vector2(900, size + 16)).gameObject.AddComponent<TextMeshProUGUI>();
        if (lobbyFont != null) text.font = lobbyFont;
        text.text = value; text.fontSize = size; text.color = color;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    // ขยาย rect ให้เต็มพ่อแม่
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // เด้งเกินนิดแล้วกลับ (0..1)
    private static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}
