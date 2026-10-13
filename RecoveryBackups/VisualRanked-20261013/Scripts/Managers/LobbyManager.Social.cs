// LobbyManager.Social.cs — หน้าจอสังคม (เฟส 8) (partial class ของ LobbyManager)
// หน้าหลัก: ปุ่ม SOCIAL (มีตัวเลขบอกคำขอเป็นเพื่อน/คำชวน) / ห้องรอ: ปุ่ม INVITE FRIENDS
// หน้าต่าง SOCIAL มี 4 แท็บ: FRIENDS (โค้ดเพื่อน เพิ่มเพื่อน สถานะ ชวน/เข้าร่วม) / REQUESTS / GUILD / CHAT
// คำชวนเข้าห้องจากเพื่อนเด้งเป็นกล่องข้อความ (JOIN / IGNORE) ไม่ว่าอยู่หน้าไหนของล็อบบี้
// ข้อมูลและการคุยกับ Firebase อยู่ใน Social.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

// ส่วนหน้าจอสังคมของ LobbyManager: ปุ่ม SOCIAL/INVITE FRIENDS, หน้าต่าง 4 แท็บ และกล่องคำชวนเข้าห้อง
public partial class LobbyManager
{
    // แท็บของหน้าต่าง SOCIAL: เพื่อน / คำขอเป็นเพื่อน / กิลด์ / แชท
    private enum SocialTab { Friends, Requests, Guild, Chat }
    private SocialTab socialTab; // แท็บที่กำลังเปิดอยู่ในหน้าต่าง SOCIAL
    private Button socialButton, inviteFriendsButton; // ปุ่ม SOCIAL บนหน้าหลัก และปุ่ม INVITE FRIENDS ในห้องรอ
    private TMP_Text socialBadge; // ตัวเลขแจ้งเตือนคำขอเป็นเพื่อนบนปุ่ม SOCIAL
    private RectTransform socialHomeRoot, socialWaitRoot; // จุดวาง UI สังคมบนหน้าหลัก และในห้องรอ
    private Image socialOverlay; // พื้นหลังมืดของหน้าต่าง SOCIAL
    private RectTransform socialWindow, socialPage; // กรอบหน้าต่าง SOCIAL และหน้าเนื้อหาแท็บที่สร้างใหม่ทุกครั้ง
    private TMP_Text socialMessageText, chatLogText; // ข้อความแจ้งเตือนด้านล่างหน้าต่าง และกล่องข้อความแชท
    private TMP_InputField chatInput, friendCodeInput, guildNameInput, guildTagInput, guildJoinInput; // ช่องพิมพ์แชท, รหัสเพื่อน, ชื่อ/แท็กกิลด์ใหม่ และรหัสกิลด์ที่จะเข้าร่วม
    private string socialMessage = ""; // ข้อความแจ้งเตือนที่จะแสดง (คงไว้ตอนวาดหน้าใหม่)
    private Image invitePopup; // กล่องคำชวนเข้าห้องจากเพื่อน
    private Social.Invite shownInvite; // คำชวนที่กำลังแสดงอยู่ (กันสร้างกล่องซ้ำ)
    private bool socialSubscribed, socialTicking; // สมัครรับอีเวนต์ Social แล้วหรือยัง / เริ่ม Coroutine SocialTick แล้วหรือยัง
    private float nextPresenceRefresh; // เวลาที่จะรีเฟรชสถานะออนไลน์ของเพื่อนครั้งถัดไป (ทุก 20 วิ)

    // ===== ปุ่มบนหน้าหลัก (เรียกจาก BuildHomeScreen หลัง BuildProgressUI) =====
    private void BuildSocialHome(RectTransform root)
    {
        socialHomeRoot = root;
        // จัดแถว MISSIONS / PROFILE ใหม่ให้มีที่วาง SOCIAL (3 ปุ่มกว้าง 120)
        if (missionsButton != null && missionsButton.transform.Find("NavigationLayoutV1") == null) ResizeButton(missionsButton, 270, 127, 120, 46);
        if (profileButton != null && profileButton.transform.Find("NavigationLayoutV1") == null) ResizeButton(profileButton, 395, 127, 120, 46);
        socialButton = UIButton("Social", root, "SOCIAL", 520, 127, 120, 46, () => OpenSocial(SocialTab.Friends));
        var dot = UIPanel("Badge", socialButton.transform, 50, 18, 28, 28, new Color(.9f, .25f, .2f));
        socialBadge = UILabel("Count", dot.transform, "", 0, 0, 28, 28, 15, Color.white);
        if (missionsBadge != null && missionsBadge.transform.parent.Find("NavigationLayoutV1") == null) ((RectTransform)missionsBadge.transform.parent).anchoredPosition = new Vector2(50, 18);
        if (Application.isPlaying && !socialSubscribed)
        {
            Social.Changed += OnSocialChanged;
            Social.ChatChanged += OnChatChanged;
            socialSubscribed = true;
        }
        if (Application.isPlaying && !socialTicking) { socialTicking = true; StartCoroutine(SocialTick()); }
        RefreshSocialBadge();
    }

