using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Equinox.ResearchCore.Definition;
using Equinox.ResearchCore.State;
using Equinox.Utils.Logging;
using Sandbox.ModAPI.Ingame;

namespace Equinox.ResearchCore.Modules
{
    public class QuestLifetimeModule : ModuleBase
    {
        public QuestLifetimeModule(ResearchManager mgr) : base(mgr)
        {
        }

        public override void Attach()
        {
            Manager.PlayerAdded += PlayerAdded;
            Manager.PlayerUnlockStateChanged += OnPlayerUnlockStateChanged;
            Manager.PlayerResearchStateChanged += OnPlayerResearchStateChanged;
            Manager.PlayerResearchStatefulStorageUpdate += OnPlayerStatefulStorageUpdate;
        }

        public override void Detach()
        {
            Manager.PlayerAdded -= PlayerAdded;
            Manager.PlayerUnlockStateChanged -= OnPlayerUnlockStateChanged;
            Manager.PlayerResearchStateChanged -= OnPlayerResearchStateChanged;
            Manager.PlayerResearchStatefulStorageUpdate -= OnPlayerStatefulStorageUpdate;
        }

        private void OnPlayerStatefulStorageUpdate(PlayerResearchState research, string key, bool old, bool @new)
        {
            CheckCompletion(research);
        }

        private void OnPlayerUnlockStateChanged(PlayerState player, VRage.Game.MyDefinitionId unlock, bool wasUnlocked,
            bool nowUnlocked)
        {
            foreach (var rd in Manager.Definitions.ResearchDepending(unlock))
                DependencyChangedState(player, rd);
        }

        private void OnPlayerResearchStateChanged(PlayerResearchState research, ResearchState old, ResearchState @new)
        {
            UpdateStatefulStorage(research);
            foreach (var rd in Manager.Definitions.ResearchDepending(research.Definition.Id))
                DependencyChangedState(research.Player, rd);
        }

        private void PlayerAdded(PlayerState state)
        {
            foreach (var rd in Manager.Definitions.AutostartResearch)
                DependencyChangedState(state, rd);
        }

        private void DependencyChangedState(PlayerState state, ResearchDefinition rd)
        {
            CheckAutostart(state, rd);
            var research = state.PlayerResearchState(rd.Id);
            if (research != null)
            {
                UpdateStatefulStorage(research);
                CheckCompletion(research);
            }
        }

        private void CheckAutostart(PlayerState state, ResearchDefinition rd)
        {
            if (!rd.AutoStart)
                return;
            if (rd.Trigger.BranchesWithPrereqs(state.ResearchState, state.HasUnlocked).Any() &&
                state.ResearchState(rd.Id) == ResearchState.NotStarted)
            {
                state.PlayerResearchState(rd.Id, true).State = ResearchState.InProgress;
            }

        }

        private void CheckCompletion(PlayerResearchState research)
        {
            if (research.State != ResearchState.InProgress)
                return;
            foreach (var branch in research.Definition.Trigger.BranchesWithPrereqs(research.Player.ResearchState,
                research.Player.HasUnlocked))
            {
                var success = true;
                foreach (var req in branch.StatefulTriggers)
                {
                    if (!(research.StatefulStorage(req) ?? false))
                    {
                        success = false;
                        break;
                    }
                }
                if (success)
                {
                    research.State = ResearchState.Completed;
                    break;
                }
            }
        }

        private void UpdateStatefulStorage(PlayerResearchState research)
        {
            if (research.State != ResearchState.InProgress)
            {
                if (research.StatefulKeys.Count == 0)
                    return;
                var keyTmp = research.StatefulKeys.ToArray();
                foreach (var k in keyTmp)
                    research.RemoveStatefulStorage(k);
                return;
            }
            var rd = research.Definition;
            var branches = rd.Trigger
                .BranchesWithPrereqs(research.Player.ResearchState, research.Player.HasUnlocked)
                .ToArray();
            var statefulKeys = new HashSet<string>();
            foreach (var branch in branches)
            foreach (var trigger in branch.StatefulTriggers)
                statefulKeys.Add(trigger);

            if (research.StatefulKeys.Count > 0)
            {
                var keyTmp = research.StatefulKeys.ToArray();
                foreach (var k in keyTmp)
                    if (!statefulKeys.Contains(k))
                        research.RemoveStatefulStorage(k);
            }
            foreach (var k in statefulKeys)
                if (!research.StatefulStorage(k).HasValue)
                    research.CreateStatefulStorage(k);
        }
    }
}