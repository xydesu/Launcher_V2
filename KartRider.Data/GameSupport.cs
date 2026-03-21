using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using ExcData;
using KartRider.Common.Utilities;
using KartRider.IO.Packet;
using Profile;
using RiderData;

namespace KartRider
{
    public class Keys
    {
        public uint first_val { get; set; }
        public uint second_val { get; set; }
        public string key1 { get; set; }
        public string key2 { get; set; }
    }

    public class Channel
    {
        public string Name { get; set; }
        public byte CreateSpeed { get; set; }
        public byte GameType { get; set; }
    }

    public static class GameSupport
    {
        public static List<List<short>> Dictionary = new List<List<short>>();
        public static List<int> scenario = new List<int>();
        public static List<int> quest = new List<int>();
        public static int seasonId = 0;
        public static Dictionary<byte, Channel> Channels = new Dictionary<byte, Channel>();

        public static Keys[] keys = new Keys[]
        {
            new Keys { first_val = 2919676295, second_val = 263300380, key1 = "QyvKvO60jogWDupzJ7gm0kRQdooFjWRjSjlq0gu/x2k=", key2 = "GXQstj1A95XiHvjrOGuPkzdyL+7qxETl/cPlUZk2KA4=" },
            new Keys { first_val = 3595571486, second_val = 2168420743, key1 = "+B1K8NAOvJd3cXFieRWTkRNj2rlv2qVmALSUdXFpNl0=", key2 = "TwKtPFLx+3AuKg5PFa021r3hKyFDK2sFBzQJJCI26wA=" },
            new Keys { first_val = 3059596768, second_val = 1772034572, key1 = "DI5gSCYZrEcZjR4fma5gSevvLBGSzKMoOPl7ZHDmfgA=", key2 = "bLV2VEcHkS8SrZVuPwitWN+I2851xwVEr+UBEzcYz+8=" },
            new Keys { first_val = 1412439591, second_val = 684842217, key1 = "nd65IIry0ZcguC7Ra8Ufby5xJmqMaXNXojL3OidbrsE=", key2 = "EmEHRGaDmK6Yz0GxPOVtloXvzSdYyNaQdIA/OWQez/U=" },
            new Keys { first_val = 1183929409, second_val = 4001694798, key1 = "jQre/0PRqRZ0oFW1u4jx1rj41LP+clRw2EhJ96Tfo0I=", key2 = "Hkk73+2YbVVquYu44C5jzbUwQ9XiBAs9QOdarBWspwE=" },
            new Keys { first_val = 2031112783, second_val = 2190302224, key1 = "5wpYhubc/NxIqTklY0UoZNu7ZaCRr8Zypw32i1PiHfs=", key2 = "HxjlBMdgLG97tWeLkzJ/1eWpNfDLz56z3FQTl72AecU=" },
            new Keys { first_val = 3640782532, second_val = 2489762877, key1 = "ComjZh2R0y82PVv25nzqrcqnusvQbGfngimO69PO7bc=", key2 = "pQ04kPHlUS67of2l4D3rukfTsJrSB15G4NtoAx+X8ec=" },
            new Keys { first_val = 912740103, second_val = 3754337362, key1 = "A7H8oUUAoWg65+rFF8h9xcr/aiYwecEfNQyGNF5WHhs=", key2 = "ycsTsKSzTxbOraG5PrjtBWP81YCor02tCxJquIl+5NM=" }
        };

        public static uint PcFirstMessage(SessionGroup Parent)
        {
            Random random = new Random();
            int index = random.Next(keys.Length);
            Keys key = keys[index];
            using (OutPacket outPacket = new OutPacket("PcFirstMessage"))
            {
                outPacket.WriteUShort(ProfileService.SettingConfig.LocaleID);
                outPacket.WriteUShort(1);
                outPacket.WriteUShort(ProfileService.SettingConfig.ClientVersion);
                outPacket.WriteString("https://yanygm.github.io/Launcher_V2/");
                outPacket.WriteUInt(key.first_val);
                outPacket.WriteUInt(key.second_val);
                outPacket.WriteByte((byte)ProfileService.SettingConfig.nClientLoc);
                outPacket.WriteString(key.key1);
                int[] time = new int[] { 1547597728, 1707244048, 1862052984 };
                outPacket.WriteInt(time.Length);
                foreach (int t in time)
                {
                    outPacket.WriteInt();
                    outPacket.WriteInt(t);
                }
                outPacket.WriteBytes(new byte[3]);
                outPacket.WriteString(key.key2);
                Parent.Client.Send(outPacket);
            }
            return key.first_val ^ key.second_val;
        }

