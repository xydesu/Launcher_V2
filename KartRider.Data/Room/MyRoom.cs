using System;
using System.Collections.Generic;
using System.Net;
using KartRider.IO.Packet;
using Profile;

namespace KartRider;

public class MyRoom
{
    public string[] Players = new string[8];
}

public class MyRoomData
{
    public static Dictionary<string, MyRoom> MyRooms = new Dictionary<string, MyRoom>(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> _memberToOwner = new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly object _lock = new object();

    public static MyRoom GetOrCreateRoom(string owner)
    {
        if (string.IsNullOrEmpty(owner))
            return null;

        lock (_lock)
        {
            if (!MyRooms.TryGetValue(owner, out var room))
            {
                room = new MyRoom();
                MyRooms[owner] = room;
            }
            return room;
        }
    }

    public static bool TryEnterMyRoom(string owner, string member)
    {
        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(member))
            return false;

        if (owner != member && !ClientManager.NicknameToUserNO.ContainsKey(owner))
            return false;

        string previousOwner = null;
        bool added = false;

        lock (_lock)
        {
            if (_memberToOwner.TryGetValue(member, out previousOwner))
            {
                if (previousOwner == owner)
                {
                    return true;
                }

                RemoveMemberFromRoom(previousOwner, member);
            }

            var room = GetOrCreateRoom(owner);
            if (room == null)
                return false;

            if (owner == member)
            {
                for (int i = 1; i < 8; i++)
                {
                    if (room.Players[i] == member)
                        room.Players[i] = null;
                }

                room.Players[0] = member;
                added = true;
            }
            else
            {
                for (int i = 0; i < 8; i++)
                {
                    if (room.Players[i] == member)
                        return true;
                }

                for (int i = 1; i < 8; i++)
                {
                    if (room.Players[i] == null)
                    {
                        room.Players[i] = member;
                        _memberToOwner[member] = owner;
                        added = true;
                        break;
                    }
                }
            }

            if (!added)
                return false;

            _memberToOwner[member] = owner;
        }

        if (!string.IsNullOrEmpty(previousOwner) && !string.Equals(previousOwner, owner, StringComparison.Ordinal))
        {
            BroadcastRoomSlotData(previousOwner);
        }
        BroadcastRoomSlotData(owner);
        return true;
    }

    public static bool TryLeaveMyRoom(string member)
    {
        if (string.IsNullOrEmpty(member))
            return false;

        string owner;
        bool removed;

        lock (_lock)
        {
            if (!_memberToOwner.TryGetValue(member, out owner))
                return false;

            removed = RemoveMemberFromRoom(owner, member);
            _memberToOwner.Remove(member);
        }

        if (removed)
        {
            BroadcastRoomSlotData(owner);
        }
        return removed;
    }

    private static bool RemoveMemberFromRoom(string owner, string member)
    {
        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(member))
            return false;

        if (!MyRooms.TryGetValue(owner, out var room))
            return false;

        bool removed = false;
        for (int i = 0; i < 8; i++)
        {
            if (room.Players[i] == member)
            {
                room.Players[i] = null;
                removed = true;
                break;
            }
        }

        if (removed && IsRoomEmpty(room))
        {
            MyRooms.Remove(owner);
        }

