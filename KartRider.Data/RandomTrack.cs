using ExcData;
using KartRider.Common.Utilities;
using Profile;
using RiderData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace KartRider
{
    public class Track
    {
        public uint hash { get; set; }
        public string ID { get; set; }
        public string Name { get; set; }
        public string gameType { get; set; }
        public bool basicAi { get; set; }
    }

    public class RandomTrack
    {
        public static Dictionary<uint, Track> TrackList = new Dictionary<uint, Track>();
        public static XDocument randomTrack = new XDocument();

        public static string GameTrack = "village_R01";

        public static string GetTrackName(uint trackId)
        {
            if (TrackList.ContainsKey(trackId))
            {
                return TrackList[trackId].Name;
            }
            else
            {
                return trackId.ToString();
            }
        }

        public static uint GetRandomTrack(string Nickname, byte GameType, uint Track, bool ai = false)
        {
            string RandomTrackGameType = "speed";
            string RandomTrackSetRandomTrack = "all";
            string RandomTrackGameTrack = "village_R01";

            if (GameType == 0)
            {
                RandomTrackGameType = "speed";
            }
            else if (GameType == 1)
            {
                RandomTrackGameType = "item";
            }

            if (Track == 0)
            {
                RandomTrackSetRandomTrack = "all";
            }
            else if (Track == 1)
            {
                RandomTrackSetRandomTrack = "clubSpeed";
            }
            else if (Track == 3)
            {
                RandomTrackSetRandomTrack = "hot1";
            }
            else if (Track == 4)
            {
                RandomTrackSetRandomTrack = "hot2";
            }
            else if (Track == 5)
            {
                RandomTrackSetRandomTrack = "hot3";
            }
            else if (Track == 6)
            {
                RandomTrackSetRandomTrack = "hot4";
            }
            else if (Track == 7)
            {
                RandomTrackSetRandomTrack = "hot5";
            }
            else if (Track == 8)
            {
                RandomTrackSetRandomTrack = "new";
            }
            else if (Track == 23)
            {
                RandomTrackSetRandomTrack = "crazy";
            }
            else if (Track == 30)
            {
                RandomTrackSetRandomTrack = "reverse";
            }
            else if (Track == 40)
            {
                RandomTrackSetRandomTrack = "speedAll";
            }
            else
            {
                RandomTrackSetRandomTrack = "Unknown";
            }

            if (RandomTrackSetRandomTrack == "all" || RandomTrackSetRandomTrack == "speedAll")
            {
                Random random = new Random();
                if (!FileName.FileNames.ContainsKey(Nickname))
                {
                    FileName.Load(Nickname);
                }
                var filename = FileName.FileNames[Nickname];
                var FavoriteTrackList = new Favorite_Track();
                if (File.Exists(filename.FavoriteTrack_LoadFile))
                {
                    FavoriteTrackList = JsonHelper.DeserializeNoBom<Favorite_Track>(filename.FavoriteTrack_LoadFile) ?? new Favorite_Track();
                }
                List<uint> availableTracks = FavoriteTrackList.GetAllTracks();
                if (availableTracks.Count > 0)
                {
                    return availableTracks[random.Next(availableTracks.Count)];
                }
                else
                {
                    Random AllRandom = new Random();
                    var validTracks = TrackList.Values
                        .Where(t => string.Equals(t.gameType, RandomTrackGameType, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(t.Name) && (ai ? t.basicAi == true : true))
                        .Select(t => t.hash)
                        .ToList();
                    if (validTracks.Count > 0)
                    {
                        int randomIndex = AllRandom.Next(validTracks.Count);
                        return validTracks[randomIndex];
                    }
                    else
                    {
                        return Adler32Helper.GenerateAdler32_UNICODE(RandomTrack.GameTrack, 0);
                    }
                }
            }
            else if (RandomTrackSetRandomTrack == "Unknown")
            {
                if (TrackList.ContainsKey(Track))
                {
                    return Track;
                }
                else
                {
                    return Adler32Helper.GenerateAdler32_UNICODE(RandomTrack.GameTrack, 0);
                }
                Console.WriteLine("RandomTrack: {0} / {1} / {2}", RandomTrackGameType, RandomTrackSetRandomTrack, RandomTrack.GameTrack);
            }
            else
            {
                XDocument doc = randomTrack;
                var TrackSet = doc.Descendants("RandomTrackSet")
                    .FirstOrDefault(rts => (string)rts.Attribute("gameType") == RandomTrackGameType && (string)rts.Attribute("randomType") == RandomTrackSetRandomTrack);
                if (TrackSet != null)
                {
                    Random random = new Random();
                    var randomTrack = TrackSet.Descendants("track").ElementAt(random.Next(TrackSet.Descendants("track").Count()));
                    RandomTrackGameTrack = (string)randomTrack.Attribute("id");
                }
                else
                {
                    var TrackList = doc.Descendants("RandomTrackList")
                        .FirstOrDefault(rts => (string)rts.Attribute("randomType") == RandomTrackSetRandomTrack);
                    if (TrackList != null)
                    {
                        Random random = new Random();
                        var randomTrack = TrackList.Descendants("track").ElementAt(random.Next(TrackList.Descendants("track").Count()));
                        RandomTrackGameTrack = (string)randomTrack.Attribute("id");
                    }
                }
                return Adler32Helper.GenerateAdler32_UNICODE(RandomTrackGameTrack, 0);
            }
        }
    }
}
