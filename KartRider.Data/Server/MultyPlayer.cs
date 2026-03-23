using ExcData;
using KartRider.Common.Network;
using KartRider.Common.Utilities;
using KartRider.IO.Packet;
using KartRider_PacketName;
using Profile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Linq;
using System.Security;

namespace KartRider;

public static class MultyPlayer
{
    static string Nickname;
    public static List<short> itemProb_indi = new List<short>();
    public static List<short> itemProb_team = new List<short>();
    public static Dictionary<short, AICharacter> aiCharacterDict = new Dictionary<short, AICharacter>();
    public static Dictionary<short, AIKart> aiKartDict = new Dictionary<short, AIKart>();
    public static Dictionary<string, byte> StartTimeAttack = new Dictionary<string, byte>();
    public static Dictionary<string, bool> Ready = null;
    public static SpecialKartConfig kartConfig = new SpecialKartConfig();
    public static int[] teamPoints = { 10, 8, 6, 5, 4, 3, 2, 1 };

    public static void milTime(uint time)
    {
        TimeSpan timeSpan = TimeSpan.FromMilliseconds((long)time);
        uint min = (uint)timeSpan.Minutes;
        uint sec = (uint)timeSpan.Seconds;
        uint mil = (uint)timeSpan.Milliseconds;
        Console.WriteLine($"成绩: {min}:{sec}:{mil}");
    }

    public static uint ConvertTick()
    {
        // 1. 先处理负数（TickCount64理论上不会为负，但做防御性判断）
        if (Environment.TickCount64 < 0)
        {
            return 0; // 或根据需求返回uint.MaxValue，TickCount64实际不会为负
        }

        // 2. 判断是否超出uint范围（uint.MaxValue是4294967295）
        if (Environment.TickCount64 > uint.MaxValue)
        {
            return uint.MaxValue; // 溢出时返回最大值
        }

        // 3. 未溢出则直接转换
        return (uint)Environment.TickCount64;
    }

    public static Dictionary<int, int> GetAllRanks(Dictionary<int, uint> timeData)
    {
        if (timeData.Count == 0)
            return new Dictionary<int, int>();

        // 按值降序排序（值越大排名越靠前）
        var sortedItems = timeData
            .OrderBy(item => item.Value)
            .ToList();

        var ranks = new Dictionary<int, int>();

        // 排名从0开始，逐个分配（相同值也会依次+1）
        for (int i = 0; i < sortedItems.Count; i++)
        {
            ranks[sortedItems[i].Key] = i; // 直接使用索引作为排名
        }

        return ranks;
    }

    static void Start(SessionGroup Parent, int roomId)
    {
        var room = RoomManager.GetRoom(roomId);
        if (room == null)
        {
            Console.WriteLine($"房间 {roomId} 不存在");
            return;
        }
        room.Started = true;
        Ready = new Dictionary<string, bool>();
        foreach (var player in room._slots)
        {
            if (player is Player p)
            {
                Ready[p.Nickname] = false;
            }
        }

        // 标记是否所有值都为true
        bool allReady = true;

        // 第一步：遍历字典值，检查是否有false
        foreach (bool value in Ready.Values)
        {
            if (!value) // 只要有一个值为false，标记为未全部就绪
            {
                allReady = false;
                break; // 找到false后提前退出遍历，提升效率
            }
        }

        // 第二步：用while循环判断（根据allReady的值执行逻辑）
        // 场景1：等待所有值变为true（循环直到全部为true）
        while (!allReady)
        {
            Console.WriteLine("存在未就绪的玩家，等待中...");

            // 模拟：重新检查字典值（实际场景中可替换为刷新数据的逻辑）
            allReady = true;
            foreach (bool value in Ready.Values)
            {
                if (!value)
                {
                    allReady = false;
                    break;
                }
            }

            // 模拟等待（避免死循环，实际场景可替换为业务逻辑）
            System.Threading.Thread.Sleep(1000);

            // 可选：添加退出条件，防止无限循环（比如超时）
            // 示例：累计等待5秒后退出
            int waitCount = 0;
            waitCount++;
            if (waitCount >= 15)
            {
                List<string> unreadyNames = Ready.Keys.Where(x => !Ready[x]).ToList();
                foreach (string name in unreadyNames)
                {
                    List<RoomMember> players = room._slots.Where(x => x is Player p && p.Nickname == name).ToList();
                    foreach (Player player in players)
                    {
                        player.Session.Client.Disconnect();
                        Ready.Remove(name);
                        break;
                    }
                }
                break;
            }
        }

        // 循环结束后输出结果
        if (allReady)
        {
            Set_startTrigger(Parent, room);
        }
    }

    static void Set_startTrigger(SessionGroup Parent, GameRoom room)
    {
        var onceTimer = new System.Timers.Timer();
        onceTimer.Interval = 1000;
        onceTimer.Elapsed += new System.Timers.ElapsedEventHandler((s, _event) => startTrigger(Parent, room, s, _event));
        onceTimer.AutoReset = false;
        onceTimer.Start();
    }

    static void startTrigger(SessionGroup Parent, GameRoom room, object sender, System.Timers.ElapsedEventArgs e)
    {
        room.StartTicks = ConvertTick() + 3000;
        using (OutPacket oPacket = new OutPacket("GameAiMasterSlotNoticePacket"))
        {
            oPacket.WriteInt();
            BroadCast(room.RoomId, oPacket);
        }
        using (OutPacket oPacket = new OutPacket("GameControlPacket"))
        {
            oPacket.WriteInt(1);
            oPacket.WriteByte(0);
            oPacket.WriteUInt(room.StartTicks);
            BroadCast(room.RoomId, oPacket);
        }
        room.TimeData = new Dictionary<int, uint>();
        room.Ranking = new Dictionary<int, int>();
        room.EndTicks = 0;
        Ready = null;
        Console.WriteLine("StartTicks = {0}", room.StartTicks);
    }

    static void Set_settleTrigger(SessionGroup Parent, int roomId)
    {
        var onceTimer = new System.Timers.Timer();
        onceTimer.Interval = 10000;
        onceTimer.Elapsed += new System.Timers.ElapsedEventHandler((s, _event) => settleTrigger(Parent, roomId, s, _event));
        onceTimer.AutoReset = false;
        onceTimer.Start();
    }

