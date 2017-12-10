using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using VRageMath;

namespace Equinox.ResearchCore.Network
{
    [ProtoContract]
    public class PlayerInteractionUnlockedMsg : IMsg
    {
        [ProtoMember]
        public string ResearchId;
        [ProtoMember]
        public string StateStorage;
        [ProtoMember]
        public long InteractionTargetEntity;
        [ProtoMember]
        public Vector3I BlockPosition;
    }
}
