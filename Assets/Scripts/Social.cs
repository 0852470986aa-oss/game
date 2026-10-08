// Social.cs — ระบบสังคม (เฟส 8): เพื่อน / คำขอเป็นเพื่อน / สถานะออนไลน์ / ชวนเข้าห้อง / แชท / กิลด์
// ทำงานผ่าน Firebase Realtime Database โดยตรง (ต้องล็อกอินแล้ว) โครงสร้างข้อมูล:
//   friend_codes/{CODE}            = uid               (โค้ดเพื่อน 8 ตัว ใช้ค้นหาเพื่อน)
//   friends/{uid}/{friendUid}      = ชื่อเพื่อน
//   friend_requests/{toUid}/{fromUid} = { name, at }
//   presence/{uid}                 = { name, online, room, last, level }   (online=false อัตโนมัติเมื่อหลุด ผ่าน OnDisconnect)
//   invites/{toUid}/{fromUid}      = { name, room, at }  (ชวนเข้าห้อง หมดอายุ 3 นาที)
//   chat/global/{id}, chat/guild_{gid}/{id}, chat/dm_{uidA}_{uidB}/{id} = { uid, name, tag, text, at }
//                                  (เก็บแค่ 50 ข้อความล่าสุด / แชทรวมหายเองหลัง 2 นาที / dm_ = แชทส่วนตัวกับเพื่อน)
//   dm_notify/{toUid}/{fromUid}    = { name, at }   (มีข้อความส่วนตัวใหม่ที่ยังไม่อ่าน)
//   guilds/{gid}                   = { name, tag, owner, members/{uid} = ชื่อ }
//   guild_tags/{TAG}               = gid ,  users/{uid}/guild = gid
// หน้าจออยู่ใน LobbyManager.Social.cs / ปิดได้ด้วย FeatureFlags.Social / Chat / Guilds
using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Database;
using Firebase.Extensions;

// คลาส static กลางของระบบสังคม: เก็บรายชื่อเพื่อน/คำขอ/คำชวน/แชท/กิลด์ไว้ในหน่วยความจำ และยิงอีเวนต์ Changed / ChatChanged ให้หน้าจอวาดใหม่
public static class Social
{
    // ข้อมูลเพื่อน 1 คน: uid, ชื่อ, ห้องที่อยู่ ('' = ล็อบบี้), ออนไลน์ไหม, เวลาออนไลน์ล่าสุด (ms) และเลเวล
    public class Friend { public string uid, name, room = ""; public bool online; public long last; public int level; }
    // คำขอเป็นเพื่อนที่ส่งมาหาเรา (uid และชื่อของผู้ส่ง)
    public class Request { public string uid, name; }
    // คำชวนเข้าห้องจากเพื่อน: uid/ชื่อผู้ชวน, ชื่อห้อง Photon และเวลาที่ชวน (ใช้เช็กหมดอายุ 3 นาที)
    public class Invite { public string uid, name, room; public long at; }
    // ข้อความแชท 1 บรรทัด: uid/ชื่อผู้ส่ง, ป้ายกิลด์ (tag), ข้อความ และเวลาที่ส่ง
    public class ChatLine { public string uid, name, tag, text; public long at; }
    // ข้อมูลกิลด์ที่เราอยู่: id, ชื่อ, ป้าย TAG, uid หัวหน้า และรายชื่อสมาชิก (uid -> ชื่อ)
    public class GuildInfo { public string id, name, tag, owner; public Dictionary<string, string> members = new Dictionary<string, string>(); }

    public const int GuildCost = 500;
    public const int GuildMaxMembers = 20;

    public static readonly List<Friend> Friends = new List<Friend>();
    public static readonly List<Request> Requests = new List<Request>();
    public static readonly List<Invite> Invites = new List<Invite>();
    public static readonly List<ChatLine> Chat = new List<ChatLine>();
    public static GuildInfo Guild { get; private set; }
    public static string ChatChannel { get; private set; } = "global";
    public static event Action Changed;
    public static event Action ChatChanged;

    private static bool started;
    private static string startedFor;
    private static Query chatQuery;
    private static DatabaseReference guildRef;
    private static float lastChatAt = -10f;