    static void settleTrigger(SessionGroup Parent, int roomId, object sender, System.Timers.ElapsedEventArgs e)
    {
        var room = RoomManager.GetRoom(roomId);
        if (room == null)
        {
            return;
        }

        if (room.TimeData.Count < room.GetCount())
        {
            foreach (RoomMember Object in room._slots)
            {
                if (Object is Player player)
                {
                    if (!room.TimeData.ContainsKey(player.ID))
                    {
                        room.TimeData[player.ID] = uint.MaxValue;
                    }
                }
                else if (Object is Ai ai)
                {
                    if (!room.TimeData.ContainsKey(ai.ID))
                    {
                        room.TimeData[ai.ID] = uint.MaxValue;
                    }
                }
            }
        }
        room.Ranking = GetAllRanks(room.TimeData);
        int redTeam = 0;
        int blueTeam = 0;
        var firstId = room.Ranking.First(kv => kv.Value == 0).Key;
        byte firstTeam = 0;
        if (RoomManager.TryGetIdDetail(roomId, firstId) is Player p)
        {
            firstTeam = p.Team;
        }
        else if (RoomManager.TryGetIdDetail(roomId, firstId) is Ai ai)
        {
            firstTeam = ai.Team;
        }
        Console.WriteLine("第一名 ID: {0} Team: {1}", firstId, firstTeam);
        foreach (RoomMember Object in room._slots)
        {
            if (Object is Player p2)
            {
                if (p2.Team == 2 && room.TimeData[p2.ID] != uint.MaxValue)
                {
                    blueTeam += teamPoints[room.Ranking[p2.ID]];
                }
                else if (p2.Team == 1 && room.TimeData[p2.ID] != uint.MaxValue)
                {
                    redTeam += teamPoints[room.Ranking[p2.ID]];
                }
            }
            if (Object is Ai a2)
            {
                if (a2.Team == 2 && room.TimeData[a2.ID] != uint.MaxValue)
                {
                    blueTeam += teamPoints[room.Ranking[a2.ID]];
                }
                else if (a2.Team == 1 && room.TimeData[a2.ID] != uint.MaxValue)
                {
                    redTeam += teamPoints[room.Ranking[a2.ID]];
                }
            }
        }

        using (OutPacket outPacket = new OutPacket("GameNextStagePacket"))
        {
            outPacket.WriteByte(room.GameType);
            outPacket.WriteInt();
            outPacket.WriteInt();
            BroadCast(roomId, outPacket);
        }
        using (OutPacket outPacket = new OutPacket("GameResultPacket"))
        {
            if (room.GameType == 3)
            {
                if (redTeam == blueTeam)
                {
                    outPacket.WriteByte(firstTeam);
                }
                else
                {
                    outPacket.WriteByte((byte)(redTeam > blueTeam ? 1 : 2));
                }
            }
            else if (room.GameType == 4)
            {
                outPacket.WriteByte(firstTeam);
            }
            else
            {
                outPacket.WriteByte(0);
            }

            outPacket.WriteInt(room.GetPlayerCount()); // player count
            foreach (RoomMember Object in room._IDs)
            {
                if (Object is Player p3)
                {
                    // Ensure profile is loaded for the player
                    if (!ProfileService.ProfileConfigs.ContainsKey(p3.Nickname))
                    {
                        if (!FileName.FileNames.ContainsKey(p3.Nickname))
                        {
                            FileName.Load(p3.Nickname);
                        }
                        ProfileService.Load(p3.Nickname);
                    }

                    outPacket.WriteInt(p3.ID); // player id
                    outPacket.WriteUInt(room.TimeData[p3.ID]);
                    outPacket.WriteByte();
                    outPacket.WriteUShort(ProfileService.ProfileConfigs[p3.Nickname].RiderItem.Set_Kart);
                    int playerRanking = room.Ranking[p3.ID];
                    int playerPoint = room.TimeData[p3.ID] == uint.MaxValue ? 0 : teamPoints[playerRanking];
                    Console.WriteLine("Player {0} 排名 {1} 得分 {2}", p3.ID, playerRanking, playerPoint);
                    outPacket.WriteInt(playerRanking);
                    if (room.GameType == 3 || room.GameType == 4)
                    {
                        outPacket.WriteShort(2); //2
                    }
                    else
                    {
                        outPacket.WriteShort(0);
                    }
                    outPacket.WriteByte();
                    outPacket.WriteUInt(ProfileService.ProfileConfigs[p3.Nickname].Rider.RP += 10000);
                    outPacket.WriteInt(10000); // Earned RP
                    outPacket.WriteInt(10000); // Earned Lucci
                    outPacket.WriteUInt(ProfileService.ProfileConfigs[p3.Nickname].Rider.Lucci += 10000);
                    outPacket.WriteBytes(new byte[29]);

                    if (room.GameType == 3 || room.GameType == 4)
                    {
                        outPacket.WriteInt(playerPoint);
                        outPacket.WriteByte(p3.Team); // Team
                    }
                    else
                    {
                        outPacket.WriteInt(0);
                        outPacket.WriteByte(0);
                    }
                    outPacket.WriteBytes(new byte[12]);
                    outPacket.WriteInt(1);
                    outPacket.WriteByte(0);
                    outPacket.WriteUShort(ProfileService.ProfileConfigs[p3.Nickname].RiderItem.Set_Character);
                    outPacket.WriteBytes(new byte[49]);
                    outPacket.WriteHexString("FF");
                    outPacket.WriteBytes(new byte[37]);
                    outPacket.WriteInt(ProfileService.ProfileConfigs[p3.Nickname].Rider.ClubMark_LOGO);
                    outPacket.WriteBytes(new byte[39]);
                }
            }

            outPacket.WriteInt(room.GetAiCount()); // AI count
            foreach (RoomMember Object in room._IDs)
            {
                if (Object is Ai a3)
                {
                    outPacket.WriteInt(a3.ID);
                    outPacket.WriteUInt(room.TimeData[a3.ID]);
                    outPacket.WriteByte();

                    // 获取 kart 属性值
                    outPacket.WriteShort(a3.Kart);
                    int AiRanking = room.Ranking[a3.ID];
                    int AiPoint = room.TimeData[a3.ID] == uint.MaxValue ? 0 : teamPoints[AiRanking];
                    Console.WriteLine("AI {0} 排名 {1} 得分 {2}", a3.ID, AiRanking, AiPoint);
                    outPacket.WriteInt(AiRanking);
                    outPacket.WriteShort(0);
                    if (room.GameType == 3 || room.GameType == 4)
                    {
                        outPacket.WriteByte(a3.Team); // Team
                        outPacket.WriteInt(AiPoint);
                    }
                    else
                    {
                        outPacket.WriteByte(0);
                        outPacket.WriteInt(0);
                    }
                }
            }
            Console.WriteLine("红队得分 {0} 蓝队得分 {1}", redTeam, blueTeam);
            outPacket.WriteBytes(new byte[34]);
            outPacket.WriteHexString("FF FF FF FF 00 00 00 00 00");
            BroadCast(roomId, outPacket);
        }
        using (OutPacket outPacket = new OutPacket("GameControlPacket"))
        {
            outPacket.WriteInt(4);
            outPacket.WriteByte(0);
            outPacket.WriteLong(room.EndTicks + 5000);
            BroadCast(roomId, outPacket);
        }

        room.StartTicks = 0;
        room.Started = false;
        room.ReqRelay = false;
        room.RelayType = 0; //0 - UDP 1 - TCP

        int firstID = room.Ranking.FirstOrDefault(x => x.Value == 0).Key;
        if (RoomManager.TryGetIdDetail(roomId, firstID) is Player p4)
        {
            room.RoomMaster = firstID;
            p4.PlayerType = 2;
        }
        Console.WriteLine("EndTicks = {0}", room.EndTicks + 5000);
    }

