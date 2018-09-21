using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Equinox.ResearchCore.Utils;
using ProtoBuf;

namespace Equinox.ResearchCore.Network
{
    [ProtoContract]
    public class MsgContainer
    {
        public IMsg Message
        {
            get
            {
                if (_interactionUnlockedMsg != null)
                    return _interactionUnlockedMsg;
                if (_researchDefValueMsg != null)
                    return _researchDefValueMsg;
                if (_researchDefRequestMsg != null)
                    return _researchDefRequestMsg;
                if (_replicationResearchMsg != null)
                    return _replicationResearchMsg;
                if (_questControlMsg != null)
                    return _questControlMsg;
                if (_adminMsg != null)
                    return _adminMsg;
                throw new Exception("Unknown message type");
            }
            set
            {
                _interactionUnlockedMsg = value as PlayerInteractionUnlockedMsg;
                _researchDefValueMsg = value as ResearchDefinitionValueMessage;
                _researchDefRequestMsg = value as ResearchDefinitionRequestMessage;
                _replicationResearchMsg = value as ReplicationResearchMessage;
                _questControlMsg = value as PlayerResearchStateControlMsg;
                _adminMsg = value as ResearchAdminMessage;
            }
        }

        [ProtoMember]
        public ulong Sender;

        [ProtoMember]
        private PlayerInteractionUnlockedMsg _interactionUnlockedMsg;

        [ProtoMember]
        private ResearchDefinitionValueMessage _researchDefValueMsg;

        [ProtoMember]
        private ResearchDefinitionRequestMessage _researchDefRequestMsg;

        [ProtoMember]
        private ReplicationResearchMessage _replicationResearchMsg;

        [ProtoMember]
        private PlayerResearchStateControlMsg _questControlMsg;

        [ProtoMember]
        private ResearchAdminMessage _adminMsg;
    }
}