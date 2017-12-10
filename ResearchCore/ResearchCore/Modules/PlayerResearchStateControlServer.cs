using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Equinox.ResearchCore.Definition;
using Equinox.ResearchCore.Network;
using Equinox.Utils;
using Equinox.Utils.Logging;
using Sandbox.ModAPI;

namespace Equinox.ResearchCore.Modules
{
    public class PlayerResearchStateControlServer : ModuleBase
    {
        public PlayerResearchStateControlServer(ResearchManager mgr) : base(mgr)
        {
        }

        public override void Attach()
        {
            if (!MyAPIGateway.Session.IsServerDecider())
                return;
            Manager.NetworkMessageRecieved += OnNetworkMessageRecieved;
        }

        private void OnNetworkMessageRecieved(ulong steamId, IMsg msg)
        {
            var ctl = msg as PlayerResearchStateControlMsg;
            if (ctl == null)
                return;
            var player = Manager.Core.Players.TryGetPlayerBySteamId(steamId);
            if (player == null)
                return;
            var data = Manager.GetOrCreatePlayer(player);
            var research = data?.PlayerResearchState(ctl.ResearchId, true);
            if (research == null)
                return;

            switch (ctl.RequestedState)
            {
                case ResearchState.NotStarted:
                    if (research.State == ResearchState.InProgress)
                    {
                        research.State = ResearchState.NotStarted;
                        return;
                    }
                    break;
                case ResearchState.InProgress:
                    if (research.State == ResearchState.NotStarted)
                    {
                        if (!research.Definition.Trigger.BranchesWithPrereqs(data.ResearchState, data.HasUnlocked)
                            .Any())
                        {
                            Logger.Error($"Player {player.DisplayName} requested to start an unstartable research");
                            return;
                        }
                        research.State = ResearchState.InProgress;
                        return;
                    }
                    break;
                // invalid operations:
                case ResearchState.Completed:
                    break;
                case ResearchState.Failed:
                    break;
            }
            Logger.Error(
                $"Player {player.DisplayName} requested a bad state {ctl.RequestedState} for {ctl.ResearchId} when already in start {research.State}");
        }

        public override void Detach()
        {
            if (!MyAPIGateway.Session.IsServerDecider())
                return;
            Manager.NetworkMessageRecieved -= OnNetworkMessageRecieved;
        }
    }
}