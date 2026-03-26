using System;
using System.Collections.Generic;
using System.Linq;
using KartLibrary.File;

namespace KartRider;

public class GameRoom
{
    // 房间唯一ID（创建后不可修改）
    public int RoomId { get; }
    public string RoomName { get; set; }
    public uint track { get; set; } = 0;
    public uint trackTemp { get; set; } = 0;
    public uint StartTicks { get; set; } = 0;
    public uint EndTicks { get; set; } = 0;
    public byte SpeedType { get; set; } = 0;
    public byte GameType { get; set; } = 0;
    public int RoomMaster { get; set; } = 0;
    public byte[] RoomData { get; set;} = new byte[32];
    public byte RandomTrackGameType { get; set; } = 0;
    public float redGauge { get; set; } = 0;
    public float blueGauge { get; set; } = 0;
    public bool Lock { get; set; } = false;
    public bool Started { get; set;} = false;
    public string LockPwd { get; set; } = "";
    public bool ReqRelay = false;
    public byte RelayType = 0; //0 - UDP 1 - TCP
    public Dictionary<int, uint> TimeData { get; set; } = new Dictionary<int, uint>();
    public Dictionary<int, int> Ranking { get; set; } = new Dictionary<int, int>();

    // 8个格子（0-7）
    public RoomMember[] _slots = new RoomMember[8];
    public RoomMember[] _IDs = new RoomMember[8];

    // 构造函数：初始化房间ID（由外部传入唯一ID）
    public GameRoom(int roomId)
    {
        RoomId = roomId; // 房间ID创建后固定不变
    }

    // 统计当前房间内的玩家数量（不包含AI）
    public int GetPlayerCount(byte team = 0)
    {
        int count = 0;
        foreach (var member in _slots)
        {
            if (member is Player && team == 0) // 仅统计玩家类型
                count++;
            else if (member is Player player && player.Team == team)
                count++;
        }
        return count;
    }

    // 统计当前房间内的Ai数量
    public int GetAiCount()
    {
        int count = 0;
        foreach (var member in _slots)
        {
            if (member is Ai)
                count++;
        }
        return count;
    }

    // 统计当前房间内的玩家数量
    public int GetCount()
    {
        int count = 0;
        foreach (var member in _slots)
        {
            if (member is Player || member is Ai)
                count++;
        }
        return count;
    }

    public SlotStatus GetSlotStatus(byte slotId)
    {
        if (!IsValidSlotId(slotId))
            throw new ArgumentOutOfRangeException(nameof(slotId), "格子ID必须在0-7之间");

        var member = _slots[slotId];
        if (member == null)
            return SlotStatus.Empty;       // 空位置
        if (member is Player)
            return SlotStatus.Player;      // 玩家
        if (member is Ai)
            return SlotStatus.Ai;          // AI
        return SlotStatus.Empty;           // 理论上不会走到这里（基类不会直接实例化）
    }

    // 辅助方法：获取指定位置的具体成员（可用于获取详细信息）
    public RoomMember GetSlotMember(byte slotId)
    {
        return IsValidSlotId(slotId) ? _slots[slotId] : null;
    }

    public RoomMember GetIdMember(int Id)
    {
        return IsValidSlotId((byte)Id) ? _IDs[Id] : null;
    }

    // 尝试添加玩家（成功后自动检查是否需要删除房间）
    public byte TryAddPlayer(string nickname, byte team, int playerType, SessionGroup client)
    {
        if (team == 2)
        {
            for (byte i = 0; i < 4; i++)
            {
                if (_slots[i] == null)
                {
                    int id = Array.IndexOf(_IDs, null);
                    _slots[i] = new Player
                    {
                        ID = id,
                        SlotId = i,
                        Nickname = nickname,
                        PlayerType = playerType,
                        Team = team,
                        Session = client
                    };
                    _IDs[id] = _slots[i];
                    return i;
                }
            }
            return 255; // 房间已满
        }
        else if (team == 1)
        {
            for (byte i = 4; i < 8; i++)
            {
                if (_slots[i] == null)
                {
                    int id = Array.IndexOf(_IDs, null);
                    _slots[i] = new Player
                    {
                        ID = id,
                        SlotId = i,
                        Nickname = nickname,
                        PlayerType = playerType,
                        Team = team,
                        Session = client
                    };
                    _IDs[id] = _slots[i];
                    return i;
                }
            }
            return 255; // 房间已满
        }
        else if (team == 0)
        {
            for (byte i = 0; i < 8; i++)
            {
                if (_slots[i] == null)
                {
                    int id = Array.IndexOf(_IDs, null);
                    _slots[i] = new Player
                    {
                        ID = id,
                        SlotId = i,
                        Nickname = nickname,
                        PlayerType = playerType,
                        Team = team,
                        Session = client
                    };
                    _IDs[id] = _slots[i];
                    return i;
                }
            }
            return 255; // 房间已满
        }
        else
        {
            return 255; // 未知队伍
        }
    }