    // ===== ปุ่มในห้องรอ (เรียกจาก BuildLobbyUI) =====
    private void BuildSocialWaiting(RectTransform root)
    {
        socialWaitRoot = root;
        inviteFriendsButton = UIButton("InviteFriends", root, "INVITE FRIENDS", 485, 276, 200, 28, () => OpenSocial(SocialTab.Friends));
    }

    // ย้ายตำแหน่งและปรับขนาดปุ่มที่สร้างไว้แล้ว พร้อมปรับขนาดข้อความในปุ่มให้พอดี
    private static void ResizeButton(Button button, float x, float y, float w, float h)
    {
        var rect = (RectTransform)button.transform;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
        var label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.rectTransform.sizeDelta = new Vector2(w - 8, h - 6);
        UiLayout.Placed(rect); // ตำแหน่งที่บันทึกเอง (UiLayout.cs)
    }

    // ทุก 1 วินาที: เริ่มระบบเมื่อพร้อม / อัปเดตสถานะห้อง / อ่านสถานะเพื่อนทุก 20 วิ / แสดงคำชวน
    private string presenceRoom = null;
    // Coroutine ที่วนทำงานทุก 1 วินาทีตลอดอายุของล็อบบี้ (เริ่มจาก BuildSocialHome)
    private IEnumerator SocialTick()
    {
        var wait = new WaitForSecondsRealtime(1f);
        while (this != null)
        {
            if (FeatureFlags.Social && profileLoaded) Social.Start();
            if (Social.Ready)
            {
                string room = PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode ? PhotonNetwork.CurrentRoom.Name : "";
                if (room != presenceRoom) { presenceRoom = room; Social.SetPresence(room); }
                if (Time.unscaledTime >= nextPresenceRefresh) { nextPresenceRefresh = Time.unscaledTime + 20f; Social.RefreshPresence(); }
            }
            Social.TickChat(); // แชทรวม: เอาข้อความที่ครบ 2 นาทีออก
            if (chatLogText != null && Social.ChatExpires && socialOverlay != null && socialOverlay.gameObject.activeInHierarchy) UpdateChatLog(); // ข้อความใกล้หมดเวลาค่อยๆ จาง
            RefreshSocialBadge();
            RefreshInvitePopup();
            if (inviteFriendsButton != null) inviteFriendsButton.gameObject.SetActive(FeatureFlags.Social && Social.Ready && PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode
                && !RankedPrivate(PhotonNetwork.CurrentRoom)); // ห้องแรงค์ชวนเพื่อนไม่ได้
            yield return wait;
        }
    }

    // ซ่อน/แสดงปุ่ม SOCIAL ตาม FeatureFlags และอัปเดตตัวเลขแจ้งเตือน (คำขอเป็นเพื่อน + คำชวน)
    private void RefreshSocialBadge()
    {
        if (socialButton != null) socialButton.gameObject.SetActive(FeatureFlags.Social);
        if (socialBadge == null) return;
        int count = Social.Requests.Count + Social.Invites.Count + Social.UnreadDms.Count; // + ข้อความส่วนตัวที่ยังไม่อ่าน
        socialBadge.transform.parent.gameObject.SetActive(count > 0);
        socialBadge.text = count.ToString();
    }

    // เรียกเมื่อ Social.Changed: อัปเดตตัวเลข กล่องคำชวน และวาดหน้าต่าง SOCIAL ใหม่ถ้าเปิดอยู่
    private void OnSocialChanged()
    {
        if (this == null) { Social.Changed -= OnSocialChanged; return; }
        RefreshSocialBadge();
        RefreshInvitePopup();
        // กำลังพิมพ์อยู่ไม่วาดหน้าใหม่ (จะทำให้คีย์บอร์ดหาย)
        if (socialOverlay != null && socialOverlay.gameObject.activeSelf && !AnyInputFocused() && socialTab != SocialTab.Chat) BuildSocialPage();
    }