        return removed;
    }

    private static bool IsRoomEmpty(MyRoom room)
    {
        if (room == null)
            return true;

        for (int i = 0; i < 8; i++)
        {
            if (!string.IsNullOrEmpty(room.Players[i]))
                return false;
        }
        return true;
    }

    public static string[] GetRoomPlayers(string owner)
    {
        if (string.IsNullOrEmpty(owner))
            return Array.Empty<string>();

        lock (_lock)
        {
            if (!MyRooms.TryGetValue(owner, out var room))
                return Array.Empty<string>();

            var result = new List<string>(8);
            for (int i = 0; i < 8; i++)
            {
                if (!string.IsNullOrEmpty(room.Players[i]))
                    result.Add(room.Players[i]);
            }
            return result.ToArray();
        }
    }

    private static SessionGroup GetOnlineSession(string nickname)
    {
        if (string.IsNullOrEmpty(nickname))
            return null;

        foreach (var session in ClientManager._clientSessions.Values)
        {
            if (string.Equals(session.Nickname, nickname, StringComparison.OrdinalIgnoreCase))
                return session;
        }
        return null;
    }

    private static void BroadcastRoomSlotData(string owner)
    {
        if (string.IsNullOrEmpty(owner))
            return;

        MyRoom room;
        lock (_lock)
        {
            if (!MyRooms.TryGetValue(owner, out room))
                return;
        }

        if (room == null)
            return;

        var sent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < 8; i++)
        {
            string member = room.Players[i];
            if (string.IsNullOrEmpty(member) || sent.Contains(member))
                continue;

            sent.Add(member);
            SessionGroup session = GetOnlineSession(member);
            if (session != null)
            {
                RmSlotDataPacket(session, member);
            }
        }
    }

    public static void ChRpEnterMyRoomPacket(SessionGroup Parent, string Nickname, string nickname = null)
    {
        string owner = string.IsNullOrEmpty(nickname) ? Nickname : nickname;

        if (!string.Equals(owner, Nickname, StringComparison.OrdinalIgnoreCase) && !ClientManager.NicknameToUserNO.ContainsKey(owner))
        {
            ChRpEnterMyRoomPacket(Parent, 3);
            return;
        }

        EnsureProfileLoaded(owner);

        if (!TryEnterMyRoom(owner, Nickname))
        {
            ChRpEnterMyRoomPacket(Parent, 1);
            return;
        }

        var ownerConfig = ProfileService.GetProfileConfig(owner);
        using (OutPacket outPacket = new OutPacket("ChRpEnterMyRoomPacket"))
        {
            outPacket.WriteString(owner);
            outPacket.WriteByte(0);
            outPacket.WriteShort(ownerConfig.MyRoom.MyRoom);
            outPacket.WriteByte(ownerConfig.MyRoom.MyRoomBGM);
            outPacket.WriteByte(ownerConfig.MyRoom.UseRoomPwd);
            outPacket.WriteByte(0);
            outPacket.WriteByte(ownerConfig.MyRoom.UseItemPwd);
            outPacket.WriteByte(ownerConfig.MyRoom.TalkLock);
            outPacket.WriteString(ownerConfig.MyRoom.RoomPwd);
            outPacket.WriteString("");
            outPacket.WriteString(ownerConfig.MyRoom.ItemPwd);
            outPacket.WriteShort(ownerConfig.MyRoom.MyRoomKart1);
            outPacket.WriteShort(ownerConfig.MyRoom.MyRoomKart2);
            Parent.Client.Send(outPacket);
        }
    }

    public static void ChRpEnterMyRoomPacket(SessionGroup Parent, byte errorCode = 3)
    {
        using (OutPacket outPacket = new OutPacket("ChRpEnterMyRoomPacket"))
        {
            outPacket.WriteString("");
            outPacket.WriteByte(errorCode); // 0：允许进入 3：玩家不存在 5：随机进入失败
            outPacket.WriteShort(0);
            outPacket.WriteByte(0);
            outPacket.WriteByte(0);
            outPacket.WriteByte(0);
            outPacket.WriteByte(0);
            outPacket.WriteByte(1);
            outPacket.WriteString("");//RoomPwd
            outPacket.WriteString("");
            outPacket.WriteString("");//ItemPwd 
            outPacket.WriteShort(0);
            outPacket.WriteShort(0);
            Parent.Client.Send(outPacket);
        }
        return;
    }

    public static void RmNotiMyRoomInfoPacket(SessionGroup Parent, string Nickname)
    {
        var myRoomConfig = ProfileService.GetProfileConfig(Nickname);
        using (OutPacket outPacket = new OutPacket("RmNotiMyRoomInfoPacket"))
        {
            outPacket.WriteShort(myRoomConfig.MyRoom.MyRoom);
            outPacket.WriteByte(myRoomConfig.MyRoom.MyRoomBGM);
            outPacket.WriteByte(myRoomConfig.MyRoom.UseRoomPwd);
            outPacket.WriteByte(0);
            outPacket.WriteByte(myRoomConfig.MyRoom.UseItemPwd);
            outPacket.WriteByte(myRoomConfig.MyRoom.TalkLock);
            outPacket.WriteString(myRoomConfig.MyRoom.RoomPwd);
            outPacket.WriteString("");
            outPacket.WriteString(myRoomConfig.MyRoom.ItemPwd);
            outPacket.WriteShort(myRoomConfig.MyRoom.MyRoomKart1);
            outPacket.WriteShort(myRoomConfig.MyRoom.MyRoomKart2);
            Parent.Client.Send(outPacket);
        }
    }

    public static void RmRiderTalkPacket(string Nickname, string Message)
    {
        string owner = GetRoomOwnerByNickname(Nickname);
        if (string.IsNullOrEmpty(owner))
            return;

        MyRoom room;
        lock (_lock)
        {
            if (!MyRooms.TryGetValue(owner, out room))
                return;
        }

        if (room == null)
            return;

        int slotIndex = -1;
        for (int i = 0; i < 8; i++)
        {
            if (string.Equals(room.Players[i], Nickname, StringComparison.OrdinalIgnoreCase))
            {
                slotIndex = i;
                break;
            }
        }

        if (slotIndex < 0)
            return;

        using (OutPacket outPacket = new OutPacket("RmRiderEchoPacket"))
        {
            outPacket.WriteInt(slotIndex);
            outPacket.WriteString(Message);
            var sent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < 8; i++)
            {
                string member = room.Players[i];
                if (string.IsNullOrEmpty(member) || string.Equals(member, Nickname, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!sent.Add(member))
                    continue;

                SessionGroup session = GetOnlineSession(member);
                if (session != null)
                {
                    session.Client.Send(outPacket);
                }
            }
        }
    }

    public static string GetRoomOwnerByNickname(string nickname)
    {
        if (string.IsNullOrEmpty(nickname))
            return null;

        lock (_lock)
        {
            if (MyRooms.ContainsKey(nickname))
                return nickname;
            if (_memberToOwner.TryGetValue(nickname, out var owner))
                return owner;
            return null;
        }
    }

    private static void EnsureProfileLoaded(string nickname)
    {
        if (string.IsNullOrEmpty(nickname))
            return;

        if (!Profile.FileName.FileNames.ContainsKey(nickname))
        {
            Profile.FileName.Load(nickname);
        }
    }

    private static IPEndPoint GetPlayerEndPoint(string nickname)
    {
        if (string.IsNullOrEmpty(nickname))
            return new IPEndPoint(System.Net.IPAddress.Any, 0);

        EnsureProfileLoaded(nickname);
        var profile = ProfileService.GetProfileConfig(nickname);
        try
        {
            IPEndPoint client = ClientManager.ClientToIPEndPoint(profile.Rider.ClientId);
            if (client != null)
            {
                return new IPEndPoint(client.Address, profile.Rider.P2pPort);
            }
        }
        catch
        {
        }

        return new IPEndPoint(System.Net.IPAddress.Any, 0);
    }

    private static void WriteEmptySlot(OutPacket outPacket)
    {
        outPacket.WriteBytes(new byte[132]);
        outPacket.WriteByte(0xFF);
    }

    private static void WritePlayerSlot(OutPacket outPacket, string nickname)
    {
        if (string.IsNullOrEmpty(nickname))
        {
            WriteEmptySlot(outPacket);
            return;
        }

        EnsureProfileLoaded(nickname);
        if (!ClientManager.NicknameToUserNO.ContainsKey(nickname))
        {
            WriteEmptySlot(outPacket);
            return;
        }
        var profile = ProfileService.GetProfileConfig(nickname);

        outPacket.WriteUInt(ClientManager.GetUserNO(nickname));
        outPacket.WriteEndPoint(GetPlayerEndPoint(nickname));
        outPacket.WriteEndPoint(new IPEndPoint(IPAddress.Any, 0));
        outPacket.WriteString(nickname);
        GameSupport.GetRider(nickname, outPacket);
        outPacket.WriteUInt(profile.Rider.RP);
        outPacket.WriteBytes(new byte[29]);
        outPacket.WriteString(profile.Rider.ClubName);
        outPacket.WriteByte(0);
    }

    public static void RmSlotDataPacket(SessionGroup Parent, string Nickname)
    {
        using (OutPacket outPacket = new OutPacket("RmSlotDataPacket"))
        {
            string owner = GetRoomOwnerByNickname(Nickname);
            MyRoom room = null;
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(owner) && MyRooms.TryGetValue(owner, out var foundRoom))
                {
                    room = foundRoom;
                }
            }

            if (room != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    WritePlayerSlot(outPacket, room.Players[i]);
                }
            }
            else
            {
                for (int i = 0; i < 8; i++)
                {
                    WriteEmptySlot(outPacket);
                }
            }

            Parent.Client.Send(outPacket);
        }
    }

    public static void ChRpSecedeMyRoomPacket(SessionGroup Parent, string Nickname)
    {
        TryLeaveMyRoom(Nickname);
        using (OutPacket outPacket = new OutPacket("ChRpSecedeMyRoomPacket"))
        {
            outPacket.WriteByte(1);
            Parent.Client.Send(outPacket);
        }
    }
}