    private static DatabaseReference Db => FirebaseManager.Instance != null ? FirebaseManager.Instance.GetDbReference() : null;
    public static string Uid => FirebaseManager.Instance != null && FirebaseManager.Instance.IsLoggedIn() ? FirebaseManager.Instance.GetUserId() : null;
    public static string MyName => FirebaseManager.Instance != null ? FirebaseManager.Instance.GetUsername() : "PILOT";
    public static bool Ready => FeatureFlags.Social && Db != null && !string.IsNullOrEmpty(Uid);
    public static string GuildTag => Guild != null ? Guild.tag : "";
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    // โค้ดเพื่อน = 8 ตัวแรก (ตัวอักษร/ตัวเลข) ของ uid ตัวพิมพ์ใหญ่
    public static string FriendCode(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return "--------";
        var chars = new System.Text.StringBuilder();
        foreach (char c in uid) { if (char.IsLetterOrDigit(c)) chars.Append(char.ToUpperInvariant(c)); if (chars.Length == 8) break; }
        return chars.ToString();
    }
    public static string MyCode => FriendCode(Uid);

    // ===== เริ่มระบบ (เรียกจากล็อบบี้ทุกวินาที ทำงานจริงครั้งเดียวต่อบัญชี) =====
    public static void Start()
    {
        if (!Ready || (started && startedFor == Uid)) return;
        started = true;
        startedFor = Uid;
        string uid = Uid;
        var db = Db;
        db.Child("friend_codes").Child(MyCode).SetValueAsync(uid);
        db.Child("users").Child(uid).Child("friend_code").SetValueAsync(MyCode);
        // สถานะออนไลน์: ออนไลน์ตอนนี้ และให้ server ตั้งเป็นออฟไลน์เองเมื่อหลุดการเชื่อมต่อ
        var presence = db.Child("presence").Child(uid);
        presence.OnDisconnect().UpdateChildren(new Dictionary<string, object> { { "online", false }, { "room", "" }, { "last", ServerValue.Timestamp } });
        SetPresence("");
        // ฟังข้อมูลที่เปลี่ยนแบบ realtime
        db.Child("friends").Child(uid).ValueChanged += (s, e) => { if (e.Snapshot != null) ReadFriends(e.Snapshot); };
        db.Child("friend_requests").Child(uid).ValueChanged += (s, e) => { if (e.Snapshot != null) ReadRequests(e.Snapshot); };
        db.Child("invites").Child(uid).ValueChanged += (s, e) => { if (e.Snapshot != null) ReadInvites(e.Snapshot); };
        db.Child("users").Child(uid).Child("guild").ValueChanged += (s, e) => { if (e.Snapshot != null) WatchGuild(e.Snapshot.Value as string); };
        if (FeatureFlags.FriendChat) db.Child("dm_notify").Child(uid).ValueChanged += (s, e) => { if (e.Snapshot != null) ReadDmNotify(e.Snapshot); };
        // เวลา server ต่างจากเครื่องเท่าไร (ใช้นับอายุข้อความแชทรวมให้ตรงกันทุกเครื่อง)
        db.Root.Child(".info").Child("serverTimeOffset").ValueChanged += (s, e) => { if (e.Snapshot != null) { serverOffset = ToLong(e.Snapshot.Value); serverOffsetKnown = true; } };
        SetChatChannel("global");
    }

    // ห้องที่อยู่ ("" = อยู่ล็อบบี้) — เรียกตอนเข้า/ออกห้อง
    public static void SetPresence(string room)
    {
        if (!Ready) return;
        Db.Child("presence").Child(Uid).UpdateChildrenAsync(new Dictionary<string, object>
        {
            { "name", MyName }, { "online", true }, { "room", room ?? "" }, { "last", ServerValue.Timestamp },
            { "level", Progression.Level }
        });
    }

    // แจ้งหน้าจอว่าข้อมูลสังคม (เพื่อน/คำขอ/คำชวน/กิลด์) เปลี่ยน ผ่านอีเวนต์ Changed
    private static void Notify() => Changed?.Invoke();