    // 移除指定格子的成员（如果是玩家，需检查是否触发删除）
    public bool RemoveMember(byte slotId, out bool shouldDeleteRoom)
    {
        shouldDeleteRoom = false;
        if (!IsValidSlotId(slotId))
            throw new ArgumentOutOfRangeException(nameof(slotId), "格子ID必须在0-7之间");

        var removedMember = _slots[slotId];
        if (removedMember == null)
            return false; // 格子已为空

        if (removedMember is Player player)
        {
            _IDs[player.ID] = null;
            if (player.ID == RoomMaster)
            {
                foreach (RoomMember Object in _IDs)
                {
                    if (Object is Player p)
                    {
                        RoomMaster = p.ID;
                        p.PlayerType = 2;
                        break;
                    }
                }
            }
        }
        else if (removedMember is Ai ai)
        {
            _IDs[ai.ID] = null;
        }
        _slots[slotId] = null; // 清空格子

        // 如果移除的是玩家，检查剩余玩家数量
        if (removedMember is Player)
        {
            shouldDeleteRoom = GetPlayerCount() == 0;
        }

        MultyPlayer.GrSlotDataPacket(RoomId);
        return true;
    }

    // 其他方法：设置AI、获取格子信息等（沿用之前的逻辑，略）
    public byte TrySetAi(Ai aiData, byte team)
    {
        if (aiData == null)
            throw new ArgumentNullException(nameof(aiData), "AI数据不能为null");

        if (team == 2)
        {
            for (byte i = 0; i < 4; i++)
            {
                if (_slots[i] == null)
                {
                    int id = Array.IndexOf(_IDs, null);
                    aiData.ID = id;
                    aiData.Team = team;
                    _slots[i] = aiData;
                    _slots[i].SlotId = i;
                    _IDs[id] = _slots[i];
                    return i;
                }
            }
            return 255; // 房间已满
        }
        else if (team == 1)
        {
            for (byte i = 4; i < 8; i++)
            {
                if (_slots[i] == null)
                {
                    int id = Array.IndexOf(_IDs, null);
                    aiData.ID = id;
                    aiData.Team = team;
                    _slots[i] = aiData;
                    _slots[i].SlotId = i;
                    _IDs[id] = _slots[i];
                    return i;
                }
            }
            return 255; // 房间已满
        }
        else if (team == 0)
        {
            for (byte i = 0; i < 8; i++)
            {
                if (_slots[i] == null)
                {
                    int id = Array.IndexOf(_IDs, null);
                    aiData.ID = id;
                    aiData.Team = team;
                    _slots[i] = aiData;
                    _slots[i].SlotId = i;
                    _IDs[id] = _slots[i];
                    return i;
                }
            }
            return 255; // 房间已满
        }
        else
        {
            return 255; // 未知队伍
        }
    }

    public bool ChangeSlotId(byte slotId, byte newSlotId)
    {
        if (!IsValidSlotId(slotId) || !IsValidSlotId(newSlotId))
            throw new ArgumentOutOfRangeException(nameof(slotId), "格子ID必须在0-7之间");

        if (_slots[newSlotId] != null)
            return false;

        _slots[newSlotId] = _slots[slotId];
        _slots[slotId] = null;
        return true;
    }

    private bool IsValidSlotId(byte slotId) => slotId >= 0 && slotId < 8;
}

// 房间成员基类
public abstract class RoomMember
{
    public byte SlotId { get; set; } // 格子ID（0-7）
}

// 玩家类
public class Player : RoomMember
{
    public int ID { get; set; }
    public string Nickname { get; set; } // 玩家昵称
    public int PlayerType { get; set; } // 玩家类型
    public byte Team { get; set; }
    public SessionGroup Session { get; set; }
}

// AI类
public class Ai : RoomMember
{
    public int ID { get; set; }
    public short Character { get; set; }
    public short Rid { get; set; }
    public short Kart { get; set; }
    public short Balloon { get; set; }
    public short HeadBand { get; set; }
    public short Goggle { get; set; }
    public byte Team { get; set; }
}

public enum SlotStatus
{
    Empty,    // 空位置
    Player,   // 玩家
    Ai        // AI
}