    public static void Clientsession(SessionGroup Parent, string nickname, uint hash, InPacket iPacket)
    {
        fileName filename = new fileName();
        if (nickname != "")
        {
            if (!FileName.FileNames.ContainsKey(nickname))
            {
                FileName.Load(nickname);
            }
            filename = FileName.FileNames[nickname];
        }
        
        // Ensure profile is loaded for the current nickname
        if (nickname != "" && !ProfileService.ProfileConfigs.ContainsKey(nickname))
        {
            ProfileService.Load(nickname);
        }
        if (hash == Adler32Helper.GenerateAdler32_ASCII("GameSlotPacket", 0))
        {
            int roomId = RoomManager.TryGetRoomId(nickname);
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                return;
            }
            int id = iPacket.ReadInt();
            uint item = iPacket.ReadUInt();
            byte type = iPacket.ReadByte();
            if (item == uint.MaxValue && iPacket.Length == 77)
            {
                byte[] data1 = iPacket.ReadBytes(25);
                short id1 = iPacket.ReadShort();
                byte unk1 = iPacket.ReadByte();
                byte[] data2 = iPacket.ReadBytes(4);
                iPacket.ReadByte();
                iPacket.ReadShort();
                byte[] data3 = iPacket.ReadBytes(29);
                short skill = GameSupport.RandomItemSkill(nickname, room.GameType);
                using (OutPacket oPacket = new OutPacket("GameSlotPacket"))
                {
                    oPacket.WriteInt(id);
                    oPacket.WriteUInt(item);
                    oPacket.WriteByte(type);
                    oPacket.WriteBytes(data1);
                    oPacket.WriteShort(skill);
                    oPacket.WriteByte(1);
                    oPacket.WriteBytes(data2);
                    oPacket.WriteByte(2);
                    oPacket.WriteShort(skill);
                    oPacket.WriteBytes(data3);
                    BroadCast(roomId, oPacket);
                }
            }
            else if (type == 11)
            {
                var uni = iPacket.ReadByte();
                var skill = iPacket.ReadShort();
                List<short> skills = V2Specs.GetSkills(nickname);
                if (skills.Contains(13) && skill == 3)
                {
                    GameSupport.AttackedSkill(roomId, id, nickname, type, uni, 10);
                }
                
                // Ensure profile is loaded before accessing
                if (ProfileService.ProfileConfigs.ContainsKey(nickname))
                {
                    if (kartConfig.SkillAttacked.TryGetValue(ProfileService.ProfileConfigs[nickname].RiderItem.Set_Kart, out var kartSkills))
                    {
                        if (kartSkills.TryGetValue(skill, out var targetSkill))
                        {
                            GameSupport.AttackedSkill(roomId, id, nickname, type, uni, targetSkill);
                        }
                    }
                }
                Console.WriteLine("GameSlotPacket, Attacked. Skill = {0}", skill);
            }
            else if (type == 18)
            {
                var uni = iPacket.ReadByte();
                iPacket.ReadShort();
                iPacket.ReadByte();
                var skill = iPacket.ReadShort();
                List<short> skills = V2Specs.GetSkills(nickname);
                if (skills.Contains(14) && skill == 5)
                {
                    GameSupport.AddItemSkill(roomId, id, nickname, 6);
                }
                
                // Ensure profile is loaded before accessing
                if (ProfileService.ProfileConfigs.ContainsKey(nickname))
                {
                    if (kartConfig.SkillMappings.TryGetValue(ProfileService.ProfileConfigs[nickname].RiderItem.Set_Kart, out var kartSkills))
                    {
                        if (kartSkills.TryGetValue(skill, out var targetSkill))
                        {
                            GameSupport.AddItemSkill(roomId, id, nickname, targetSkill);
                        }
                    }
                }
                Console.WriteLine("GameSlotPacket, Mapping. Skill = {0}", skill);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GameControlPacket"))
        {
            int roomId = RoomManager.TryGetRoomId(nickname);
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                return;
            }
            var state = iPacket.ReadByte();
            //start
            if (state == 0 && room.StartTicks == 0 && !room.Started)
            {
                Start(Parent, roomId);
            }
            //finish
            else if (state == 2)
            {
                iPacket.ReadInt();
                var time = iPacket.ReadUInt();
                var player = RoomManager.GetPlayer(roomId, nickname);
                if (player != null)
                {
                    using (OutPacket oPacket = new OutPacket("GameRaceTimePacket"))
                    {
                        oPacket.WriteInt(player.ID);
                        oPacket.WriteUInt(time);
                        BroadCast(roomId, oPacket);
                    }
                    room.TimeData.TryAdd(player.ID, time);
                    Console.WriteLine("GameControlPacket, ID = {0}, Time = {1}", player.ID, time);
                }
                if (room.EndTicks == 0)
                {
                    room.EndTicks = ConvertTick() + 10000;
                    using (OutPacket oPacket = new OutPacket("GameControlPacket"))
                    {
                        oPacket.WriteInt(3);
                        oPacket.WriteByte(0);
                        oPacket.WriteUInt(room.EndTicks);
                        BroadCast(roomId, oPacket, nickname);
                    }
                    Set_settleTrigger(Parent, roomId);
                }
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("ChGetRoomListRequestPacket"))
        {
            int page = iPacket.ReadInt();
            var rooms = RoomManager.GetRoomsByPage(page);
            using (OutPacket oPacket = new OutPacket("ChGetRoomListReplyPacket"))
            {
                Console.WriteLine($"Room Count: {RoomManager._rooms.Count}");
                oPacket.WriteInt(RoomManager._rooms.Count); // 房间总数
                oPacket.WriteInt(0);
                oPacket.WriteInt(rooms.Count); // 房间数量
                foreach (var _room in rooms)
                {
                    oPacket.WriteShort((short)_room.Key);
                    oPacket.WriteString(_room.Value.RoomName); // 房间名称
                    oPacket.WriteUInt(_room.Value.track); // 赛道
                    oPacket.WriteBool(_room.Value.Lock); // 是否上锁
                    oPacket.WriteByte(_room.Value.GameType); // 模式
                    oPacket.WriteByte(_room.Value.SpeedType); // 速度模式
                    oPacket.WriteBool(_room.Value.Started); // 房间状态
                    oPacket.WriteByte(8); // 房间最大人数
                    oPacket.WriteByte((byte)_room.Value.GetCount()); // 房间人数
                    oPacket.WriteHexString("00 00 00 00 00 00");
                }
                Parent.Client.Send(oPacket);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("PqChannelSwitch", 0))
        {
            Nickname = nickname;
            int length = iPacket.ReadInt();
            iPacket.ReadBytes(length);
            byte channel = (byte)(iPacket.ReadByte() - 1);
            var channelData = GameSupport.Channels.ContainsKey(channel) ? GameSupport.Channels[channel] : null;
            StartTimeAttack[nickname] = channelData.CreateSpeed;
            Console.WriteLine("Channel Switch, channel = {0}", channelData.Name);
            IPEndPoint serverEndPoint = Parent.Client.Socket.LocalEndPoint as IPEndPoint;
            if (serverEndPoint == null) return;
            using (OutPacket oPacket = new OutPacket("PrChannelSwitch"))
            {
                oPacket.WriteInt(0);
                oPacket.WriteShort(channel);
                oPacket.WriteShort(iPacket.ReadShort());
                oPacket.WriteEndPoint(ProfileService.SettingConfig.ServerIP == "127.0.0.1" ? serverEndPoint.Address : IPAddress.Parse(ProfileService.SettingConfig.ServerIP), ProfileService.SettingConfig.ServerPort);
                Parent.Client.Send(oPacket);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("PqChannelMovein", 0))
        {
            IPEndPoint clientEndPoint = Parent.Client.Socket.RemoteEndPoint as IPEndPoint;
            if (clientEndPoint == null) return;
            IPEndPoint serverEndPoint = Parent.Client.Socket.LocalEndPoint as IPEndPoint;
            if (serverEndPoint == null) return;
            string clientId = ClientManager.GetClientId(clientEndPoint);
            var ClientGroup = ClientManager.ClientGroups[clientId];
            if (ClientGroup.Nickname == "" && Nickname != "")
            {
                ClientGroup.Nickname = Nickname;
            }
            using (OutPacket oPacket = new OutPacket("PrChannelMoveIn"))
            {
                oPacket.WriteByte(1);
                oPacket.WriteEndPoint(ProfileService.SettingConfig.ServerIP == "127.0.0.1" ? serverEndPoint.Address : IPAddress.Parse(ProfileService.SettingConfig.ServerIP), ProfileService.SettingConfig.ServerPort);
                oPacket.WriteEndPoint(ProfileService.SettingConfig.ServerIP == "127.0.0.1" ? serverEndPoint.Address : IPAddress.Parse(ProfileService.SettingConfig.ServerIP), (ushort)(ProfileService.SettingConfig.ServerPort + 1));
                Parent.Client.Send(oPacket);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("ChCreateRoomRequestPacket", 0))
        {
            string RoomName = iPacket.ReadString();    //room name
            Console.WriteLine("RoomName = {0}, len = {1}", RoomName, RoomName.Length);
            string Password = iPacket.ReadString();
            Console.WriteLine("Password = {0}, len = {1}", Password, Password.Length);
            byte GameType = iPacket.ReadEncodedByte(); //7c
            iPacket.ReadInt();
            var AiCount = iPacket.ReadInt();
            Console.WriteLine("AiCount = {0}", AiCount);
            iPacket.ReadInt();
            iPacket.ReadInt();
            byte[] RoomData = iPacket.ReadBytes(32);
            iPacket.ReadBytes(29);
            byte AiSwitch = iPacket.ReadByte();
            Console.WriteLine("AiSwitch = {0}", AiSwitch);

            var RoomId = RoomManager.CreateRoom();
            var Room = RoomManager.GetRoom(RoomId);
            Console.WriteLine("CreateRoom = {0}", RoomId);
            byte randomTrackGameType = 0;
            if (GameType == 2 || GameType == 4 || GameType == 14 || GameType == 54)
            {
                randomTrackGameType = 1;
            }
            if (GameType == 3 || GameType == 4)
            {
                RoomManager.AddPlayer(RoomId, nickname, 2, 2, Parent);
                Player player = RoomManager.GetPlayer(RoomId, nickname);
                Room.RoomMaster = player.ID;
                if (player == null)
                {
                    Console.WriteLine("CreateRoom Failed");
                    return;
                }
                using (OutPacket oPacket = new OutPacket("ChCreateRoomReplyPacket"))
                {
                    oPacket.WriteByte(1);
                    oPacket.WriteByte(1);
                    oPacket.WriteByte(2);
                    oPacket.WriteEncByte(GameType);
                    Parent.Client.Send(oPacket);
                }
            }
            else
            {
                RoomManager.AddPlayer(RoomId, nickname, 0, 2, Parent);
                Player player = RoomManager.GetPlayer(RoomId, nickname);
                Room.RoomMaster = player.ID;
                if (player == null)
                {
                    Console.WriteLine("CreateRoom Failed");
                    return;
                }
                using (OutPacket oPacket = new OutPacket("ChCreateRoomReplyPacket"))
                {
                    oPacket.WriteByte(1);
                    oPacket.WriteByte(0);
                    oPacket.WriteByte(8);
                    oPacket.WriteEncByte(GameType);
                    Parent.Client.Send(oPacket);
                }
            }
            Room.RoomName = RoomName;
            if (Password != "")
            {
                Room.Lock = true;
            }
            Room.LockPwd = Password;
            if (StartTimeAttack.ContainsKey(nickname))
            {
                Room.SpeedType = StartTimeAttack[nickname];
            }
            else
            {
                Room.SpeedType = 7;
            }
            Room.GameType = GameType;
            Room.RandomTrackGameType = randomTrackGameType;
            Room.RoomData = RoomData;
            if (AiCount > 0 && AiSwitch == 6)
            {
                // 新增 AI 数量
                AddAis(Room, AiCount - 1, randomTrackGameType);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GrFirstRequestPacket"))
        {
            int roomId = RoomManager.TryGetRoomId(nickname);
            if (roomId == -1)
            {
                return;
            }
            GrSessionDataPacket(Parent, nickname);
            //Thread.Sleep(10);
            GrSlotDataPacket(roomId);
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GrChangeTrackPacket"))
        {
            int roomId = RoomManager.TryGetRoomId(nickname);
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                return;
            }
            room.track = iPacket.ReadUInt();
            Console.WriteLine("Gr Track Changed : {0}", RandomTrack.GetTrackName(room.track));
            GrSlotDataPacket(roomId);
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GrRequestSetSlotStatePacket"))
        {
            int roomId = RoomManager.TryGetRoomId(nickname);
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                return;
            }

            var player = RoomManager.GetPlayer(roomId, nickname);
            if (player == null)
            {
                Console.WriteLine("GetPlayer Failed, roomId = {0}, nickname = {1}", roomId, nickname);
                return;
            }

            player.PlayerType = iPacket.ReadInt();
            GrSlotStatePacket(roomId);
            using (OutPacket oPacket = new OutPacket("GrReplySetSlotStatePacket"))
            {
                oPacket.WriteUInt(ClientManager.GetUserNO(nickname));
                oPacket.WriteByte(1);
                oPacket.WriteInt(player.ID);
                oPacket.WriteInt(player.PlayerType);
                BroadCast(roomId, oPacket);
            }
            GrSlotDataPacket(roomId);
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GrRequestClosePacket"))
        {
            using (OutPacket oPacket = new OutPacket("GrReplyClosePacket"))
            {
                oPacket.WriteUInt(ClientManager.GetUserNO(nickname));
                oPacket.WriteByte(0);
                oPacket.WriteInt(0);
                oPacket.WriteInt(0);
                oPacket.WriteInt(0);
                oPacket.WriteInt(0);
                Parent.Client.Send(oPacket);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GrRequestStartPacket"))
        {
            int roomId = RoomManager.TryGetRoomId(nickname);
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                return;
            }

            room.trackTemp = RandomTrack.GetRandomTrack(nickname, room.RandomTrackGameType, room.track);

            using (OutPacket oPacket = new OutPacket("GrReplyStartPacket"))
            {
                oPacket.WriteInt(0);
                Parent.Client.Send(oPacket);
            }

            foreach (RoomMember Object in room._IDs)
            {
                if (Object is Player p)
                {
                    using (OutPacket oPacket = new OutPacket("GrCommandStartPacket"))
                    {
                        oPacket.WriteUInt(Adler32Helper.GenerateAdler32(Encoding.ASCII.GetBytes("GrSessionDataPacket")));
                        GrSessionDataPacket(p.Nickname, oPacket);

                        oPacket.WriteUInt(Adler32Helper.GenerateAdler32(Encoding.ASCII.GetBytes("GrSlotDataPacket")));
                        GrSlotDataPacket(roomId, oPacket, true);
                        oPacket.WriteInt();

                        //kart data
                        StartGameData.GetKartSpac(oPacket, p.Nickname, room.SpeedType);

                        oPacket.WriteInt(room.GetAiCount()); //AI count
                        if (room.GetAiCount() > 0)
                        {
                            for (int j = 0; j < room.GetAiCount(); j++)
                            {
                                var AiSpec = AI.GetAISpec(room.RandomTrackGameType);
                                oPacket.WriteEncFloat(AiSpec[0]);
                                oPacket.WriteEncFloat(AiSpec[1]);
                                oPacket.WriteEncFloat(AiSpec[2]);
                                oPacket.WriteEncFloat(AiSpec[3]);
                                oPacket.WriteEncFloat(AiSpec[4]);
                                oPacket.WriteEncFloat(AiSpec[5]);
                            }
                        }
                        oPacket.WriteUInt(room.trackTemp); //track name hash
                        oPacket.WriteInt(10000);

                        oPacket.WriteInt();
                        oPacket.WriteUInt(Adler32Helper.GenerateAdler32(Encoding.ASCII.GetBytes("MissionInfo")));
                        oPacket.WriteHexString("00 00 00 00 00 00 00 00 00 00 FF FF FF FF 00 00 00 00 00 00 00 00 00");
                        //oPacket.WriteString("[applied param]\r\ntransAccelFactor='1.8555' driftEscapeForce='4720' steerConstraint='24.95' normalBoosterTime='3860' \r\npartsBoosterLock='1' \r\n\r\n[equipped / default parts param]\r\ntransAccelFactor='1.86' driftEscapeForce='2120' steerConstraint='2.7' normalBoosterTime='860' \r\n\r\n\r\n[gamespeed param]\r\ntransAccelFactor='-0.0045' driftEscapeForce='2600' steerConstraint='22.25' normalBoosterTime='3000' \r\n\r\n\r\n[factory enchant param]\r\n");
                        Console.WriteLine("Track : {0}", RandomTrack.GetTrackName(room.trackTemp));
                        p.Session.Client.Send(oPacket);
                    }
                }
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("PcReportStateInGame", 0))
        {
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("ChLeaveRoomRequestPacket"))
        {
            using (OutPacket oPacket = new OutPacket("ChLeaveRoomReplyPacket"))
            {
                oPacket.WriteByte(1);
                Parent.Client.Send(oPacket);
            }
            int roomId = RoomManager.TryGetRoomId(nickname);
            int slotId = RoomManager.GetPlayerSlotId(roomId, nickname);
            if (slotId != -1)
            {
                RoomManager.RemovePlayer(roomId, (byte)slotId);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GrRequestBasicAiPacket"))
        {
            int roomId = RoomManager.TryGetRoomId(nickname);
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                return;
            }
            byte slotId = iPacket.ReadByte();
            if (RoomManager.TryGetSlotDetail(roomId, slotId) is Ai ai)
            {
                room.RemoveMember(ai.SlotId, out bool DeleteAi);
                using (OutPacket oPacket = new OutPacket("GrSlotDataBasicAi"))
                {
                    oPacket.WriteInt(1);
                    oPacket.WriteByte(1);
                    oPacket.WriteInt(slotId);
                    oPacket.WriteHexString("00 00 00 00 00 00 00 00 00 00 00 00 00");
                    Position(roomId, oPacket);
                    BroadCast(roomId, oPacket);
                }
            }
            else
            {
                AddAi(Parent, roomId, slotId);
            }
            using (OutPacket oPacket = new OutPacket("GrReplyBasicAiPacket"))
            {
                oPacket.WriteByte(1);
                oPacket.WriteHexString("00 00 00 00");
                Parent.Client.Send(oPacket);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GameAiGoalinPacket"))
        {
            int roomId = RoomManager.TryGetRoomId(nickname);
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                return;
            }
            var Id = iPacket.ReadInt();
            var Time = iPacket.ReadUInt();
            using (OutPacket oPacket = new OutPacket("GameRaceTimePacket"))
            {
                oPacket.WriteInt(Id);
                oPacket.WriteUInt(Time);
                BroadCast(roomId, oPacket);
            }
            room.TimeData.TryAdd(Id, Time);
            Console.WriteLine("GameAiGoalinPacket, Id = {0}, Time = {1}", Id, Time);
            if (room.EndTicks == 0)
            {
                room.EndTicks = ConvertTick() + 10000;
                using (OutPacket oPacket = new OutPacket("GameControlPacket"))
                {
                    oPacket.WriteInt(3);
                    oPacket.WriteByte(0);
                    oPacket.WriteUInt(room.EndTicks);
                    BroadCast(roomId, oPacket);
                }
                Set_settleTrigger(Parent, roomId);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GameTeamBoosterRequestAddGaugePacket"))
        {
            int roomId = RoomManager.TryGetRoomId(nickname);
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                return;
            }
            var team = iPacket.ReadByte();
            var value = iPacket.ReadFloat();
            Console.WriteLine("GameTeamBoosterRequestAddGaugePacket, teams = {0}, value = {1}", team, value);

            if (team == 1)
            {
                room.redGauge += (value * 0.000125f / room.GetPlayerCount(team));
                if (room.redGauge > 1f) room.redGauge = 1f;
                using (OutPacket oPacket = new OutPacket("GameTeamBoosterSetGaugePacket"))
                {
                    oPacket.WriteByte(team);
                    oPacket.WriteFloat(room.redGauge);
                    BroadCast(roomId, oPacket, "", team);
                }
                if (room.redGauge == 1f) room.redGauge = 0f;
            }
            else if (team == 2)
            {
                room.blueGauge += (value * 0.000125f / room.GetPlayerCount(team));
                if (room.blueGauge > 1f) room.blueGauge = 1f;
                using (OutPacket oPacket = new OutPacket("GameTeamBoosterSetGaugePacket"))
                {
                    oPacket.WriteByte(team);
                    oPacket.WriteFloat(room.blueGauge);
                    BroadCast(roomId, oPacket, "", team);
                }
                if (room.blueGauge == 1f) room.blueGauge = 0f;
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GrChangeTeamPacket"))
        {
            int roomId = RoomManager.TryGetRoomId(nickname);
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                return;
            }
            var player = RoomManager.GetPlayer(roomId, nickname);
            if (player == null)
            {
                Console.WriteLine("GetPlayer Failed, roomId = {0}, nickname = {1}", roomId, nickname);
                return;
            }
            byte team = iPacket.ReadByte();
            var Bool = RoomManager.ChangeMemberTeam(roomId, player.SlotId, team);
            Console.WriteLine("ChangeMemberTeam, roomId = {0}, SlotId = {1}, Team = {2}, {3}", roomId, player.SlotId, team, Bool);
            using (OutPacket oPacket = new OutPacket("GrChangeTeamPacketReply"))
            {
                oPacket.WriteInt(player.ID);
                oPacket.WriteByte(player.Team);
                Position(roomId, oPacket);
                Parent.Client.Send(oPacket);
            }
            GrSlotDataPacket(roomId);
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("ChJoinRoomRequestPacket"))
        {
            var roomId = iPacket.ReadByte();
            var unk = iPacket.ReadByte();
            var pwd = iPacket.ReadString();
            Console.WriteLine("ChJoinRoomRequestPacket, roomId = {0}, unk = {1}, pwd = {2}", roomId, unk, pwd);

            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                using (OutPacket outPacket = new OutPacket("ChJoinRoomReplyPacket"))
                {
                    outPacket.WriteByte(1);
                    outPacket.WriteByte(0);
                    outPacket.WriteByte(0);
                    outPacket.WriteEncByte(0);
                    outPacket.WriteBytes(new byte[5]);
                    Parent.Client.Send(outPacket);
                }
                return;
            }
            if (pwd != room.LockPwd)
            {
                using (OutPacket outPacket = new OutPacket("ChJoinRoomReplyPacket"))
                {
                    outPacket.WriteByte(2);
                    outPacket.WriteByte(0);
                    outPacket.WriteByte(0);
                    outPacket.WriteEncByte(room.GameType);
                    outPacket.WriteBytes(new byte[5]);
                    Parent.Client.Send(outPacket);
                }
            }
            else
            {
                int slot = Array.IndexOf(room._slots, null);
                if (slot == -1)
                {
                    using (OutPacket outPacket = new OutPacket("ChJoinRoomReplyPacket"))
                    {
                        outPacket.WriteByte(1);
                        outPacket.WriteByte(0);
                        outPacket.WriteByte(0);
                        outPacket.WriteEncByte(room.GameType);
                        outPacket.WriteBytes(new byte[5]);
                        Parent.Client.Send(outPacket);
                    }
                    return;
                }
                if (room.GameType == 3 || room.GameType == 4)
                {
                    if (slot < 4)
                    {
                        RoomManager.AddPlayer(roomId, nickname, 2, 2, Parent);
                        using (OutPacket outPacket = new OutPacket("ChJoinRoomReplyPacket"))
                        {
                            outPacket.WriteByte(0);
                            outPacket.WriteByte(2);
                            outPacket.WriteByte(2);
                            outPacket.WriteEncByte(room.GameType);
                            outPacket.WriteBytes(new byte[5]);
                            Parent.Client.Send(outPacket);
                        }
                        return;
                    }
                    else
                    {
                        RoomManager.AddPlayer(roomId, nickname, 1, 2, Parent);
                        using (OutPacket outPacket = new OutPacket("ChJoinRoomReplyPacket"))
                        {
                            outPacket.WriteByte(0);
                            outPacket.WriteByte(2);
                            outPacket.WriteByte(2);
                            outPacket.WriteEncByte(room.GameType);
                            outPacket.WriteBytes(new byte[5]);
                            Parent.Client.Send(outPacket);
                        }
                        return;
                    }
                }
                else
                {
                    RoomManager.AddPlayer(roomId, nickname, 0, 2, Parent);
                    using (OutPacket outPacket = new OutPacket("ChJoinRoomReplyPacket"))
                    {
                        outPacket.WriteByte(0);
                        outPacket.WriteByte(0);
                        outPacket.WriteByte(8);
                        outPacket.WriteEncByte(room.GameType);
                        outPacket.WriteBytes(new byte[5]);
                        Parent.Client.Send(outPacket);
                    }
                }
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GrRiderTalkPacket"))
        {
            string value = iPacket.ReadString();
            int roomId = RoomManager.TryGetRoomId(nickname);
            Console.WriteLine("GrSlotDataPacket, roomId = {0}", roomId);
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                Console.WriteLine("GetRoom Failed, roomId = {0}", roomId);
                return;
            }
            var player = RoomManager.GetPlayer(roomId, nickname);
            if (player == null)
            {
                Console.WriteLine("GetPlayer Failed, roomId = {0}, nickname = {1}", roomId, nickname);
                return;
            }
            foreach (var Object in room._slots)
            {
                if (Object is Player p)
                {
                    if (p.Nickname != nickname)
                    {
                        using (OutPacket outPacket = new OutPacket("GrRiderEchoPacket"))
                        {
                            outPacket.WriteInt(player.ID);
                            outPacket.WriteString(value);
                            p.Session.Client.Send(outPacket);
                        }
                    }
                }
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("PqRoomMasterChangePacket"))
        {
            int roomId = RoomManager.TryGetRoomId(nickname);
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                Console.WriteLine("GetRoom Failed, roomId = {0}", roomId);
                return;
            }
            string Target = iPacket.ReadString();
            var player = RoomManager.GetPlayer(roomId, Target);
            if (player != null)
            {
                room.RoomMaster = player.ID;
                player.PlayerType = 2;
                GrSlotDataPacket(roomId);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("PcStartMatching") || hash == Adler32Helper.GenerateAdler32_ASCII("PcCancelMatching"))
        {
            using (OutPacket outPacket = new OutPacket("PcMatchingFound"))
            {
                outPacket.WriteInt(0);
                Parent.Client.Send(outPacket);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("ChGetCurrentCmpRequestPacket"))
        {
            using (OutPacket outPacket = new OutPacket("ChGetCurrentCmpReplyPacket"))
            {
                outPacket.WriteInt(0);
                Parent.Client.Send(outPacket);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("PqRotationModeDataPacket"))
        {
            using (OutPacket outPacket = new OutPacket("PrRotationModeDataPacket"))
            {
                outPacket.WriteInt(0);
                Parent.Client.Send(outPacket);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("PqChangeRoomInfoPacket"))
        {
            string RoomName = iPacket.ReadString();
            string RoomPassword = iPacket.ReadString();

            int LimitTime = iPacket.ReadInt();
            byte RKeyAllowed = iPacket.ReadByte();
            int roomId = RoomManager.TryGetRoomId(nickname);
            if (roomId == -1)
            {
                Console.WriteLine("TryGetRoomId Failed, nickname = {0}", nickname);
                return;
            }
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                Console.WriteLine("GetRoom Failed, roomId = {0}", roomId);
                return;
            }
            room.RoomName = RoomName;
            if (RoomPassword.Length > 0)
            {
                room.Lock = true;
            }
            else
            {
                room.Lock = false;
            }
            room.LockPwd = RoomPassword;

            using (OutPacket outPacket = new OutPacket("PrChangeRoomInfoPacket"))
            {
                outPacket.WriteBool(true);
                outPacket.WriteString(RoomName);
                outPacket.WriteString(RoomPassword);
                outPacket.WriteInt(LimitTime);
                outPacket.WriteByte(RKeyAllowed);
                BroadCast(roomId, outPacket);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GrRequestKickPacket"))
        {
            int roomId = RoomManager.TryGetRoomId(nickname);
            if (roomId == -1)
            {
                Console.WriteLine("TryGetRoomId Failed, nickname = {0}", nickname);
                return;
            }
            int TargetSlotIndex = iPacket.ReadInt();
            if (RoomManager.TryGetSlotDetail(roomId, (byte)TargetSlotIndex) is Player p)
            {
                var player = RoomManager.RemovePlayer(roomId, (byte)TargetSlotIndex);
                if (player)
                {
                    using (OutPacket outPacket = new OutPacket("ChLeaveRoomReplyPacket"))
                    {
                        outPacket.WriteByte(1);
                        p.Session.Client.Send(outPacket);
                    }
                    using (OutPacket outPacket = new OutPacket("GrKickBroadcastPacket"))
                    {
                        outPacket.WriteString(p.Nickname);
                        BroadCast(roomId, outPacket);
                    }
                    using (OutPacket outPacket = new OutPacket("GrReplyKickPacket"))
                    {
                        outPacket.WriteByte(0);
                        Parent.Client.Send(outPacket);
                    }
                }
            }
            return;
        }
        else
        {
            return;
        }
    }

    public static void GrSlotDataPacket(int roomId)
    {
        using (OutPacket outPacket = new OutPacket("GrSlotDataPacket"))
        {
            GrSlotDataPacket(roomId, outPacket);
            BroadCast(roomId, outPacket);
        }
    }

    static void GrSlotDataPacket(int roomId, OutPacket outPacket, bool enter = false)
    {
        var room = RoomManager.GetRoom(roomId);
        outPacket.WriteUInt(room.track); // track name hash
        outPacket.WriteInt(0);
        outPacket.WriteBytes(room.RoomData); // 32
        outPacket.WriteInt(room.RoomMaster); // RoomMaster

        outPacket.WriteBytes(new byte[31]);

        /* ---- Player ---- */
        foreach (RoomMember Object in room._IDs)
        {
            if (Object is Player p)
            {
                // Ensure profile is loaded for the player
                if (!ProfileService.ProfileConfigs.ContainsKey(p.Nickname))
                {
                    if (!FileName.FileNames.ContainsKey(p.Nickname))
                    {
                        FileName.Load(p.Nickname);
                    }
                    ProfileService.Load(p.Nickname);
                }
                
                Console.WriteLine("Player Nickname = {0}, ID = {1}, SlotId = {2}", p.Nickname, p.ID, p.SlotId);
                if (enter)
                {
                    outPacket.WriteInt(3);
                }
                else
                {
                    outPacket.WriteInt(p.PlayerType); // Player Type, 2 = RoomMaster, 3 = AutoReady, 4 = Observer, 5 = Preparing, 7 = AI
                }
                outPacket.WriteUInt(ClientManager.GetUserNO(p.Nickname));
                if (ClientManager.ClientP2pAddrs.TryGetValue(p.Nickname, out IPEndPoint P2pPoint))
                {
                    outPacket.WriteEndPoint(P2pPoint);
                }
                else
                {
                    outPacket.WriteEndPoint(new IPEndPoint(IPAddress.Any, (ushort)(ProfileService.SettingConfig.ServerPort + 1)));
                }
                if (ClientManager.ClientUdpAddrs.TryGetValue(p.Nickname, out IPEndPoint UdpPoint))
                {
                    outPacket.WriteEndPoint(UdpPoint);
                }
                else
                {
                    outPacket.WriteEndPoint(new IPEndPoint(IPAddress.Any, ProfileService.SettingConfig.ServerPort));
                }
                outPacket.WriteString(p.Nickname);
                outPacket.WriteShort(ProfileService.ProfileConfigs[p.Nickname].Rider.Emblem1);
                outPacket.WriteShort(ProfileService.ProfileConfigs[p.Nickname].Rider.Emblem2);
                outPacket.WriteShort(0);
                GameSupport.GetRider(p.Nickname, outPacket);
                outPacket.WriteString(ProfileService.ProfileConfigs[p.Nickname].Rider.Card);
                outPacket.WriteUInt(ProfileService.ProfileConfigs[p.Nickname].Rider.RP);
                if (room.GameType == 3 || room.GameType == 4)
                {
                    outPacket.WriteByte(p.Team);
                }
                else
                {
                    outPacket.WriteByte(0);
                }
                outPacket.WriteInt(p.ID);

                outPacket.WriteBytes(new byte[30]);

                outPacket.WriteInt(1500);
                outPacket.WriteInt(1499);
                outPacket.WriteInt(0);
                outPacket.WriteInt(2000);
                outPacket.WriteInt(5);
                outPacket.WriteHexString("FF 00 00 00");

                outPacket.WriteByte(RiderData.RiderSchool.catLevel); //3
                if (ProfileService.ProfileConfigs[p.Nickname].Rider.ClubMark_LOGO == 0)
                {
                    outPacket.WriteString("");
                    outPacket.WriteInt(0);
                }
                else
                {
                    outPacket.WriteString(ProfileService.ProfileConfigs[p.Nickname].Rider.ClubName);
                    outPacket.WriteInt(ProfileService.ProfileConfigs[p.Nickname].Rider.ClubMark_LOGO);
                }
                outPacket.WriteBytes(new byte[19]);
            }
            else if (Object is Ai a)
            {
                Console.WriteLine("Ai ID = {0}, SlotId = {1}", a.ID, a.SlotId);
                outPacket.WriteInt(7);
                outPacket.WriteShort(a.Character);
                outPacket.WriteShort(a.Rid);
                outPacket.WriteShort(a.Kart);
                outPacket.WriteShort(a.Balloon);
                outPacket.WriteShort(a.HeadBand);
                outPacket.WriteShort(a.Goggle);
                if (room.GameType == 3 || room.GameType == 4)
                {
                    outPacket.WriteByte(a.Team);
                }
                else
                {
                    outPacket.WriteByte(0);
                }
            }
            else
            {
                outPacket.WriteInt(0);
            }
        }
        outPacket.WriteBytes(new byte[32]);
        Position(roomId, outPacket);
    }

    static void GrSessionDataPacket(SessionGroup Parent, string nickname)
    {
        using (OutPacket oPacket = new OutPacket("GrSessionDataPacket"))
        {
            GrSessionDataPacket(nickname, oPacket);
            Parent.Client.Send(oPacket);
        }
    }

    static void GrSessionDataPacket(string nickname, OutPacket outPacket)
    {
        int roomId = RoomManager.TryGetRoomId(nickname);
        var room = RoomManager.GetRoom(roomId);
        if (room == null)
        {
            Console.WriteLine("GetRoom Failed, roomId = {0}", roomId);
        }
        outPacket.WriteString(room.RoomName);
        outPacket.WriteString(room.LockPwd);
        outPacket.WriteByte(room.GameType);
        outPacket.WriteByte(room.SpeedType); //7
        outPacket.WriteInt(0);
        outPacket.WriteByte(0);
        outPacket.WriteInt(0);
        outPacket.WriteBytes(new byte[7]);
    }

    public static void BroadCast(int roomId, OutPacket outPacket, string Self = "", byte team = 0)
    {
        var room = RoomManager.GetRoom(roomId);
        if (room == null)
        {
            return;
        }
        foreach (RoomMember Object in room._slots)
        {
            if (Object is Player p)
            {
                if (team == 0)
                {
                    if (Self != p.Nickname)
                    {
                        p.Session.Client.Send(outPacket);
                    }
                }
                else if (p.Team == team)
                {
                    p.Session.Client.Send(outPacket);
                }
            }
        }
    }

    // 添加指定数量的 Ai
    static void AddAis(GameRoom room, int count, byte randomTrackGameType)
    {
        var selector = new DictionaryRandomSelector();
        List<short> randomCharIds = selector.GetRandomCharacterIds(aiCharacterDict, 8);
        List<short> randomKartIds = null;
        if (randomTrackGameType == 0)
        {
            randomKartIds = selector.GetRandomKartIds(aiKartDict, 8, true, false);
        }
        else if (randomTrackGameType == 1)
        {
            randomKartIds = selector.GetRandomKartIds(aiKartDict, 8, false, true);
        }
        int aiCount = 0;
        for (int i = 0; i < 8; i++)
        {
            short targetCharId = randomCharIds[i];
            short targetKartId = randomKartIds[i];
            if (aiCharacterDict.TryGetValue(targetCharId, out var targetChar))
            {
                short? ridIndex = selector.GetRandomRidIndex(targetChar);
                short? balloonId = 0;
                short? headbandId = 0;
                short? goggleId = 0;
                if (randomTrackGameType == 1)
                {
                    balloonId = selector.GetRandomAccessoryId(targetChar.Balloons);
                    headbandId = selector.GetRandomAccessoryId(targetChar.Headbands);
                    goggleId = selector.GetRandomAccessoryId(targetChar.Goggles);
                }
                byte team = i < 4 ? (byte)2 : (byte)1;
                Ai ai = new Ai
                {
                    Character = targetCharId,
                    Rid = ridIndex ?? 0,
                    Kart = targetKartId,
                    Balloon = balloonId ?? 0,
                    HeadBand = headbandId ?? 0,
                    Goggle = goggleId ?? 0,
                    Team = team
                };
                if (room.TrySetAi(ai, team) != 255)
                {
                    aiCount++;
                }
                if (aiCount == count)
                {
                    break;
                }
            }
        }
    }

    static void AddAi(SessionGroup Parent, int roomId, int slot)
    {
        var room = RoomManager.GetRoom(roomId);
        if (room == null)
        {
            Console.WriteLine("GetRoom Failed, roomId = {0}", roomId);
        }
        var selector = new DictionaryRandomSelector();
        List<short> randomCharIds = selector.GetRandomCharacterIds(aiCharacterDict, 2);
        List<short> randomKartIds = new List<short>();
        if (room.RandomTrackGameType == 0)
        {
            randomKartIds = selector.GetRandomKartIds(aiKartDict, 2, true, false);
        }
        else if (room.RandomTrackGameType == 1)
        {
            randomKartIds = selector.GetRandomKartIds(aiKartDict, 2, false, true);
        }
        if (room.GameType == 3 || room.GameType == 4)
        {
            byte team = slot < 4 ? (byte)2 : (byte)1;
            var Ais = new List<Ai>();
            for (int i = 0; i < 2; i++)
            {
                short targetCharId = randomCharIds[i];
                short targetKartId = randomKartIds[i];
                if (aiCharacterDict.TryGetValue(targetCharId, out var targetChar))
                {
                    short? ridIndex = selector.GetRandomRidIndex(targetChar);
                    short? balloonId = 0;
                    short? headbandId = 0;
                    short? goggleId = 0;
                    if (room.RandomTrackGameType == 1)
                    {
                        balloonId = selector.GetRandomAccessoryId(targetChar.Balloons);
                        headbandId = selector.GetRandomAccessoryId(targetChar.Headbands);
                        goggleId = selector.GetRandomAccessoryId(targetChar.Goggles);
                    }
                    Ais.Add(new Ai
                    {
                        Character = targetCharId,
                        Rid = ridIndex ?? 0,
                        Kart = targetKartId,
                        Balloon = balloonId ?? 0,
                        HeadBand = headbandId ?? 0,
                        Goggle = goggleId ?? 0,
                        Team = i == 0 ? team : (byte)(3 - team)
                    });
                }
            }
            byte slot0 = room.TrySetAi(Ais[0], Ais[0].Team);
            byte slot1 = room.TrySetAi(Ais[1], Ais[1].Team);
            if (slot0 != 255 && slot1 != 255)
            {
                var ai0 = RoomManager.TryGetSlotDetail(roomId, slot0) as Ai;
                var ai1 = RoomManager.TryGetSlotDetail(roomId, slot1) as Ai;
                using (OutPacket oPacket = new OutPacket("GrSlotDataBasicAi"))
                {
                    oPacket.WriteInt(0);
                    oPacket.WriteByte(2);
                    oPacket.WriteInt(ai0.ID);
                    oPacket.WriteShort(ai0.Character);
                    oPacket.WriteShort(ai0.Rid);
                    oPacket.WriteShort(ai0.Kart);
                    oPacket.WriteShort(ai0.Balloon);
                    oPacket.WriteShort(ai0.HeadBand);
                    oPacket.WriteShort(ai0.Goggle);
                    oPacket.WriteByte(ai0.Team);
                    oPacket.WriteInt(ai1.ID);
                    oPacket.WriteShort(ai1.Character);
                    oPacket.WriteShort(ai1.Rid);
                    oPacket.WriteShort(ai1.Kart);
                    oPacket.WriteShort(ai1.Balloon);
                    oPacket.WriteShort(ai1.HeadBand);
                    oPacket.WriteShort(ai1.Goggle);
                    oPacket.WriteByte(ai1.Team);
                    Position(roomId, oPacket);
                    BroadCast(roomId, oPacket);
                }
            }
        }
        else
        {
            short targetCharId = randomCharIds[0];
            short targetKartId = randomKartIds[0];
            if (aiCharacterDict.TryGetValue(targetCharId, out var targetChar))
            {
                short? ridIndex = selector.GetRandomRidIndex(targetChar);
                short? balloonId = 0;
                short? headbandId = 0;
                short? goggleId = 0;
                if (room.RandomTrackGameType == 1)
                {
                    balloonId = selector.GetRandomAccessoryId(targetChar.Balloons);
                    headbandId = selector.GetRandomAccessoryId(targetChar.Headbands);
                    goggleId = selector.GetRandomAccessoryId(targetChar.Goggles);
                }
                byte slot2 = room.TrySetAi(new Ai
                {
                    Character = targetCharId,
                    Rid = ridIndex ?? 0,
                    Kart = targetKartId,
                    Balloon = balloonId ?? 0,
                    HeadBand = headbandId ?? 0,
                    Goggle = goggleId ?? 0,
                    Team = 0
                }, 0);
                if (slot2 != 255)
                {
                    var ai2 = RoomManager.TryGetSlotDetail(roomId, slot2) as Ai;
                    using (OutPacket oPacket = new OutPacket("GrSlotDataBasicAi"))
                    {
                        oPacket.WriteInt(0);
                        oPacket.WriteByte(1);
                        oPacket.WriteInt(ai2.ID);
                        oPacket.WriteShort(ai2.Character);
                        oPacket.WriteShort(ai2.Rid);
                        oPacket.WriteShort(ai2.Kart);
                        oPacket.WriteShort(ai2.Balloon);
                        oPacket.WriteShort(ai2.HeadBand);
                        oPacket.WriteShort(ai2.Goggle);
                        oPacket.WriteByte(0);
                        Position(roomId, oPacket);
                        BroadCast(roomId, oPacket);
                    }
                }
            }
        }
    }

    static void Position(int roomId, OutPacket outPacket)
    {
        var room = RoomManager.GetRoom(roomId);
        if (room == null)
        {
            return;
        }
        foreach (RoomMember Object in room._slots)
        {
            if (Object is Player player)
            {
                outPacket.WriteInt(player.ID);
            }
            else if (Object is Ai ai)
            {
                outPacket.WriteInt(ai.ID);
            }
            else
            {
                outPacket.WriteHexString("FFFFFFFF");
            }
        }
    }

    static void GrSlotStatePacket(int roomId)
    {
        var room = RoomManager.GetRoom(roomId);
        if (room == null)
        {
            return;
        }
        using (OutPacket oPacket = new OutPacket("GrSlotStatePacket"))
        {
            foreach (RoomMember Object in room._IDs)
            {
                if (Object is Player player)
                {
                    oPacket.WriteInt(player.PlayerType);
                }
                else if (Object is Ai ai)
                {
                    oPacket.WriteInt(7);
                }
                else
                {
                    oPacket.WriteInt(0);
                }
            }
            oPacket.WriteBytes(new byte[32]);
            BroadCast(roomId, oPacket);
        }
    }
}

public class RoomList
{
    public byte RandomTrackGameType { get; set; }
    public byte SpeedType { get; set; }
    public byte GameType { get; set; }
}