    // เรียกเมื่อ Social.ChatChanged: อัปเดตข้อความในกล่องแชท (ถ้าล็อบบี้ถูกทำลายแล้วจะยกเลิกการฟัง)
    private void OnChatChanged()
    {
        if (this == null) { Social.ChatChanged -= OnChatChanged; return; }
        UpdateChatLog();
    }

    // เช็กว่ามีช่องพิมพ์ในหน้าต่าง SOCIAL ช่องไหนกำลังโฟกัสอยู่ไหม (true = กำลังพิมพ์)
    private bool AnyInputFocused()
    {
        foreach (var input in new[] { chatInput, friendCodeInput, guildNameInput, guildTagInput, guildJoinInput })
            if (input != null && input.isFocused) return true;
        return false;
    }

    // ===== หน้าต่าง =====
    private RectTransform ActiveSocialRoot => waitingRoomPanel != null && waitingRoomPanel.activeInHierarchy && socialWaitRoot != null ? socialWaitRoot : socialHomeRoot;

    // เปิดหน้าต่าง SOCIAL ที่แท็บที่ระบุ (สร้าง overlay ครั้งแรก แล้วย้ายไปอยู่หน้าหลักหรือห้องรอตามที่เปิดอยู่)
    private void OpenSocial(SocialTab tab)
    {
        if (!FeatureFlags.Social) return;
        var root = ActiveSocialRoot;
        if (root == null) return;
        if (socialOverlay == null)
        {
            socialOverlay = UIPanel("SocialOverlay", root, 0, 0, 1280, 720, new Color(0, 0, 0, .72f));
            socialOverlay.raycastTarget = true;
            socialWindow = UIPanel("SocialWindow", socialOverlay.transform, 0, 0, 1080, 640, panelColor).rectTransform;
        }
        socialOverlay.transform.SetParent(root, false);
        socialOverlay.gameObject.SetActive(true);
        socialOverlay.transform.SetAsLastSibling();
        SolidOverlay(socialOverlay, socialWindow);
        socialTab = tab;
        socialMessage = Social.Ready ? "" : "Log in with an online account to use friends, chat and guilds.";
        Social.RefreshPresence();
        BuildSocialPage();
    }

    // ปิดหน้าต่าง SOCIAL (แค่ซ่อน overlay ไม่ทำลายทิ้ง)
    private void CloseSocial()
    {
        if (socialOverlay != null) socialOverlay.gameObject.SetActive(false);
        LeaveDmChat();
    }

    // ออกจากแชทส่วนตัว (ปิดหน้าต่าง/ไปแท็บอื่น) กลับเป็นแชทรวม — ไม่งั้นข้อความใหม่จากเพื่อนคนนั้นจะถูกนับว่าอ่านแล้วทั้งที่ไม่ได้เปิดดู
    private void LeaveDmChat()
    {
        if (Social.IsDmChannel) Social.SetChatChannel("global");
    }

    // แสดงข้อความแจ้งผลด้านล่างหน้าต่าง SOCIAL (ใช้เป็น callback ของ Social.* ด้วย)
    private void SocialNotice(string message)
    {
        if (this == null) return;
        socialMessage = message;
        if (socialMessageText != null) socialMessageText.text = message;
    }

