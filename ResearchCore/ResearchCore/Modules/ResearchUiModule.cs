using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Equinox.ResearchCore.Definition;
using Equinox.ResearchCore.Definition.ObjectBuilders.Triggers;
using Equinox.ResearchCore.Network;
using Equinox.ResearchCore.State;
using Equinox.ResearchCore.Utils;
using Equinox.Utils;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Input;

namespace Equinox.ResearchCore.Modules
{
    public class ResearchUiModule : ModuleBase
    {
        public ResearchUiModule(ResearchManager mgr) : base(mgr)
        {
        }

        public bool HideMessages { get; set; }

        public override void Attach()
        {
            if (!MyAPIGateway.Session.IsPlayerController())
                return;

            Manager.PlayerResearchStateChanged += ResearchStateChanged;
            Manager.PlayerResearchStatefulStorageAddRemove += StatefulStorageAddRemove;
            MyAPIGateway.Utilities.MessageEntered += OnMessageEntered;
        }

        private void OnMessageEntered(string messageText, ref bool sendToOthers)
        {
            if (messageText.StartsWith("!research"))
            {
                sendToOthers = false;
                var args = messageText.Split(new[] {' '}, 3);
                var res = HandleCommand(args);
                if (res != null)
                    foreach (var msg in res.Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries))
                        MyAPIGateway.Utilities.ShowMessage("Research", msg);
            }
        }

        private string HandleCommand(IReadOnlyList<string> args)
        {
            if (MyAPIGateway.Session.LocalHumanPlayer == null)
                return null;
            var player = Manager.GetOrCreatePlayer(MyAPIGateway.Session.LocalHumanPlayer);
            if (player == null)
                return null;
            if (args.Count < 2 || args[1].Equals("help"))
                return "Usage: !research (start|cancel|info|available|in-progress|completed)";
            var cmd = args[1].ToLower();
            switch (cmd)
            {
                case "grant":
                case "revoke":
                case "promote":
                case "demote":
                // ReSharper disable once StringLiteralTypo
                case "setdata":
                {
                    Manager.SendMessageToServer(new ResearchAdminMessage(args));
                    return null;
                }
                case "start":
                case "info":
                case "cancel":
                {
                    if (args.Count < 3)
                        return $"Usage: !research {args[1]} (research identifier...)";
                    string msg;
                    var research = Manager.TryFindResearch(args[2], out msg);
                    if (research == null || msg != null)
                        return msg;

                    var state = player.ResearchState(research.Id);
                    if (cmd.Equals("start"))
                    {
                        switch (state)
                        {
                            case ResearchState.InProgress:
                                return $"Research {research.DisplayName} is already started";
                            case ResearchState.Completed:
                                return $"Research {research.DisplayName} is already completed";
                            case ResearchState.Failed:
                                return $"Research {research.DisplayName} was failed";
                        }

                        if (!research.Trigger.BranchesWithPrereqs(player.ResearchState, player.HasUnlocked).Any())
                            return $"Research {research.DisplayName} prerequisites are not satisfied";
                        Manager.SendMessageToServer(new PlayerResearchStateControlMsg()
                        {
                            ResearchId = research.Id,
                            RequestedState = ResearchState.InProgress
                        });
                        return null;
                    }

                    if (cmd.Equals("cancel"))
                    {
                        switch (state)
                        {
                            case ResearchState.NotStarted:
                                return $"Research {research.DisplayName} has not been started";
                            case ResearchState.Completed:
                                return $"Research {research.DisplayName} is already completed";
                            case ResearchState.Failed:
                                return $"Research {research.DisplayName} was failed";
                        }

                        Manager.SendMessageToServer(new PlayerResearchStateControlMsg()
                        {
                            ResearchId = research.Id,
                            RequestedState = ResearchState.NotStarted
                        });
                        return null;
                    }

                    if (cmd.Equals("info"))
                    {
                        _futureDisplay.Enqueue(new FutureDisplay
                        {
                            State = player.PlayerResearchState(research.Id, true),
                            ShowDialog = ShowResearchInfo
                        });
                        return null;
                    }

                    return null;
                }
                case "available":
                {
                    _futureDisplay.Enqueue(new FutureDisplay {State = null, ShowDialog = ShowAvailableResearch});
                    return null;
                }
                case "completed":
                {
                    _futureDisplay.Enqueue(new FutureDisplay() {State = null, ShowDialog = ShowCompletedResearch});
                    return null;
                }
                case "inprogress":
                case "in-progress":
                {
                    _futureDisplay.Enqueue(new FutureDisplay {State = null, ShowDialog = ShowInProgressResearch});
                    return null;
                }
                default:
                {
                    return $"Unknown subcommand: {args[1]}";
                }
            }
        }

