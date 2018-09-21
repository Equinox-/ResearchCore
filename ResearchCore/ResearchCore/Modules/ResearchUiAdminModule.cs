using System;
using System.Collections.Generic;
using Equinox.ResearchCore.Definition;
using Equinox.ResearchCore.Definition.ObjectBuilders.Triggers;
using Equinox.ResearchCore.Network;
using Equinox.Utils;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace Equinox.ResearchCore.Modules
{
    public class ResearchUiAdminModule : ModuleBase
    {
        public ResearchUiAdminModule(ResearchManager mgr) : base(mgr)
        {
        }

        public override void Attach()
        {
            if (!MyAPIGateway.Session.IsServerDecider())
                return;
            Manager.NetworkMessageRecieved += MessageRecv;
        }

        private void MessageRecv(ulong arg1, IMsg arg2)
        {
            var player = Manager.Core.Players.TryGetPlayerBySteamId(arg1);
            if (player == null || player.IdentityId == 0)
                return;

            var admin = MyAPIGateway.Session.GetUserPromoteLevel(arg1) >= MyPromoteLevel.Moderator;
            var playerResearchData = Manager.GetOrCreatePlayer(player);
            var researchAdmin = admin || playerResearchData.AdminMode;

            var msg = arg2 as ResearchAdminMessage;
            if (msg == null)
                return;

            if (msg.Arguments.Length < 3)
                return;
            var command = msg.Arguments[1].ToLower();
            switch (command)
            {
                case "grant":
                case "revoke":
                {
                    if (!researchAdmin)
                        return;
                    ResearchDefinition research;
                    // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                    if (msg.Arguments[2].Equals("all", StringComparison.OrdinalIgnoreCase))
                        research = null;
                    else
                    {
                        string err = null;
                        research = Manager.TryFindResearch(msg.Arguments[2], out err);
                        if (err != null)
                        {
                            MyVisualScriptLogicProvider.SendChatMessage(err, "ResearchAdmin", player.IdentityId);
                            return;
                        }
                    }

                    var changed = 0;
                    var state = command.Equals("grant") ? ResearchState.Completed : ResearchState.NotStarted;
                    if (research == null)
                    {
                        foreach (var r in Manager.Definitions.Research)
                            if (playerResearchData.ResearchState(r.Id) != state)
                            {
                                playerResearchData.PlayerResearchState(r.Id, true).State = state;
                                changed++;
                            }
                    }
                    else if (playerResearchData.ResearchState(research.Id) != state)
                    {
                        playerResearchData.PlayerResearchState(research.Id, true).State = state;
                        changed++;
                    }

                    MyVisualScriptLogicProvider.SendChatMessage($"Changed {changed} research states to {state}", "ResearchAdmin", player.IdentityId);

                    return;
                }
                case "setdata":
                {
                    if (!researchAdmin)
                        return;
                    var playerEntity = player.Controller?.ControlledEntity?.Entity as IMyCharacter;
                    if (playerEntity == null)
                        return;
                    var tool = playerEntity.EquippedTool?.Components.Get<MyCasterComponent>();
                    if (tool == null)
                    {
                        MyVisualScriptLogicProvider.SendChatMessage($"No equipped tool found.", "ResearchAdmin", player.IdentityId);
                        return;
                    }

                    var block = (tool.HitBlock as IMySlimBlock)?.FatBlock;
                    if (block == null)
                    {
                        MyVisualScriptLogicProvider.SendChatMessage($"No fat block found.", "ResearchAdmin", player.IdentityId);
                        return;
                    }

                    var storage = block.Storage ?? (block.Storage = new MyModStorageComponent());
                    storage.SetValue(Ob_Trigger_Interact.InteractResearchStorageComponent, msg.Arguments[2]);
                    MyVisualScriptLogicProvider.SendChatMessage(
                        $"Set block {(block as IMyTerminalBlock)?.CustomName ?? block.ToString()} storage to {msg.Arguments[2] ?? "null"}");
                    return;
                }
                case "promote":
                case "demote":
                {
                    var playerName = msg.Arguments[2];
                    long playerId;
                    long.TryParse(playerName, out playerId);
                    var tmp = new List<IMyPlayer>(4);
                    MyAPIGateway.Players.GetPlayers(tmp, (x) =>
                    {
                        if (x.IdentityId == playerId || x.SteamUserId == (ulong) playerId)
                            return true;
                        return x.DisplayName.StartsWith(playerName, StringComparison.OrdinalIgnoreCase);
                    });
                    if (tmp.Count != 1)
                    {
                        MyVisualScriptLogicProvider.SendChatMessage("Player query " + playerName + " was ambiguous", "ResearchAdmin", player.IdentityId);
                        return;
                    }

                    var pdata = Manager.GetOrCreatePlayer(tmp[0]);
                    var oldMode = pdata.AdminMode;
                    pdata.AdminMode = command.Equals("promote");
                    MyVisualScriptLogicProvider.SendChatMessage(
                        $"Changed {tmp[0].DisplayName} from {(oldMode ? "Admin" : "Normal")} to {(pdata.AdminMode ? "Admin" : "Normal")}",
                        "ResearchAdmin", player.IdentityId);
                    return;
                }
                default:
                    break;
            }

            MyVisualScriptLogicProvider.SendChatMessage($"Unknown admin command {command}", "ResearchAdmin", player.IdentityId);
        }

        public override void Detach()
        {
            Manager.NetworkMessageRecieved -= MessageRecv;
        }
    }
}