    // วาดหน้าต่าง SOCIAL ใหม่ทั้งหน้า: ลบหน้าเก่า สร้างหัวข้อ ปุ่ม BACK แท็บ 4 อัน แล้วเนื้อหาของแท็บที่เลือก
    private void BuildSocialPage()
    {
        if (socialWindow == null) return;
        if (socialPage != null) Destroy(socialPage.gameObject);
        chatInput = friendCodeInput = guildNameInput = guildTagInput = guildJoinInput = null;
        chatLogText = null;
        chatScroll = null;
        if (socialTab != SocialTab.Chat) LeaveDmChat();
        socialPage = UIRect("SPage" + (++pageSerial), socialWindow, 0, 0, 1080, 640);
        var page = socialPage;
        UILabel("Title", page, "SOCIAL", -440, 286, 180, 44, 30, Color.white);
        UIButton("Close", page, "BACK", 440, 286, 170, 54, CloseSocial);
        string[] tabs = { "FRIENDS (" + Social.Friends.Count + ")", "REQUESTS (" + Social.Requests.Count + ")", "GUILD", "CHAT" };
        bool[] on = { true, true, FeatureFlags.Guilds, FeatureFlags.Chat };
        for (int i = 0; i < tabs.Length; i++)
        {
            int tab = i;
            var button = UIButton("Tab" + i, page, tabs[i], -240 + i * 165, 286, 155, 54, () => { socialTab = (SocialTab)tab; socialMessage = ""; BuildSocialPage(); });
            StyleTab(button, (int)socialTab == i, on[i]);
        }
        switch (socialTab)
        {
            case SocialTab.Friends: BuildFriendsTab(page); break;
            case SocialTab.Requests: BuildRequestsTab(page); break;
            case SocialTab.Guild: BuildGuildTab(page); break;
            default: BuildChatTab(page); break;
        }
        socialMessageText = UILabel("Message", page, socialMessage, 0, -298, 1040, 28, 17, new Color(1f, .85f, .5f));
        socialMessageText.richText = false;
    }

    // ===== แท็บเพื่อน =====
    private void BuildFriendsTab(RectTransform page)
    {
        UILabel("MyCode", page, "YOUR FRIEND CODE:  " + Social.MyCode, -270, 226, 500, 34, 22, accentColor);
        UIButton("CopyCode", page, "COPY", -10, 226, 110, 36, () => { GUIUtility.systemCopyBuffer = Social.MyCode; SocialNotice("Friend code copied: " + Social.MyCode); });
        friendCodeInput = CreateInput("FriendCodeInput", page, 270, 226, 260, 40, "Friend code (8)", 8);
        UIButton("Add", page, "ADD", 460, 226, 120, 40, () =>
        {
            SocialNotice("Sending...");
            Social.SendFriendRequest(friendCodeInput != null ? friendCodeInput.text : "", SocialNotice);
        });
        if (Social.Friends.Count == 0)
        {
            UILabel("Empty", page, "No friends yet. Share your code or add a friend's code.", 0, 60, 900, 40, 20, Color.gray);
            return;
        }
        // เรียง: ออนไลน์ก่อน แล้วตามชื่อ
        var list = new System.Collections.Generic.List<Social.Friend>(Social.Friends);
        list.Sort((a, b) => a.online != b.online ? b.online.CompareTo(a.online) : string.CompareOrdinal(a.name, b.name));
        bool inRoom = PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode;
        bool rankedHere = RankedPrivate(PhotonNetwork.CurrentRoom); // อยู่ห้องแรงค์ = ชวนใครไม่ได้
        for (int i = 0; i < list.Count && i < 8; i++)
        {
            var friend = list[i];
            float y = 166 - i * 54;
            var row = UIPanel("Friend" + i, page, 0, y, 1040, 48, new Color(.06f, .1f, .17f));
            UIPanel("Dot", row.transform, -500, 0, 14, 14, friend.online ? new Color(.3f, 1f, .5f) : new Color(.4f, .45f, .5f));
            var name = UILabel("Name", row.transform, friend.name + (friend.level > 0 ? "   Lv" + friend.level : ""), -330, 0, 320, 40, 19, Color.white);
            name.richText = false;
            name.alignment = TextAlignmentOptions.Left;
            bool friendRanked = FeatureFlags.RankedNoInvite && IsRankedRoomName(friend.room); // เพื่อนอยู่ห้องแรงค์ = จอยตามไม่ได้
            string status = !friend.online ? "OFFLINE" + LastSeen(friend.last) : string.IsNullOrEmpty(friend.room) ? "ONLINE  /  IN LOBBY"
                : friendRanked ? "ONLINE  /  IN RANKED MATCH" : "ONLINE  /  ROOM " + friend.room;
            UILabel("Status", row.transform, status, -30, 0, 280, 40, 16, friend.online ? new Color(.5f, 1f, .7f) : Color.gray);
            var target = friend;
            if (FeatureFlags.FriendChat && FeatureFlags.Chat)
            {
                // แชทส่วนตัว: มีข้อความใหม่ = ปุ่มสีเหลืองขึ้นว่า NEW MSG
                bool unread = Social.UnreadDms.ContainsKey(friend.uid);
                var chat = UIButton("Chat", row.transform, unread ? "NEW MSG" : "CHAT", 175, 0, 110, 38, () =>
                {
                    Social.OpenDm(target);
                    socialTab = SocialTab.Chat; socialMessage = ""; BuildSocialPage();
                });
                if (unread) chat.GetComponent<Image>().color = new Color(.85f, .62f, .15f);
                UiIcon.OnButton(chat, "chat");
            }
            if (inRoom && !rankedHere && friend.online && friend.room != PhotonNetwork.CurrentRoom.Name)
                UIButton("Invite", row.transform, "INVITE", 300, 0, 130, 38, () => { Social.SendInvite(target, PhotonNetwork.CurrentRoom.Name); SocialNotice("Invite sent to " + target.name + "."); });
            else if (!inRoom && friend.online && !string.IsNullOrEmpty(friend.room) && !friendRanked)
                UIButton("Join", row.transform, "JOIN", 300, 0, 130, 38, () => { CloseSocial(); JoinRoomByName(target.room); });
            UIButton("Remove", row.transform, "X", 470, 0, 60, 38, () =>
            {
                if (pendingRemove == target.uid) { Social.RemoveFriend(target); pendingRemove = null; SocialNotice("Removed " + target.name + "."); }
                else { pendingRemove = target.uid; SocialNotice("Tap X again to remove " + target.name + "."); }
            });
        }
    }

