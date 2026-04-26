using System;
using ExcData;
using Profile;

namespace KartRider
{
    public class GameDataReset
    {
        public static void DataReset(string Nickname)
        {
            var resetConfig = ProfileService.GetProfileConfig(Nickname);
            if (resetConfig.Rider.Lucci > uint.MaxValue)
            {
                resetConfig.Rider.Lucci = SessionGroup.LucciMax;
            }
            ProfileService.Save(Nickname, resetConfig);
            SpeedPatch.SpeedPatcData();
            //GameSupport.PrLogin();
            Console.WriteLine("Login...OK");
        }
    }
}