        private void StatefulStorageAddRemove(PlayerResearchState research, string key, bool removed)
        {
            if (research.Player.Player != MyAPIGateway.Session.Player)
                return;
        }

        private void ResearchStateChanged(PlayerResearchState research, ResearchState old, ResearchState @new)
        {
            if (research.Player.Player != MyAPIGateway.Session.Player)
                return;
            string msg;
            switch (@new)
            {
                case ResearchState.NotStarted:
                    msg = old == ResearchState.InProgress ? "aborted" : null;
                    break;
                case ResearchState.InProgress:
                    msg = "started";
                    break;
                case ResearchState.Completed:
                    msg = "completed";
                    if (!HideMessages && research.Definition.ShowCompletionWindow)
                        _futureDisplay.Enqueue(new FutureDisplay {State = research, ShowDialog = ShowResearchComplete});
                    break;
                case ResearchState.Failed:
                    msg = "failed";
                    break;
                default:
                    throw new Exception($"State out of range {@new}");
            }

            if (msg == null || HideMessages) return;
            var resMsg = @new == ResearchState.Completed && !string.IsNullOrEmpty(research.Definition.CompletionMessage)
                ? research.Definition.CompletionMessage
                : $"You've {msg} {research.Definition.DisplayName}";
            if (research.Definition.UpdatesAsNotifications)
                MyAPIGateway.Utilities.ShowNotification(resMsg);
            else
                MyAPIGateway.Utilities.ShowMessage("Research", resMsg);
        }

        private DateTime? _lastScreenShown;

        private struct FutureDisplay
        {
            public PlayerResearchState State;
            public Action<PlayerResearchState> ShowDialog;
        }

        private readonly Queue<FutureDisplay> _futureDisplay = new Queue<FutureDisplay>();

        public override void Update()
        {
            if (_lastScreenShown.HasValue && (DateTime.Now - _lastScreenShown) < TimeSpan.FromSeconds(30))
                return;

            _lastScreenShown = null;
            FutureDisplay state;
            if (_futureDisplay.TryDequeue(out state))
                state.ShowDialog(state.State);
        }

        private void ShowAvailableResearch(PlayerResearchState state)
        {
            ShowResearchList("Available Research", (player, id) =>
            {
                var def = Manager.Definitions.ResearchById(id);
                return player.ResearchState(id) == ResearchState.NotStarted && !def.Hidden && def.Trigger
                           .BranchesWithPrereqs(player.ResearchState, player.HasUnlocked).Any();
            });
        }

        private void ShowCompletedResearch(PlayerResearchState state)
        {
            ShowResearchList("Completed Research", (player, id) => player.ResearchState(id) == ResearchState.Completed);
        }

        private void ShowInProgressResearch(PlayerResearchState state)
        {
            ShowResearchList("In-Progress Research",
                (player, id) => player.ResearchState(id) == ResearchState.InProgress);
        }

        private void ShowResearchList(string title, Func<PlayerState, string, bool> pred)
        {
            if (MyAPIGateway.Session.LocalHumanPlayer == null)
                return;
            var player = Manager.GetOrCreatePlayer(MyAPIGateway.Session.LocalHumanPlayer);
            if (player == null)
                return;
            var content = new StringBuilder();
            foreach (var def in Manager.Definitions.Research)
                if (pred(player, def.Id))
                    content.Append(def.DisplayName).Append("\n");

            _lastScreenShown = DateTime.Now;
            MyAPIGateway.Utilities.ShowMissionScreen(title, "", null, content.ToString(),
                (x) => _lastScreenShown = null);
        }

        private void ShowResearchInfo(PlayerResearchState state)
        {
            ShowResearchInfoScreen(state, "Research Information", "Unlocks:", true, true);
        }

        private void ShowResearchComplete(PlayerResearchState state)
        {
            if (state.State == ResearchState.Completed)
                ShowResearchInfoScreen(state, "Research Complete!",
                    (!string.IsNullOrEmpty(state.Definition.CompletionMessage) ? state.Definition.CompletionMessage + "\n\n" : "") + "You've unlocked:", false,
                    false);
        }

