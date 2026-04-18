using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ExcData;
using KartRider.Common.Security;
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

        public static async Task<uint> PcFirstMessageAsync(SessionGroup Parent)
        {
            Random random = new Random();
            int index = random.Next(keys.Length);
            Keys key = keys[index];
            string updateUrl = "http://kart.myany.uk/";
            ushort ClientVersion = ProfileService.SettingConfig.ClientVersion;
            var data = await Update.GetUpdateAsync().ConfigureAwait(false);
            if (data != null)
            {
                updateUrl = data.download_prefix;
                if (data.version.StartsWith('P') && ushort.TryParse(data.version.TrimStart('P'), out ushort version))
                {
                    ClientVersion = version;
                }
            }
            using (OutPacket outPacket = new OutPacket("PcFirstMessage"))
            {
                outPacket.WriteUShort(ProfileService.SettingConfig.LocaleID);
                outPacket.WriteUShort(1);
                outPacket.WriteUShort(ClientVersion);
                outPacket.WriteString(updateUrl);
                outPacket.WriteUInt(key.first_val);
                outPacket.WriteUInt(key.second_val);
                outPacket.WriteByte((byte)ProfileService.SettingConfig.nClientLoc);
                outPacket.WriteString(key.key1);
                outPacket.WriteBytes(new byte[31]);
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
                // outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].GameOption.HideCompetitiveRank);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.QuickMsg.GetValueOrDefault(0) ?? "", false);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.QuickMsg.GetValueOrDefault(1) ?? "", false);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.QuickMsg.GetValueOrDefault(2) ?? "", false);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.QuickMsg.GetValueOrDefault(3) ?? "", false);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.QuickMsg.GetValueOrDefault(4) ?? "", false);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.QuickMsg.GetValueOrDefault(5) ?? "", false);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.QuickMsg.GetValueOrDefault(6) ?? "", false);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.QuickMsg.GetValueOrDefault(7) ?? "", false);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.QuickMsg.GetValueOrDefault(8) ?? "", false);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.QuickMsg.GetValueOrDefault(9) ?? "", false);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.TeamQuickMsg.GetValueOrDefault(0) ?? "", false);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.TeamQuickMsg.GetValueOrDefault(1) ?? "", false);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.TeamQuickMsg.GetValueOrDefault(2) ?? "", false);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.TeamQuickMsg.GetValueOrDefault(3) ?? "", false);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.TeamQuickMsg.GetValueOrDefault(4) ?? "", false);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.TeamQuickMsg.GetValueOrDefault(5) ?? "", false);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.TeamQuickMsg.GetValueOrDefault(6) ?? "", false);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.TeamQuickMsg.GetValueOrDefault(7) ?? "", false);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.TeamQuickMsg.GetValueOrDefault(8) ?? "", false);
                outPacket.WriteString(ProfileService.ProfileConfigs[Nickname].GameOption.TeamQuickMsg.GetValueOrDefault(9) ?? "", false);
                Parent.Client.Send(outPacket);
            }
        }

        public static void ChRequestChStaticReplyPacket(SessionGroup Parent)
        {
            byte[] ChannelData = Array.Empty<byte>();
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
                    ChannelData = oPacket.ToArray();
                }
                outPacket.WriteBool(true);
                byte[] ChannelDataEncode = KREncodedBlock.Encode(ChannelData, KREncodedBlock.EncodeFlag.ZLib, null);
                outPacket.WriteInt(ChannelDataEncode.Length);
                outPacket.WriteBytes(ChannelDataEncode);
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
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Character);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Paint);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Kart);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Plate);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Goggle);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Balloon);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Unknown1);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_HeadBand);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_HeadPhone);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_HandGearL);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Unknown2);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Uniform);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Decal);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Pet);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_FlyingPet);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Aura);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_SkidMark);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_SpecialKit);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_RidColor);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_BonusCard);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_BossModeCard);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartPlant1);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartPlant2);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartPlant3);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartPlant4);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Unknown3);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_FishingPole);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Tachometer);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Dye);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartSN);
            outPacket.WriteByte(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Unknown4);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartCoating);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartTailLamp);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_slotBg);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartCoating12);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartTailLamp12);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_KartBoosterEffect12);
            outPacket.WriteUShort(ProfileService.ProfileConfigs[Nickname].RiderItem.Set_Unknown5);
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
                outPacket.WriteDateTime(DateTime.Now);
                outPacket.WriteBytes(new byte[17]);
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
                outPacket.WriteInt(0);//ClubMember
                outPacket.WriteByte(5);//Level
                IPEndPoint serverEndPoint = Parent.Client.Socket.LocalEndPoint as IPEndPoint;
                if (serverEndPoint == null) return;
                outPacket.WriteEndPoint(serverEndPoint.Address, 39322);
                Parent.Client.Send(outPacket);
            }
        }

        public static List<short> GetTuns(List<short> tunes, short Item)
        {
            short[] speed = { 103, 203, 303, 403, 503, 603, 703, 803, 903 };
            short[] item = { 10103, 10203, 10303, 10401, 10503, 10603, 10703, 10803, 10901, 11001, 11103, 11201, 11301, 11403, 11501, 11601, 11701, 11803, 11903, 12003 };
            short[] All = new short[speed.Length + item.Length];
            Array.Copy(speed, 0, All, 0, speed.Length);
            Array.Copy(item, 0, All, speed.Length, item.Length);

            short[] sourceArray;
            if (Item == 6)
            {
                sourceArray = speed;
            }
            else if (Item == 4)
            {
                sourceArray = item;
            }
            else
            {
                sourceArray = All;
            }

            // ============= 核心逻辑 =============
            Random random = new Random();

            // 1. 获取当前列表中已存在的非0数字（不能重复使用）
            HashSet<short> usedNumbers = new HashSet<short>(tunes.Where(x => x != 0));

            // 2. 从源数组中筛选出【可用的候选数字】：不在原列表中
            var availableNumbers = sourceArray.Where(num => !usedNumbers.Contains(num)).ToList();

            // 3. 找到所有需要填充的 0 的索引
            var zeroIndexes = tunes
                .Select((value, index) => new { value, index })
                .Where(x => x.value == 0)
                .Select(x => x.index)
                .ToList();

            // 4. 随机不重复填充每个 0 位置
            foreach (int index in zeroIndexes)
            {
                // 随机取一个可用数字
                int randomIndex = random.Next(availableNumbers.Count);
                short selectedNum = availableNumbers[randomIndex];

                // 填充
                tunes[index] = selectedNum;

                // 从候选池中移除，保证不重复
                availableNumbers.RemoveAt(randomIndex);
            }

            // 输出结果查看
            return tunes;
        }
    }
}