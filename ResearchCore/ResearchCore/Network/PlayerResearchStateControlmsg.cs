using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Equinox.ResearchCore.Definition;
using ProtoBuf;

namespace Equinox.ResearchCore.Network
{
    [ProtoContract]
    public class PlayerResearchStateControlMsg : IMsg
    {
        [ProtoMember]
        public string ResearchId;
        [ProtoMember]
        public ResearchState RequestedState;
    }
}