    private string pendingRemove; // uid เพื่อนที่กด X ครั้งแรกแล้ว รอกดซ้ำเพื่อยืนยันลบ

    // แปลงเวลาออนไลน์ล่าสุด (Unix ms) เป็นข้อความ เช่น "  /  5m ago", "3h ago", "2d ago" (0 = ไม่แสดง)
    private static string LastSeen(long last)
    {
        if (last <= 0) return "";
        var span = System.DateTimeOffset.UtcNow - System.DateTimeOffset.FromUnixTimeMilliseconds(last);
        if (span.TotalMinutes < 60) return "  /  " + Mathf.Max(1, (int)span.TotalMinutes) + "m ago";
        if (span.TotalHours < 48) return "  /  " + (int)span.TotalHours + "h ago";
        return "  /  " + (int)span.TotalDays + "d ago";
    }

    // ===== แท็บคำขอ =====
    private void BuildRequestsTab(RectTransform page)
    {
        if (Social.Requests.Count == 0) { UILabel("Empty", page, "No friend requests.", 0, 60, 900, 40, 20, Color.gray); return; }
        for (int i = 0; i < Social.Requests.Count && i < 9; i++)
        {
            var request = Social.Requests[i];
            float y = 220 - i * 54;
            var row = UIPanel("Request" + i, page, 0, y, 1040, 48, new Color(.06f, .1f, .17f));
            var name = UILabel("Name", row.transform, request.name + "  wants to be your friend", -200, 0, 600, 40, 19, Color.white);
            name.richText = false;
            name.alignment = TextAlignmentOptions.Left;
            UIButton("Accept", row.transform, "ACCEPT", 300, 0, 140, 38, () => { Social.Accept(request); SocialNotice("You and " + request.name + " are now friends!"); });
            UIButton("Decline", row.transform, "DECLINE", 455, 0, 120, 38, () => Social.Decline(request));
        }
    }