        public static void OnDisconnect(SessionGroup Parent)
        {
            Parent.Client.Disconnect();
        }

        public static void SpRpLotteryPacket(SessionGroup Parent)
        {
            using (OutPacket outPacket = new OutPacket("SpRpLotteryPacket"))
            {
                outPacket.WriteHexString("05 00 00 00 00 00 00 00 FF FF FF FF 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00");
                Parent.Client.Send(outPacket);
            }
        }

        public static void PrGetGameOption(SessionGroup Parent, string Nickname)
        {
            using (OutPacket outPacket = new OutPacket("PrGetGameOption"))
            {
                outPacket.WriteFloat(ProfileService.ProfileConfigs[Nickname].GameOption.Set_BGM);
                outPacket.WriteFloat(ProfileService.ProfileConfigs[Nickname].GameOption.Set_Sound);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.Main_BGM);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.Sound_effect);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.Full_screen);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.ShowMirror);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.ShowOtherPlayerNames);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.ShowOutlines);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.ShowShadows);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.HighLevelEffect);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.MotionBlurEffect);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.MotionDistortionEffect);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.HighEndOptimization);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.AutoReady);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.PropDescription);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.VideoQuality);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.BGM_Check);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.Sound_Check);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.ShowHitInfo);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.AutoBoost);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.GameType);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.SetGhost);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.SpeedType);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.RoomChat);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.DrivingChat);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.ShowAllPlayerHitInfo);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.ShowTeamColor);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.Set_screen);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.HideCompetitiveRank);
                outPacket.WriteBytes(new byte[79]);
                Parent.Client.Send(outPacket);
            }
        }

        public static void ChRpEnterMyRoomPacket(SessionGroup Parent, string Nickname)
        {
            if (ProfileService.ProfileConfigs[Nickname].Rider.EnterMyRoomType == 0)
            {
                using (OutPacket outPacket = new OutPacket("ChRpEnterMyRoomPacket"))
                {
                    outPacket.WriteString(Nickname);
                    outPacket.WriteByte(0);
                    outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].MyRoom.MyRoom);
                    outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].MyRoom.MyRoomBGM);
                    outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].MyRoom.UseRoomPwd);
                    outPacket.WriteByte(0);
                    outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].MyRoom.UseItemPwd);
                    outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].MyRoom.TalkLock);
                    outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].MyRoom.RoomPwd);
                    outPacket.WriteString("");
                    outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].MyRoom.ItemPwd);
                    outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].MyRoom.MyRoomKart1);
                    outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].MyRoom.MyRoomKart2);
                    Parent.Client.Send(outPacket);
                }
            }
            else
            {
                using (OutPacket outPacket = new OutPacket("ChRpEnterMyRoomPacket"))
                {
                    outPacket.WriteString("");
                    outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].Rider.EnterMyRoomType);
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
            }
        }

        public static void RmNotiMyRoomInfoPacket(SessionGroup Parent, string Nickname)
        {
            using (OutPacket outPacket = new OutPacket("RmNotiMyRoomInfoPacket"))
            {
                outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].MyRoom.MyRoom);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].MyRoom.MyRoomBGM);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].MyRoom.UseRoomPwd);
                outPacket.WriteByte(0);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].MyRoom.UseItemPwd);
                outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].MyRoom.TalkLock);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].MyRoom.RoomPwd);
                outPacket.WriteString("");
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].MyRoom.ItemPwd);
                outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].MyRoom.MyRoomKart1);
                outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].MyRoom.MyRoomKart2);
                Parent.Client.Send(outPacket);
            }
        }

        public static void PrCheckMyClubStatePacket(SessionGroup Parent, string Nickname)
        {
            using (OutPacket outPacket = new OutPacket("PrCheckMyClubStatePacket"))
            {
                if (ProfileService.ProfileConfigs[Nickname].Rider.ClubMark_LOGO == 0)
                {
                    outPacket.WriteInt(0);
                    outPacket.WriteString("");
                    outPacket.WriteInt(0);
                    outPacket.WriteInt(0);
                }
                else
                {
                    outPacket.WriteInt(ProfileService.ProfileConfigs[Nickname].Rider.ClubCode);
                    outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].Rider.ClubName);
                    outPacket.WriteInt(ProfileService.ProfileConfigs[Nickname].Rider.ClubMark_LOGO);
                    outPacket.WriteInt(ProfileService.ProfileConfigs[Nickname].Rider.ClubMark_LINE);
                }
                outPacket.WriteShort(5);//Grade
                outPacket.WriteString(Nickname);
                outPacket.WriteInt(1);//ClubMember
                outPacket.WriteByte(5);//Level
                if (ClientManager.ClientP2pAddrs.TryGetValue(Nickname, out IPEndPoint endPoint))
                {
                    outPacket.WriteEndPoint(endPoint);
                }
                else
                {
                    outPacket.WriteEndPoint(new IPEndPoint(IPAddress.Any, 0));
                }
                Parent.Client.Send(outPacket);
            }
        }

        public static void ChRequestChStaticReplyPacket(SessionGroup Parent)
        {
            byte[] abcd = Array.Empty<byte>();
            using (OutPacket outPacket = new OutPacket("ChRequestChStaticReplyPacket"))
            {
                using (OutPacket oPacket = new OutPacket())
                {
                    oPacket.WriteInt(Channels.Count);
                    foreach (var channel in Channels)
                    {
                        oPacket.WriteByte((byte)(channel.Key + 1));
                        oPacket.WriteString(channel.Value.Name);
                    }

                    oPacket.WriteInt(Channels.Count);
                    foreach (var channel in Channels)
                    {
                        oPacket.WriteByte(channel.Key);
                        oPacket.WriteByte();
                        oPacket.WriteString(channel.Value.Name);
                        oPacket.WriteByte((byte)(channel.Key + 1));
                        oPacket.WriteInt();
                    }
                    abcd = oPacket.ToArray();
                }
                outPacket.WriteBool(true);
                byte[] hacc = ChannelUtils.Encode(abcd, ChannelUtils.EncodeFlag.ZLib);
                outPacket.WriteInt(hacc.Length);
                outPacket.WriteBytes(hacc);
                Parent.Client.Send(outPacket);
            }
        }

        public static void PrDynamicCommand(SessionGroup Parent)
        {
            using (OutPacket outPacket = new OutPacket("PrDynamicCommand"))
            {
                outPacket.WriteByte(0);
                Parent.Client.Send(outPacket);
            }
        }

        public static void PrQuestUX2ndPacket(OutPacket outPacket)
        {
            int All_Quest = quest.Count;
            outPacket.WriteInt(1);
            outPacket.WriteInt(1);
            outPacket.WriteInt(All_Quest);
            foreach (var item in quest)
            {
                outPacket.WriteInt(item);
                outPacket.WriteInt(item);
                outPacket.WriteInt(0);
                outPacket.WriteShort(-1);
                outPacket.WriteShort(0);
                outPacket.WriteInt(0);
                outPacket.WriteInt(0);
                outPacket.WriteInt(1);
                outPacket.WriteInt(0);
                outPacket.WriteByte(0);
            }
        }

        public static void GetRider(string Nickname, OutPacket outPacket)
        {
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Character);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Paint);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Kart);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Plate);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Goggle);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Balloon);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Unknown1);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_HeadBand);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_HeadPhone);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_HandGearL);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Unknown2);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Uniform);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Decal);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Pet);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_FlyingPet);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Aura);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_SkidMark);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_SpecialKit);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_RidColor);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_BonusCard);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_BossModeCard);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartPlant1);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartPlant2);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartPlant3);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartPlant4);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Unknown3);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_FishingPole);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Tachometer);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Dye);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartSN);
            outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Unknown4);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartCoating);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartTailLamp);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_slotBg);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartCoating12);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartTailLamp12);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartBoosterEffect12);
            outPacket.WriteShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Unknown5);
        }

        public static void PrGetRiderInfo(string nickname, SessionGroup Parent)
        {
            using (OutPacket outPacket = new OutPacket("PrGetRiderInfo"))
            {
                outPacket.WriteByte(1);
                outPacket.WriteUInt(ClientManager.GetUserNO(nickname));
                outPacket.WriteString(nickname);
                outPacket.WriteString(nickname);
                outPacket.WriteDateTime(DateTime.Now);
                GameSupport.GetRider(nickname, outPacket);
                outPacket.WriteString(ProfileService.ProfileConfigs[nickname].Rider.Card);
                outPacket.WriteUInt(ProfileService.ProfileConfigs[nickname].Rider.RP);
                outPacket.WriteInt(0);
                outPacket.WriteByte(RiderSchool.catLevel);//Licenses
                outPacket.WriteBytes(new byte[21]);
                outPacket.WriteShort(ProfileService.ProfileConfigs[nickname].Rider.Emblem1);
                outPacket.WriteShort(ProfileService.ProfileConfigs[nickname].Rider.Emblem2);
                outPacket.WriteShort(0);
                outPacket.WriteString(ProfileService.ProfileConfigs[nickname].Rider.RiderIntro);
                outPacket.WriteInt(ProfileService.ProfileConfigs[nickname].Rider.Premium);
                outPacket.WriteByte(1);
                if (ProfileService.ProfileConfigs[nickname].Rider.Premium == 0)
                    outPacket.WriteInt(0);
                else if (ProfileService.ProfileConfigs[nickname].Rider.Premium == 1)
                    outPacket.WriteInt(10000);
                else if (ProfileService.ProfileConfigs[nickname].Rider.Premium == 2)
                    outPacket.WriteInt(30000);
                else if (ProfileService.ProfileConfigs[nickname].Rider.Premium == 3)
                    outPacket.WriteInt(60000);
                else if (ProfileService.ProfileConfigs[nickname].Rider.Premium == 4)
                    outPacket.WriteInt(120000);
                else if (ProfileService.ProfileConfigs[nickname].Rider.Premium == 5)
                    outPacket.WriteInt(200000);
                else
                    outPacket.WriteInt(0);
                if (ProfileService.ProfileConfigs[nickname].Rider.ClubMark_LOGO == 0)
                {
                    outPacket.WriteInt(0);
                    outPacket.WriteInt(0);
                    outPacket.WriteInt(0);
                    outPacket.WriteString("");
                }
                else
                {
                    outPacket.WriteInt(ProfileService.ProfileConfigs[nickname].Rider.ClubCode);
                    outPacket.WriteInt(ProfileService.ProfileConfigs[nickname].Rider.ClubMark_LOGO);
                    outPacket.WriteInt(ProfileService.ProfileConfigs[nickname].Rider.ClubMark_LINE);
                    outPacket.WriteString(ProfileService.ProfileConfigs[nickname].Rider.ClubName);
                }
                outPacket.WriteInt(0);
                outPacket.WriteByte(ProfileService.ProfileConfigs[nickname].Rider.Ranker);
                outPacket.WriteBytes(new byte[30]);
                Parent.Client.Send(outPacket);
            }
        }

        public static short RandomItemSkill(string Nickname, byte gameType)
        {
            if (gameType == 2)
            {
                Random random = new Random();
                int index = random.Next(MultyPlayer.itemProb_indi.Count);
                short skill = MultyPlayer.itemProb_indi[index];
                skill = GameSupport.GetItemSkill(Nickname, skill);
                return skill;
            }
            else if (gameType == 4)
            {
                Random random = new Random();
                int index = random.Next(MultyPlayer.itemProb_team.Count);
                short skill = MultyPlayer.itemProb_team[index];
                skill = GameSupport.GetItemSkill(Nickname, skill);
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
            if (MultyPlayer.kartConfig.SkillChange.TryGetValue(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Kart, out var changes) &&
                changes.TryGetValue(skill, out var changesSkill))
            {
                return changesSkill;
            }
            return skill;
        }

        public static void AddItemSkill(int roomId, int id, string Nickname, short skill)
        {
            skill = GameSupport.GetItemSkill(Nickname, skill);
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
                MultyPlayer.BroadCast(roomId, oPacket);
            }
        }

        public static void AttackedSkill(int roomId, int id, string Nickname, byte type, byte uni, short skill)
        {
            skill = GameSupport.GetItemSkill(Nickname, skill);
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
                MultyPlayer.BroadCast(roomId, oPacket);
            }
        }
    }
}