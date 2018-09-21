using Equinox.ResearchCore.Network;
using Equinox.Utils;
using Sandbox.ModAPI;

namespace Equinox.ResearchCore.Modules
{
    public class ReplicationModuleClient : ModuleBase
    {
        public ReplicationModuleClient(ResearchManager mgr) : base(mgr)
        {
        }

        public override void Attach()
        {
            if (MyAPIGateway.Session.IsServerDecider())
                return;
            Manager.NetworkMessageRecieved += OnMessage;
        }

        private void OnMessage(ulong @ulong, IMsg msg)
        {
            if (MyAPIGateway.Session.LocalHumanPlayer == null)
                return;
            var msgResearch = msg as ReplicationResearchMessage;
            if (msgResearch != null)
            {
                var context = Manager.GetOrCreatePlayer(MyAPIGateway.Session.LocalHumanPlayer)
                    .PlayerResearchState(msgResearch.ResearchId, true);
                if (string.IsNullOrWhiteSpace(msgResearch.StorageId) && msgResearch.StorageOperation ==
                    ReplicationResearchMessage.StorageOp.None)
                {
                    context.State = msgResearch.State;
                }
                if (!string.IsNullOrWhiteSpace(msgResearch.StorageId))
                {
                    switch (msgResearch.StorageOperation)
                    {
                        case ReplicationResearchMessage.StorageOp.Create:
                            context.CreateStatefulStorage(msgResearch.StorageId);
                            break;
                        case ReplicationResearchMessage.StorageOp.Remove:
                            context.RemoveStatefulStorage(msgResearch.StorageId);
                            break;
                        case ReplicationResearchMessage.StorageOp.Set:
                            context.UpdateStatefulStorage(msgResearch.StorageId, true);
                            break;
                        case ReplicationResearchMessage.StorageOp.Unset:
                            context.UpdateStatefulStorage(msgResearch.StorageId, false);
                            break;
                        case ReplicationResearchMessage.StorageOp.None:
                        default:
                            break;
                    }
                }
            }
        }

        public override void Detach()
        {
            if (MyAPIGateway.Session.IsServerDecider())
                return;
            Manager.NetworkMessageRecieved -= OnMessage;
        }
    }
}