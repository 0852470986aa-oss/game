// LobbyManager.Premium.cs — หน้าจอระบบพรีเมียมในล็อบบี้ (partial class ของ LobbyManager)
// หน้าหลัก: ป้ายคริสตัล (มุมบน ข้าง SETTINGS) + ปุ่ม SHOP / PASS / GACHA (ใต้ยอดเหรียญ) พร้อมจุดแจ้งเตือน
// หน้าต่างพรีเมียม 5 แท็บ: TOP UP (เติมเงิน) / BATTLE PASS / STAR FORGE (กาชาสกิน) / MY SKINS / HISTORY
// ไฟล์นี้: ปุ่มหน้าหลัก + กรอบหน้าต่าง + แท็บเติมเงิน + หน้าชำระเงินจำลอง + ประวัติ
// แท็บ Pass / กาชา / สกิน / ฉากเปิดผลกาชา อยู่ที่ LobbyManager.PremiumPages.cs
// กติกา/ข้อมูลอยู่ใน Premium.cs, BattlePass.cs, SkinGacha.cs — ไฟล์นี้ทำแค่หน้าจอ
// สวิตช์: FeatureFlags.PremiumShop / PremiumPass / SkinGacha (ปิดทั้งหมด = ไม่มีปุ่มใด ๆ เพิ่มบนหน้าหลัก)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public partial class LobbyManager
{
    private static readonly Color CrystalColor = new Color(.45f, .9f, 1f);   // สีคริสตัล
    private static readonly Color GoldText = new Color(1f, .8f, .35f);       // สีทอง
    private static readonly Color DemoColor = new Color(1f, .62f, .3f);      // สีป้ายระบบจำลอง
    private static readonly Color SuccessColor = new Color(.35f, 1f, .6f);   // สีสำเร็จ

    private Button crystalWalletButton, shopHomeButton, passHomeButton, gachaHomeButton; // ปุ่มบนหน้าหลัก
    private TMP_Text crystalWalletText, passDotText, shopDotText, gachaDotText;      // ยอดคริสตัล / ตัวเลขบนจุดแจ้งเตือน
    private Image premiumOverlay;            // พื้นหลังมืดเต็มจอของหน้าต่างพรีเมียม
    private RectTransform premiumWindow;     // กรอบหน้าต่าง
    private RectTransform premiumPage;       // เนื้อหาแท็บปัจจุบัน (สร้างใหม่ทุกครั้ง)
    private RectTransform premiumModal;      // หน้าต่างซ้อน (ชำระเงิน / โอกาสสุ่ม / ผลกาชา)
    private enum PremiumTab { TopUp, Pass, Gacha, Skins, History }
    private PremiumTab premiumTab;           // แท็บที่เปิดอยู่
    private bool premiumSubscribed;          // สมัครฟัง Premium.Changed แล้ว
    private bool premiumBusy;                // รอผล (กันกดซ้ำ)
    private string premiumMessage = "";      // ข้อความผลล่าสุดท้ายหน้า
    private int premiumSerial;               // เลขต่อท้ายชื่อหน้าที่สร้างใหม่
    private bool premiumModalLocked;         // หน้าต่างซ้อนกำลังทำงาน (ห้ามวาดหน้าใหม่ทับ)

    // ===== ปุ่มบนหน้าหลัก (เรียกจาก BuildHomeScreen) =====
    private void BuildPremiumUI(RectTransform root)
    {
        // ป้ายคริสตัลมุมบน (ตำแหน่งเดิมของปุ่ม WORKSHOP ที่ย้ายเข้า Hangar แล้ว)
        crystalWalletButton = UIButton("CrystalWallet", root, "", 255, 315, 180, 46, () => OpenPremium(PremiumTab.TopUp, root));
        CrystalIcon(crystalWalletButton.transform, -66, 0, 20);
        crystalWalletText = UILabel("Amount", crystalWalletButton.transform, "...", 2, 0, 100, 36, 20, CrystalColor);
        var plus = UIPanel("Plus", crystalWalletButton.transform, 68, 0, 30, 30, new Color(.1f, .55f, .35f));
        UILabel("Text", plus.transform, "+", 0, 1, 30, 30, 22, Color.white);
        var label = crystalWalletButton.transform.Find("Label");
        if (label != null) label.gameObject.SetActive(false);

        // แถวปุ่ม SHOP / PASS / GACHA ใต้ยอดเหรียญ
        shopHomeButton = UIButton("PremiumShopButton", root, "SHOP", 272, 162, 118, 36, () => OpenPremium(PremiumTab.TopUp, root));
        passHomeButton = UIButton("PremiumPassButton", root, "PASS", 395, 162, 118, 36, () => OpenPremium(PremiumTab.Pass, root));
        gachaHomeButton = UIButton("PremiumGachaButton", root, "GACHA", 518, 162, 118, 36, () => OpenPremium(PremiumTab.Gacha, root));
        shopDotText = NoticeDot(shopHomeButton.transform, new Color(.95f, .55f, .15f));
        passDotText = NoticeDot(passHomeButton.transform, new Color(.9f, .25f, .2f));
        gachaDotText = NoticeDot(gachaHomeButton.transform, new Color(.6f, .3f, .95f));

        // หน้าต่างลอย (ซ่อนไว้)
        premiumOverlay = UIPanel("PremiumOverlay", root, 0, 0, 1280, 720, new Color(0, 0, 0, .9f));
        premiumOverlay.raycastTarget = true;
        premiumWindow = UIPanel("PremiumWindow", premiumOverlay.transform, 0, 0, 1060, 620, panelColor).rectTransform;
        premiumOverlay.gameObject.SetActive(false);

        if (Application.isPlaying)
        {
            if (!premiumSubscribed) { Premium.Changed += OnPremiumChanged; premiumSubscribed = true; }
            Premium.Load();
        }
        RefreshPremiumHome();
    }

    // จุดแจ้งเตือนมุมขวาบนของปุ่ม
    private TMP_Text NoticeDot(Transform button, Color color)
    {
        var dot = UIPanel("Notice", button, 52, 14, 24, 24, color);
        return UILabel("Count", dot.transform, "", 0, 0, 24, 24, 14, Color.white);
    }

    // ไอคอนคริสตัล (สี่เหลี่ยมหมุน 45° + ประกายด้านใน)
    private void CrystalIcon(Transform parent, float x, float y, float size)
    {
        if (PremiumItemArt.Crystal != null)
        {
            var art = UIPanel("Crystal", parent, x, y, size * 1.5f, size * 1.5f, Color.white);
            art.sprite = PremiumItemArt.Crystal;
            art.preserveAspect = true;
            art.raycastTarget = false;
            return;
        }
        var gem = UIPanel("Crystal", parent, x, y, size, size, CrystalColor);
        gem.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        var shine = UIPanel("Shine", gem.transform, -size * .16f, size * .16f, size * .38f, size * .38f, new Color(1f, 1f, 1f, .75f));
        shine.raycastTarget = false;
    }

    private void TicketIcon(Transform parent, float x, float y, float width)
    {
        var art = UIPanel("Ticket", parent, x, y, width, width * .6f, Color.white);
        art.sprite = PremiumItemArt.Ticket;
        art.preserveAspect = true;
        art.raycastTarget = false;
        if (art.sprite == null) art.color = new Color(.65f, .35f, .9f);
    }

    // ข้อมูลพรีเมียมเปลี่ยน: อัปเดตปุ่มหน้าหลัก และวาดแท็บที่เปิดอยู่ใหม่
    private void OnPremiumChanged()
    {
        if (this == null) { Premium.Changed -= OnPremiumChanged; return; }
        RefreshPremiumHome();
        if (premiumOverlay != null && premiumOverlay.gameObject.activeSelf && !premiumModalLocked) BuildPremiumPage();
    }

    // อัปเดตยอดคริสตัล จุดแจ้งเตือน และการแสดงปุ่มตามสวิตช์
    private void RefreshPremiumHome()
    {
        bool shop = FeatureFlags.PremiumShop, pass = FeatureFlags.PremiumPass, gacha = FeatureFlags.SkinGacha;
        if (crystalWalletButton != null) crystalWalletButton.gameObject.SetActive(shop || pass || gacha);
        if (crystalWalletText != null) crystalWalletText.text = Premium.CrystalsKnown ? Premium.Crystals.ToString("N0") : "...";
        if (shopHomeButton != null) shopHomeButton.gameObject.SetActive(shop);
        if (passHomeButton != null) passHomeButton.gameObject.SetActive(pass);
        if (gachaHomeButton != null) gachaHomeButton.gameObject.SetActive(gacha);
        SetDot(shopDotText, shop && Premium.Data != null && Premium.StarterAvailable ? "!" : "");
        int claimable = pass ? BattlePass.ClaimableCount() : 0;
        SetDot(passDotText, claimable > 0 ? claimable.ToString() : "");
        int tickets = gacha && Premium.Data != null ? Premium.Data.tickets : 0;
        SetDot(gachaDotText, tickets > 0 ? tickets.ToString() : "");
    }

    private static void SetDot(TMP_Text text, string value)
    {
        if (text == null) return;
        text.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(value));
        text.text = value;
    }

    // ===== เปิด/ปิดหน้าต่าง =====
    // host = พื้นผิวที่จะแสดงหน้าต่าง (หน้าหลัก หรือ Hangar)
    private void OpenPremium(PremiumTab tab, Transform host)
    {
        if (premiumOverlay == null) return;
        if (!TabEnabled(tab)) tab = FirstEnabledTab();
        if (host != null && premiumOverlay.transform.parent != host) premiumOverlay.transform.SetParent(host, false);
        Premium.EnsureLoaded();
        premiumTab = tab;
        premiumMessage = "";
        premiumModalLocked = false;
        if (tab == PremiumTab.Pass) passPage = Mathf.Clamp((Mathf.Max(1, BattlePass.Tier) - 1) / PassColumns, 0, PassPages - 1);
        premiumOverlay.gameObject.SetActive(true);
        premiumOverlay.transform.SetAsLastSibling();
        SolidOverlay(premiumOverlay, premiumWindow);
        CloseModal();
        BuildPremiumPage();
        if (Application.isPlaying) Premium.RefreshCrystals();
    }

    private void ClosePremium()
    {
        if (premiumBusy) return;
        CloseModal();
        if (premiumOverlay != null) premiumOverlay.gameObject.SetActive(false);
    }

    private static bool TabEnabled(PremiumTab tab)
    {
        switch (tab)
        {
            case PremiumTab.TopUp: case PremiumTab.History: return FeatureFlags.PremiumShop;
            case PremiumTab.Pass: return FeatureFlags.PremiumPass;
            default: return FeatureFlags.SkinGacha;
        }
    }

    private static PremiumTab FirstEnabledTab()
    {
        foreach (PremiumTab tab in System.Enum.GetValues(typeof(PremiumTab))) if (TabEnabled(tab)) return tab;
        return PremiumTab.TopUp;
    }

    // ===== กรอบหน้า: หัวข้อ ยอดเงิน BACK แท็บ ข้อความท้ายหน้า =====
    private void BuildPremiumPage()
    {
        if (premiumWindow == null) return;
        if (premiumPage != null) Destroy(premiumPage.gameObject);
        premiumPage = UIRect("PremiumPage" + (++premiumSerial), premiumWindow, 0, 0, 1060, 620);
        string[] titles = { "PREMIUM SHOP", "BATTLE PASS", "STAR FORGE", "MY SKINS", "HISTORY" };
        var title = UILabel("Title", premiumPage, titles[(int)premiumTab], -340, 270, 340, 46, 30, Color.white);
        title.alignment = TextAlignmentOptions.Left;
        var data = Premium.Data;
        string wallet = "CRYSTALS " + (Premium.CrystalsKnown ? Premium.Crystals.ToString("N0") : "...");
        if (FeatureFlags.SkinGacha && data != null) wallet += "  /  TICKETS " + data.tickets + "  /  SHARDS " + data.shards;
        CrystalIcon(premiumPage, -166, 270, 18);
        var walletText = UILabel("Wallet", premiumPage, wallet, 95, 270, 470, 34, 18, CrystalColor);
        walletText.alignment = TextAlignmentOptions.Left;
        UIButton("Close", premiumPage, "BACK", 425, 270, 170, 54, ClosePremium);

        // แท็บ (เฉพาะที่เปิดสวิตช์)
        var tabs = new List<PremiumTab>();
        foreach (PremiumTab tab in System.Enum.GetValues(typeof(PremiumTab))) if (TabEnabled(tab)) tabs.Add(tab);
        string[] tabNames = { "TOP UP", "BATTLE PASS", "STAR FORGE", "MY SKINS", "HISTORY" };
        for (int i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            var button = UIButton("Tab" + tab, premiumPage, tabNames[(int)tab], (i - (tabs.Count - 1) / 2f) * 204, 208, 196, 46, () =>
            {
                if (premiumBusy) return;
                premiumTab = tab;
                premiumMessage = "";
                if (tab == PremiumTab.Pass) passPage = Mathf.Clamp((Mathf.Max(1, BattlePass.Tier) - 1) / PassColumns, 0, PassPages - 1);
                BuildPremiumPage();
            });
            StyleTab(button, tab == premiumTab);
        }

        switch (premiumTab)
        {
            case PremiumTab.TopUp: BuildTopUpPage(premiumPage); break;
            case PremiumTab.Pass: BuildPassPage(premiumPage); break;
            case PremiumTab.Gacha: BuildGachaPage(premiumPage); break;
            case PremiumTab.Skins: BuildSkinsPage(premiumPage); break;
            default: BuildHistoryPage(premiumPage); break;
        }
        var message = UILabel("Message", premiumPage, premiumMessage, 0, -288, 1000, 30, 17, GoldText);
        message.richText = false;
    }

    // ===== แท็บเติมเงิน =====
    private void BuildTopUpPage(RectTransform page)
    {
        UILabel("DemoNote", page, "DEMO PAYMENT  /  NO REAL MONEY IS CHARGED", 0, 160, 900, 26, 16, DemoColor);
        bool starter = Premium.StarterAvailable;
        if (starter)
        {
            var banner = UIPanel("StarterPack", page, 0, 100, 1000, 80, new Color(.14f, .12f, .2f));
            UIPanel("Accent", banner.transform, -497, 0, 4, 80, GoldText);
            TicketIcon(banner.transform, -443, 0, 76);
            var head = UILabel("Head", banner.transform, "STARTER PACK  /  ONE TIME ONLY", -70, 16, 600, 32, 23, GoldText);
            head.alignment = TextAlignmentOptions.Left;
            var desc = UILabel("Desc", banner.transform, "150 CRYSTALS  +  3 GACHA TICKETS  +  STARTER ACE SKIN  +  2,000 ASTRONIUM", -70, -16, 600, 26, 14, Color.white);
            desc.alignment = TextAlignmentOptions.Left;
            var buy = UIButton("Buy", banner.transform, "THB " + Premium.Starter.thb, 390, 0, 180, 54, () => OpenCheckout(Premium.Starter));
            buy.GetComponent<Image>().color = new Color(.75f, .5f, .12f);
        }
        float top = starter ? -10 : 60;
        for (int i = 0; i < Premium.Packs.Length; i++)
        {
            var pack = Premium.Packs[i];
            float x = -335 + (i % 3) * 335, y = top - (i / 3) * 148;
            var card = UIPanel("Pack" + i, page, x, y, 310, 136, new Color(.055f, .09f + i * .004f, .16f + i * .009f));
            UIPanel("Edge", card.transform, 0, 67, 310, 2, new Color(.2f, .65f, .8f, .65f));
            var glow = UIPanel("ItemGlow", card.transform, -95, 5, 115, 100, new Color(.1f, .65f, 1f, .35f));
            glow.sprite = UiFx.Glow;
            glow.raycastTarget = false;
            CrystalIcon(card.transform, -95, 13, 42 + i * 1.5f);
            UILabel("Amount", card.transform, pack.crystals.ToString("N0"), 48, 25, 180, 40, 34, Color.white);
            UILabel("Unit", card.transform, "CRYSTALS", 48, -2, 180, 20, 14, CrystalColor);
            bool first = Premium.FirstBonus(pack);
            if (first)
            {
                var ribbon = UIPanel("Ribbon", card.transform, -100, 54, 100, 22, new Color(.85f, .2f, .25f));
                UILabel("Text", ribbon.transform, "x2 FIRST", 0, 0, 96, 22, 13, Color.white);
                UILabel("Bonus", card.transform, "+" + pack.crystals.ToString("N0") + " BONUS", 48, -20, 180, 18, 14, GoldText);
            }
            var price = UIButton("Price", card.transform, "THB " + pack.thb.ToString("N0"), 0, -46, 200, 34, () => OpenCheckout(pack));
            price.GetComponent<Image>().color = new Color(.08f, .4f, .5f);
        }
    }

    // ===== หน้าชำระเงิน (จำลอง) =====
    private TopupPack checkoutPack;      // แพ็กที่กำลังจ่าย
    private int checkoutMethod;          // ช่องทาง (index ใน Premium.Methods)
    private Texture2D checkoutQr;        // ภาพ QR จำลอง (ทำลายเมื่อสร้างใหม่)
    private int checkoutSerial;          // ใช้หยุดนาฬิกานับถอยหลังของหน้าก่อน
    private DemoCheckoutSession checkoutSession;

    private void OpenCheckout(TopupPack pack)
    {
        if (premiumBusy || pack == null) return;
        checkoutPack = pack;
        checkoutSession = new DemoCheckoutSession(Time.unscaledTimeAsDouble);
        BuildCheckout();
    }

    // วาดหน้าชำระเงิน (ซ้อนบนหน้าต่าง)
    private void BuildCheckout()
    {
        var box = OpenModal(640, 560);
        var pack = checkoutPack;
        UILabel("Title", box, "CHECKOUT", 0, 240, 560, 40, 28, Color.white);
        UILabel("Demo", box, Premium.Payment.IsSimulated ? "DEMO PAYMENT  /  NO REAL MONEY IS CHARGED" : Premium.Payment.Name, 0, 206, 600, 24, 15, DemoColor);
        int crystals = Premium.CrystalsFor(pack);
        string item = pack.starter ? "STARTER PACK  /  150 CRYSTALS + 3 TICKETS + SKIN + 2,000 ASTRONIUM"
            : crystals.ToString("N0") + " CRYSTALS" + (crystals > pack.crystals ? "  (INCLUDES x2 FIRST TOP-UP BONUS)" : "");
        UILabel("Item", box, item, 0, 172, 600, 28, 17, CrystalColor);
        UILabel("Price", box, "THB " + pack.thb.ToString("N0"), 0, 136, 400, 40, 32, GoldText);
        string[] methodNames = { "PROMPTPAY", "TRUEMONEY", "CARD" };
        for (int i = 0; i < Premium.Methods.Length; i++)
        {
            int method = i;
            var tab = UIButton("Method" + i, box, methodNames[i], -200 + i * 200, 88, 190, 44, () => { checkoutMethod = method; BuildCheckout(); });
            StyleTab(tab, i == checkoutMethod);
        }
        if (checkoutMethod == 0)
        {
            var frame = UIPanel("QrFrame", box, 0, -40, 216, 216, Color.white);
            var qr = UIPanel("Qr", frame.transform, 0, 0, 196, 196, Color.white);
            qr.sprite = DemoQr();
            UILabel("QrHint", box, "DEMO QR ONLY - DO NOT PAY WITH A BANKING APP", 0, -162, 600, 24, 14, DemoColor);
            var watermark = UIPanel("DemoWatermark", qr.transform, 0, 0, 130, 30, new Color(.07f, .12f, .2f));
            UILabel("Text", watermark.transform, "DEMO", 0, 0, 120, 28, 22, GoldText);
        }
        else
        {
            var panel = UIPanel("MethodInfo", box, 0, -40, 460, 200, new Color(.06f, .1f, .17f));
            string info = checkoutMethod == 1
                ? "DEMO WALLET\n\nTEST ACCOUNT  /  ***-***-0000\nAPPROVAL REQUIRED\nNo wallet app is contacted."
                : "DEMO CARD\n\nTEST CARD  /  **** **** **** 0000\nTEST HOLDER  /  EXPIRES 12/99\nNo card details are collected.";
            UILabel("Text", panel.transform, info, 0, 0, 430, 180, 17, Color.white);
        }
        var timer = UILabel("Timer", box, "EXPIRES IN 5:00", 0, -190, 400, 24, 15, Color.gray);
        UILabel("Order", box, checkoutSession != null ? checkoutSession.OrderId : "DEMO", 0, -214, 580, 18, 12, CrystalColor);
        UIButton("Cancel", box, "CANCEL", -150, -248, 220, 44, CancelDemoCheckout);
        var pay = UIButton("Pay", box, "CONTINUE TO DEMO APPROVAL", 150, -248, 280, 44, ConfirmCheckout);
        pay.GetComponent<Image>().color = new Color(.1f, .55f, .35f);
        if (Application.isPlaying) StartCoroutine(CheckoutTimer(timer, ++checkoutSerial));
    }

    // นับถอยหลัง 5 นาที (หมดเวลา = ปิดหน้าชำระเงิน)
    private IEnumerator CheckoutTimer(TMP_Text label, int serial)
    {
        double end = checkoutSession != null ? checkoutSession.ExpiresAt : Time.unscaledTimeAsDouble + 300;
        while (label != null && serial == checkoutSerial)
        {
            float left = (float)System.Math.Max(0, end - Time.unscaledTimeAsDouble);
            label.text = "EXPIRES IN " + (int)(left / 60) + ":" + ((int)(left % 60)).ToString("00");
            if (left <= 0) { checkoutSession?.Expire(Time.unscaledTimeAsDouble); premiumMessage = "Payment session expired."; CloseModal(); BuildPremiumPage(); yield break; }
            yield return new WaitForSecondsRealtime(.5f);
        }
    }

    // กดจ่าย: แสดงกำลังดำเนินการ แล้วส่งให้ Premium.Purchase
    private void ConfirmCheckout()
    {
        if (premiumBusy || checkoutPack == null || checkoutSession == null || !checkoutSession.Authorize(Time.unscaledTimeAsDouble)) return;
        var box = OpenModal(640, 480);
        premiumModalLocked = true;
        UILabel("Title", box, "DEMO PAYMENT AUTHORIZATION", 0, 192, 580, 40, 26, CrystalColor);
        UILabel("Safety", box, "DEMO PAYMENT  /  NO REAL MONEY IS CHARGED", 0, 151, 580, 30, 16, DemoColor);
        UILabel("Order", box, checkoutSession.OrderId, 0, 108, 580, 28, 18, Color.gray);
        UILabel("Method", box, Premium.Methods[checkoutMethod] + "  /  TEST ACCOUNT", 0, 63, 580, 36, 21, Color.white);
        UILabel("Total", box, "THB " + checkoutPack.thb.ToString("N0"), 0, 8, 580, 50, 36, GoldText);
        UILabel("Hint", box, "Choose a test result. No bank, wallet or card will be contacted.", 0, -48, 570, 48, 17, Color.white);
        var timer = UILabel("Timer", box, "", 0, -94, 500, 26, 16, Color.gray);
        UIButton("Decline", box, "SIMULATE DECLINED", -150, -148, 270, 48, () => {
            if (checkoutSession == null || !checkoutSession.Decline(Time.unscaledTimeAsDouble)) return;
            ShowCheckoutResult(checkoutPack, new PaymentResult { success = false, message = "Demo declined. No crystals granted and no money charged." }, 0);
        });
        var approve = UIButton("Approve", box, "SIMULATE APPROVED", 150, -148, 270, 48, ProcessDemoCheckout);
        approve.GetComponent<Image>().color = new Color(.1f, .5f, .35f);
        UIButton("Cancel", box, "CANCEL", 0, -206, 220, 36, CancelDemoCheckout);
        if (Application.isPlaying) StartCoroutine(CheckoutTimer(timer, ++checkoutSerial));
    }

    private void CancelDemoCheckout()
    {
        if (premiumBusy) return;
        checkoutSession?.Cancel();
        premiumMessage = "Demo payment cancelled. No money charged.";
        CloseModal();
        BuildPremiumPage();
    }

    private void ProcessDemoCheckout()
    {
        // This screen is deliberately a sandbox; never hand its test approval to a live provider.
        if (!Premium.Payment.IsSimulated)
        {
            checkoutSession?.Cancel();
            premiumMessage = "Demo checkout requires a simulated payment provider.";
            CloseModal();
            BuildPremiumPage();
            return;
        }
        if (premiumBusy || checkoutPack == null || checkoutSession == null || !checkoutSession.Approve(Time.unscaledTimeAsDouble)) return;
        premiumBusy = true;
        premiumModalLocked = true;
        checkoutSerial++;
        var box = OpenModal(560, 360);
        UILabel("Title", box, "PROCESSING PAYMENT...", 0, 110, 500, 40, 26, Color.white);
        var ring = UIPanel("Spinner", box, 0, 0, 110, 110, CrystalColor);
        ring.sprite = UiFx.Ring;
        if (Application.isPlaying) ring.gameObject.AddComponent<UiSpin>().degreesPerSecond = -300f;
        UILabel("Demo", box, "DEMO PAYMENT  /  NO REAL MONEY IS CHARGED", 0, -110, 520, 24, 15, DemoColor);
        if (Application.isPlaying) StartCoroutine(PayAfterDelay(checkoutPack, Premium.Methods[checkoutMethod]));
    }

    private IEnumerator PayAfterDelay(TopupPack pack, string method)
    {
        yield return new WaitForSecondsRealtime(1.4f);
        Premium.Purchase(pack, method, (result, crystals) =>
        {
            if (this == null) return;
            premiumBusy = false;
            checkoutSession?.Complete(result != null && result.success && crystals > 0);
            ShowCheckoutResult(pack, result, crystals);
        });
    }

    // ใบเสร็จ
    private void ShowCheckoutResult(TopupPack pack, PaymentResult result, int crystals)
    {
        var box = OpenModal(600, 460);
        premiumModalLocked = true;
        bool ok = result != null && result.success && crystals > 0;
        UILabel("DemoReceipt", box, "DEMO RECEIPT - NO REAL MONEY CHARGED", 0, 142, 560, 26, 16, DemoColor);
        UILabel("Title", box, ok ? "PAYMENT SUCCESSFUL" : "PAYMENT FAILED", 0, 180, 540, 44, 30, ok ? SuccessColor : new Color(1f, .4f, .4f));
        if (ok)
        {
            var glow = UIPanel("Glow", box, 0, 70, 200, 200, new Color(CrystalColor.r, CrystalColor.g, CrystalColor.b, .5f));
            glow.sprite = UiFx.Glow;
            CrystalIcon(box, 0, 70, 56);
            UILabel("Gain", box, "+" + crystals.ToString("N0") + " CRYSTALS", 0, -20, 540, 40, 30, CrystalColor);
            if (pack.starter) UILabel("Extra", box, "+3 GACHA TICKETS  +  STARTER ACE SKIN  +  2,000 ASTRONIUM", 0, -56, 560, 26, 15, GoldText);
            UILabel("Ref", box, "REF " + result.reference + "  /  THB " + pack.thb.ToString("N0") + "  /  DEMO", 0, -92, 560, 24, 14, Color.gray);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Celebrate");
            if (pack.starter) StartCoroutine(RefreshCoinsLater());
        }
        else UILabel("Error", box, result != null ? result.message : "Payment failed.", 0, 20, 540, 80, 18, Color.white);
        UILabel("Order", box, checkoutSession != null ? checkoutSession.OrderId : "DEMO", 0, -124, 550, 24, 14, CrystalColor);
        UIButton("Done", box, "DONE", 0, -170, 240, 56, () => { premiumModalLocked = false; CloseModal(); BuildPremiumPage(); RefreshPremiumHome(); });
    }

    // ภาพ QR จำลอง (สุ่มลาย + มุมระบุตำแหน่ง 3 มุมเหมือน QR จริง แต่ไม่มีข้อมูลใด ๆ)
    private Sprite DemoQr()
    {
        const int n = 29;
        if (checkoutQr != null) Destroy(checkoutQr);
        checkoutQr = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var random = new System.Random(checkoutPack != null ? checkoutPack.thb * 7919 + checkoutMethod : 1);
        var pixels = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool dark = random.NextDouble() < .5;
                int fx = x < 8 ? x : x >= n - 8 ? x - (n - 8) : -1;
                int fy = y < 8 ? y : y >= n - 8 ? y - (n - 8) : -1;
                bool finder = fx >= 0 && fy >= 0 && !(x >= n - 8 && y >= n - 8);
                if (finder)
                {
                    int dx = x < 8 ? x : x - (n - 7), dy = y < 8 ? y : y - (n - 7);
                    dark = dx >= 0 && dx < 7 && dy >= 0 && dy < 7 && (dx == 0 || dx == 6 || dy == 0 || dy == 6 || (dx >= 2 && dx <= 4 && dy >= 2 && dy <= 4));
                }
                pixels[y * n + x] = dark ? new Color32(10, 14, 30, 255) : new Color32(255, 255, 255, 255);
            }
        checkoutQr.SetPixels32(pixels);
        checkoutQr.Apply(false);
        return Sprite.Create(checkoutQr, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f);
    }

    // ===== หน้าต่างซ้อน =====
    private RectTransform OpenModal(float width, float height)
    {
        CloseModal();
        var shade = UIPanel("PremiumModal" + (++premiumSerial), premiumOverlay.transform, 0, 0, 1280, 720, new Color(0, 0, 0, .82f));
        shade.raycastTarget = true;
        premiumModal = shade.rectTransform;
        var box = UIPanel("Box", premiumModal, 0, 0, width, height, WindowFill);
        box.raycastTarget = true;
        UIPanel("Edge", box.transform, 0, height / 2f - 2, width, 4, accentColor);
        return box.rectTransform;
    }

    private void CloseModal()
    {
        if (premiumModal != null) Destroy(premiumModal.gameObject);
        premiumModal = null;
        checkoutSerial++;
        if (!premiumBusy) premiumModalLocked = false;
    }

    // ===== แท็บประวัติ =====
    private void BuildHistoryPage(RectTransform page)
    {
        var data = Premium.Data;
        var head = UILabel("TopupHead", page, "TOP-UP HISTORY  /  TOTAL THB " + Premium.TotalSpentThb().ToString("N0"), -260, 160, 500, 28, 18, accentColor);
        head.alignment = TextAlignmentOptions.Left;
        UILabel("DemoNote", page, "ALL PAYMENTS ARE DEMO (NO REAL MONEY)", -260, 134, 500, 22, 13, DemoColor).alignment = TextAlignmentOptions.Left;
        if (data.history.Count == 0) UILabel("Empty", page, "No top-ups yet.", -260, 90, 500, 26, 16, Color.gray);
        for (int i = 0; i < data.history.Count && i < 10; i++)
        {
            var record = data.history[i];
            float y = 100 - i * 36;
            var row = UIPanel("Topup" + i, page, -260, y, 510, 32, new Color(.06f, .1f, .17f));
            string when = System.DateTimeOffset.FromUnixTimeMilliseconds(record.at).ToLocalTime().ToString("dd/MM HH:mm");
            HistoryCell(row.transform, when, -195, 110, Color.gray);
            HistoryCell(row.transform, "+" + record.crystals.ToString("N0"), -95, 90, CrystalColor);
            HistoryCell(row.transform, "THB " + record.thb.ToString("N0"), -5, 90, GoldText);
            HistoryCell(row.transform, record.id, 155, 220, Color.white, 12);
        }
        var pullHead = UILabel("PullHead", page, "RECENT PULLS  /  TOTAL " + data.totalPulls, 260, 160, 500, 28, 18, accentColor);
        pullHead.alignment = TextAlignmentOptions.Left;
        if (!FeatureFlags.SkinGacha) return;
        if (data.pulls.Count == 0) UILabel("NoPulls", page, "No pulls yet.", 260, 90, 500, 26, 16, Color.gray);
        for (int i = 0; i < data.pulls.Count && i < 11; i++)
        {
            var record = data.pulls[i];
            float y = 128 - i * 36;
            var row = UIPanel("Pull" + i, page, 260, y, 510, 32, new Color(.06f, .1f, .17f));
            string when = System.DateTimeOffset.FromUnixTimeMilliseconds(record.at).ToLocalTime().ToString("dd/MM HH:mm");
            var skin = SkinCatalog.Find(record.skin);
            HistoryCell(row.transform, when, -195, 110, Color.gray);
            HistoryCell(row.transform, skin != null ? skin.name : "STAR SHARDS x" + record.shards, -15, 230, skin != null ? SkinCatalog.RarityColors[record.rarity] : Color.white);
            HistoryCell(row.transform, skin != null ? (record.shards > 0 ? "+" + record.shards + " SHARDS" : "NEW") : "", 180, 130, skin != null && record.shards == 0 ? GoldText : Color.gray, 13);
        }
    }

    private void HistoryCell(Transform row, string value, float x, float width, Color color, int size = 14)
    {
        var cell = UILabel("C" + x, row, value, x, 0, width, 28, size, color);
        cell.richText = false;
    }
}