        private void ShowResearchInfoScreen(PlayerResearchState def, string title, string unlockTagLine,
            bool showRequirements, bool showState)
        {
            var hasPrevBlock = false;
            StringBuilder content = new StringBuilder();
            if (def.Definition.Description != null)
            {
                content.Append(def.Definition.Description).Append('\n');
                hasPrevBlock = true;
            }

            if (showRequirements)
            {
                if (hasPrevBlock)
                    content.Append('\n');
                hasPrevBlock = true;
                content.Append("Requires ").Append(def.Definition.Trigger.Branches.Count > 1 ? "any of:\n" : "");
                foreach (var branch in def.Definition.Trigger.Branches)
                    StringifyTriggerBranch(def, content, def.Definition.Trigger.Branches.Count > 1 ? "  " : "", branch);
            }

            if (unlockTagLine != null)
            {
                if (hasPrevBlock)
                    content.Append('\n');
                hasPrevBlock = true;
                if (!string.IsNullOrWhiteSpace(unlockTagLine))
                    content.Append(unlockTagLine).Append('\n');
                var first = true;
                var msg = new HashSet<string>();
                foreach (var k in def.Definition.UnlocksOriginal.OrderBy(a => a.SubtypeName).Select(StringifyUnlock))
                {
                    if (!msg.Add(k))
                        continue;
                    if (!first)
                        content.Append('\n');
                    first = false;
                    content.Append(k);
                }
            }

            _lastScreenShown = DateTime.Now;
            MyAPIGateway.Utilities.ShowMissionScreen(title,
                def.Definition.DisplayName + (showState ? ": " + CapWords(StringifyState(def.State)) : ""),
                null,
                content.ToString(),
                (x) => _lastScreenShown = null);
        }

        private static string CapWords(string s)
        {
            var tmp = new char[s.Length];
            for (var i = 0; i < s.Length; i++)
            {
                if (i == 0 || !char.IsLetter(s[i - 1]))
                    tmp[i] = char.ToUpper(s[i]);
                else tmp[i] = s[i];
            }

            return new string(tmp);
        }