    // ===== เพื่อน =====
    private static void ReadFriends(DataSnapshot snapshot)
    {
        var old = new Dictionary<string, Friend>();
        foreach (var f in Friends) old[f.uid] = f;
        Friends.Clear();
        foreach (var child in snapshot.Children)
        {
            var friend = old.TryGetValue(child.Key, out var known) ? known : new Friend { uid = child.Key };
            friend.name = child.Value?.ToString() ?? "PILOT";
            Friends.Add(friend);
        }
        RefreshPresence();
        Notify();
    }

    // อ่านสถานะออนไลน์ของเพื่อนทุกคน (ล็อบบี้เรียกทุก 20 วินาที)
    public static void RefreshPresence()
    {
        if (!Ready) return;
        foreach (var friend in Friends.ToArray())
        {
            var target = friend;
            Db.Child("presence").Child(target.uid).GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled || task.Result == null) return;
                var snap = task.Result;
                target.online = snap.Child("online").Value is bool on && on;
                target.room = snap.Child("room").Value?.ToString() ?? "";
                target.last = ToLong(snap.Child("last").Value);
                target.level = (int)ToLong(snap.Child("level").Value);
                if (snap.Child("name").Value != null) target.name = snap.Child("name").Value.ToString();
                Notify();
            });
        }
    }

    // แปลงค่าจาก Firebase (long/double/string) เป็น long อย่างปลอดภัย ถ้าเป็น null หรือแปลงไม่ได้คืน 0
    private static long ToLong(object value)
    {
        if (value == null) return 0;
        try { return Convert.ToInt64(value); } catch (Exception) { return 0; }
    }

    // ส่งคำขอเป็นเพื่อนด้วยโค้ด 8 ตัว
    public static void SendFriendRequest(string code, Action<string> done)
    {
        if (!Ready) { done("Log in online to add friends."); return; }
        code = (code ?? "").Trim().ToUpperInvariant();
        if (code.Length != 8) { done("Friend code must be 8 characters."); return; }
        if (code == MyCode) { done("That is your own code."); return; }
        string me = Uid;
        Db.Child("friend_codes").Child(code).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            string target = !task.IsFaulted && !task.IsCanceled && task.Result != null ? task.Result.Value as string : null;
            if (string.IsNullOrEmpty(target)) { done("No pilot found with code " + code + "."); return; }
            if (Friends.Exists(f => f.uid == target)) { done("You are already friends."); return; }
            Db.Child("friend_requests").Child(target).Child(me).SetValueAsync(new Dictionary<string, object> { { "name", MyName }, { "at", Now } })
                .ContinueWithOnMainThread(t => done(t.IsFaulted ? "Could not send the request." : "Friend request sent!"));
        });
    }

    // อ่านคำขอเป็นเพื่อนทั้งหมดจาก friend_requests/{uid} ใหม่ทุกครั้งที่ข้อมูลเปลี่ยน แล้วแจ้งหน้าจอ
    private static void ReadRequests(DataSnapshot snapshot)
    {
        Requests.Clear();
        foreach (var child in snapshot.Children)
            Requests.Add(new Request { uid = child.Key, name = child.Child("name").Value?.ToString() ?? "PILOT" });
        Notify();
    }

    // รับคำขอเป็นเพื่อน: เขียนชื่อลง friends ของทั้งสองฝั่ง แล้วลบคำขอนั้นทิ้ง
    public static void Accept(Request request)
    {
        if (!Ready || request == null) return;
        Db.Child("friends").Child(Uid).Child(request.uid).SetValueAsync(request.name);
        Db.Child("friends").Child(request.uid).Child(Uid).SetValueAsync(MyName);
        Db.Child("friend_requests").Child(Uid).Child(request.uid).RemoveValueAsync();
    }

    // ปฏิเสธคำขอเป็นเพื่อน: ลบคำขอออกจาก friend_requests ของเราเท่านั้น
    public static void Decline(Request request)
    {
        if (!Ready || request == null) return;
        Db.Child("friend_requests").Child(Uid).Child(request.uid).RemoveValueAsync();
    }

    // ลบเพื่อน: ลบออกจาก friends ของทั้งเราและของเพื่อน (เลิกเป็นเพื่อนทั้งสองฝั่ง)
    public static void RemoveFriend(Friend friend)
    {
        if (!Ready || friend == null) return;
        Db.Child("friends").Child(Uid).Child(friend.uid).RemoveValueAsync();
        Db.Child("friends").Child(friend.uid).Child(Uid).RemoveValueAsync();
    }

    // ===== ชวนเข้าห้อง =====
    public static void SendInvite(Friend friend, string room)
    {
        if (!Ready || friend == null || string.IsNullOrEmpty(room)) return;
        if (FeatureFlags.RankedNoInvite && LobbyManager.IsRankedRoomName(room)) return; // ห้องแรงค์ชวนไม่ได้
        Db.Child("invites").Child(friend.uid).Child(Uid).SetValueAsync(new Dictionary<string, object> { { "name", MyName }, { "room", room }, { "at", Now } });
    }

    // อ่านคำชวนเข้าห้องจาก invites/{uid} ทุกครั้งที่ข้อมูลเปลี่ยน คำชวนที่เก่ากว่า 3 นาทีจะถูกลบทิ้ง
    private static void ReadInvites(DataSnapshot snapshot)
    {
        Invites.Clear();
        foreach (var child in snapshot.Children)
        {
            long at = ToLong(child.Child("at").Value);
            // คำชวนเก่ากว่า 3 นาทีถือว่าหมดอายุ (ลบทิ้ง)
            if (Now - at > 180000) { child.Reference.RemoveValueAsync(); continue; }
            Invites.Add(new Invite { uid = child.Key, name = child.Child("name").Value?.ToString() ?? "PILOT", room = child.Child("room").Value?.ToString() ?? "", at = at });
        }
        Notify();
    }

    // ลบคำชวนออกจากรายการและจาก Firebase (เรียกตอนกด JOIN หรือ IGNORE ในกล่องคำชวน)
    public static void ClearInvite(Invite invite)
    {
        if (!Ready || invite == null) return;
        Invites.Remove(invite);
        Db.Child("invites").Child(Uid).Child(invite.uid).RemoveValueAsync();
        Notify();
    }

    // ===== แชท =====
    // ทุกช่อง (รวม / กิลด์ / ส่วนตัว) เก็บแค่ ChatKeep ข้อความล่าสุด ข้อความเก่ากว่านั้นคนส่งลบทิ้งจากฐานข้อมูล
    // แชทรวม: ข้อความหายเองหลัง GlobalChatLifeMs (2 นาที) — ห้องคุยสาธารณะไม่ค้างข้อความเก่า (แชทกิลด์/ส่วนตัวไม่หาย)
    // ปิด FeatureFlags.ChatLimits = เก็บ 30 ข้อความแบบเดิม ไม่หมดอายุ
    public const int ChatKeep = 50;
    public const long GlobalChatLifeMs = 120000;
    public static bool ChatExpires => FeatureFlags.ChatLimits && ChatChannel == "global";
    private static string oldestChatKey; // key ข้อความเก่าสุดที่เก็บ (ใช้ลบที่เก่ากว่านี้)
    private static long serverOffset;    // เวลา server - เวลาเครื่อง (ms) ใช้นับอายุข้อความให้ตรงกันทุกเครื่อง
    private static bool serverOffsetKnown; // ได้ค่า offset จาก server แล้ว (ก่อนหน้านั้นแค่ซ่อน ไม่ลบจากฐานข้อมูล กันนาฬิกาเครื่องเพี้ยนลบข้อความคนอื่น)
    private static long ServerNow => Now + serverOffset;
    // เวลา server (ms) ให้ระบบอื่นใช้ เช่นร้านค้ารายวัน (ยังไม่ได้ offset = เวลาเครื่อง)
    public static long ServerTimeMs => ServerNow;

    // ชื่อเพื่อนที่กำลังคุยส่วนตัวด้วย (ช่อง dm_...) / คนที่ส่งข้อความส่วนตัวมาแต่เรายังไม่ได้เปิดอ่าน (uid -> ชื่อ)
    public static string DmFriendName { get; private set; } = "";
    public static readonly Dictionary<string, string> UnreadDms = new Dictionary<string, string>();
    public static bool IsDmChannel => ChatChannel.StartsWith("dm_");

    // ชื่อช่องแชทส่วนตัวของ 2 คน (เรียง uid ให้ทั้งสองฝั่งได้ชื่อเดียวกัน)
    public static string DmChannel(string a, string b) => string.CompareOrdinal(a, b) < 0 ? "dm_" + a + "_" + b : "dm_" + b + "_" + a;

    // เปลี่ยนช่องแชท (global / guild_{gid} / dm_{uid}_{uid}) แล้วเริ่มฟังข้อความล่าสุดของช่องนั้น
    public static void SetChatChannel(string channel)
    {
        if (!Ready) return;
        if (chatQuery != null) chatQuery.ValueChanged -= OnChat;
        ChatChannel = channel;
        if (!channel.StartsWith("dm_")) DmFriendName = "";
        Chat.Clear();
        oldestChatKey = null;
        chatQuery = Db.Child("chat").Child(channel).LimitToLast(FeatureFlags.ChatLimits ? ChatKeep : 30);
        chatQuery.ValueChanged += OnChat;
        ChatChanged?.Invoke();
    }

    // เปิดแชทส่วนตัวกับเพื่อน (ปุ่ม CHAT ในรายชื่อเพื่อน) และล้างสถานะ "ยังไม่อ่าน" ของเพื่อนคนนั้น
    public static void OpenDm(Friend friend)
    {
        if (!Ready || friend == null || !FeatureFlags.FriendChat) return;
        SetChatChannel(DmChannel(Uid, friend.uid));
        DmFriendName = friend.name;
        dmFriendUid = friend.uid;
        MarkDmRead(friend.uid);
    }
    private static string dmFriendUid;

    // ตัวรับ ValueChanged ของช่องแชทปัจจุบัน: อ่านข้อความล่าสุดใหม่ทั้งหมด (แชทรวมข้ามข้อความหมดอายุ) แล้วยิงอีเวนต์ ChatChanged
    private static void OnChat(object sender, ValueChangedEventArgs e)
    {
        if (e.Snapshot == null) return;
        Chat.Clear();
        oldestChatKey = null;
        bool expires = ChatExpires;
        long count = e.Snapshot.ChildrenCount;
        foreach (var child in e.Snapshot.Children)
        {
            if (oldestChatKey == null && count >= ChatKeep) oldestChatKey = child.Key;
            long at = ToLong(child.Child("at").Value);
            // แชทรวมหมดอายุ: ไม่แสดง และลบทิ้งจากฐานข้อมูล (เครื่องไหนเห็นก่อนก็ลบ)
            if (expires && at > 0 && ServerNow - at > GlobalChatLifeMs) { if (serverOffsetKnown) child.Reference.RemoveValueAsync(); continue; }
            Chat.Add(new ChatLine
            {
                uid = child.Child("uid").Value?.ToString(), name = child.Child("name").Value?.ToString() ?? "PILOT",
                tag = child.Child("tag").Value?.ToString() ?? "", text = child.Child("text").Value?.ToString() ?? "", at = at
            });
        }
        ChatChanged?.Invoke();
    }

    // เรียกทุก 1 วินาทีจากล็อบบี้: เอาข้อความแชทรวมที่ครบ 2 นาทีออกจากจอ
    public static void TickChat()
    {
        if (!ChatExpires || Chat.Count == 0) return;
        int removed = Chat.RemoveAll(line => line.at > 0 && ServerNow - line.at > GlobalChatLifeMs);
        if (removed > 0) ChatChanged?.Invoke();
    }

    // วินาทีที่เหลือก่อนข้อความหายไป (ใช้แสดงในแชทรวม) / -1 = ไม่หมดอายุ
    public static int SecondsLeft(ChatLine line)
    {
        if (!ChatExpires || line.at <= 0) return -1;
        return (int)Mathf.Max(0, (GlobalChatLifeMs - (ServerNow - line.at)) / 1000);
    }

    // ส่งข้อความ (จำกัด 120 ตัวอักษร, 1 ข้อความ / 1.5 วินาที, กรองคำหยาบพื้นฐาน)
    public static string SendChat(string text)
    {
        if (!Ready || !FeatureFlags.Chat) return "Chat is offline.";
        text = (text ?? "").Trim();
        if (text.Length == 0) return null;
        if (Time.unscaledTime - lastChatAt < 1.5f) return "Slow down a little.";
        lastChatAt = Time.unscaledTime;
        if (text.Length > 120) text = text.Substring(0, 120);
        text = Filter(text);
        var channel = Db.Child("chat").Child(ChatChannel);
        channel.Push().SetValueAsync(new Dictionary<string, object>
        {
            { "uid", Uid }, { "name", MyName }, { "tag", GuildTag }, { "text", text }, { "at", ServerValue.Timestamp }
        });
        // แชทส่วนตัว: บอกเพื่อนว่ามีข้อความใหม่ (ขึ้นตัวเลขแจ้งเตือนที่ปุ่ม SOCIAL ของเพื่อน)
        if (IsDmChannel && !string.IsNullOrEmpty(dmFriendUid))
            Db.Child("dm_notify").Child(dmFriendUid).Child(Uid).SetValueAsync(new Dictionary<string, object> { { "name", MyName }, { "at", ServerValue.Timestamp } });
        PruneChat(channel);
        return null;
    }

    // ลบข้อความที่เก่ากว่า 50 ข้อความล่าสุดของช่องนี้ออกจากฐานข้อมูล (คนส่งเป็นคนลบ)
    private static void PruneChat(DatabaseReference channel)
    {
        if (!FeatureFlags.ChatLimits || string.IsNullOrEmpty(oldestChatKey)) return;
        string keep = oldestChatKey;
        channel.OrderByKey().EndAt(keep).LimitToFirst(40).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled || task.Result == null) return;
            foreach (var child in task.Result.Children)
                if (string.CompareOrdinal(child.Key, keep) < 0) child.Reference.RemoveValueAsync();
        });
    }

    // อ่านรายการ "มีข้อความส่วนตัวใหม่" จาก dm_notify/{uid} (ไม่นับเพื่อนที่กำลังเปิดคุยอยู่)
    private static void ReadDmNotify(DataSnapshot snapshot)
    {
        UnreadDms.Clear();
        foreach (var child in snapshot.Children)
        {
            if (IsDmChannel && child.Key == dmFriendUid) { child.Reference.RemoveValueAsync(); continue; }
            UnreadDms[child.Key] = child.Child("name").Value?.ToString() ?? "PILOT";
        }
        Notify();
    }

    // อ่านข้อความส่วนตัวของเพื่อนแล้ว: ลบแจ้งเตือน
    public static void MarkDmRead(string friendUid)
    {
        if (!Ready || string.IsNullOrEmpty(friendUid)) return;
        if (UnreadDms.Remove(friendUid)) Notify();
        Db.Child("dm_notify").Child(Uid).Child(friendUid).RemoveValueAsync();
    }

    // แก้รายการคำที่ไม่ต้องการได้ที่นี่
    private static readonly string[] BlockedWords = { "fuck", "shit", "เหี้ย", "ควย", "สัส" };
    // แทนคำใน BlockedWords ด้วย * (ไม่สนตัวพิมพ์เล็ก/ใหญ่) และตัด < > ออกกัน rich text ในแชท
    private static string Filter(string text)
    {
        foreach (string word in BlockedWords)
        {
            int index;
            while ((index = text.IndexOf(word, StringComparison.OrdinalIgnoreCase)) >= 0)
                text = text.Substring(0, index) + new string('*', word.Length) + text.Substring(index + word.Length);
        }
        return text.Replace("<", "").Replace(">", "");
    }

    // ===== กิลด์ =====
    private static void WatchGuild(string guildId)
    {
        if (guildRef != null) guildRef.ValueChanged -= OnGuild;
        guildRef = null;
        Guild = null;
        if (string.IsNullOrEmpty(guildId)) { Notify(); return; }
        guildRef = Db.Child("guilds").Child(guildId);
        guildRef.ValueChanged += OnGuild;
    }

    // ตัวรับ ValueChanged ของ guilds/{gid}: อ่านข้อมูลกิลด์ใหม่ ถ้ากิลด์ถูกลบหรือเราไม่อยู่ในสมาชิกแล้ว จะเคลียร์กิลด์ของเรา
    private static void OnGuild(object sender, ValueChangedEventArgs e)
    {
        var snap = e.Snapshot;
        if (snap == null || !snap.Exists) { Guild = null; Notify(); return; }
        var guild = new GuildInfo
        {
            id = snap.Key, name = snap.Child("name").Value?.ToString() ?? "GUILD",
            tag = snap.Child("tag").Value?.ToString() ?? "", owner = snap.Child("owner").Value?.ToString() ?? ""
        };
        foreach (var member in snap.Child("members").Children) guild.members[member.Key] = member.Value?.ToString() ?? "PILOT";
        // ถูกเอาออกจากกิลด์แล้ว
        if (!guild.members.ContainsKey(Uid)) { Db.Child("users").Child(Uid).Child("guild").RemoveValueAsync(); return; }
        Guild = guild;
        Notify();
    }

    // ตรวจป้ายกิลด์: ยาว 3-5 ตัว และเป็นตัวอักษรอังกฤษ/ตัวเลข ASCII เท่านั้น
    private static bool ValidTag(string tag)
    {
        if (tag.Length < 3 || tag.Length > 5) return false;
        foreach (char c in tag) if (!char.IsLetterOrDigit(c) || c > 127) return false;
        return true;
    }

    // สร้างกิลด์ (เสีย 500 เหรียญ) ชื่อ 3-20 ตัว ป้าย (TAG) 3-5 ตัวอังกฤษ/ตัวเลข ห้ามซ้ำ
    // FeatureFlags.AutoGuildTag: เกมสร้างแท็กให้เองจากชื่อกิลด์ (ไม่ต้องพิมพ์) ซ้ำกับของคนอื่น = เติมตัวเลขสุ่มจนได้แท็กว่าง
    public static void CreateGuild(string name, string tag, Action<string> done)
    {
        if (!Ready || !FeatureFlags.Guilds) { done("Guilds are offline."); return; }
        name = (name ?? "").Trim();
        if (name.Length < 3 || name.Length > 20) { done("Guild name must be 3-20 characters."); return; }
        if (Guild != null) { done("Leave your current guild first."); return; }
        if (FeatureFlags.AutoGuildTag) { FindFreeTag(name, 0, free => { if (free == null) done("Could not make a guild tag. Try again."); else CreateWithTag(name, free, done); }); return; }
        tag = (tag ?? "").Trim().ToUpperInvariant();
        if (!ValidTag(tag)) { done("Tag must be 3-5 letters or numbers (A-Z, 0-9)."); return; }
        CreateWithTag(name, tag, done);
    }

    // แท็กอัตโนมัติจากชื่อกิลด์: ตัวแรกของแต่ละคำ (อังกฤษ/ตัวเลข) เช่น "Star Hunter Squad" → SHS
    // คำน้อยไป = ใช้ตัวอักษรต้นชื่อ / ชื่อภาษาไทยล้วน = "GLD" / attempt > 0 = 3 ตัวแรก + เลขสุ่ม 2 หลัก (กันซ้ำ)
    public static string AutoTag(string name, int attempt)
    {
        var letters = new System.Text.StringBuilder();
        var initials = new System.Text.StringBuilder();
        bool wordStart = true;
        foreach (char raw in name ?? "")
        {
            char c = char.ToUpperInvariant(raw);
            bool ok = c < 128 && char.IsLetterOrDigit(c);
            if (ok) { letters.Append(c); if (wordStart) initials.Append(c); }
            wordStart = !ok;
        }
        string tag = initials.Length >= 3 ? initials.ToString() : letters.ToString();
        if (tag.Length < 3) tag = (tag + "GLD").Substring(0, 3);
        if (tag.Length > 4) tag = tag.Substring(0, 4);
        if (attempt > 0) tag = tag.Substring(0, 3) + UnityEngine.Random.Range(10, 100);
        return tag;
    }

    // หาแท็กที่ยังไม่มีใครใช้ (ลองสูงสุด 6 ครั้ง) แล้วส่งให้ found (หาไม่ได้ = null)
    private static void FindFreeTag(string name, int attempt, Action<string> found)
    {
        if (attempt >= 6) { found(null); return; }
        string tag = AutoTag(name, attempt);
        Db.Child("guild_tags").Child(tag).GetValueAsync().ContinueWithOnMainThread(check =>
        {
            if (check.IsFaulted || check.IsCanceled) { found(null); return; }
            if (check.Result != null && check.Result.Exists) FindFreeTag(name, attempt + 1, found);
            else found(tag);
        });
    }

    // สร้างกิลด์ด้วยแท็กที่ตรวจแล้ว: เช็กว่าแท็กว่าง → หักเหรียญ → เขียนข้อมูลกิลด์/แท็ก/สมาชิก
    private static void CreateWithTag(string name, string tag, Action<string> done)
    {
        var db = Db;
        string uid = Uid;
        db.Child("guild_tags").Child(tag).GetValueAsync().ContinueWithOnMainThread(check =>
        {
            if (!check.IsFaulted && check.Result != null && check.Result.Exists) { done("Tag [" + tag + "] is already taken."); return; }
            FirebaseManager.Instance.SpendCoins(GuildCost, (paid, balance) =>
            {
                if (!paid) { done("Not enough Astronium (" + GuildCost + " needed)."); return; }
                string gid = db.Child("guilds").Push().Key;
                db.Child("guilds").Child(gid).SetValueAsync(new Dictionary<string, object>
                {
                    { "name", name }, { "tag", tag }, { "owner", uid }, { "created", Now },
                    { "members", new Dictionary<string, object> { { uid, MyName } } }
                });
                db.Child("guild_tags").Child(tag).SetValueAsync(gid);
                db.Child("users").Child(uid).Child("guild").SetValueAsync(gid);
                done("Guild [" + tag + "] " + name + " created!");
            });
        });
    }

    // เข้าร่วมกิลด์ด้วยป้าย TAG: หา gid จาก guild_tags เช็กว่ายังไม่เต็ม 20 คน แล้วเพิ่มตัวเองเป็นสมาชิก (ผลส่งกลับทาง done)
    public static void JoinGuild(string tag, Action<string> done)
    {
        if (!Ready || !FeatureFlags.Guilds) { done("Guilds are offline."); return; }
        tag = (tag ?? "").Trim().ToUpperInvariant();
        if (Guild != null) { done("Leave your current guild first."); return; }
        var db = Db;
        string uid = Uid;
        db.Child("guild_tags").Child(tag).GetValueAsync().ContinueWithOnMainThread(lookup =>
        {
            string gid = !lookup.IsFaulted && lookup.Result != null ? lookup.Result.Value as string : null;
            if (string.IsNullOrEmpty(gid)) { done("No guild with tag [" + tag + "]."); return; }
            db.Child("guilds").Child(gid).Child("members").GetValueAsync().ContinueWithOnMainThread(members =>
            {
                if (!members.IsFaulted && members.Result != null && members.Result.ChildrenCount >= GuildMaxMembers) { done("That guild is full."); return; }
                db.Child("guilds").Child(gid).Child("members").Child(uid).SetValueAsync(MyName);
                db.Child("users").Child(uid).Child("guild").SetValueAsync(gid);
                done("Joined guild [" + tag + "]!");
            });
        });
    }

    // ออกจากกิลด์: หัวหน้าออก = ยกให้สมาชิกคนแรก / คนสุดท้ายออก = ลบกิลด์
    public static void LeaveGuild(Action<string> done)
    {
        if (!Ready || Guild == null) { done("You are not in a guild."); return; }
        var guild = Guild;
        var db = Db;
        string uid = Uid;
        guild.members.Remove(uid);
        if (guild.members.Count == 0)
        {
            db.Child("guilds").Child(guild.id).RemoveValueAsync();
            db.Child("guild_tags").Child(guild.tag).RemoveValueAsync();
        }
        else
        {
            db.Child("guilds").Child(guild.id).Child("members").Child(uid).RemoveValueAsync();
            if (guild.owner == uid) foreach (var next in guild.members.Keys) { db.Child("guilds").Child(guild.id).Child("owner").SetValueAsync(next); break; }
        }
        db.Child("users").Child(uid).Child("guild").RemoveValueAsync();
        if (ChatChannel.StartsWith("guild_")) SetChatChannel("global");
        done("You left [" + guild.tag + "].");
    }
}