    // ===== แท็บกิลด์ =====
    private void BuildGuildTab(RectTransform page)
    {
        var guild = Social.Guild;
        if (guild == null)
        {
            bool autoTag = FeatureFlags.AutoGuildTag;
            UILabel("CreateHead", page, "CREATE A GUILD  (" + Social.GuildCost + " ASTRONIUM)", -260, 200, 500, 32, 20, accentColor);
            guildNameInput = CreateInput("GuildName", page, autoTag ? -290 : -330, 150, autoTag ? 440 : 360, 44, "Guild name (3-20)", 20);
            if (autoTag)
            {
                // แท็กสร้างอัตโนมัติจากชื่อ (Social.AutoTag) แสดงตัวอย่างสดตอนพิมพ์ — ถ้าซ้ำกับกิลด์อื่นเกมจะเติมตัวเลขให้
                var preview = UILabel("TagPreview", page, "TAG  [---]", -290, 108, 440, 28, 17, Color.gray);
                preview.richText = false;
                guildNameInput.onValueChanged.AddListener(v => preview.text = "TAG  [" + (v.Trim().Length >= 3 ? Social.AutoTag(v, 0) : "---") + "]");
            }
            else guildTagInput = CreateInput("GuildTag", page, -60, 150, 150, 44, "TAG (3-5)", 5);
            UIButton("Create", page, "CREATE", autoTag ? 30 : 100, 150, 140, 44, () =>
            {
                SocialNotice("Creating...");
                Social.CreateGuild(guildNameInput.text, guildTagInput != null ? guildTagInput.text : "", msg => { SocialNotice(msg); StartCoroutine(RefreshCoinsLater()); });
            });
            UILabel("JoinHead", page, "OR JOIN A GUILD BY TAG", -260, 40, 500, 32, 20, accentColor);
            guildJoinInput = CreateInput("GuildJoin", page, -330, -10, 360, 44, "Guild tag", 5);
            UIButton("Join", page, "JOIN", -60, -10, 140, 44, () => { SocialNotice("Joining..."); Social.JoinGuild(guildJoinInput.text, SocialNotice); });
            UILabel("Info", page, "Guild members get a [TAG] before their name, a private guild chat and a member list.", 0, -100, 1000, 30, 16, Color.gray);
            return;
        }
        UILabel("GuildName", page, "[" + guild.tag + "]  " + guild.name, -230, 220, 560, 44, 28, Color.white).richText = false;
        UILabel("Count", page, guild.members.Count + " / " + Social.GuildMaxMembers + " MEMBERS", 230, 220, 300, 30, 18, accentColor);
        int i = 0;
        foreach (var member in guild.members)
        {
            if (i >= 16) break;
            float x = -390 + (i % 4) * 260, y = 160 - (i / 4) * 50;
            var cell = UIPanel("Member" + i, page, x, y, 250, 44, new Color(.06f, .1f, .17f));
            var label = UILabel("Name", cell.transform, (member.Key == guild.owner ? "* " : "") + member.Value, 0, 0, 240, 40, 17,
                member.Key == Social.Uid ? new Color(1f, .87f, .4f) : Color.white);
            label.richText = false;
            i++;
        }
        UIButton("Chat", page, "GUILD CHAT", -150, -200, 240, 50, () => { Social.SetChatChannel("guild_" + guild.id); socialTab = SocialTab.Chat; BuildSocialPage(); });
        UIButton("Leave", page, "LEAVE GUILD", 150, -200, 240, 50, () =>
        {
            if (pendingRemove == "guild") { pendingRemove = null; Social.LeaveGuild(SocialNotice); }
            else { pendingRemove = "guild"; SocialNotice("Tap LEAVE GUILD again to confirm."); }
        });
        UILabel("OwnerNote", page, "* = guild leader", 0, -250, 400, 24, 14, Color.gray);
    }

