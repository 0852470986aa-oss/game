// LobbyManager.PremiumPages.cs — แท็บ BATTLE PASS / STAR FORGE (กาชาสกิน) / MY SKINS + ฉากเปิดผลกาชา (partial class ของ LobbyManager)
// กรอบหน้าต่าง/แท็บเติมเงิน/ชำระเงิน อยู่ที่ LobbyManager.Premium.cs — กติกาอยู่ที่ BattlePass.cs / SkinGacha.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public partial class LobbyManager
{
    private const int PassColumns = 6;                                         // ขั้นที่แสดงต่อหน้า
    private static int PassPages => (BattlePass.Tiers + PassColumns - 1) / PassColumns; // จำนวนหน้า
    private int passPage;                                                      // หน้าปัจจุบันของแถบ Pass

    // ===== แท็บ BATTLE PASS =====
    private void BuildPassPage(RectTransform page)
    {
        var left = BattlePass.SeasonLeft;
        UIPanel("SeasonBackdrop", page, 0, 143, 1040, 82, new Color(.045f, .10f, .16f));
        UIPanel("SeasonAccent", page, -518, 143, 4, 82, accentColor);
        var season = UILabel("Season", page, BattlePass.SeasonName + "  /  ENDS IN  " + left.Days + "d " + left.Hours + "h", -250, 160, 520, 28, 18, accentColor);
        season.alignment = TextAlignmentOptions.Left;
        int tier = BattlePass.Tier;
        var tierText = UILabel("Tier", page, "TIER " + tier + " / " + BattlePass.Tiers + "    " + (tier >= BattlePass.Tiers ? "MAX" : BattlePass.XpIntoTier + " / " + BattlePass.XpPerTier + " XP"), -250, 130, 520, 28, 18, Color.white);
        tierText.alignment = TextAlignmentOptions.Left;
        var track = UIPanel("XpTrack", page, -250, 111, 520, 6, new Color(.12f, .15f, .22f));
        var fill = UIPanel("XpFill", track.transform, -260, 0, 520, 6, GoldText);
        fill.rectTransform.pivot = new Vector2(0, .5f);
        fill.rectTransform.anchoredPosition = new Vector2(-260, 0);
        fill.rectTransform.localScale = new Vector3(tier >= BattlePass.Tiers ? 1f : BattlePass.XpIntoTier / (float)BattlePass.XpPerTier, 1, 1);

        if (BattlePass.IsPremium) UILabel("PremiumOn", page, "PREMIUM ACTIVE", 225, 140, 300, 40, 22, GoldText);
        else
        {
            var buy = UIButton("BuyPremium", page, "UNLOCK PREMIUM  /  " + BattlePass.PremiumCost, 225, 140, 300, 48, () => PremiumAction(BattlePass.BuyPremium));
            buy.GetComponent<Image>().color = new Color(.75f, .5f, .12f);
        }
        var skip = UIButton("BuyTier", page, "+1 TIER  /  " + BattlePass.TierSkipCost, 435, 140, 160, 48, () => PremiumAction(BattlePass.BuyTier));
        skip.interactable = tier < BattlePass.Tiers && !premiumBusy;

        // แถบรางวัล: ป้ายเส้น FREE / PREMIUM + 6 ขั้นต่อหน้า
        UILabel("FreeLane", page, "FREE", -462, 24, 110, 40, 20, Color.white);
        UILabel("PremiumLane", page, "PREMIUM", -462, -112, 110, 40, 20, GoldText);
        for (int c = 0; c < PassColumns; c++)
        {
            int t = passPage * PassColumns + c + 1;
            if (t > BattlePass.Tiers) break;
            float x = -330 + c * 150;
            UILabel("TierNo" + t, page, "TIER " + t, x, 96, 136, 22, 15, t == tier ? GoldText : t < tier ? accentColor : Color.gray);
            BuildPassCard(page, "F" + t, x, 24, t, false);
            BuildPassCard(page, "P" + t, x, -112, t, true);
        }
        var prev = UIButton("Prev", page, "<", -460, -205, 80, 44, () => { passPage = Mathf.Max(0, passPage - 1); BuildPremiumPage(); });
        prev.interactable = passPage > 0;
        UILabel("PageNo", page, "TIERS " + (passPage * PassColumns + 1) + "-" + Mathf.Min(BattlePass.Tiers, (passPage + 1) * PassColumns), -320, -205, 180, 30, 16, Color.gray);
        var next = UIButton("Next", page, ">", 460, -205, 80, 44, () => { passPage = Mathf.Min(PassPages - 1, passPage + 1); BuildPremiumPage(); });
        next.interactable = passPage < PassPages - 1;
        var claimAll = UIButton("ClaimAll", page, "CLAIM ALL", 0, -205, 260, 48, ClaimAllPass);
        claimAll.interactable = BattlePass.ClaimableCount() > 0 && !premiumBusy;
        UILabel("Info", page, "Earn pass XP from every match and mission  /  Premium skins: SEASON ACE (tier 15), STAR SOVEREIGN (tier 30)", 0, -248, 1000, 24, 14, Color.gray);
    }

    // การ์ดรางวัล 1 ช่อง
    private void BuildPassCard(RectTransform page, string key, float x, float y, int tier, bool premium)
    {
        var reward = premium ? BattlePass.PremiumReward(tier) : BattlePass.FreeReward(tier);
        bool claimed = premium ? BattlePass.PremiumClaimed(tier) : BattlePass.FreeClaimed(tier);
        bool can = premium ? BattlePass.CanClaimPremium(tier) : BattlePass.CanClaimFree(tier);
        bool locked = !BattlePass.Reached(tier) || (premium && !BattlePass.IsPremium);
        var skin = reward.type == PassRewardType.Skin ? SkinCatalog.Find(reward.skin) : null;
        Color bg = can ? new Color(.08f, .38f, .36f) : claimed ? new Color(.07f, .09f, .13f) : premium ? new Color(.18f, .13f, .05f) : new Color(.06f, .1f, .17f);
        var card = UIPanel("Card" + key, page, x, y, 136, 118, bg);
        Color edge = premium ? GoldText : accentColor;
        UIPanel("RewardEdge", card.transform, 0, 58, 136, 2, edge);
        UIPanel("RewardBase", card.transform, 0, -42, 132, 30, new Color(.02f, .04f, .08f, .85f));
        var aura = UIPanel("RewardAura", card.transform, 0, 21, 108, 78, new Color(edge.r, edge.g, edge.b, claimed ? .1f : .24f));
        aura.sprite = UiFx.Glow;
        aura.raycastTarget = false;
        if (skin != null)
        {
            var glow = UIPanel("Glow", card.transform, 0, 16, 100, 100, new Color(skin.aura.r, skin.aura.g, skin.aura.b, .55f));
            glow.sprite = UiFx.Glow;
            var ship = UIPanel("Ship", card.transform, 0, 16, 64, 56, skin.tint);
            VisualSkin.Preview(ship, equippedShipIndex, skin);
            ship.preserveAspect = true;
        }
        else if (reward.type == PassRewardType.Crystals) CrystalIcon(card.transform, 0, 36, 18);
        else if (reward.type == PassRewardType.Tickets) TicketIcon(card.transform, 0, 36, 48);
        else
        {
            var coin = UIPanel("Coin", card.transform, 0, 36, 30, 30, Color.white);
            coin.sprite = PolishSprites.Coin();
            coin.preserveAspect = true;
        }
        var label = UILabel("Reward", card.transform, reward.Label, 0, skin != null ? -18 : 6, 130, 46, 15,
            skin != null ? SkinCatalog.RarityColors[(int)skin.rarity] : reward.type == PassRewardType.Crystals ? CrystalColor : reward.type == PassRewardType.Tickets ? new Color(.8f, .6f, 1f) : GoldText);
        label.richText = false;
        if (can)
        {
            var claim = UIButton("Claim", card.transform, "CLAIM", 0, -40, 120, 30, () => ClaimPass(tier, premium));
            claim.GetComponent<Image>().color = new Color(.1f, .55f, .35f);
        }
        else UILabel("State", card.transform, claimed ? "CLAIMED" : premium && !BattlePass.IsPremium ? "PREMIUM" : "LOCKED", 0, -42, 120, 24, 13,
            claimed ? new Color(.5f, .9f, .6f) : Color.gray);
        if (locked && !claimed)
        {
            var dim = UIPanel("Dim", card.transform, 0, 0, 136, 118, new Color(0, 0, 0, .35f));
            dim.raycastTarget = false;
        }
    }

    private void ClaimPass(int tier, bool premium)
    {
        if (premiumBusy) return;
        string text = BattlePass.Claim(tier, premium);
        if (text == null) return;
        premiumMessage = text;
        if (text.Contains("ASTRONIUM")) StartCoroutine(RefreshCoinsLater());
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(text.StartsWith("NEW SKIN") ? "SFX_Celebrate" : "SFX_PowerUp");
        BuildPremiumPage();
        RefreshPremiumHome();
    }

    private void ClaimAllPass()
    {
        if (premiumBusy) return;
        int count = BattlePass.ClaimAll(out int coins, out int crystals, out int tickets, out int skins);
        if (count == 0) return;
        var parts = new List<string>();
        if (coins > 0) parts.Add("+" + coins.ToString("N0") + " ASTRONIUM");
        if (crystals > 0) parts.Add("+" + crystals + " CRYSTALS");
        if (tickets > 0) parts.Add("+" + tickets + " TICKETS");
        if (skins > 0) parts.Add("+" + skins + " SKIN");
        premiumMessage = "REWARDS CLAIMED: " + count + "  /  " + string.Join("  /  ", parts);
        if (coins > 0) StartCoroutine(RefreshCoinsLater());
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(skins > 0 ? "SFX_Celebrate" : "SFX_PowerUp");
        BuildPremiumPage();
        RefreshPremiumHome();
    }

    // การกระทำที่ต้องรอผล (หักคริสตัล) — กันกดซ้ำ แล้ววาดหน้าใหม่พร้อมข้อความผล
    private void PremiumAction(System.Action<System.Action<bool, string>> action)
    {
        if (premiumBusy) return;
        premiumBusy = true;
        premiumMessage = "Please wait...";
        BuildPremiumPage();
        action((ok, message) =>
        {
            if (this == null) return;
            premiumBusy = false;
            premiumMessage = message;
            if (ok && AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_PowerUp");
            BuildPremiumPage();
            RefreshPremiumHome();
        });
    }

    // ===== แท็บ STAR FORGE (กาชาสกิน) =====
    private void BuildGachaPage(RectTransform page)
    {
        var data = Premium.Data;
        var featured = SkinGacha.Featured;
        var banner = UIPanel("Banner", page, -200, -30, 620, 400, new Color(.09f, .05f, .16f));
        var rays = UIPanel("Rays", banner.transform, 0, 10, 420, 420, new Color(1f, .8f, .35f, .22f));
        rays.sprite = UiFx.Rays12;
        if (Application.isPlaying && rays.GetComponent<UiSpin>() == null) rays.gameObject.AddComponent<UiSpin>().degreesPerSecond = -10f;
        var glow = UIPanel("Glow", banner.transform, 0, 10, 320, 320, new Color(featured.aura.r, featured.aura.g, featured.aura.b, .65f));
        glow.sprite = UiFx.Glow;
        var ship = UIPanel("Ship", banner.transform, 0, 10, 250, 210, featured.tint);
        VisualSkin.Preview(ship, equippedShipIndex, featured);
        ship.preserveAspect = true;
        UILabel("Title", banner.transform, "STAR FORGE  /  SKIN BANNER", 0, 172, 600, 32, 24, GoldText);
        UILabel("Featured", banner.transform, "RATE UP: " + featured.name, 0, 140, 600, 26, 18, Color.white);
        UILabel("Rarity", banner.transform, "LEGENDARY", 0, -128, 300, 30, 20, SkinCatalog.RarityColors[(int)SkinRarity.Legendary]);
        var change = SkinGacha.FeaturedLeft;
        UILabel("Ends", banner.transform, "50% OF LEGENDARY PULLS  /  CHANGES IN  " + change.Days + "d " + change.Hours + "h", 0, -162, 600, 24, 14, Color.gray);

        UILabel("Pity", page, "LEGENDARY GUARANTEED IN  " + SkinGacha.PullsToLegendary + "  PULLS", 320, 150, 400, 26, 16, GoldText);
        UILabel("EpicPity", page, "EPIC OR BETTER IN  " + SkinGacha.PullsToEpic + "  PULLS", 320, 122, 400, 24, 15, SkinCatalog.RarityColors[(int)SkinRarity.Epic]);
        bool oneTicket = data.tickets >= 1, tenTickets = data.tickets >= 10;
        TicketIcon(page, 158, -181, 56);
        var one = UIButton("PullOne", page, oneTicket ? "PULL x1  /  TICKET x1" : "PULL x1  /  " + SkinGacha.PullCost + " CRYSTALS", 320, 60, 380, 60, () => StartPull(1, oneTicket));
        one.GetComponent<Image>().color = new Color(.35f, .2f, .6f);
        var ten = UIButton("PullTen", page, tenTickets ? "PULL x10  /  TICKET x10" : "PULL x10  /  " + SkinGacha.TenPullCost.ToString("N0") + " CRYSTALS", 320, -10, 380, 60, () => StartPull(10, tenTickets));
        ten.GetComponent<Image>().color = new Color(.6f, .35f, .1f);
        one.interactable = ten.interactable = !premiumBusy;
        UIButton("Odds", page, "ODDS & RULES", 220, -82, 180, 44, ShowGachaOdds);
        UIButton("ToSkins", page, "MY SKINS", 420, -82, 180, 44, () => { premiumTab = PremiumTab.Skins; premiumMessage = ""; BuildPremiumPage(); });
        UILabel("Shards", page, "STAR SHARDS " + data.shards + "  /  EXCHANGE IN MY SKINS", 320, -134, 420, 24, 15, Color.white);
        UILabel("Fair", page, "Unique ship artwork.\nThey never change ship stats.", 320, -186, 420, 50, 14, Color.gray);
    }

    private void StartPull(int count, bool tickets)
    {
        if (premiumBusy) return;
        premiumBusy = true;
        premiumMessage = "";
        SkinGacha.Pull(count, tickets, (results, error) =>
        {
            if (this == null) return;
            premiumBusy = false;
            if (results == null)
            {
                premiumMessage = error;
                if (error != null && error.StartsWith("NOT ENOUGH CRYSTALS") && FeatureFlags.PremiumShop) premiumMessage += "  Top up in the TOP UP tab.";
                BuildPremiumPage();
                return;
            }
            BuildPremiumPage();
            RefreshPremiumHome();
            ShowGachaReveal(results);
        });
    }

    // หน้าโอกาสสุ่มและกติกา (แสดงก่อนสุ่มได้ทุกเมื่อ)
    private void ShowGachaOdds()
    {
        var box = OpenModal(760, 580);
        UILabel("Title", box, "ODDS & RULES", 0, 250, 700, 40, 28, Color.white);
        string[] lines = SkinGacha.RuleLines;
        for (int i = 0; i < lines.Length; i++) UILabel("Line" + i, box, lines[i], 0, 196 - i * 32, 720, 30, i == 0 ? 17 : 15, i == 0 ? Color.white : new Color(.85f, .9f, 1f));
        UILabel("PoolHead", box, "SKINS IN THIS BANNER", 0, -64, 700, 26, 17, accentColor);
        int index = 0;
        foreach (var skin in SkinCatalog.All)
        {
            if (skin.source != SkinSource.Gacha) continue;
            float x = -270 + (index % 4) * 180, y = -98 - (index / 4) * 32;
            UILabel("Pool" + index, box, skin.name, x, y, 176, 28, 14, SkinCatalog.RarityColors[(int)skin.rarity]);
            index++;
        }
        UIButton("Close", box, "CLOSE", 0, -244, 220, 52, CloseModal);
    }

    // ===== ฉากเปิดผลกาชา =====
    private void ShowGachaReveal(List<GachaResult> results)
    {
        if (results == null || results.Count == 0) return;
        premiumModalLocked = true;
        CloseModal();
        premiumModalLocked = true;
        var shade = UIPanel("PremiumModal" + (++premiumSerial), premiumOverlay.transform, 0, 0, 1280, 720, new Color(.005f, .008f, .02f, .97f));
        shade.raycastTarget = true;
        premiumModal = shade.rectTransform;
        SkinRarity best = SkinRarity.Rare;
        bool anySkin = false;
        foreach (var r in results) if (r.skin != null) { anySkin = true; if (r.rarity > best) best = r.rarity; }
        Color bestColor = anySkin ? SkinCatalog.RarityColors[(int)best] : new Color(.6f, .7f, 1f);
        var rays = UIPanel("Rays", premiumModal, 0, 0, 1100, 1100, new Color(bestColor.r, bestColor.g, bestColor.b, .25f));
        rays.sprite = UiFx.Rays20;
        if (Application.isPlaying) rays.gameObject.AddComponent<UiSpin>().degreesPerSecond = -8f;
        var halo = UIPanel("Halo", premiumModal, 0, 0, 760, 760, new Color(bestColor.r, bestColor.g, bestColor.b, .3f));
        halo.sprite = UiFx.Glow;
        UILabel("Title", premiumModal, results.Count > 1 ? "STAR FORGE  /  RESULT x10" : "STAR FORGE  /  RESULT", 0, 300, 900, 40, 28, Color.white);
        var cards = new List<RectTransform>();
        if (results.Count == 1) cards.Add(BuildRevealCard(premiumModal, results[0], 0, -10, 1.6f));
        else
            for (int i = 0; i < results.Count; i++)
                cards.Add(BuildRevealCard(premiumModal, results[i], -440 + (i % 5) * 220, i < 5 ? 130 : -140, 1f));
        var ok = UIButton("Ok", premiumModal, "OK", 0, -310, 240, 54, () => { premiumModalLocked = false; CloseModal(); BuildPremiumPage(); RefreshPremiumHome(); });
        ok.gameObject.SetActive(false);
        if (Application.isPlaying) StartCoroutine(RevealCards(cards, ok.gameObject, anySkin && best >= SkinRarity.Epic));
        else { foreach (var c in cards) c.localScale = Vector3.one; ok.gameObject.SetActive(true); }
    }

    // การ์ดผล 1 ใบ (ฐาน 200x250 คูณ scale)
    private RectTransform BuildRevealCard(RectTransform parent, GachaResult result, float x, float y, float scale)
    {
        var skin = result.skin;
        Color rarity = skin != null ? SkinCatalog.RarityColors[(int)skin.rarity] : new Color(.6f, .7f, 1f);
        var card = UIPanel("Card" + x + "_" + y, parent, x, y, 200, 250, new Color(rarity.r * .22f, rarity.g * .22f, rarity.b * .28f, 1f));
        UIPanel("Top", card.transform, 0, 123, 200, 4, rarity);
        var glow = UIPanel("Glow", card.transform, 0, 30, 190, 190, new Color(rarity.r, rarity.g, rarity.b, skin != null ? .5f : .3f));
        glow.sprite = UiFx.Glow;
        if (skin != null)
        {
            var ship = UIPanel("Ship", card.transform, 0, 30, 130, 110, skin.tint);
            VisualSkin.Preview(ship, equippedShipIndex, skin);
            ship.preserveAspect = true;
            UILabel("Name", card.transform, skin.name, 0, -52, 190, 30, 18, Color.white);
            UILabel("Rarity", card.transform, SkinCatalog.RarityNames[(int)skin.rarity], 0, -78, 190, 22, 14, rarity);
            UILabel("Tag", card.transform, result.isNew ? "NEW!" : "DUPLICATE  +" + result.shards + " SHARDS", 0, -104, 190, 22, result.isNew ? 17 : 13, result.isNew ? GoldText : Color.gray);
        }
        else
        {
            var spark = UIPanel("Spark", card.transform, 0, 30, 90, 90, new Color(.75f, .85f, 1f));
            spark.sprite = UiFx.Spark;
            UILabel("Name", card.transform, "STAR SHARDS", 0, -52, 190, 30, 18, Color.white);
            UILabel("Amount", card.transform, "x" + result.shards, 0, -84, 190, 26, 18, new Color(.75f, .85f, 1f));
        }
        card.rectTransform.localScale = Vector3.zero; // เริ่มเล็ก แล้วเด้งขึ้นใน RevealCards
        revealScales[card.rectTransform] = scale;
        return card.rectTransform;
    }
    private readonly Dictionary<RectTransform, float> revealScales = new Dictionary<RectTransform, float>();

    // การ์ดเด้งขึ้นทีละใบ (เสียงติ๊ก) ใบสุดท้ายเล่นเสียงเปิดกล่อง
    private IEnumerator RevealCards(List<RectTransform> cards, GameObject ok, bool epic)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card == null) yield break;
            float target = revealScales.TryGetValue(card, out float s) ? s : 1f;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_CrateTick");
            float t = 0;
            while (t < .18f && card != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / .18f);
                float pop = k < .7f ? Mathf.Lerp(0, 1.12f, k / .7f) : Mathf.Lerp(1.12f, 1f, (k - .7f) / .3f);
                card.localScale = Vector3.one * pop * target;
                yield return null;
            }
            if (card != null) card.localScale = Vector3.one * target;
            yield return new WaitForSecondsRealtime(cards.Count > 1 ? .06f : .1f);
        }
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(epic ? "SFX_CrateEpic" : "SFX_CrateOpen");
        revealScales.Clear();
        if (ok != null) ok.SetActive(true);
    }

    // ===== แท็บ MY SKINS =====
    private void BuildSkinsPage(RectTransform page)
    {
        var data = Premium.Data;
        var equipped = SkinCatalog.FromPaint(ShipPaint.Local);
        var head = UILabel("Equipped", page, "EQUIPPED: " + (equipped != null ? equipped.name : "ORIGINAL") + "  /  STAR SHARDS " + data.shards, -150, 160, 700, 28, 17, accentColor);
        head.alignment = TextAlignmentOptions.Left;
        var remove = UIButton("Remove", page, "ORIGINAL SHIP", 380, 160, 240, 36, () => EquipSkin(null));
        remove.interactable = equipped != null;
        for (int i = 0; i < SkinCatalog.All.Length; i++)
        {
            var skin = SkinCatalog.All[i];
            float x = -420 + (i % 5) * 210, y = 88 - (i / 5) * 122;
            BuildSkinCard(page, skin, i, x, y, equipped == skin);
        }
        UILabel("Fair", page, "Unique ship artwork / animated previews / no stat changes", 0, -248, 1000, 24, 14, Color.gray);
    }

    private void BuildSkinCard(RectTransform page, SkinDef skin, int index, float x, float y, bool isEquipped)
    {
        bool owned = Premium.OwnsSkin(skin.id);
        Color rarity = SkinCatalog.RarityColors[(int)skin.rarity];
        var card = UIPanel("Skin" + index, page, x, y, 200, 112, owned ? new Color(rarity.r * .2f, rarity.g * .2f, rarity.b * .26f) : new Color(.05f, .07f, .1f));
        UIPanel("Edge", card.transform, -98, 0, 4, 112, rarity);
        var glow = UIPanel("Glow", card.transform, -55, 6, 96, 96, new Color(skin.aura.r, skin.aura.g, skin.aura.b, owned ? .6f : .25f));
        glow.sprite = UiFx.Glow;
        var ship = UIPanel("Ship", card.transform, -55, 6, 70, 62, owned ? skin.tint : skin.tint * .45f);
        VisualSkin.Preview(ship, equippedShipIndex, skin, owned);
        ship.preserveAspect = true;
        UILabel("Name", card.transform, skin.name, 38, 34, 120, 34, 15, owned ? Color.white : Color.gray);
        UILabel("Rarity", card.transform, SkinCatalog.RarityNames[(int)skin.rarity], 38, 8, 120, 20, 12, rarity);
        if (owned)
        {
            var equip = UIButton("Equip", card.transform, isEquipped ? "EQUIPPED" : "EQUIP", 38, -30, 118, 32, () => EquipSkin(skin));
            equip.interactable = !isEquipped;
            if (!isEquipped) equip.GetComponent<Image>().color = new Color(.1f, .5f, .4f);
        }
        else if (skin.source == SkinSource.Gacha)
        {
            int cost = SkinGacha.ExchangeCost[(int)skin.rarity];
            var exchange = UIButton("Exchange", card.transform, cost + " SHARDS", 38, -30, 118, 32, () =>
            {
                SkinGacha.Exchange(skin, out string message);
                premiumMessage = message;
                BuildPremiumPage();
            });
            exchange.interactable = Premium.Data.shards >= cost;
        }
        else UILabel("Source", card.transform, skin.source == SkinSource.Pass ? "BATTLE PASS" : "STARTER PACK", 38, -30, 118, 28, 13, GoldText);
    }

    // ใส่สกิน (null = กลับไปสีพื้นฐาน ORIGINAL)
    private void EquipSkin(SkinDef skin)
    {
        int paint = skin != null ? SkinCatalog.PaintIndex(skin.id) : 0;
        if (skin != null && !SkinCatalog.Usable(paint)) return;
        SelectPaint(paint);
        UpdateShipDisplay(equippedShipIndex);
        if (Photon.Pun.PhotonNetwork.InRoom && profileLoaded) PublishLocalLoadout(false);
        premiumMessage = skin != null ? "EQUIPPED: " + skin.name : "Original ship restored.";
        BuildPremiumPage();
    }

    // ===== ปุ่ม SKINS ในหน้า Hangar (ต่อท้ายแถวสี) =====
    private void BuildHangarSkinsButton(Transform parent, float x, float y)
    {
        var button = UIButton("SkinsButton", parent, "SKINS", x, y, 100, 40, () => OpenPremium(PremiumTab.Skins, SurfaceOf(parent)));
        button.GetComponent<Image>().color = new Color(.4f, .22f, .6f);
        button.gameObject.SetActive(FeatureFlags.SkinGacha);
    }

    // หา LobbySurface ที่ครอบอยู่ (ใช้เป็นที่วางหน้าต่างพรีเมียม)
    private static Transform SurfaceOf(Transform t)
    {
        for (var p = t; p != null; p = p.parent) if (p.name == "LobbySurface") return p;
        return t;
    }
}
