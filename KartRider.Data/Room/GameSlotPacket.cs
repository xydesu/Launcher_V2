using System;
using System.Collections.Generic;
using ExcData;
using KartRider.IO.Packet;
using Profile;

namespace KartRider;

public class SlotData
{
    public static SpecialKartConfig kartConfig = new SpecialKartConfig();

    public static void GameSlotPacket(SessionGroup Parent, InPacket iPacket)
    {
        int roomId = RoomManager.TryGetRoomId(Parent.Nickname);
        var room = RoomManager.GetRoom(roomId);
        if (room == null)
        {
            return;
        }

        Player player = RoomManager.GetPlayer(roomId, Parent.Nickname);
        int id = iPacket.ReadInt();
        uint item = iPacket.ReadUInt();
        byte type = iPacket.ReadByte();

        if (id == player.ID)
        {
            if (item == uint.MaxValue && iPacket.Length == 77)
            {
                byte[] data1 = iPacket.ReadBytes(25);
                short skill1 = iPacket.ReadShort();
                byte unk1 = iPacket.ReadByte();
                byte[] data2 = iPacket.ReadBytes(4);
                byte unk2 = iPacket.ReadByte();
                short skill2 = iPacket.ReadShort();
                byte[] data3 = iPacket.ReadBytes(21);
                int id2 = iPacket.ReadInt();
                uint ticks = iPacket.ReadUInt();
                short skill = RandomItemSkill(Parent.Nickname, room.GameType);
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
                    oPacket.WriteInt(id2);
                    oPacket.WriteUInt(ticks);
                    MultyPlayer.BroadCast(roomId, oPacket);
                }
            }
            else if (type == 9 || type == 10 || type == 12)
            {
                using (OutPacket oPacket = new OutPacket())
                {
                    oPacket.WriteBytes(iPacket.ToArray());
                    MultyPlayer.BroadCast(roomId, oPacket, Parent.Nickname);
                }
            }
            if (type == 11)
            {
                var uni = iPacket.ReadByte();
                var skill = iPacket.ReadShort();
                List<short> skills = V2Specs.GetSkills(Parent.Nickname);
                if (skills.Contains(13) && skill == 3)
                {
                    AttackedSkill(roomId, id, Parent, type, uni, 10);
                }

                // Ensure profile is loaded before accessing
                if (ProfileService.ProfileConfigs.ContainsKey(Parent.Nickname))
                {
                    if (kartConfig.SkillAttacked.TryGetValue(ProfileService.ProfileConfigs[Parent.Nickname].RiderItem.Set_Kart, out var kartSkills))
                    {
                        if (kartSkills.TryGetValue(skill, out var targetSkill))
                        {
                            AttackedSkill(roomId, id, Parent, type, uni, targetSkill);
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
                List<short> skills = V2Specs.GetSkills(Parent.Nickname);
                if (skills.Contains(14) && skill == 5)
                {
                    AddItemSkill(roomId, id, Parent, 6);
                }

                // Ensure profile is loaded before accessing
                if (ProfileService.ProfileConfigs.ContainsKey(Parent.Nickname))
                {
                    if (kartConfig.SkillMappings.TryGetValue(ProfileService.ProfileConfigs[Parent.Nickname].RiderItem.Set_Kart, out var kartSkills))
                    {
                        if (kartSkills.TryGetValue(skill, out var targetSkill))
                        {
                            AddItemSkill(roomId, id, Parent, targetSkill);
                        }
                    }
                }
                Console.WriteLine("GameSlotPacket, Mapping. Skill = {0}", skill);
            }
        }
    }

    public static short RandomItemSkill(string Nickname, byte gameType)
    {
        if (gameType == 2)
        {
            Random random = new Random();
            int index = random.Next(MultyPlayer.itemProb_indi.Count);
            short skill = MultyPlayer.itemProb_indi[index];
            skill = GetItemSkill(Nickname, skill);
            return skill;
        }
        else if (gameType == 4)
        {
            Random random = new Random();
            int index = random.Next(MultyPlayer.itemProb_team.Count);
            short skill = MultyPlayer.itemProb_team[index];
            skill = GetItemSkill(Nickname, skill);
            return skill;
        }
        return 0;
    }

    public static short GetItemSkill(string Nickname, short skill)
    {
        List<short> skills = V2Specs.GetSkills(Nickname);
        for (int i = 0; i < skills.Count; i++)
        {
            if (V2Specs.itemSkill.TryGetValue(skills[i], out var Level) &&
                Level.TryGetValue(skill, out var LevelSkill))
            {
                return LevelSkill;
            }
        }
        if (kartConfig.SkillChange.TryGetValue(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Kart, out var changes) &&
            changes.TryGetValue(skill, out var changesSkill))
        {
            return changesSkill;
        }
        return skill;
    }

    public static void AddItemSkill(int roomId, int id, SessionGroup Parent, short skill)
    {
        skill = GetItemSkill(Parent.Nickname, skill);
        using (OutPacket oPacket = new OutPacket("GameSlotPacket"))
        {
            oPacket.WriteInt(id);
            oPacket.WriteUInt(uint.MaxValue);
            oPacket.WriteByte(10);
            oPacket.WriteHexString("001000");
            oPacket.WriteShort(skill);
            oPacket.WriteByte(1);
            oPacket.WriteBytes(new byte[3]);
            oPacket.WriteByte(2);
            oPacket.WriteShort(skill);
            oPacket.WriteBytes(new byte[5]);
            Parent.Client.Send(oPacket);
            BroadCast(roomId, id, Parent.Nickname, skill);
        }
    }

    public static void AttackedSkill(int roomId, int id, SessionGroup Parent, byte type, byte uni, short skill)
    {
        skill = GetItemSkill(Parent.Nickname, skill);
        using (OutPacket oPacket = new OutPacket("GameSlotPacket"))
        {
            oPacket.WriteInt(id);
            oPacket.WriteUInt();
            oPacket.WriteByte(type);
            oPacket.WriteByte(uni);
            oPacket.WriteShort(skill);
            oPacket.WriteByte(1);
            oPacket.WriteShort();
            oPacket.WriteByte(2);
            oPacket.WriteShort(skill);
            oPacket.WriteBytes(new byte[5]);
            Parent.Client.Send(oPacket);
            BroadCast(roomId, id, Parent.Nickname, skill);
        }
    }

    public static void BroadCast(int roomId, int id, string Nickname, short skill, uint ticks = 0)
    {
        using (OutPacket oPacket = new OutPacket("GameSlotPacket"))
        {
            oPacket.WriteInt(id);
            oPacket.WriteUInt(uint.MaxValue);
            oPacket.WriteByte(1);
            oPacket.WriteByte(0);
            oPacket.WriteHexString("00 00 00 F0");
            oPacket.WriteUInt(ticks == 0 ? MultyPlayer.ConvertTick() : ticks);
            oPacket.WriteBytes(new byte[16]);
            oPacket.WriteShort(skill);
            oPacket.WriteByte(1);
            oPacket.WriteHexString("FF FF 00 00");
            oPacket.WriteByte(2);
            oPacket.WriteShort(skill);
            oPacket.WriteBytes(new byte[13]);
            oPacket.WriteHexString("00 00 00 F0 01 00 00 00");
            oPacket.WriteInt(id);
            oPacket.WriteUInt(ticks == 0 ? MultyPlayer.ConvertTick() : ticks);
            MultyPlayer.BroadCast(roomId, oPacket, Nickname);
        }
    }
}