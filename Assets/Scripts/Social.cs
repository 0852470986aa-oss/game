// Social.cs — ระบบสังคม (เฟส 8): เพื่อน / คำขอเป็นเพื่อน / สถานะออนไลน์ / ชวนเข้าห้อง / แชท / กิลด์
// ทำงานผ่าน Firebase Realtime Database โดยตรง (ต้องล็อกอินแล้ว) โครงสร้างข้อมูล:
//   friend_codes/{CODE}            = uid               (โค้ดเพื่อน 8 ตัว ใช้ค้นหาเพื่อน)
//   friends/{uid}/{friendUid}      = ชื่อเพื่อน
//   friend_requests/{toUid}/{fromUid} = { name, at }
//   presence/{uid}                 = { name, online, room, last, level }   (online=false อัตโนมัติเมื่อหลุด ผ่าน OnDisconnect)
//   invites/{toUid}/{fromUid}      = { name, room, at }  (ชวนเข้าห้อง หมดอายุ 3 นาที)
//   chat/global/{id}, chat/guild_{gid}/{id} = { uid, name, tag, text, at }   (อ่านแค่ 30 ข้อความล่าสุด)
//   guilds/{gid}                   = { name, tag, owner, members/{uid} = ชื่อ }
//   guild_tags/{TAG}               = gid ,  users/{uid}/guild = gid
// หน้าจออยู่ใน LobbyManager.Social.cs / ปิดได้ด้วย FeatureFlags.Social / Chat / Guilds
using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Database;
using Firebase.Extensions;

public static class Social
{
    public class Friend { public string uid, name, room = ""; public bool online; public long last; public int level; }
    public class Request { public string uid, name; }
    public class Invite { public string uid, name, room; public long at; }
    public class ChatLine { public string uid, name, tag, text; public long at; }
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

    private static void ReadRequests(DataSnapshot snapshot)
    {
        Requests.Clear();
        foreach (var child in snapshot.Children)
            Requests.Add(new Request { uid = child.Key, name = child.Child("name").Value?.ToString() ?? "PILOT" });
        Notify();
    }

    public static void Accept(Request request)
    {
        if (!Ready || request == null) return;
        Db.Child("friends").Child(Uid).Child(request.uid).SetValueAsync(request.name);
        Db.Child("friends").Child(request.uid).Child(Uid).SetValueAsync(MyName);
        Db.Child("friend_requests").Child(Uid).Child(request.uid).RemoveValueAsync();
    }

    public static void Decline(Request request)
    {
        if (!Ready || request == null) return;
        Db.Child("friend_requests").Child(Uid).Child(request.uid).RemoveValueAsync();
    }

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
        Db.Child("invites").Child(friend.uid).Child(Uid).SetValueAsync(new Dictionary<string, object> { { "name", MyName }, { "room", room }, { "at", Now } });
    }

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

    public static void ClearInvite(Invite invite)
    {
        if (!Ready || invite == null) return;
        Invites.Remove(invite);
        Db.Child("invites").Child(Uid).Child(invite.uid).RemoveValueAsync();
        Notify();
    }

    // ===== แชท =====
    public static void SetChatChannel(string channel)
    {
        if (!Ready) return;
        if (chatQuery != null) chatQuery.ValueChanged -= OnChat;
        ChatChannel = channel;
        Chat.Clear();
        chatQuery = Db.Child("chat").Child(channel).LimitToLast(30);
        chatQuery.ValueChanged += OnChat;
    }

    private static void OnChat(object sender, ValueChangedEventArgs e)
    {
        if (e.Snapshot == null) return;
        Chat.Clear();
        foreach (var child in e.Snapshot.Children)
            Chat.Add(new ChatLine
            {
                uid = child.Child("uid").Value?.ToString(), name = child.Child("name").Value?.ToString() ?? "PILOT",
                tag = child.Child("tag").Value?.ToString() ?? "", text = child.Child("text").Value?.ToString() ?? "", at = ToLong(child.Child("at").Value)
            });
        ChatChanged?.Invoke();
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
        Db.Child("chat").Child(ChatChannel).Push().SetValueAsync(new Dictionary<string, object>
        {
            { "uid", Uid }, { "name", MyName }, { "tag", GuildTag }, { "text", text }, { "at", Now }
        });
        return null;
    }

    // แก้รายการคำที่ไม่ต้องการได้ที่นี่
    private static readonly string[] BlockedWords = { "fuck", "shit", "เหี้ย", "ควย", "สัส" };
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

    private static bool ValidTag(string tag)
    {
        if (tag.Length < 3 || tag.Length > 5) return false;
        foreach (char c in tag) if (!char.IsLetterOrDigit(c) || c > 127) return false;
        return true;
    }

    // สร้างกิลด์ (เสีย 500 เหรียญ) ชื่อ 3-20 ตัว ป้าย (TAG) 3-5 ตัวอังกฤษ/ตัวเลข ห้ามซ้ำ
    public static void CreateGuild(string name, string tag, Action<string> done)
    {
        if (!Ready || !FeatureFlags.Guilds) { done("Guilds are offline."); return; }
        name = (name ?? "").Trim();
        tag = (tag ?? "").Trim().ToUpperInvariant();
        if (name.Length < 3 || name.Length > 20) { done("Guild name must be 3-20 characters."); return; }
        if (!ValidTag(tag)) { done("Tag must be 3-5 letters or numbers (A-Z, 0-9)."); return; }
        if (Guild != null) { done("Leave your current guild first."); return; }
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
