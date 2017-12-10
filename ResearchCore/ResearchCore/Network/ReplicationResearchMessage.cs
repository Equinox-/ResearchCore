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
    public class ReplicationResearchMessage : IMsg
    {
        [ProtoMember]
        public string ResearchId;
        [ProtoMember]
        public string StorageId;
        [ProtoMember]
        public ResearchState State;
        [ProtoMember]
        public StorageOp StorageOperation = StorageOp.None;

        public enum StorageOp
        {
            None,
            Create,
            Remove,
            Set,
            Unset
        }
    }
}
