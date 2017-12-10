using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Equinox.ResearchCore.Definition;
using Equinox.ResearchCore.Network;
using Equinox.ResearchCore.State;
using Equinox.Utils;
using Sandbox.ModAPI;
using VRage.Game;

namespace Equinox.ResearchCore.Modules
{
    public class ReplicationModuleServer : ModuleBase
    {
        public ReplicationModuleServer(ResearchManager mgr) : base(mgr)
        {
        }

        public override void Attach()
        {
            if (!MyAPIGateway.Session.IsServerDecider() || MyAPIGateway.Multiplayer == null)
                return;
            Manager.PlayerResearchStatefulStorageUpdate += OnPlayerResearchStatefulStorageUpdate;
            Manager.PlayerResearchStateChanged += OnPlayerResearchStateChanged;
            Manager.PlayerResearchStatefulStorageAddRemove += OnPlayerResearchStatefulStorageAddRemove;
        }
        
        private void OnPlayerResearchStatefulStorageAddRemove(PlayerResearchState research, string key, bool removed)
        {
            var player = research.Player;
            if (player.Player.SteamUserId == MyAPIGateway.Multiplayer.MyId ||
                player.Player == MyAPIGateway.Session.Player)
                return;
            var state = research.State;
            Manager.SendMessage(player.Player.SteamUserId, new ReplicationResearchMessage
            {
                ResearchId = research.Definition.Id,
                StorageId = key,
                State = state,
                StorageOperation = removed
                    ? ReplicationResearchMessage.StorageOp.Remove
                    : ReplicationResearchMessage.StorageOp.Create
            });
        }

        private void OnPlayerResearchStateChanged(PlayerResearchState research, ResearchState old, ResearchState @new)
        {
            var player = research.Player;
            if (player.Player.SteamUserId == MyAPIGateway.Multiplayer.MyId ||
                player.Player == MyAPIGateway.Session.Player)
                return;
            Manager.SendMessage(player.Player.SteamUserId, new ReplicationResearchMessage
            {
                ResearchId = research.Definition.Id,
                StorageId = null,
                State = @new,
                StorageOperation = ReplicationResearchMessage.StorageOp.None
            });
        }

        private void OnPlayerResearchStatefulStorageUpdate(PlayerResearchState research, string key, bool old,
            bool @new)
        {
            var player = research.Player;
            if (player.Player.SteamUserId == MyAPIGateway.Multiplayer.MyId ||
                player.Player == MyAPIGateway.Session.Player)
                return;
            var state = research.State;
            Manager.SendMessage(player.Player.SteamUserId, new ReplicationResearchMessage
            {
                ResearchId = research.Definition.Id,
                StorageId = key,
                State = state,
                StorageOperation = @new
                    ? ReplicationResearchMessage.StorageOp.Set
                    : ReplicationResearchMessage.StorageOp.Unset
            });
        }

        public override void Detach()
        {
            if (!MyAPIGateway.Session.IsServerDecider() || MyAPIGateway.Multiplayer == null)
                return;
            Manager.PlayerResearchStatefulStorageUpdate -= OnPlayerResearchStatefulStorageUpdate;
            Manager.PlayerResearchStateChanged -= OnPlayerResearchStateChanged;
            Manager.PlayerResearchStatefulStorageAddRemove -= OnPlayerResearchStatefulStorageAddRemove;
        }
    }
}