    // ===== แท็บแชท =====
    // ช่อง: GLOBAL (ทุกคน ข้อความหายใน 2 นาที) / GUILD / แชทส่วนตัวกับเพื่อน (เปิดจากปุ่ม CHAT ในแท็บเพื่อน)
    // กล่องข้อความเลื่อนขึ้นลงได้ เก็บ 50 ข้อความล่าสุด
    private ScrollRect chatScroll;
    // สร้างแท็บแชท: ปุ่มเลือกช่อง, หัวข้อช่อง, กล่องข้อความเลื่อนได้, ช่องพิมพ์ + ปุ่ม SEND
    private void BuildChatTab(RectTransform page)
    {
        string channel = Social.ChatChannel;
        bool isGlobal = channel == "global", isGuild = channel.StartsWith("guild_"), isDm = Social.IsDmChannel;
        var global = UIButton("Global", page, "GLOBAL", -420, 226, 160, 38, () => { Social.SetChatChannel("global"); BuildSocialPage(); });
        global.interactable = !isGlobal;
        var guildButton = UIButton("GuildCh", page, "GUILD", -250, 226, 160, 38, () =>
        {
            if (Social.Guild != null) { Social.SetChatChannel("guild_" + Social.Guild.id); BuildSocialPage(); }
            else SocialNotice("Join a guild to use guild chat.");
        });
        guildButton.interactable = !isGuild;
        string title = isDm ? "PRIVATE  /  " + Social.DmFriendName
            : isGuild && Social.Guild != null ? "GUILD CHAT  [" + Social.Guild.tag + "]"
            : "GLOBAL CHAT  (all pilots)";
        var channelLabel = UILabel("Channel", page, title, 200, 236, 560, 26, 18, isDm ? new Color(1f, .85f, .45f) : accentColor);
        channelLabel.richText = false;
        string hint = isDm ? "Only you and your friend can see this chat." : Social.ChatExpires ? "Messages disappear after 2 minutes." : "";
        UILabel("ChannelHint", page, hint, 200, 212, 560, 20, 13, Color.gray);
        if (isDm) UiIcon.Attach(channelLabel, "chat", .9f);
        // กล่องข้อความแบบเลื่อนได้ (ข้อความเก่าอยู่บน ใหม่อยู่ล่าง)
        var log = UIPanel("Log", page, 0, -10, 1040, 420, new Color(.03f, .05f, .1f));
        log.raycastTarget = true; // ลากเลื่อนได้ทั้งกล่อง
        if (log.GetComponent<RectMask2D>() == null) log.gameObject.AddComponent<RectMask2D>();
        chatLogText = UILabel("Lines", log.transform, "", 0, 0, 1010, 400, 17, Color.white);
        var lines = chatLogText.rectTransform;
        lines.anchorMin = new Vector2(0, 1); lines.anchorMax = new Vector2(1, 1); lines.pivot = new Vector2(.5f, 1);
        lines.offsetMin = new Vector2(15, 0); lines.offsetMax = new Vector2(-15, 0);
        lines.anchoredPosition = new Vector2(0, -8);
        chatLogText.alignment = TextAlignmentOptions.TopLeft;
        chatLogText.enableAutoSizing = false;
        chatLogText.fontSize = 17;
        chatLogText.textWrappingMode = TextWrappingModes.Normal;
        chatLogText.overflowMode = TextOverflowModes.Overflow;
        chatLogText.richText = true;
        if (!chatLogText.TryGetComponent(out ContentSizeFitter fitter)) fitter = chatLogText.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        if (!log.TryGetComponent(out chatScroll)) chatScroll = log.gameObject.AddComponent<ScrollRect>();
        chatScroll.viewport = log.rectTransform; chatScroll.content = lines;
        chatScroll.horizontal = false; chatScroll.vertical = true;
        chatScroll.movementType = ScrollRect.MovementType.Clamped;
        chatScroll.scrollSensitivity = 30f;
        chatInput = CreateInput("ChatInput", page, -90, -252, 820, 44, isDm ? "Message " + Social.DmFriendName + "..." : "Type a message...", 120);
        chatInput.onSubmit.AddListener(_ => SendChatMessage());
        UIButton("Send", page, "SEND", 430, -252, 150, 44, SendChatMessage);
        UpdateChatLog(true);
    }

    // ส่งข้อความในช่องพิมพ์แชท ถ้าส่งไม่ได้ (ส่งถี่เกิน/ออฟไลน์) แสดงข้อความแจ้ง ส่งได้แล้วล้างช่องพิมพ์
    private void SendChatMessage()
    {
        if (chatInput == null) return;
        string error = Social.SendChat(chatInput.text);
        if (error != null) { SocialNotice(error); return; }
        chatInput.text = "";
    }

    // แสดงข้อความทั้งหมดของช่อง (สูงสุด 50) ข้อความเราสีเหลือง / แชทรวม: ข้อความใกล้หมดเวลา (เหลือ < 20 วิ) จางลง
    // เลื่อนลงล่างสุดอัตโนมัติ ยกเว้นผู้เล่นเลื่อนขึ้นไปอ่านข้อความเก่าอยู่ (forceBottom = เลื่อนลงเสมอ)
    private void UpdateChatLog() => UpdateChatLog(false);
    // วาดข้อความแชทของช่องปัจจุบันใหม่ทั้งหมด (forceBottom = เลื่อนลงล่างสุดเสมอ)
    private void UpdateChatLog(bool forceBottom)
    {
        if (chatLogText == null) return;
        bool atBottom = forceBottom || chatScroll == null || chatScroll.verticalNormalizedPosition < .05f;
        var lines = new System.Text.StringBuilder();
        int start = Mathf.Max(0, Social.Chat.Count - Social.ChatKeep);
        for (int i = start; i < Social.Chat.Count; i++)
        {
            var line = Social.Chat[i];
            string time = line.at > 0 ? System.DateTimeOffset.FromUnixTimeMilliseconds(line.at).ToLocalTime().ToString("HH:mm") + "  " : "";
            string tag = string.IsNullOrEmpty(line.tag) ? "" : "[" + Clean(line.tag) + "] ";
            int left = Social.SecondsLeft(line);
            string a = left >= 0 && left < 20 ? "80" : "FF"; // ความทึบ (ใส่ในสีทุกส่วน เพราะ <color> ล้างค่า alpha)
            string color = (line.uid == Social.Uid ? "#FFD95A" : "#7FE0F0") + a;
            lines.Append("<color=#7A8590" + a + ">" + time + "</color><color=" + color + ">" + tag + Clean(line.name) + ":</color> <color=#FFFFFF" + a + ">" + Clean(line.text) + "</color>\n");
        }
        chatLogText.text = lines.Length > 0 ? lines.ToString().TrimEnd('\n')
            : "<color=#7A8590>" + (Social.IsDmChannel ? "No messages yet. Say hi to " + Clean(Social.DmFriendName) + "!" : "No messages yet. Say hello!") + "</color>";
        if (chatScroll != null && atBottom)
        {
            Canvas.ForceUpdateCanvases();
            chatScroll.verticalNormalizedPosition = 0f;
        }
    }