        private void StringifyTriggerBranch(PlayerResearchState state, StringBuilder output, string indent,
            ResearchTrigger.IResearchBranch branch)
        {
            if (branch.ResearchStates.Count + branch.StatefulTriggers.Count + branch.Unlocked.Count > 1)
            {
                output.Append(indent).Append(indent.Length > 0 ? 'A' : 'a').Append("ll of:\n");
                indent += "  ";
            }

            foreach (var k in branch.ResearchStates)
            {
                output.Append(indent).Append(Manager.Definitions.ResearchById(k.Key).DisplayName ?? k.Key)
                    .Append(" is ").Append(StringifyState(k.Value));
                if ((k.Value & state.Player.ResearchState(k.Key)) != 0)
                    output.Append(" (done)");
                output.Append("\n");
            }

            foreach (var k in branch.Unlocked)
            {
                output.Append(indent).Append(MyDefinitionManager.Static.GetDefinitionAny(k)?.DisplayNameText ?? k.ToString())
                    .Append(" unlocked");
                if (state.Player.HasUnlocked(k))
                    output.Append(" (done)");
                output.Append("\n");
            }

            foreach (var piece in branch.StatefulTriggers.Select(state.Definition.Trigger.StateStorageProvider))
            {
                var hasItem = piece as Ob_Trigger_HasItem;
                var location = piece as Ob_Trigger_Location;
                var interact = piece as Ob_Trigger_Interact;
                if (hasItem != null)
                    output.Append(indent).Append(hasItem.Consume ? "Sacrifice" : "Have").Append(" ")
                        .Append(hasItem.Count).Append(" ")
                        .Append(MyDefinitionManager.Static.GetDefinitionAny(hasItem.DefinitionId)?.DisplayNameText ??
                                hasItem.DefinitionId.ToString());
                else if (interact != null)
                {
                    output.Append(indent).Append("Interact ");
                    var control = MyAPIGateway.Input.GetGameControl(interact.GameControlId);
                    if (control != null)
                    {
                        var desc = control.GetControlButtonName(MyGuiInputDeviceEnum.Mouse);
                        if (string.IsNullOrWhiteSpace(desc))
                            desc = control.GetControlButtonName(MyGuiInputDeviceEnum.Keyboard);
                        if (string.IsNullOrWhiteSpace(desc))
                            desc = control.GetControlButtonName(MyGuiInputDeviceEnum.KeyboardSecond);
                        if (!string.IsNullOrWhiteSpace(desc))
                            output.Append("(").Append(desc).Append(") ");
                    }

                    output.Append("with ");
                    var itemsPrinted = 0;
                    var totalItems = interact.BlockInteractTarget.Count + (interact.OnCharacterInteract ? 1 : 0);
                    if (interact.OnCharacterInteract)
                    {
                        itemsPrinted++;
                        output.Append("another character");
                    }

                    foreach (var entry in interact.BlockInteractTarget)
                    {
                        itemsPrinted++;
                        output.Append(itemsPrinted == totalItems ? (totalItems > 2 ? ", or " : " or ") : ", ");
                        output.Append(MyDefinitionManager.Static.GetDefinitionAny(entry)?.DisplayNameText ??
                                      entry.ToString());
                    }

                    output.Append(" using ");
                    if (interact.HandItem.Count == 0)
                        output.Append("any hand item");
                    else
                    {
                        var handsPrinted = 0;
                        foreach (var k in interact.HandItem)
                        {
                            handsPrinted++;
                            if (handsPrinted > 1)
                                output.Append(handsPrinted == interact.HandItem.Count ? ", or " : ", ");
                            output.Append(MyDefinitionManager.Static.GetDefinitionAny(k)?.DisplayNameText ??
                                          k.ToString());
                        }
                    }
                }
                else if (location != null)
                {
                    output.Append(indent).Append("Get within ");
                    if (location.Radius > 5e3)
                        output.Append(Math.Floor(location.Radius / 1000)).Append(" km");
                    else
                        output.Append(Math.Floor(location.Radius)).Append(" m");
                    if (location.ObscureLocation)
                        output.Append(" of a location");
                    else
                        output.Append(" of ").Append((long) location.Position.X).Append(", ").Append((long) location.Position.Y).Append(", ")
                            .Append((long) location.Position.Z);
                }

                if (state.StatefulStorage(piece.StateStorageKey) ?? false)
                    output.Append(" (done)");
                output.Append("\n");
            }
        }

        private static string StringifyState(ResearchState state)
        {
            switch (state)
            {
                case ResearchState.NotStarted:
                    return "not started";
                case ResearchState.InProgress:
                    return "in progress";
                case ResearchState.Completed:
                    return "completed";
                case ResearchState.Failed:
                    return "failed";
                case ResearchState.InProgressOrCompleted:
                    return "in progress or completed";
                case ResearchState.FailedOrNotStarted:
                    return "failed or not started";
                case ResearchState.NotFailed:
                    return "not failed";
                default:
                    return $"state unknown {state}";
            }
        }

        private string StringifyUnlock(MyDefinitionId id)
        {
            var unlockedDefinition = MyDefinitionManager.Static.GetDefinitionAny(id);
            if (unlockedDefinition == null)
                return "unknown " + id;

            string unlockDesc = unlockedDefinition.DisplayNameText ?? unlockedDefinition.Id.SubtypeName ?? "default";

            if (unlockedDefinition is MyCubeBlockDefinition)
                return "- The " + unlockDesc + " block";
            if (unlockedDefinition is MyBlueprintDefinition)
                return "- The recipe for " +
                       (unlockDesc.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? "a " : "") + unlockDesc;
            var blueprintClass = unlockedDefinition as MyBlueprintClassDefinition;
            if (blueprintClass != null)
            {
                var sb = new StringBuilder("- The " + unlockDesc + " recipe group");
                foreach (var sub in blueprintClass)
                    sb.Append("\n   - " + (sub.DisplayNameText ?? sub.Id.SubtypeName ?? "default"));
                return sb.ToString();
            }

            return "- The " + id.TypeId + " " + unlockDesc;
        }

        public override void Detach()
        {
            if (!MyAPIGateway.Session.IsPlayerController())
                return;

            Manager.PlayerResearchStateChanged -= ResearchStateChanged;
            Manager.PlayerResearchStatefulStorageAddRemove -= StatefulStorageAddRemove;
            MyAPIGateway.Utilities.MessageEntered -= OnMessageEntered;
        }
    }
}