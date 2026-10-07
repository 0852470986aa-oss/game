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

public partial class LobbyManager
{
    private enum SocialTab { Friends, Requests, Guild, Chat }
    private SocialTab socialTab;
    private Button socialButton, inviteFriendsButton;
    private TMP_Text socialBadge;
    private RectTransform socialHomeRoot, socialWaitRoot;
    private Image socialOverlay;
    private RectTransform socialWindow, socialPage;
    private TMP_Text socialMessageText, chatLogText;
    private TMP_InputField chatInput, friendCodeInput, guildNameInput, guildTagInput, guildJoinInput;
    private string socialMessage = "";
    private Image invitePopup;
    private Social.Invite shownInvite;
    private bool socialSubscribed, socialTicking;
    private float nextPresenceRefresh;

    // ===== ปุ่มบนหน้าหลัก (เรียกจาก BuildHomeScreen หลัง BuildProgressUI) =====
    private void BuildSocialHome(RectTransform root)
    {
        socialHomeRoot = root;
        // จัดแถว MISSIONS / PROFILE ใหม่ให้มีที่วาง SOCIAL (3 ปุ่มกว้าง 120)
        if (missionsButton != null) ResizeButton(missionsButton, 270, 127, 120, 46);
        if (profileButton != null) ResizeButton(profileButton, 395, 127, 120, 46);
        socialButton = UIButton("Social", root, "SOCIAL", 520, 127, 120, 46, () => OpenSocial(SocialTab.Friends));
        var dot = UIPanel("Badge", socialButton.transform, 50, 18, 28, 28, new Color(.9f, .25f, .2f));
        socialBadge = UILabel("Count", dot.transform, "", 0, 0, 28, 28, 15, Color.white);
        if (missionsBadge != null) ((RectTransform)missionsBadge.transform.parent).anchoredPosition = new Vector2(50, 18);
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

    private static void ResizeButton(Button button, float x, float y, float w, float h)
    {
        var rect = (RectTransform)button.transform;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
        var label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.rectTransform.sizeDelta = new Vector2(w - 8, h - 6);
    }

    // ทุก 1 วินาที: เริ่มระบบเมื่อพร้อม / อัปเดตสถานะห้อง / อ่านสถานะเพื่อนทุก 20 วิ / แสดงคำชวน
    private string presenceRoom = null;
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
            RefreshSocialBadge();
            RefreshInvitePopup();
            if (inviteFriendsButton != null) inviteFriendsButton.gameObject.SetActive(FeatureFlags.Social && Social.Ready && PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode);
            yield return wait;
        }
    }

    private void RefreshSocialBadge()
    {
        if (socialButton != null) socialButton.gameObject.SetActive(FeatureFlags.Social);
        if (socialBadge == null) return;
        int count = Social.Requests.Count + Social.Invites.Count;
        socialBadge.transform.parent.gameObject.SetActive(count > 0);
        socialBadge.text = count.ToString();
    }

    private void OnSocialChanged()
    {
        if (this == null) { Social.Changed -= OnSocialChanged; return; }
        RefreshSocialBadge();
        RefreshInvitePopup();
        // กำลังพิมพ์อยู่ไม่วาดหน้าใหม่ (จะทำให้คีย์บอร์ดหาย)
        if (socialOverlay != null && socialOverlay.gameObject.activeSelf && !AnyInputFocused() && socialTab != SocialTab.Chat) BuildSocialPage();
    }

    private void OnChatChanged()
    {
        if (this == null) { Social.ChatChanged -= OnChatChanged; return; }
        UpdateChatLog();
    }

    private bool AnyInputFocused()
    {
        foreach (var input in new[] { chatInput, friendCodeInput, guildNameInput, guildTagInput, guildJoinInput })
            if (input != null && input.isFocused) return true;
        return false;
    }

    // ===== หน้าต่าง =====
    private RectTransform ActiveSocialRoot => waitingRoomPanel != null && waitingRoomPanel.activeInHierarchy && socialWaitRoot != null ? socialWaitRoot : socialHomeRoot;

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
        socialTab = tab;
        socialMessage = Social.Ready ? "" : "Log in with an online account to use friends, chat and guilds.";
        Social.RefreshPresence();
        BuildSocialPage();
    }

    private void CloseSocial()
    {
        if (socialOverlay != null) socialOverlay.gameObject.SetActive(false);
    }

    private void SocialNotice(string message)
    {
        if (this == null) return;
        socialMessage = message;
        if (socialMessageText != null) socialMessageText.text = message;
    }

    private void BuildSocialPage()
    {
        if (socialWindow == null) return;
        if (socialPage != null) Destroy(socialPage.gameObject);
        chatInput = friendCodeInput = guildNameInput = guildTagInput = guildJoinInput = null;
        chatLogText = null;
        socialPage = UIRect("SPage" + (++pageSerial), socialWindow, 0, 0, 1080, 640);
        var page = socialPage;
        UILabel("Title", page, "SOCIAL", -440, 286, 180, 44, 30, Color.white);
        UIButton("Close", page, "CLOSE", 460, 286, 130, 44, CloseSocial);
        string[] tabs = { "FRIENDS (" + Social.Friends.Count + ")", "REQUESTS (" + Social.Requests.Count + ")", "GUILD", "CHAT" };
        bool[] on = { true, true, FeatureFlags.Guilds, FeatureFlags.Chat };
        for (int i = 0; i < tabs.Length; i++)
        {
            int tab = i;
            var button = UIButton("Tab" + i, page, tabs[i], -255 + i * 190, 286, 180, 44, () => { socialTab = (SocialTab)tab; socialMessage = ""; BuildSocialPage(); });
            button.interactable = on[i] && (int)socialTab != i;
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
        for (int i = 0; i < list.Count && i < 8; i++)
        {
            var friend = list[i];
            float y = 166 - i * 54;
            var row = UIPanel("Friend" + i, page, 0, y, 1040, 48, new Color(.06f, .1f, .17f));
            UIPanel("Dot", row.transform, -500, 0, 14, 14, friend.online ? new Color(.3f, 1f, .5f) : new Color(.4f, .45f, .5f));
            var name = UILabel("Name", row.transform, friend.name + (friend.level > 0 ? "   Lv" + friend.level : ""), -330, 0, 320, 40, 19, Color.white);
            name.richText = false;
            name.alignment = TextAlignmentOptions.Left;
            string status = !friend.online ? "OFFLINE" + LastSeen(friend.last) : string.IsNullOrEmpty(friend.room) ? "ONLINE  /  IN LOBBY" : "ONLINE  /  ROOM " + friend.room;
            UILabel("Status", row.transform, status, 0, 0, 340, 40, 16, friend.online ? new Color(.5f, 1f, .7f) : Color.gray);
            var target = friend;
            if (inRoom && friend.online && friend.room != PhotonNetwork.CurrentRoom.Name)
                UIButton("Invite", row.transform, "INVITE", 300, 0, 130, 38, () => { Social.SendInvite(target, PhotonNetwork.CurrentRoom.Name); SocialNotice("Invite sent to " + target.name + "."); });
            else if (!inRoom && friend.online && !string.IsNullOrEmpty(friend.room))
                UIButton("Join", row.transform, "JOIN", 300, 0, 130, 38, () => { CloseSocial(); JoinRoomByName(target.room); });
            UIButton("Remove", row.transform, "X", 470, 0, 60, 38, () =>
            {
                if (pendingRemove == target.uid) { Social.RemoveFriend(target); pendingRemove = null; SocialNotice("Removed " + target.name + "."); }
                else { pendingRemove = target.uid; SocialNotice("Tap X again to remove " + target.name + "."); }
            });
        }
    }

    private string pendingRemove;

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
            UILabel("CreateHead", page, "CREATE A GUILD  (" + Social.GuildCost + " ASTRONIUM)", -260, 220, 500, 32, 20, accentColor);
            guildNameInput = CreateInput("GuildName", page, -330, 170, 360, 44, "Guild name (3-20)", 20);
            guildTagInput = CreateInput("GuildTag", page, -60, 170, 150, 44, "TAG (3-5)", 5);
            UIButton("Create", page, "CREATE", 100, 170, 140, 44, () =>
            {
                SocialNotice("Creating...");
                Social.CreateGuild(guildNameInput.text, guildTagInput.text, msg => { SocialNotice(msg); StartCoroutine(RefreshCoinsLater()); });
            });
            UILabel("JoinHead", page, "OR JOIN A GUILD BY TAG", -260, 90, 500, 32, 20, accentColor);
            guildJoinInput = CreateInput("GuildJoin", page, -330, 40, 360, 44, "Guild tag", 5);
            UIButton("Join", page, "JOIN", -60, 40, 140, 44, () => { SocialNotice("Joining..."); Social.JoinGuild(guildJoinInput.text, SocialNotice); });
            UILabel("Info", page, "Guild members get a [TAG] before their name, a private guild chat and a member list.", 0, -60, 1000, 30, 16, Color.gray);
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
    private void BuildChatTab(RectTransform page)
    {
        bool guildChannel = Social.ChatChannel.StartsWith("guild_");
        var global = UIButton("Global", page, "GLOBAL", -420, 226, 160, 38, () => { Social.SetChatChannel("global"); BuildSocialPage(); });
        global.interactable = guildChannel;
        var guildButton = UIButton("GuildCh", page, "GUILD", -250, 226, 160, 38, () =>
        {
            if (Social.Guild != null) { Social.SetChatChannel("guild_" + Social.Guild.id); BuildSocialPage(); }
            else SocialNotice("Join a guild to use guild chat.");
        });
        guildButton.interactable = !guildChannel;
        UILabel("Channel", page, guildChannel && Social.Guild != null ? "GUILD CHAT  [" + Social.Guild.tag + "]" : "GLOBAL CHAT  (all pilots)", 200, 226, 500, 30, 18, accentColor);
        var log = UIPanel("Log", page, 0, -10, 1040, 420, new Color(.03f, .05f, .1f));
        chatLogText = UILabel("Lines", log.transform, "", 0, 0, 1010, 400, 17, Color.white);
        chatLogText.alignment = TextAlignmentOptions.BottomLeft;
        chatLogText.enableAutoSizing = false;
        chatLogText.fontSize = 17;
        chatLogText.textWrappingMode = TextWrappingModes.Normal;
        chatLogText.overflowMode = TextOverflowModes.Truncate;
        chatLogText.richText = true;
        chatInput = CreateInput("ChatInput", page, -90, -252, 820, 44, "Type a message...", 120);
        chatInput.onSubmit.AddListener(_ => SendChatMessage());
        UIButton("Send", page, "SEND", 430, -252, 150, 44, SendChatMessage);
        UpdateChatLog();
    }

    private void SendChatMessage()
    {
        if (chatInput == null) return;
        string error = Social.SendChat(chatInput.text);
        if (error != null) { SocialNotice(error); return; }
        chatInput.text = "";
    }

    // แสดง 14 ข้อความล่าสุด (ข้อความเราสีเหลือง)
    private void UpdateChatLog()
    {
        if (chatLogText == null) return;
        var lines = new System.Text.StringBuilder();
        int start = Mathf.Max(0, Social.Chat.Count - 14);
        for (int i = start; i < Social.Chat.Count; i++)
        {
            var line = Social.Chat[i];
            string time = line.at > 0 ? System.DateTimeOffset.FromUnixTimeMilliseconds(line.at).ToLocalTime().ToString("HH:mm") + "  " : "";
            string tag = string.IsNullOrEmpty(line.tag) ? "" : "[" + Clean(line.tag) + "] ";
            string color = line.uid == Social.Uid ? "#FFD95A" : "#7FE0F0";
            lines.Append("<color=#7A8590>" + time + "</color><color=" + color + ">" + tag + Clean(line.name) + ":</color> " + Clean(line.text) + "\n");
        }
        chatLogText.text = lines.Length > 0 ? lines.ToString() : "<color=#7A8590>No messages yet. Say hello!</color>";
    }

    private static string Clean(string text) => string.IsNullOrEmpty(text) ? "" : text.Replace("<", "").Replace(">", "");

    // ===== กล่องคำชวนเข้าห้อง =====
    private void RefreshInvitePopup()
    {
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