    // ตัด < > ออกจากข้อความ กันผู้เล่นใส่ rich text tag ในกล่องแชท
    private static string Clean(string text) => string.IsNullOrEmpty(text) ? "" : text.Replace("<", "").Replace(">", "");

    // ===== กล่องคำชวนเข้าห้อง =====
    private void RefreshInvitePopup()
    {
        // คำชวนเข้าห้องแรงค์ (ไม่ควรมี แต่กันไว้) ทิ้งไปเลย
        if (FeatureFlags.RankedNoInvite)
            foreach (var stale in Social.Invites.ToArray()) if (IsRankedRoomName(stale.room)) Social.ClearInvite(stale);
        var invite = Social.Invites.Count > 0 && !PhotonNetwork.InRoom && profileLoaded ? Social.Invites[0] : null;
        if (invite == null)
        {
            if (invitePopup != null) invitePopup.gameObject.SetActive(false);
            shownInvite = null;
            return;
        }
        var root = ActiveSocialRoot;
        if (root == null || invite == shownInvite && invitePopup != null && invitePopup.gameObject.activeSelf) return;
        shownInvite = invite;
        if (invitePopup != null) Destroy(invitePopup.gameObject);
        invitePopup = UIPanel("InvitePopup" + (++pageSerial), root, 0, 250, 620, 120, new Color(.06f, .2f, .26f, .97f));
        invitePopup.raycastTarget = true;
        var text = UILabel("Text", invitePopup.transform, invite.name + " invites you to room " + invite.room, 0, 28, 590, 40, 22, Color.white);
        text.richText = false;
        UIButton("Join", invitePopup.transform, "JOIN", -110, -28, 180, 44, () =>
        {
            Social.ClearInvite(invite);
            invitePopup.gameObject.SetActive(false);
            CloseSocial();
            JoinRoomByName(invite.room);
        });
        UIButton("Ignore", invitePopup.transform, "IGNORE", 110, -28, 180, 44, () => { Social.ClearInvite(invite); invitePopup.gameObject.SetActive(false); });
    }

    // ===== ช่องพิมพ์ข้อความ (TMP_InputField สร้างด้วยโค้ด) =====
    private TMP_InputField CreateInput(string name, Transform parent, float x, float y, float w, float h, string placeholder, int limit)
    {
        var box = UIPanel(name, parent, x, y, w, h, new Color(.02f, .04f, .08f));
        box.raycastTarget = true;
        box.gameObject.SetActive(false);
        var area = UIRect("TextArea", box.transform, 0, 0, w - 20, h - 8);
        if (area.GetComponent<RectMask2D>() == null) area.gameObject.AddComponent<RectMask2D>();
        var text = UILabel("Text", area, "", 0, 0, w - 20, h - 8, 18, Color.white);
        var hint = UILabel("Placeholder", area, placeholder, 0, 0, w - 20, h - 8, 18, new Color(.5f, .55f, .6f));
        foreach (var label in new[] { text, hint })
        {
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableAutoSizing = false;
            label.fontSize = 18;
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }
        var input = box.GetComponent<TMP_InputField>();
        if (input == null) input = box.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = hint;
        input.characterLimit = limit;
        if (lobbyFont != null) input.fontAsset = lobbyFont;
        input.targetGraphic = box;
        box.gameObject.SetActive(true);
        return input;
    }
}
