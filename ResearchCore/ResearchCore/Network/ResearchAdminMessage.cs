using System.Collections.Generic;
using System.Linq;
using ProtoBuf;

namespace Equinox.ResearchCore.Network
{
    public class ResearchAdminMessage : IMsg
    {
        [ProtoMember]
        public string[] Arguments;
        
        public ResearchAdminMessage(IEnumerable<string> args)
        {
            Arguments = args.ToArray();
        }
    }
}