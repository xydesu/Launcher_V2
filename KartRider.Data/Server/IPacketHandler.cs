using KartRider_PacketName;
using System.Collections.Generic;

namespace KartRider
{
    public interface IPacketHandler
    {
        string Name { get; }

        int Priority { get; }

        HashSet<PacketName> InterestedPackets { get; }

        bool Handle(PacketContext context);
    }
}
